using System;
using System.Collections.Generic;
using DialogueGraph;
using GameplayKit.Quest;
using UnityEngine;

namespace GameplayKit.Director
{
    /// <summary>导演编排存档快照，可直接 JsonUtility 序列化。</summary>
    [Serializable]
    public class DirectorRunnerState
    {
        [Serializable]
        public class RuleRecord
        {
            public string ruleId;
            public int fireCount;
        }

        public List<RuleRecord> rules = new List<RuleRecord>();
    }

    /// <summary>
    /// 导演编排执行器（纯 C#，不依赖场景）：
    /// 监听游戏事件 / 黑板变化 / 任务状态 / 对话结束，按规则执行动作。
    /// 改黑板、操作任务直接执行；开对话、抛事件通过事件交给游戏侧。
    /// </summary>
    public class DirectorRunner
    {
        const int MaxChainDepth = 32;

        DirectorAsset asset;
        Blackboard blackboard;
        QuestRunner questRunner;

        readonly Dictionary<string, int> fireCounts = new Dictionary<string, int>();

        /// <summary>「黑板条件」触发器的边沿状态：条件当前不满足、等待变为满足的规则。</summary>
        readonly HashSet<string> armed = new HashSet<string>();

        readonly List<(VNPlayer player, Action<string, string> onEvent, Action onEnded)> attachedPlayers =
            new List<(VNPlayer, Action<string, string>, Action)>();

        int chainDepth;

        /// <summary>开始对话动作：游戏侧据此播放（如 vnPlayer.graph = g; vnPlayer.Play()）。</summary>
        public event Action<DialogueGraphAsset> OnStartDialogue;

        /// <summary>抛游戏事件动作的对外通知（同时会回流为触发源，可链式触发其他规则）。</summary>
        public event Action<string, string> OnGameEvent;

        /// <summary>任意规则触发时通知（调试 / 模拟器用）。</summary>
        public event Action<DirectorRule> OnRuleFired;

        public int GetFireCount(DirectorRule rule) =>
            rule != null && fireCounts.TryGetValue(rule.id, out var count) ? count : 0;

        // ---------------- 生命周期 ----------------

        /// <summary>开始一局新游戏。「黑板条件」规则若开局即满足会立刻触发。</summary>
        public void Begin(DirectorAsset directorAsset, Blackboard board)
        {
            Initialize(directorAsset, board);
            EvaluateVariableTriggers(initial: true);
        }

        /// <summary>
        /// 从存档恢复（不触发任何规则）。未触发过的「黑板条件」规则一律上膛：
        /// 若恢复时条件已满足（如改版新增的规则），会在下一次黑板变化时补触发。
        /// </summary>
        public void RestoreFromJson(DirectorAsset directorAsset, Blackboard board, string json)
        {
            Initialize(directorAsset, board);
            var state = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<DirectorRunnerState>(json);
            if (state != null)
            {
                foreach (var record in state.rules)
                    fireCounts[record.ruleId] = record.fireCount; // 已删除规则的记录无害，规则匹配按当前资产
            }

            foreach (var rule in asset.rules)
            {
                if (rule.trigger is VariableTrigger && CanFire(rule))
                    armed.Add(rule.id);
            }
        }

        public string CaptureSaveJson()
        {
            var state = new DirectorRunnerState();
            foreach (var kv in fireCounts)
            {
                if (kv.Value <= 0) continue;
                state.rules.Add(new DirectorRunnerState.RuleRecord { ruleId = kv.Key, fireCount = kv.Value });
            }
            return JsonUtility.ToJson(state);
        }

        /// <summary>解绑黑板 / 任务 / 对话事件。更换资产或丢弃 Runner 前调用。</summary>
        public void Detach()
        {
            if (blackboard != null)
                blackboard.OnValueChanged -= OnBlackboardChanged;
            blackboard = null;

            DetachQuestRunner();

            foreach (var (player, onEvent, onEnded) in attachedPlayers)
            {
                if (player == null) continue;
                player.OnGameEvent -= onEvent;
                player.OnDialogueEnded -= onEnded;
            }
            attachedPlayers.Clear();
        }

