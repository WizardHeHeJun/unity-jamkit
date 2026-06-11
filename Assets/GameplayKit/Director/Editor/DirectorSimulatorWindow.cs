using System.Collections.Generic;
using System.Linq;
using DialogueGraph;
using GameplayKit.Quest;
using UnityEditor;
using UnityEngine;

namespace GameplayKit.Director.Editor
{
    /// <summary>
    /// 导演编排模拟器：不进 Play Mode 试跑规则。
    /// 左侧改黑板变量（黑板条件规则实时复评），中间上报事件 / 结束对话、看规则状态，
    /// 配了任务图时右侧出现任务面板（任务状态触发与任务类动作联动），底部是事件日志。
    /// </summary>
    public class DirectorSimulatorWindow : EditorWindow
    {
        DirectorAsset asset;
        Blackboard blackboard;
        DirectorRunner director;
        QuestRunner questRunner;

        string eventName = "";
        string eventParam = "";

        /// <summary>「开始对话」动作开出的对话，等待手动点「结束」回报。</summary>
        readonly List<DialogueGraphAsset> openDialogues = new List<DialogueGraphAsset>();

        Vector2 boardScroll;
        Vector2 ruleScroll;
        Vector2 questScroll;
        Vector2 logScroll;
        readonly List<string> log = new List<string>();

        public static void Open(DirectorAsset directorAsset)
        {
            var window = GetWindow<DirectorSimulatorWindow>("导演模拟");
            window.minSize = new Vector2(720, 420);
            window.Setup(directorAsset);
        }

        void Setup(DirectorAsset directorAsset)
        {
            asset = directorAsset;
            Restart();
        }

        void Restart()
        {
            director?.Detach();
            questRunner?.Detach();
            blackboard = Blackboard.CreateFrom(asset != null ? asset.blackboard : null);
            log.Clear();
            openDialogues.Clear();

            director = new DirectorRunner();
            director.OnRuleFired += rule => AddLog($"★ 触发：{RuleTitle(rule)}");
            director.OnStartDialogue += dialogue =>
            {
                openDialogues.Add(dialogue);
                AddLog($"▶ 开始对话：{dialogue.name}（点「结束」回报对话结束）");
            };
            director.OnGameEvent += (name, param) =>
                AddLog($"→ 抛事件：{name}{(string.IsNullOrEmpty(param) ? "" : $"({param})")}");

            if (asset != null && asset.questGraph != null)
            {
                questRunner = new QuestRunner();
                questRunner.OnQuestAvailable += e => AddLog($"任务可接取：{QuestTitle(e)}");
                questRunner.OnQuestAccepted += e => AddLog($"任务已接取：{QuestTitle(e)}");
                questRunner.OnQuestCompleted += e => AddLog($"任务完成：{QuestTitle(e)}");
                questRunner.OnRewardGranted += (e, reward) =>
                    AddLog($"发奖：{QuestTitle(e)} → {reward.rewardId} ×{reward.amount}");

                director.AttachQuestRunner(questRunner);
            }
            else
            {
                questRunner = null;
            }

            if (asset != null)
                director.Begin(asset, blackboard);

            // 任务图后开（开局即触发的规则先就位，再让任务事件流入导演）
            if (questRunner != null)
                questRunner.Begin(asset.questGraph, blackboard);
        }

        void OnDestroy()
        {
            director?.Detach();
            questRunner?.Detach();
        }

        static string RuleTitle(DirectorRule rule) =>
            string.IsNullOrEmpty(rule.title) ? "(未命名规则)" : rule.title;

        static string QuestTitle(QuestRunner.QuestEntry entry) =>
            string.IsNullOrEmpty(entry.Data.title) ? entry.Data.questId : entry.Data.title;

        void AddLog(string message)
        {
            log.Add(message);
            if (log.Count > 200) log.RemoveAt(0);
            logScroll.y = float.MaxValue;
            Repaint();
        }

        void OnGUI()
        {
            if (asset == null)
            {
                EditorGUILayout.HelpBox("请从导演编排编辑器工具栏的「模拟」打开本窗口。", MessageType.Info);
                return;
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label($"导演编排：{asset.name}", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("重新开始", EditorStyles.toolbarButton, GUILayout.Width(70)))
                    Restart();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawBlackboardPanel();
                DrawDirectorPanel();
                if (questRunner != null)
                    DrawQuestPanel();
            }

            DrawLogPanel();
        }

        // ---------------- 黑板 ----------------

