using System.Collections.Generic;
using System.Linq;

namespace DialogueGraph.Editor
{
    /// <summary>
    /// 对话图静态校验：断头连线、不可达节点、空文案、缺失表情 / Loc Key、
    /// 条件变量名为空、子图引用无效等。保存时自动执行。
    /// </summary>
    public static class GraphValidator
    {
        public enum Severity
        {
            Warning,
            Error
        }

        public class Issue
        {
            public Severity severity;
            public string message;
            public string nodeGuid;
        }

        public static List<Issue> Validate(DialogueGraphAsset asset)
        {
            var issues = new List<Issue>();
            if (asset == null) return issues;

            var nodes = asset.nodes.Where(n => n != null).ToList();
            var guids = new HashSet<string>(nodes.Select(n => n.guid));

            foreach (var node in nodes)
            {
                switch (node)
                {
                    case StartNodeData start:
                        CheckNext(issues, guids, start.guid, start.nextGuid, "开始节点");
                        break;

                    case DialogueNodeData dialogue:
                        CheckNext(issues, guids, dialogue.guid, dialogue.nextGuid, "对话节点");
                        CheckText(issues, asset, dialogue.guid, dialogue.line, "对话文案");
                        CheckExpression(issues, dialogue.guid, dialogue.character, dialogue.expression);
                        CheckCommands(issues, dialogue.guid, dialogue.commands);
                        break;

                    case ChoiceNodeData choice:
                        if (choice.options.Count == 0)
                            Add(issues, Severity.Error, choice.guid, "选项分支没有任何选项");
                        for (int i = 0; i < choice.options.Count; i++)
                        {
                            var option = choice.options[i];
                            if (string.IsNullOrEmpty(option.nextGuid) || !guids.Contains(option.nextGuid))
                                Add(issues, Severity.Warning, choice.guid, $"第 {i + 1} 个选项没有连线");
                            CheckText(issues, asset, choice.guid, option.text, $"第 {i + 1} 个选项文案");
                        }
                        // 提示文案允许为空（直接弹选项），只查 Loc Key
                        CheckText(issues, asset, choice.guid, choice.prompt, "选项提示文案", allowEmpty: true);
                        CheckExpression(issues, choice.guid, choice.character, choice.expression);
                        break;

                    case ConditionNodeData condition:
                        if (string.IsNullOrEmpty(condition.condition.variableName))
                            Add(issues, Severity.Error, condition.guid, "条件节点未填变量名");
                        CheckNext(issues, guids, condition.guid, condition.trueGuid, "条件节点「满足」分支");
                        CheckNext(issues, guids, condition.guid, condition.falseGuid, "条件节点「不满足」分支");
                        break;

                    case EventNodeData evt:
                        if (string.IsNullOrEmpty(evt.eventName))
                            Add(issues, Severity.Warning, evt.guid, "事件节点未填事件名");
                        CheckNext(issues, guids, evt.guid, evt.nextGuid, "事件节点");
                        break;

                    case StageNodeData stage:
                        if (stage.commands.Count == 0)
                            Add(issues, Severity.Warning, stage.guid, "演出节点没有任何指令");
                        CheckNext(issues, guids, stage.guid, stage.nextGuid, "演出节点");
                        CheckCommands(issues, stage.guid, stage.commands);
                        break;

                    case SubGraphNodeData sub:
                        if (sub.graph == null)
                            Add(issues, Severity.Error, sub.guid, "子图节点未指定子图资产");
                        else if (sub.graph == asset)
                            Add(issues, Severity.Error, sub.guid, "子图节点引用了图自身（会无限递归）");
                        else if (sub.graph.GetStartNode() == null)
                            Add(issues, Severity.Error, sub.guid, $"子图「{sub.graph.name}」缺少开始节点");
                        break;
                }
            }

            CheckReachability(issues, asset, nodes);
            return issues;
        }

        static void CheckReachability(List<Issue> issues, DialogueGraphAsset asset, List<BaseNodeData> nodes)
        {
            var start = asset.GetStartNode();
            if (start == null)
            {
                Add(issues, Severity.Error, null, "图中缺少开始节点");
                return;
            }

            var byGuid = nodes.ToDictionary(n => n.guid);
            var visited = new HashSet<string>();
            var queue = new Queue<string>();
            queue.Enqueue(start.guid);

            while (queue.Count > 0)
            {
                var guid = queue.Dequeue();
                if (string.IsNullOrEmpty(guid) || !visited.Add(guid)) continue;
                if (!byGuid.TryGetValue(guid, out var node)) continue;
                foreach (var next in GetNextGuids(node))
                    queue.Enqueue(next);
            }

            foreach (var node in nodes)
            {
                if (!visited.Contains(node.guid))
                    Add(issues, Severity.Warning, node.guid, $"{GetNodeLabel(node)}从开始节点不可达");
            }
        }