        void Initialize(DirectorAsset directorAsset, Blackboard board)
        {
            if (blackboard != null)
                blackboard.OnValueChanged -= OnBlackboardChanged;

            asset = directorAsset;
            blackboard = board;
            if (blackboard != null)
                blackboard.OnValueChanged += OnBlackboardChanged;

            fireCounts.Clear();
            armed.Clear();
            chainDepth = 0;
        }

        // ---------------- 挂接外部系统 ----------------

        /// <summary>挂接任务系统：任务状态触发器生效，任务类动作可执行。</summary>
        public void AttachQuestRunner(QuestRunner runner)
        {
            DetachQuestRunner();
            questRunner = runner;
            if (questRunner == null) return;
            questRunner.OnQuestAvailable += OnQuestAvailable;
            questRunner.OnQuestAccepted += OnQuestAccepted;
            questRunner.OnQuestCompleted += OnQuestCompleted;
        }

        void DetachQuestRunner()
        {
            if (questRunner == null) return;
            questRunner.OnQuestAvailable -= OnQuestAvailable;
            questRunner.OnQuestAccepted -= OnQuestAccepted;
            questRunner.OnQuestCompleted -= OnQuestCompleted;
            questRunner = null;
        }

        /// <summary>挂接 VNPlayer：对话事件节点回流为游戏事件，对话结束回流为对话结束触发。</summary>
        public void Attach(VNPlayer player)
        {
            if (player == null) return;
            Action<string, string> onEvent = ReportEvent;
            Action onEnded = () => ReportDialogueEnded(player.graph);
            player.OnGameEvent += onEvent;
            player.OnDialogueEnded += onEnded;
            attachedPlayers.Add((player, onEvent, onEnded));
        }

        // ---------------- 触发入口 ----------------

        /// <summary>游戏侧上报事件（对话事件节点经 Attach 自动走这里）。</summary>
        public void ReportEvent(string eventName, string param = null)
        {
            if (asset == null || string.IsNullOrEmpty(eventName)) return;

            foreach (var rule in asset.rules)
            {
                if (!(rule.trigger is GameEventTrigger trigger)) continue;
                if (trigger.eventName != eventName) continue;
                if (!string.IsNullOrEmpty(trigger.param) && trigger.param != param) continue;
                TryFire(rule);
            }
        }

        /// <summary>游戏侧上报对话结束（经 Attach 自动走这里）。</summary>
        public void ReportDialogueEnded(DialogueGraphAsset dialogue)
        {
            if (asset == null) return;

            foreach (var rule in asset.rules)
            {
                if (!(rule.trigger is DialogueEndedTrigger trigger)) continue;
                if (trigger.dialogue != null && trigger.dialogue != dialogue) continue;
                TryFire(rule);
            }
        }

        void OnQuestAvailable(QuestRunner.QuestEntry entry) => OnQuestPhase(entry, QuestTriggerPhase.Available);
        void OnQuestAccepted(QuestRunner.QuestEntry entry) => OnQuestPhase(entry, QuestTriggerPhase.Accepted);
        void OnQuestCompleted(QuestRunner.QuestEntry entry) => OnQuestPhase(entry, QuestTriggerPhase.Completed);

        void OnQuestPhase(QuestRunner.QuestEntry entry, QuestTriggerPhase phase)
        {
            if (asset == null) return;

            foreach (var rule in asset.rules)
            {
                if (!(rule.trigger is QuestStateTrigger trigger)) continue;
                if (trigger.phase != phase || trigger.questId != entry.Data.questId) continue;
                TryFire(rule);
            }
        }

        void OnBlackboardChanged(string variableName)
        {
            EvaluateVariableTriggers(initial: false);
        }

