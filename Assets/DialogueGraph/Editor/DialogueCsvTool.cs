using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DialogueGraph.Editor
{
    /// <summary>
    /// 文案 CSV 导出 / 导入：给翻译、校对、配音用。
    /// 行 ID 稳定（对话节点 guid / 选项 id），配音文件可直接按行 ID 命名对照。
    /// 列：行ID, 类型, 说话人, LocKey, 原文, [每语言一列]。
    /// </summary>
    public class DialogueCsvTool : EditorWindow
    {
        ObjectField graphField;
        Toggle autoKeyToggle;

        [MenuItem("Tools/剧情工具/文案导出导入 (CSV)")]
        public static void OpenWindow()
        {
            var window = GetWindow<DialogueCsvTool>("文案 CSV");
            window.minSize = new Vector2(360, 160);
        }

        void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;
            root.style.paddingTop = 8;

            graphField = new ObjectField("对话图")
            {
                objectType = typeof(DialogueGraphAsset),
                allowSceneObjects = false
            };
            root.Add(graphField);

            autoKeyToggle = new Toggle("导出时自动生成并回写 Loc Key")
            {
                value = false,
                tooltip = "对没有 Loc Key 的文案生成「图名_序号」格式的 Key 写回资产，并在本地化表中建立条目"
            };
            root.Add(autoKeyToggle);

            var buttons = new VisualElement();
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.marginTop = 8;
            buttons.Add(new Button(Export) { text = "导出 CSV" });
            buttons.Add(new Button(Import) { text = "从 CSV 导入" });
            root.Add(buttons);

            var hint = new Label(
                "导出文件为 UTF-8（带 BOM），Excel 可直接打开。\n" +
                "翻译列按本地化表 languages 顺序生成；导入按「行ID」匹配，\n" +
                "回写原文、Loc Key 与本地化表译文。导入后请重新打开对话图查看。");
            hint.style.whiteSpace = WhiteSpace.Normal;
            hint.style.color = new Color(0.6f, 0.6f, 0.6f);
            hint.style.marginTop = 8;
            root.Add(hint);
        }

        // ---------------- 文本行收集 ----------------

        class TextRow
        {
            public string lineId;
            public string nodeType;
            public string speaker;
            public LocalizedText text;
        }

        static List<TextRow> CollectRows(DialogueGraphAsset graph)
        {
            var rows = new List<TextRow>();
            foreach (var node in graph.nodes)
            {
                switch (node)
                {
                    case DialogueNodeData dialogue:
                        rows.Add(new TextRow
                        {
                            lineId = dialogue.guid + "/line",
                            nodeType = "对话",
                            speaker = dialogue.character != null ? dialogue.character.name : "旁白",
                            text = dialogue.line
                        });
                        break;
                    case ChoiceNodeData choice:
                        rows.Add(new TextRow
                        {
                            lineId = choice.guid + "/prompt",
                            nodeType = "选项提示",
                            speaker = choice.character != null ? choice.character.name : "旁白",
                            text = choice.prompt
                        });
                        foreach (var option in choice.options)
                        {
                            rows.Add(new TextRow
                            {
                                lineId = option.id + "/text",
                                nodeType = "选项",
                                speaker = "玩家",
                                text = option.text
                            });
                        }
                        break;
                }
            }
            return rows;
        }

        // ---------------- 导出 ----------------

        void Export()
        {
            var graph = graphField.value as DialogueGraphAsset;
            if (graph == null)
            {
                EditorUtility.DisplayDialog("提示", "请先选择对话图", "好");
                return;
            }

            string path = EditorUtility.SaveFilePanel("导出文案 CSV", "", graph.name + "_文案", "csv");
            if (string.IsNullOrEmpty(path)) return;

            var rows = CollectRows(graph);
            var table = graph.localizationTable;

            if (autoKeyToggle.value)
                AutoGenerateKeys(graph, rows, table);

            var sb = new StringBuilder();
            var header = new List<string> { "行ID", "类型", "说话人", "LocKey", "原文" };
            if (table != null) header.AddRange(table.languages);
            sb.AppendLine(string.Join(",", header.Select(Escape)));

            foreach (var row in rows)
            {
                var cells = new List<string>
                {
                    row.lineId,
                    row.nodeType,
                    row.speaker,
                    row.text.key ?? "",
                    row.text.fallbackText ?? ""
                };
                if (table != null)
                {
                    var entry = string.IsNullOrEmpty(row.text.key)
                        ? null
                        : table.entries.Find(e => e.key == row.text.key);
                    for (int i = 0; i < table.languages.Count; i++)
                        cells.Add(entry != null && i < entry.texts.Count ? entry.texts[i] ?? "" : "");
                }
                sb.AppendLine(string.Join(",", cells.Select(Escape)));
            }

            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
            EditorUtility.DisplayDialog("导出完成", $"共导出 {rows.Count} 行文案到：\n{path}", "好");
        }

        static void AutoGenerateKeys(DialogueGraphAsset graph, List<TextRow> rows, LocalizationTable table)
        {
            var usedKeys = new HashSet<string>(rows
                .Where(r => !string.IsNullOrEmpty(r.text.key))
                .Select(r => r.text.key));
            if (table != null)
                usedKeys.UnionWith(table.entries.Select(e => e.key));

            int counter = 1;
            foreach (var row in rows)
            {
                if (!string.IsNullOrEmpty(row.text.key)) continue;

                string key;
                do
                {
                    key = $"{graph.name}_{counter:D4}";
                    counter++;
                } while (usedKeys.Contains(key));

                usedKeys.Add(key);
                row.text.key = key;

                if (table != null && table.entries.All(e => e.key != key))
                    table.entries.Add(new LocalizationTable.Entry { key = key });
            }

            EditorUtility.SetDirty(graph);
            if (table != null) EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssets();
        }

        // ---------------- 导入 ----------------

        void Import()
        {
            var graph = graphField.value as DialogueGraphAsset;
            if (graph == null)
            {
                EditorUtility.DisplayDialog("提示", "请先选择对话图", "好");
                return;
            }

            string path = EditorUtility.OpenFilePanel("导入文案 CSV", "", "csv");
            if (string.IsNullOrEmpty(path)) return;

            var records = ParseCsv(File.ReadAllText(path, Encoding.UTF8));
            if (records.Count < 2)
            {
                EditorUtility.DisplayDialog("导入失败", "CSV 内容为空或只有表头", "好");
                return;
            }

            var header = records[0];
            int idCol = header.IndexOf("行ID");
            int keyCol = header.IndexOf("LocKey");
            int fallbackCol = header.IndexOf("原文");
            if (idCol < 0 || fallbackCol < 0)
            {
                EditorUtility.DisplayDialog("导入失败", "CSV 缺少「行ID」或「原文」列", "好");
                return;
            }

            var table = graph.localizationTable;
            var langCols = new Dictionary<int, int>(); // 列下标 → 语言下标
            if (table != null)
            {
                for (int c = 0; c < header.Count; c++)
                {
                    int langIndex = table.languages.IndexOf(header[c]);
                    if (langIndex >= 0) langCols[c] = langIndex;
                }
            }

            var rowsById = CollectRows(graph).ToDictionary(r => r.lineId);
            int updated = 0, unmatched = 0;

            for (int i = 1; i < records.Count; i++)
            {
                var record = records[i];
                if (record.Count <= idCol) continue;

                if (!rowsById.TryGetValue(record[idCol], out var row))
                {
                    unmatched++;
                    continue;
                }

                if (fallbackCol < record.Count)
                    row.text.fallbackText = record[fallbackCol];
                if (keyCol >= 0 && keyCol < record.Count)
                    row.text.key = record[keyCol];

                if (table != null && !string.IsNullOrEmpty(row.text.key))
                {
                    foreach (var kv in langCols)
                    {
                        if (kv.Key >= record.Count) continue;
                        var entry = table.entries.Find(e => e.key == row.text.key);
                        if (entry == null)
                        {
                            entry = new LocalizationTable.Entry { key = row.text.key };
                            table.entries.Add(entry);
                        }
                        while (entry.texts.Count <= kv.Value) entry.texts.Add("");
                        entry.texts[kv.Value] = record[kv.Key];
                    }
                }
                updated++;
            }

            EditorUtility.SetDirty(graph);
            if (table != null) EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog("导入完成",
                $"更新 {updated} 行，未匹配 {unmatched} 行。\n如对话图编辑器已打开，请重新选择该图刷新显示。", "好");
        }

        // ---------------- CSV 解析 ----------------

        static List<List<string>> ParseCsv(string content)
        {
            var records = new List<List<string>>();
            var current = new List<string>();
            var field = new StringBuilder();
            bool inQuotes = false;

            // 去掉 BOM
            if (content.Length > 0 && content[0] == '\uFEFF')
                content = content.Substring(1);

            for (int i = 0; i < content.Length; i++)
            {
                char c = content[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < content.Length && content[i + 1] == '"')
                        {
                            field.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        field.Append(c);
                    }
                }
                else
                {
                    switch (c)
                    {
                        case '"':
                            inQuotes = true;
                            break;
                        case ',':
                            current.Add(field.ToString());
                            field.Clear();
                            break;
                        case '\r':
                            break;
                        case '\n':
                            current.Add(field.ToString());
                            field.Clear();
                            if (current.Count > 1 || current[0].Length > 0)
                                records.Add(current);
                            current = new List<string>();
                            break;
                        default:
                            field.Append(c);
                            break;
                    }
                }
            }

            if (field.Length > 0 || current.Count > 0)
            {
                current.Add(field.ToString());
                if (current.Count > 1 || current[0].Length > 0)
                    records.Add(current);
            }
            return records;
        }

        static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n") || value.Contains("\r"))
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            return value;
        }
    }
}
