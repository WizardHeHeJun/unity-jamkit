using System.Collections.Generic;
using GameplayKit.Quest;
using GameplayKit.Director;

namespace GameplayKit.Director.Editor
{
    /// <summary>导演编排静态校验。保存时自动执行，问题列表点击可定位规则。</summary>
    public static class DirectorValidator
    {
        public class Issue
        {
            public string message;
            public int ruleIndex = -1;
            public bool isError = true;
        }

        public static List<Issue> Validate(DirectorAsset asset)
        {
            var issues = new List<Issue>();
            if (asset == null) return issues;

            var questIds = CollectQuestIds(asset.questGraph);
            var seenIds = new HashSet<string>();

            for (int i = 0; i < asset.rules.Count; i++)
            {
                var rule = asset.rules[i];
                if (rule == null) continue;
                string label = string.IsNullOrEmpty(rule.title) ? $"规则 {i + 1}" : rule.title;

                if (string.IsNullOrEmpty(rule.id))
                    Add(issues, i, $"{label}：缺少稳定 ID（请删除后重建该规则）");
                else if (!seenIds.Add(rule.id))
                    Add(issues, i, $"{label}：规则 ID 重复");

                ValidateTrigger(issues, i, label, rule, asset, questIds);
                ValidateCondition(issues, i, label, rule, asset);
                ValidateActions(issues, i, label, rule, asset, questIds);
            }
            return issues;
        }

        static void ValidateTrigger(List<Issue> issues, int index, string label,
            DirectorRule rule, DirectorAsset asset, HashSet<string> questIds)
        {
            switch (rule.trigger)
            {
                case null:
                    Add(issues, index, $"{label}：没有触发器");
                    break;

                case GameEventTrigger e when string.IsNullOrEmpty(e.eventName):
                    Add(issues, index, $"{label}：事件触发器未填事件名");
                    break;

                case VariableTrigger _ when rule.condition.IsEmpty:
                    Add(issues, index, $"{label}：黑板条件触发器没有任何条件，开局即触发", error: false);
                    break;

                case QuestStateTrigger q:
                    if (string.IsNullOrEmpty(q.questId))
                        Add(issues, index, $"{label}：任务状态触发器未选任务");
                    else if (questIds != null && !questIds.Contains(q.questId))
                        Add(issues, index, $"{label}：任务「{q.questId}」在任务图中不存在");
                    break;
            }
        }

        static void ValidateCondition(List<Issue> issues, int index, string label,
            DirectorRule rule, DirectorAsset asset)
        {
            foreach (var clause in rule.condition.clauses)
            {
                if (clause == null) continue;
                if (string.IsNullOrEmpty(clause.variableName))
                    Add(issues, index, $"{label}：条件中有未选变量的比较");
                else if (asset.blackboard != null && !asset.blackboard.Has(clause.variableName))
                    Add(issues, index, $"{label}：条件引用的变量「{clause.variableName}」不在黑板中");
            }
        }

        static void ValidateActions(List<Issue> issues, int index, string label,
            DirectorRule rule, DirectorAsset asset, HashSet<string> questIds)
        {
            if (rule.actions.Count == 0)
            {
                Add(issues, index, $"{label}：没有任何动作", error: false);
                return;
            }

            foreach (var action in rule.actions)
            {
                switch (action)
                {
                    case StartDialogueAction d when d.dialogue == null:
                        Add(issues, index, $"{label}：「开始对话」未指定对话图");
                        break;

                    case SetVariableAction v:
                        if (string.IsNullOrEmpty(v.variableName))
                            Add(issues, index, $"{label}：「修改变量」未选变量");
                        else if (asset.blackboard != null && !asset.blackboard.Has(v.variableName))
                            Add(issues, index, $"{label}：「修改变量」引用的变量「{v.variableName}」不在黑板中");
                        if (v.op == SetVariableOp.Add &&
                            (v.variableType == BlackboardVariableType.Bool ||
                             v.variableType == BlackboardVariableType.String))
                            Add(issues, index, $"{label}：布尔 / 文本变量不支持「累加」");
                        break;

                    case RaiseEventAction r when string.IsNullOrEmpty(r.eventName):
                        Add(issues, index, $"{label}：「抛游戏事件」未填事件名");
                        break;

                    case AcceptQuestAction a:
                        CheckQuestId(issues, index, label, "接取任务", a.questId, questIds);
                        break;

                    case CompleteQuestAction c:
                        CheckQuestId(issues, index, label, "完成任务", c.questId, questIds);
                        break;

                    case ReportProgressAction p when string.IsNullOrEmpty(p.objectiveType):
                        Add(issues, index, $"{label}：「上报任务进度」未填目标类型");
                        break;
                }
            }
        }

        static void CheckQuestId(List<Issue> issues, int index, string label,
            string actionName, string questId, HashSet<string> questIds)
        {
            if (string.IsNullOrEmpty(questId))
                Add(issues, index, $"{label}：「{actionName}」未选任务");
            else if (questIds != null && !questIds.Contains(questId))
                Add(issues, index, $"{label}：「{actionName}」的任务「{questId}」在任务图中不存在");
        }

        /// <summary>任务图里全部任务 ID；未配任务图时返回 null（跳过存在性检查）。</summary>
        public static HashSet<string> CollectQuestIds(QuestGraphAsset questGraph)
        {
            if (questGraph == null) return null;
            var ids = new HashSet<string>();
            foreach (var node in questGraph.nodes)
            {
                if (node is QuestNodeData quest && !string.IsNullOrEmpty(quest.questId))
                    ids.Add(quest.questId);
            }
            return ids;
        }

        static void Add(List<Issue> issues, int ruleIndex, string message, bool error = true)
        {
            issues.Add(new Issue { message = message, ruleIndex = ruleIndex, isError = error });
        }
    }
}