        /// <summary>
        /// 评估「黑板条件」触发器（边沿触发）：
        /// 条件不满足时上膛（armed），变为满足的瞬间触发并退膛；重复规则在条件再次不满足后重新上膛。
        /// initial 为 true 时（开局），条件已满足的规则直接触发。
        /// </summary>
        void EvaluateVariableTriggers(bool initial)
        {
            if (asset == null) return;

            foreach (var rule in asset.rules)
            {
                if (!(rule.trigger is VariableTrigger)) continue;
                if (!CanFire(rule))
                {
                    armed.Remove(rule.id);
                    continue;
                }

                bool met = rule.condition.Evaluate(blackboard);
                if (!met)
                {
                    armed.Add(rule.id);
                }
                else if (initial || armed.Remove(rule.id))
                {
                    Fire(rule);
                }
            }
        }

        // ---------------- 触发与执行 ----------------

        bool CanFire(DirectorRule rule)
        {
            if (rule == null || !rule.enabled || string.IsNullOrEmpty(rule.id)) return false;
            return !rule.once || GetFireCount(rule) == 0;
        }

        void TryFire(DirectorRule rule)
        {
            if (!CanFire(rule)) return;
            if (!rule.condition.Evaluate(blackboard)) return;
            Fire(rule);
        }

        void Fire(DirectorRule rule)
        {
            if (chainDepth >= MaxChainDepth)
            {
                Debug.LogError($"导演编排「{asset.name}」规则链过深（>{MaxChainDepth}），疑似规则互相触发成环，已中断：{rule.title}");
                return;
            }

            fireCounts[rule.id] = GetFireCount(rule) + 1;
            OnRuleFired?.Invoke(rule);

            chainDepth++;
            try
            {
                foreach (var action in rule.actions)
                    ExecuteAction(action);
            }
            finally
            {
                chainDepth--;
            }
        }

        void ExecuteAction(DirectorAction action)
        {
            switch (action)
            {
                case StartDialogueAction start:
                    if (start.dialogue != null)
                        OnStartDialogue?.Invoke(start.dialogue);
                    break;

                case SetVariableAction set:
                    ExecuteSetVariable(set);
                    break;

                case RaiseEventAction raise:
                    if (string.IsNullOrEmpty(raise.eventName)) break;
                    OnGameEvent?.Invoke(raise.eventName, raise.param);
                    ReportEvent(raise.eventName, raise.param); // 回流，允许规则链式触发
                    break;

                case AcceptQuestAction accept:
                    if (RequireQuestRunner("接取任务"))
                        questRunner.AcceptQuest(accept.questId);
                    break;

                case CompleteQuestAction complete:
                    if (RequireQuestRunner("完成任务"))
                        questRunner.ForceCompleteQuest(complete.questId);
                    break;

                case ReportProgressAction progress:
                    if (RequireQuestRunner("上报任务进度"))
                        questRunner.ReportProgress(progress.objectiveType, progress.targetId,
                            Mathf.Max(1, progress.amount));
                    break;
            }
        }

        void ExecuteSetVariable(SetVariableAction set)
        {
            if (blackboard == null || string.IsNullOrEmpty(set.variableName)) return;

            switch (set.variableType)
            {
                case BlackboardVariableType.Bool:
                    blackboard.SetBool(set.variableName, set.boolValue);
                    break;

                case BlackboardVariableType.Int:
                    if (set.op == SetVariableOp.Add)
                    {
                        blackboard.TryGetInt(set.variableName, out var i);
                        blackboard.SetInt(set.variableName, i + set.intValue);
                    }
                    else
                    {
                        blackboard.SetInt(set.variableName, set.intValue);
                    }
                    break;

                case BlackboardVariableType.Float:
                    if (set.op == SetVariableOp.Add)
                    {
                        blackboard.TryGetFloat(set.variableName, out var f);
                        blackboard.SetFloat(set.variableName, f + set.floatValue);
                    }
                    else
                    {
                        blackboard.SetFloat(set.variableName, set.floatValue);
                    }
                    break;

                case BlackboardVariableType.String:
                    blackboard.SetString(set.variableName, set.stringValue);
                    break;
            }
        }

        bool RequireQuestRunner(string actionLabel)
        {
            if (questRunner != null) return true;
            Debug.LogWarning($"导演编排「{asset.name}」动作「{actionLabel}」被跳过：未挂接 QuestRunner（调用 AttachQuestRunner）。");
            return false;
        }
    }
}
