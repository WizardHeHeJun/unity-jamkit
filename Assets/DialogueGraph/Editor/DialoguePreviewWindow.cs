using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DialogueGraph.Editor
{
    /// <summary>
    /// 编辑器内剧情预览：不进 Play Mode，以文本日志形式跑剧情。
    /// 左侧变量表驱动条件节点，演出 / 事件节点以日志行显示并自动继续。
    /// </summary>
    public class DialoguePreviewWindow : EditorWindow
    {
        // 顺序与 VariableType 枚举一一对应
        static readonly List<string> TypeNames = new List<string> { "布尔", "整数", "小数", "文本" };
        static readonly List<string> SlotNames = new List<string> { "左", "中", "右" };

        [SerializeField] DialogueGraphAsset graph;
        [SerializeField] string startGuid;

        DialogueRunner runner;
        readonly DialogueVariableStore variables = new DialogueVariableStore();

        ObjectField graphField;
        TextField languageField;
        ScrollView logView;
        VisualElement actionBar;
        VisualElement variableRows;
        TextField newVarName;
        DropdownField newVarType;

        [MenuItem("Tools/剧情工具/剧情预览")]
        public static void OpenWindow()
        {
            GetWindow<DialoguePreviewWindow>("剧情预览");
        }

        public static void Open(DialogueGraphAsset graphAsset, string fromNodeGuid = null)
        {
            var window = GetWindow<DialoguePreviewWindow>("剧情预览");
            window.graph = graphAsset;
            window.startGuid = fromNodeGuid;
            window.graphField?.SetValueWithoutNotify(graphAsset);
            window.Restart();
        }

        void CreateGUI()
        {
            var toolbar = new Toolbar();

            graphField = new ObjectField
            {
                objectType = typeof(DialogueGraphAsset),
                allowSceneObjects = false
            };
            graphField.style.minWidth = 200;
            graphField.RegisterValueChangedCallback(e =>
            {
                graph = e.newValue as DialogueGraphAsset;
                startGuid = null;
                Restart();
            });
            toolbar.Add(graphField);

            languageField = new TextField { value = "zh-CN", tooltip = "语言代码，对应本地化表" };
            languageField.style.width = 70;
            toolbar.Add(languageField);

            toolbar.Add(new ToolbarButton(Restart) { text = "重新开始" });
            rootVisualElement.Add(toolbar);

            var split = new VisualElement();
            split.style.flexDirection = FlexDirection.Row;
            split.style.flexGrow = 1;
            rootVisualElement.Add(split);

            // 左侧：变量面板
            var left = new VisualElement();
            left.style.width = 230;
            left.style.borderRightWidth = 1;
            left.style.borderRightColor = new Color(0.1f, 0.1f, 0.1f);
            left.style.paddingLeft = 4;
            left.style.paddingRight = 4;

            var varHeader = new Label("变量（驱动条件节点）");
            varHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            varHeader.style.marginTop = 4;
            left.Add(varHeader);

            variableRows = new ScrollView();
            variableRows.style.flexGrow = 1;
            left.Add(variableRows);

            var addRow = new VisualElement();
            addRow.style.flexDirection = FlexDirection.Row;
            newVarName = new TextField { tooltip = "变量名" };
            newVarName.style.flexGrow = 1;
            newVarType = new DropdownField(TypeNames, 0);
            newVarType.style.width = 56;
            addRow.Add(newVarName);
            addRow.Add(newVarType);
            addRow.Add(new Button(AddVariableFromInput) { text = "＋" });
            left.Add(addRow);
            split.Add(left);

            // 右侧：日志 + 操作栏
            var right = new VisualElement();
            right.style.flexGrow = 1;

            logView = new ScrollView();
            logView.style.flexGrow = 1;
            logView.style.paddingLeft = 6;
            logView.style.paddingTop = 4;
            right.Add(logView);

            actionBar = new VisualElement();
            actionBar.style.flexDirection = FlexDirection.Row;
            actionBar.style.flexWrap = Wrap.Wrap;
            actionBar.style.minHeight = 28;
            actionBar.style.borderTopWidth = 1;
            actionBar.style.borderTopColor = new Color(0.1f, 0.1f, 0.1f);
            right.Add(actionBar);
            split.Add(right);

            if (graph != null)
            {
                graphField.SetValueWithoutNotify(graph);
                Restart();
            }
        }

        void AddVariableFromInput()
        {
            var name = newVarName.value?.Trim();
            if (string.IsNullOrEmpty(name)) return;
            var type = (VariableType)Mathf.Max(0, newVarType.index);
            newVarName.value = string.Empty;
            AddVariableRow(name, type);
        }

        void AddVariableRow(string name, VariableType type)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;

            var nameLabel = new Label(name) { tooltip = name };
            nameLabel.style.width = 76;
            nameLabel.style.overflow = Overflow.Hidden;
            row.Add(nameLabel);

            VisualElement valueField;
            switch (type)
            {
                case VariableType.Bool:
                    variables.SetBool(name, false);
                    var toggle = new Toggle { value = false };
                    toggle.RegisterValueChangedCallback(e => variables.SetBool(name, e.newValue));
                    valueField = toggle;
                    break;
                case VariableType.Int:
                    variables.SetInt(name, 0);
                    var intField = new IntegerField { value = 0 };
                    intField.RegisterValueChangedCallback(e => variables.SetInt(name, e.newValue));
                    valueField = intField;
                    break;
                case VariableType.Float:
                    variables.SetFloat(name, 0f);
                    var floatField = new FloatField { value = 0f };
                    floatField.RegisterValueChangedCallback(e => variables.SetFloat(name, e.newValue));
                    valueField = floatField;
                    break;
                default:
                    variables.SetString(name, string.Empty);
                    var textField = new TextField { value = string.Empty };
                    textField.RegisterValueChangedCallback(e => variables.SetString(name, e.newValue));
                    valueField = textField;
                    break;
            }
            valueField.style.flexGrow = 1;
            row.Add(valueField);

            row.Add(new Button(() =>
            {
                variables.Remove(name);
                row.RemoveFromHierarchy();
            }) { text = "✕" });

            variableRows.Add(row);
        }

        // ---------------- 执行 ----------------

        void Restart()
        {
            if (logView == null) return; // CreateGUI 尚未执行，结束时会再调

            logView.Clear();
            actionBar.Clear();

            if (graph == null)
            {
                AppendInfo("在上方选择一个对话图开始预览");
                return;
            }

            runner = new DialogueRunner
            {
                Language = string.IsNullOrEmpty(languageField.value) ? "zh-CN" : languageField.value,
                Variables = variables
            };
            runner.OnLine += HandleLine;
            runner.OnChoices += HandleChoices;
            runner.OnStage += HandleStage;
            runner.OnEvent += (eventName, param) =>
                AppendInfo($"⚡ 事件：{eventName}{(string.IsNullOrEmpty(param) ? "" : $"（{param}）")}");
            runner.OnEnded += () =>
            {
                AppendInfo("—— 剧情结束 ——");
                actionBar.Clear();
            };

            if (!string.IsNullOrEmpty(startGuid))
                AppendInfo("（从选中节点开始）");
            runner.BeginFrom(graph, startGuid);
        }

        void HandleLine(DialogueLine line)
        {
            LogCommands(line.commands);
            AppendSpeech(string.IsNullOrEmpty(line.speakerName) ? "旁白" : line.speakerName, line.text);

            actionBar.Clear();
            actionBar.Add(new Button(() => runner.Continue()) { text = "继续 ▶" });
        }

        void HandleChoices(ChoicePrompt prompt)
        {
            if (!string.IsNullOrEmpty(prompt.promptText))
                AppendSpeech(string.IsNullOrEmpty(prompt.speakerName) ? "旁白" : prompt.speakerName,
                    prompt.promptText);

            actionBar.Clear();
            for (int i = 0; i < prompt.options.Count; i++)
            {
                int index = i;
                actionBar.Add(new Button(() =>
                {
                    AppendInfo($"→ 选择了「{prompt.options[index].text}」");
                    runner.SelectChoice(index);
                }) { text = $"{i + 1}. {prompt.options[i].text}" });
            }
        }

        void HandleStage(IReadOnlyList<StageCommand> commands)
        {
            AppendInfo("▶ 演出节点（预览中自动继续）");
            LogCommands(commands);
            runner.Continue();
        }

        void LogCommands(IReadOnlyList<StageCommand> commands)
        {
            if (commands == null) return;
            foreach (var command in commands)
                AppendInfo("　· " + DescribeCommand(command));
        }

        static string DescribeCommand(StageCommand command)
        {
            switch (command)
            {
                case ShowCharacterCommand show:
                    return $"显示立绘：{(show.character != null ? show.character.name : "(未指定)")}" +
                           $"{(string.IsNullOrEmpty(show.expression) ? "" : $"[{show.expression}]")} @ {SlotNames[(int)show.slot]}";
                case HideCharacterCommand hide:
                    return $"隐藏立绘：{(hide.character != null ? hide.character.name : "全部")}";
                case SetBackgroundCommand bg:
                    return $"切换背景：{(bg.background != null ? bg.background.name : "(未指定)")}";
                case PlayBgmCommand bgm:
                    return $"播放BGM：{(bgm.clip != null ? bgm.clip.name : "(未指定)")}";
                case StopBgmCommand _:
                    return "停止BGM";
                case PlaySfxCommand sfx:
                    return $"播放音效：{(sfx.clip != null ? sfx.clip.name : "(未指定)")}";
                case WaitCommand wait:
                    return $"等待 {wait.seconds:0.##} 秒";
                default:
                    return command != null ? command.GetType().Name : "(空指令)";
            }
        }

        // ---------------- 日志输出 ----------------

        void AppendSpeech(string speaker, string text)
        {
            var label = new Label($"{speaker}：{text}");
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginBottom = 4;
            logView.Add(label);
            ScrollToBottom();
        }

        void AppendInfo(string message)
        {
            var label = new Label(message);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.color = new Color(0.6f, 0.6f, 0.6f);
            label.style.marginBottom = 2;
            logView.Add(label);
            ScrollToBottom();
        }

        void ScrollToBottom()
        {
            logView.schedule.Execute(() =>
            {
                if (logView.verticalScroller != null)
                    logView.verticalScroller.value = logView.verticalScroller.highValue;
            }).StartingIn(30);
        }
    }
}
