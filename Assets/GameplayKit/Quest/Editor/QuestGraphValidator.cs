using System.Collections.Generic;
using System.Linq;
using GameplayKit;

namespace GameplayKit.Quest.Editor
{
    /// <summary>
    /// 任务图静态校验：任务 ID 重复 / 为空、断头连线、循环依赖、
    /// 条件引用了黑板中不存在的变量、目标数量非法等。保存时自动执行。
    /// </summary>
    public static class QuestGraphValidator
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

        public static List<Issue> Validate(QuestGraphAsset asset)
        {
            var issues = new List<Issue>();
            if (asset == null) return issues;

            var nodes = asset.nodes.Where(n => n != null).ToList();
            var guids = new HashSet<string>(nodes.Select(n => n.guid));
            var predecessors = asset.BuildPredecessorMap();

            // 连线目标存在性
            foreach (var node in nodes)
            {
                foreach (var next in node.nextGuids)
                {
                    if (!string.IsNullOrEmpty(next) && !guids.Contains(next))
                        Add(issues, Severity.Error, node.guid,
                            $"{GetNodeLabel(node)}的连线目标不存在（数据损坏，请重新连线并保存）");
                }
            }

            // 任务 ID 重复
            var duplicateIds = nodes.OfType<QuestNodeData>()
                .Where(q => !string.IsNullOrEmpty(q.questId))
                .GroupBy(q => q.questId)
                .Where(g => g.Count() > 1);
            foreach (var group in duplicateIds)
            {
                foreach (var quest in group)
                    Add(issues, Severity.Error, quest.guid, $"任务 ID「{group.Key}」重复，存档与代码引用会串");
            }

            foreach (var node in nodes)
            {
                switch (node)
                {
                    case QuestNodeData quest:
                        ValidateQuest(issues, asset, quest);
                        break;

                    case ConditionGateNodeData gate:
                        if (gate.condition.IsEmpty)
                            Add(issues, Severity.Warning, gate.guid, "条件门没有任何条件（恒通过，等于不需要这个门）");
                        ValidateCondition(issues, asset, gate.guid, gate.condition, "条件门");
                        if (gate.nextGuids.Count == 0)
                            Add(issues, Severity.Warning, gate.guid, "条件门没有连接任何后续节点");
                        break;

                    case JoinAllNodeData join:
                        int inCount = predecessors.TryGetValue(join.guid, out var list) ? list.Count : 0;
                        if (inCount < 2)
                            Add(issues, Severity.Warning, join.guid,
                                $"汇合节点只有 {inCount} 条进入连线，至少 2 条才有意义");
                        if (join.nextGuids.Count == 0)
                            Add(issues, Severity.Warning, join.guid, "汇合节点没有连接任何后续节点");
                        break;
                }
            }

            CheckReachability(issues, asset, nodes);
            CheckCycles(issues, nodes);
            return issues;
        }

        static void ValidateQuest(List<Issue> issues, QuestGraphAsset asset, QuestNodeData quest)
        {
            if (string.IsNullOrEmpty(quest.questId))
                Add(issues, Severity.Error, quest.guid, "任务未填任务 ID（存档与代码引用依赖它）");
            if (string.IsNullOrEmpty(quest.title))
                Add(issues, Severity.Warning, quest.guid, "任务未填标题");

            if (quest.objectives.Count == 0)
                Add(issues, Severity.Warning, quest.guid, "任务没有目标，接取后会立即完成（剧情任务可忽略）");

            for (int i = 0; i < quest.objectives.Count; i++)
            {
                var objective = quest.objectives[i];
                if (objective.requiredCount <= 0)
                    Add(issues, Severity.Error, quest.guid, $"第 {i + 1} 个目标的需求数量必须大于 0");
                if (string.IsNullOrEmpty(objective.objectiveType))
                    Add(issues, Severity.Error, quest.guid, $"第 {i + 1} 个目标未选类型");
                else if (!QuestObjectiveTypes.All.Contains(objective.objectiveType))
                    Add(issues, Severity.Warning, quest.guid,
                        $"第 {i + 1} 个目标的类型「{objective.objectiveType}」不在注册列表中（游戏侧可能不会上报）");
                if (string.IsNullOrEmpty(objective.description))
                    Add(issues, Severity.Warning, quest.guid, $"第 {i + 1} 个目标未填追踪文案");
            }

            for (int i = 0; i < quest.rewards.Count; i++)
            {
                var reward = quest.rewards[i];
                if (string.IsNullOrEmpty(reward.rewardId))
                    Add(issues, Severity.Warning, quest.guid, $"第 {i + 1} 条奖励未填奖励 ID");
                if (reward.amount <= 0)
                    Add(issues, Severity.Warning, quest.guid, $"第 {i + 1} 条奖励数量小于等于 0");
            }

            ValidateCondition(issues, asset, quest.guid, quest.unlockCondition, "额外解锁条件");
        }