        static IEnumerable<string> GetNextGuids(BaseNodeData node)
        {
            switch (node)
            {
                case StartNodeData s:
                    yield return s.nextGuid;
                    break;
                case DialogueNodeData d:
                    yield return d.nextGuid;
                    break;
                case ChoiceNodeData c:
                    foreach (var option in c.options) yield return option.nextGuid;
                    break;
                case ConditionNodeData cond:
                    yield return cond.trueGuid;
                    yield return cond.falseGuid;
                    break;
                case EventNodeData e:
                    yield return e.nextGuid;
                    break;
                case StageNodeData st:
                    yield return st.nextGuid;
                    break;
                case SubGraphNodeData sub:
                    yield return sub.nextGuid;
                    break;
            }
        }

        static string GetNodeLabel(BaseNodeData node)
        {
            switch (node)
            {
                case DialogueNodeData _: return "对话节点";
                case ChoiceNodeData _: return "选项分支节点";
                case ConditionNodeData _: return "条件节点";
                case EventNodeData _: return "事件节点";
                case StageNodeData _: return "演出节点";
                case SubGraphNodeData _: return "子图节点";
                case EndNodeData _: return "结束节点";
                default: return "节点";
            }
        }

        static void CheckNext(List<Issue> issues, HashSet<string> guids, string nodeGuid, string nextGuid,
            string label)
        {
            if (string.IsNullOrEmpty(nextGuid))
                Add(issues, Severity.Warning, nodeGuid, $"{label}没有连接下一步（执行到此即结束/返回）");
            else if (!guids.Contains(nextGuid))
                Add(issues, Severity.Error, nodeGuid, $"{label}的连线目标不存在（数据损坏，请重新连线并保存）");
        }

        static void CheckText(List<Issue> issues, DialogueGraphAsset asset, string nodeGuid, LocalizedText text,
            string label, bool allowEmpty = false)
        {
            if (text == null || (string.IsNullOrEmpty(text.key) && string.IsNullOrEmpty(text.fallbackText)))
            {
                if (!allowEmpty)
                    Add(issues, Severity.Warning, nodeGuid, $"{label}为空");
                return;
            }

            if (string.IsNullOrEmpty(text.key)) return;

            var table = asset.localizationTable;
            if (table == null)
            {
                Add(issues, Severity.Warning, nodeGuid, $"{label}填了 Loc Key「{text.key}」但图未配置本地化表");
                return;
            }

            var entry = table.entries.Find(e => e.key == text.key);
            if (entry == null)
            {
                Add(issues, Severity.Error, nodeGuid, $"Loc Key「{text.key}」在本地化表中不存在");
                return;
            }

            for (int i = 0; i < table.languages.Count; i++)
            {
                if (i >= entry.texts.Count || string.IsNullOrEmpty(entry.texts[i]))
                    Add(issues, Severity.Warning, nodeGuid,
                        $"Loc Key「{text.key}」缺少 {table.languages[i]} 译文");
            }
        }

        static void CheckExpression(List<Issue> issues, string nodeGuid, CharacterAsset character,
            string expression)
        {
            if (character == null || string.IsNullOrEmpty(expression)) return;
            if (!character.HasExpression(expression))
                Add(issues, Severity.Error, nodeGuid, $"角色「{character.name}」不存在表情「{expression}」");
        }

        static void CheckCommands(List<Issue> issues, string nodeGuid, List<StageCommand> commands)
        {
            if (commands == null) return;
            foreach (var command in commands)
            {
                switch (command)
                {
                    case ShowCharacterCommand show:
                        if (show.character == null)
                            Add(issues, Severity.Warning, nodeGuid, "「显示立绘」指令未指定角色");
                        else if (!string.IsNullOrEmpty(show.expression) && !show.character.HasExpression(show.expression))
                            Add(issues, Severity.Error, nodeGuid,
                                $"「显示立绘」指令：角色「{show.character.name}」不存在表情「{show.expression}」");
                        break;
                    case SetBackgroundCommand bg:
                        if (bg.background == null)
                            Add(issues, Severity.Warning, nodeGuid, "「切换背景」指令未指定背景图");
                        break;
                    case PlayBgmCommand bgm:
                        if (bgm.clip == null)
                            Add(issues, Severity.Warning, nodeGuid, "「播放BGM」指令未指定音频");
                        break;
                    case PlaySfxCommand sfx:
                        if (sfx.clip == null)
                            Add(issues, Severity.Warning, nodeGuid, "「播放音效」指令未指定音频");
                        break;
                }
            }
        }

        static void Add(List<Issue> issues, Severity severity, string nodeGuid, string message)
        {
            issues.Add(new Issue { severity = severity, nodeGuid = nodeGuid, message = message });
        }
    }
}
