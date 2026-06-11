using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameplayKit.Quest
{
    public enum QuestState
    {
        /// <summary>前置或解锁条件未满足。</summary>
        Locked,

        /// <summary>已解锁，等待接取。</summary>
        Available,

        /// <summary>进行中。</summary>
        Active,

        Completed
    }

    /// <summary>任务系统存档快照，可直接 JsonUtility 序列化。黑板快照请另行保存。</summary>
    [Serializable]
    public class QuestRunnerState
    {
        [Serializable]
        public class QuestRecord
        {
            public string questId;
            public int state;
            public List<int> objectiveCounts = new List<int>();
        }

        public List<QuestRecord> quests = new List<QuestRecord>();
    }

    /// <summary>
    /// 任务图执行器（纯 C#，不依赖场景）。
    /// 解锁规则：任务的前置连线「任一满足」即可（需要全部满足时在图里加汇合节点），
    /// 再叠加自身的额外解锁条件；条件不成立时挂起，黑板变化后自动复评。
    /// 进度由游戏侧 <see cref="ReportProgress"/> 上报；全部目标达成自动完成并发奖。
    /// </summary>
    public class QuestRunner
    {
        /// <summary>一个任务的运行时状态。</summary>
        public class QuestEntry
        {
            public QuestNodeData Data { get; internal set; }
            public QuestState State { get; internal set; }

            internal int[] counts;

            public int GetObjectiveCount(int index) =>
                counts != null && index >= 0 && index < counts.Length ? counts[index] : 0;

            public bool AllObjectivesDone
            {
                get
                {
                    for (int i = 0; i < Data.objectives.Count; i++)
                    {
                        if (counts[i] < Mathf.Max(1, Data.objectives[i].requiredCount))
                            return false;
                    }
                    return true;
                }
            }
        }

        QuestGraphAsset graph;
        Blackboard blackboard;

        readonly Dictionary<string, QuestBaseNodeData> nodesByGuid = new Dictionary<string, QuestBaseNodeData>();
        readonly Dictionary<string, List<string>> predecessors = new Dictionary<string, List<string>>();
        readonly Dictionary<string, QuestEntry> questsByGuid = new Dictionary<string, QuestEntry>();
        readonly Dictionary<string, QuestEntry> questsById = new Dictionary<string, QuestEntry>();

        /// <summary>已「通过」的节点（开始 / 已完成任务 / 已通过的门与汇合）。</summary>
        readonly HashSet<string> satisfied = new HashSet<string>();

        /// <summary>前置已满足、只差黑板条件的节点，黑板变化时复评。</summary>
        readonly HashSet<string> pendingCondition = new HashSet<string>();

        bool silent; // 恢复存档期间不抛事件

        public event Action<QuestEntry> OnQuestAvailable;
        public event Action<QuestEntry> OnQuestAccepted;
        /// <summary>(任务, 目标下标, 当前计数)。</summary>
        public event Action<QuestEntry, int, int> OnObjectiveProgress;
        public event Action<QuestEntry> OnQuestCompleted;
        public event Action<QuestEntry, QuestRewardData> OnRewardGranted;

        public IEnumerable<QuestEntry> Quests => questsByGuid.Values;

        public QuestEntry FindQuest(string questId) =>
            questId != null && questsById.TryGetValue(questId, out var entry) ? entry : null;

        public QuestState GetState(string questId) => FindQuest(questId)?.State ?? QuestState.Locked;

        /// <summary>开始一局新游戏：从开始节点向后解锁。</summary>
        public void Begin(QuestGraphAsset questGraph, Blackboard board)
        {
            Initialize(questGraph, board);
            var start = graph.GetStartNode();
            if (start == null)
            {
                Debug.LogError($"任务图「{graph.name}」缺少开始节点");
                return;
            }
            MarkSatisfied(start.guid);
        }

        /// <summary>从存档恢复（不触发任何事件，恢复后请整体刷新任务 UI）。</summary>
        public void RestoreFromJson(QuestGraphAsset questGraph, Blackboard board, string json)
        {
            Initialize(questGraph, board);
            var state = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<QuestRunnerState>(json);

            silent = true;
            try
            {
                if (state != null)
                {
                    foreach (var record in state.quests)
                    {
                        if (!questsById.TryGetValue(record.questId, out var entry)) continue; // 图已改版，丢弃旧记录
                        entry.State = (QuestState)record.state;
                        for (int i = 0; i < entry.counts.Length && i < record.objectiveCounts.Count; i++)
                            entry.counts[i] = record.objectiveCounts[i];
                    }
                }

                // 重建「已通过」集合并重新传播，门/汇合的通过状态按当前黑板重算
                var start = graph.GetStartNode();
                if (start != null) MarkSatisfied(start.guid);
                foreach (var entry in questsByGuid.Values)
                {
                    if (entry.State == QuestState.Completed)
                        MarkSatisfied(entry.Data.guid);
                }
            }
            finally
            {
                silent = false;
            }
        }

        public string CaptureSaveJson()
        {
            var state = new QuestRunnerState();
            foreach (var entry in questsByGuid.Values)
            {
                state.quests.Add(new QuestRunnerState.QuestRecord
                {
                    questId = entry.Data.questId,
                    state = (int)entry.State,
                    objectiveCounts = entry.counts.ToList()
                });
            }
            return JsonUtility.ToJson(state);
        }

        /// <summary>解绑黑板事件。更换图 / 丢弃 Runner 前调用。</summary>
        public void Detach()
        {
            if (blackboard != null)
                blackboard.OnValueChanged -= OnBlackboardChanged;
            blackboard = null;
        }

        void Initialize(QuestGraphAsset questGraph, Blackboard board)
        {
            Detach();
            graph = questGraph;
            blackboard = board;
            if (blackboard != null)
                blackboard.OnValueChanged += OnBlackboardChanged;

            nodesByGuid.Clear();
            predecessors.Clear();
            questsByGuid.Clear();
            questsById.Clear();
            satisfied.Clear();
            pendingCondition.Clear();

            foreach (var node in graph.nodes)
            {
                if (node == null || string.IsNullOrEmpty(node.guid)) continue;
                nodesByGuid[node.guid] = node;

                if (node is QuestNodeData quest)
                {
                    var entry = new QuestEntry
                    {
                        Data = quest,
                        State = QuestState.Locked,
                        counts = new int[quest.objectives.Count]
                    };
                    questsByGuid[quest.guid] = entry;
                    if (!string.IsNullOrEmpty(quest.questId))
                        questsById[quest.questId] = entry; // 重复 ID 由校验拦截，这里后者覆盖
                }
            }

            foreach (var kv in graph.BuildPredecessorMap())
                predecessors[kv.Key] = kv.Value;
        }

        // ---- 解锁传播 ----

        void MarkSatisfied(string guid)
        {
            if (string.IsNullOrEmpty(guid) || !satisfied.Add(guid)) return;
            pendingCondition.Remove(guid);

            if (!nodesByGuid.TryGetValue(guid, out var node)) return;
            foreach (var next in node.nextGuids)
                EvaluateNode(next);
        }

        void EvaluateNode(string guid)
        {
            if (string.IsNullOrEmpty(guid) || satisfied.Contains(guid)) return;
            if (!nodesByGuid.TryGetValue(guid, out var node)) return;

            switch (node)
            {
                case QuestNodeData quest:
                    TryUnlockQuest(questsByGuid[quest.guid]);
                    break;

                case ConditionGateNodeData gate:
                    if (!AnyPredecessorSatisfied(guid)) return;
                    if (gate.condition.Evaluate(blackboard))
                        MarkSatisfied(guid);
                    else
                        pendingCondition.Add(guid);
                    break;

                case JoinAllNodeData _:
                    if (AllPredecessorsSatisfied(guid))
                        MarkSatisfied(guid);
                    break;
            }
        }

        void TryUnlockQuest(QuestEntry entry)
        {
            if (entry.State != QuestState.Locked) return;
            if (!AnyPredecessorSatisfied(entry.Data.guid)) return;

            if (!entry.Data.unlockCondition.Evaluate(blackboard))
            {
                pendingCondition.Add(entry.Data.guid);
                return;
            }

            pendingCondition.Remove(entry.Data.guid);
            entry.State = QuestState.Available;
            if (!silent) OnQuestAvailable?.Invoke(entry);

            if (entry.Data.autoAccept)
                Accept(entry);
        }

        bool AnyPredecessorSatisfied(string guid)
        {
            return predecessors.TryGetValue(guid, out var list) && list.Any(satisfied.Contains);
        }

        bool AllPredecessorsSatisfied(string guid)
        {
            return predecessors.TryGetValue(guid, out var list) && list.Count > 0 && list.All(satisfied.Contains);
        }

        void OnBlackboardChanged(string variableName)
        {
            foreach (var guid in pendingCondition.ToList())
                EvaluateNode(guid);
        }

        // ---- 玩家操作 / 游戏上报 ----

        /// <summary>接取一个「可接取」状态的任务。</summary>
        public bool AcceptQuest(string questId)
        {
            var entry = FindQuest(questId);
            if (entry == null || entry.State != QuestState.Available) return false;
            Accept(entry);
            return true;
        }

        void Accept(QuestEntry entry)
        {
            entry.State = QuestState.Active;
            if (!silent) OnQuestAccepted?.Invoke(entry);

            // 无目标的任务（剧情任务）接取即完成
            if (entry.Data.objectives.Count == 0 || entry.AllObjectivesDone)
                Complete(entry);
        }

        /// <summary>
        /// 游戏侧上报进度：杀了怪、捡了道具、到达地点、对话完成等。
        /// 所有进行中任务里「类型匹配 且（targetId 匹配 或 目标未填 targetId）」的目标都会计数。
        /// </summary>
        public void ReportProgress(string objectiveType, string targetId, int amount = 1)
        {
            if (amount <= 0) return;

            foreach (var entry in questsByGuid.Values.Where(e => e.State == QuestState.Active).ToList())
            {
                bool changed = false;
                for (int i = 0; i < entry.Data.objectives.Count; i++)
                {
                    var objective = entry.Data.objectives[i];
                    if (objective.objectiveType != objectiveType) continue;
                    if (!string.IsNullOrEmpty(objective.targetId) && objective.targetId != targetId) continue;

                    int required = Mathf.Max(1, objective.requiredCount);
                    int newCount = Mathf.Min(required, entry.counts[i] + amount);
                    if (newCount == entry.counts[i]) continue;

                    entry.counts[i] = newCount;
                    changed = true;
                    if (!silent) OnObjectiveProgress?.Invoke(entry, i, newCount);
                }

                if (changed && entry.AllObjectivesDone)
                    Complete(entry);
            }
        }

        /// <summary>直接完成任务（GM 指令 / 模拟器用）。Locked 状态下也会强制完成。</summary>
        public bool ForceCompleteQuest(string questId)
        {
            var entry = FindQuest(questId);
            if (entry == null || entry.State == QuestState.Completed) return false;

            for (int i = 0; i < entry.counts.Length; i++)
                entry.counts[i] = Mathf.Max(1, entry.Data.objectives[i].requiredCount);
            Complete(entry);
            return true;
        }

        void Complete(QuestEntry entry)
        {
            entry.State = QuestState.Completed;
            if (!silent)
            {
                OnQuestCompleted?.Invoke(entry);
                foreach (var reward in entry.Data.rewards)
                    OnRewardGranted?.Invoke(entry, reward);
            }
            MarkSatisfied(entry.Data.guid);
        }
    }
}