        static void ValidateCondition(List<Issue> issues, QuestGraphAsset asset, string nodeGuid,
            GameCondition condition, string label)
        {
            if (condition == null || condition.IsEmpty) return;

            if (asset.blackboard == null)
            {
                Add(issues, Severity.Warning, nodeGuid, $"{label}使用了条件但图未配置黑板，变量无法校验与下拉选择");
                return;
            }

            for (int i = 0; i < condition.clauses.Count; i++)
            {
                var clause = condition.clauses[i];
                if (clause == null || string.IsNullOrEmpty(clause.variableName))
                {
                    Add(issues, Severity.Error, nodeGuid, $"{label}第 {i + 1} 条未选变量");
                    continue;
                }

                var def = asset.blackboard.Find(clause.variableName);
                if (def == null)
                    Add(issues, Severity.Error, nodeGuid,
                        $"{label}引用的变量「{clause.variableName}」在黑板中不存在");
                else if (def.type != clause.variableType)
                    Add(issues, Severity.Error, nodeGuid,
                        $"{label}中变量「{clause.variableName}」的类型与黑板声明不一致（重新选择一次该变量即可修复）");
            }
        }

        static void CheckReachability(List<Issue> issues, QuestGraphAsset asset, List<QuestBaseNodeData> nodes)
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
                foreach (var next in node.nextGuids)
                    queue.Enqueue(next);
            }

            foreach (var node in nodes)
            {
                if (!visited.Contains(node.guid))
                    Add(issues, Severity.Warning, node.guid, $"{GetNodeLabel(node)}从开始节点不可达，永远不会解锁");
            }
        }

        static void CheckCycles(List<Issue> issues, List<QuestBaseNodeData> nodes)
        {
            var byGuid = nodes.ToDictionary(n => n.guid);
            var state = new Dictionary<string, int>(); // 0 未访问 1 访问中 2 完成
            var reported = false;

            foreach (var node in nodes)
            {
                if (Visit(node.guid)) break;
            }

            bool Visit(string guid)
            {
                if (reported || string.IsNullOrEmpty(guid) || !byGuid.TryGetValue(guid, out var node)) return false;
                state.TryGetValue(guid, out var s);
                if (s == 1)
                {
                    Add(issues, Severity.Error, guid, $"{GetNodeLabel(node)}处存在循环依赖（任务链会死锁）");
                    reported = true;
                    return true;
                }
                if (s == 2) return false;

                state[guid] = 1;
                foreach (var next in node.nextGuids)
                {
                    if (Visit(next)) return true;
                }
                state[guid] = 2;
                return false;
            }
        }

        static string GetNodeLabel(QuestBaseNodeData node)
        {
            switch (node)
            {
                case QuestStartNodeData _: return "开始节点";
                case QuestNodeData quest:
                    return string.IsNullOrEmpty(quest.title) ? "任务节点" : $"任务「{quest.title}」";
                case ConditionGateNodeData _: return "条件门";
                case JoinAllNodeData _: return "汇合节点";
                default: return "节点";
            }
        }

        static void Add(List<Issue> issues, Severity severity, string nodeGuid, string message)
        {
            issues.Add(new Issue { severity = severity, nodeGuid = nodeGuid, message = message });
        }
    }
}