        void DrawBlackboardPanel()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(220)))
            {
                GUILayout.Label("黑板变量", EditorStyles.boldLabel);

                if (asset.blackboard == null)
                {
                    EditorGUILayout.HelpBox("未配置黑板", MessageType.None);
                    return;
                }

                boardScroll = EditorGUILayout.BeginScrollView(boardScroll);
                foreach (var def in asset.blackboard.variables)
                {
                    if (def == null || string.IsNullOrEmpty(def.name)) continue;
                    switch (def.type)
                    {
                        case BlackboardVariableType.Bool:
                            blackboard.TryGetBool(def.name, out var b);
                            var newBool = EditorGUILayout.Toggle(def.name, b);
                            if (newBool != b) blackboard.SetBool(def.name, newBool);
                            break;

                        case BlackboardVariableType.Int:
                            blackboard.TryGetInt(def.name, out var i);
                            var newInt = EditorGUILayout.IntField(def.name, i);
                            if (newInt != i) blackboard.SetInt(def.name, newInt);
                            break;

                        case BlackboardVariableType.Float:
                            blackboard.TryGetFloat(def.name, out var f);
                            var newFloat = EditorGUILayout.FloatField(def.name, f);
                            if (!Mathf.Approximately(newFloat, f)) blackboard.SetFloat(def.name, newFloat);
                            break;

                        case BlackboardVariableType.String:
                            blackboard.TryGetString(def.name, out var s);
                            var newString = EditorGUILayout.TextField(def.name, s);
                            if (newString != s) blackboard.SetString(def.name, newString);
                            break;
                    }
                }
                EditorGUILayout.EndScrollView();
            }
        }

        // ---------------- 触发与规则 ----------------

        void DrawDirectorPanel()
        {
            using (new EditorGUILayout.VerticalScope())
            {
                GUILayout.Label("上报事件", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    eventName = EditorGUILayout.TextField(eventName);
                    eventParam = EditorGUILayout.TextField(eventParam, GUILayout.Width(80));
                    using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(eventName)))
                    {
                        if (GUILayout.Button("触发", GUILayout.Width(44)))
                        {
                            AddLog($"上报事件：{eventName}{(string.IsNullOrEmpty(eventParam) ? "" : $"({eventParam})")}");
                            director.ReportEvent(eventName, string.IsNullOrEmpty(eventParam) ? null : eventParam);
                        }
                    }
                }

                if (openDialogues.Count > 0)
                {
                    GUILayout.Label("进行中的对话", EditorStyles.boldLabel);
                    foreach (var dialogue in openDialogues.ToList())
                    {
                        using (new EditorGUILayout.HorizontalScope("box"))
                        {
                            GUILayout.Label(dialogue != null ? dialogue.name : "(无名对话)");
                            GUILayout.FlexibleSpace();
                            if (GUILayout.Button("结束", GUILayout.Width(44)))
                            {
                                openDialogues.Remove(dialogue);
                                AddLog($"对话结束：{(dialogue != null ? dialogue.name : "(无名对话)")}");
                                director.ReportDialogueEnded(dialogue);
                            }
                        }
                    }
                }

                GUILayout.Label("规则", EditorStyles.boldLabel);
                ruleScroll = EditorGUILayout.BeginScrollView(ruleScroll);
                foreach (var rule in asset.rules)
                    DrawRule(rule);
                EditorGUILayout.EndScrollView();
            }
        }

        void DrawRule(DirectorRule rule)
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(RuleTitle(rule), EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();

                    int fired = director.GetFireCount(rule);
                    var style = new GUIStyle(EditorStyles.miniLabel);
                    string status;
                    if (!rule.enabled)
                    {
                        status = "已停用";
                        style.normal.textColor = new Color(0.55f, 0.55f, 0.55f);
                    }
                    else if (fired > 0)
                    {
                        status = rule.once ? "已触发" : $"已触发 ×{fired}";
                        style.normal.textColor = new Color(0.5f, 0.9f, 0.5f);
                    }
                    else
                    {
                        status = "待触发";
                        style.normal.textColor = new Color(1f, 0.8f, 0.4f);
                    }
                    GUILayout.Label(status, style);
                }
                GUILayout.Label("当 " + TriggerSummary(rule.trigger), EditorStyles.miniLabel);
            }
        }

        static string TriggerSummary(DirectorTrigger trigger)
        {
            switch (trigger)
            {
                case GameEventTrigger e:
                    return $"事件「{e.eventName}」" +
                           (string.IsNullOrEmpty(e.param) ? "" : $"（参数 {e.param}）");
                case VariableTrigger _:
                    return "黑板条件满足";
                case QuestStateTrigger q:
                    string phase = q.phase == QuestTriggerPhase.Available ? "变为可接取"
                        : q.phase == QuestTriggerPhase.Accepted ? "被接取" : "完成";
                    return $"任务「{q.questId}」{phase}";
                case DialogueEndedTrigger d:
                    return d.dialogue != null ? $"对话「{d.dialogue.name}」结束" : "任意对话结束";
                default:
                    return "（无触发器）";
            }
        }

        // ---------------- 任务 ----------------

        void DrawQuestPanel()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(240)))
            {
                GUILayout.Label("任务", EditorStyles.boldLabel);
                questScroll = EditorGUILayout.BeginScrollView(questScroll);

                foreach (var entry in questRunner.Quests.OrderBy(e => e.State).ThenBy(QuestTitle))
                {
                    using (new EditorGUILayout.HorizontalScope("box"))
                    {
                        GUILayout.Label($"{QuestTitle(entry)}（{StateLabel(entry.State)}）", EditorStyles.miniLabel);
                        GUILayout.FlexibleSpace();

                        if (entry.State == QuestState.Available &&
                            GUILayout.Button("接取", GUILayout.Width(40)))
                            questRunner.AcceptQuest(entry.Data.questId);

                        if (entry.State != QuestState.Completed &&
                            GUILayout.Button("完成", GUILayout.Width(40)))
                            questRunner.ForceCompleteQuest(entry.Data.questId);
                    }
                }
                EditorGUILayout.EndScrollView();
            }
        }

        static string StateLabel(QuestState state)
        {
            switch (state)
            {
                case QuestState.Locked: return "未解锁";
                case QuestState.Available: return "可接取";
                case QuestState.Active: return "进行中";
                default: return "已完成";
            }
        }

        // ---------------- 日志 ----------------

        void DrawLogPanel()
        {
            GUILayout.Label("日志", EditorStyles.boldLabel);
            logScroll = EditorGUILayout.BeginScrollView(logScroll, GUILayout.Height(110));
            foreach (var line in log)
                GUILayout.Label(line, EditorStyles.miniLabel);
            EditorGUILayout.EndScrollView();
        }
    }
}
