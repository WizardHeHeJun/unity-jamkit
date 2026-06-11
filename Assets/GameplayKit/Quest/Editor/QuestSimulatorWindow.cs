using System.Collections.Generic;
using System.Linq;
using GameplayKit;
using UnityEditor;
using UnityEngine;

namespace GameplayKit.Quest.Editor
{
    /// <summary>
    /// 任务模拟器：不进 Play Mode 试跑任务链。
    /// 左侧改黑板变量（条件门 / 解锁条件实时复评），右侧接任务、上报目标进度，底部是事件日志。
    /// </summary>
    public class QuestSimulatorWindow : EditorWindow
    {
        QuestGraphAsset asset;
        Blackboard blackboard;
        QuestRunner runner;

        Vector2 boardScroll;
        Vector2 questScroll;
        Vector2 logScroll;
        readonly List<string> log = new List<string>();

        public static void Open(QuestGraphAsset graphAsset)
        {
            var window = GetWindow<QuestSimulatorWindow>("任务模拟");
            window.minSize = new Vector2(640, 400);
            window.Setup(graphAsset);
        }

        void Setup(QuestGraphAsset graphAsset)
        {
            asset = graphAsset;
            Restart();
        }

        void Restart()
        {
            runner?.Detach();
            blackboard = Blackboard.CreateFrom(asset != null ? asset.blackboard : null);
            runner = new QuestRunner();
            log.Clear();

            runner.OnQuestAvailable += e => AddLog($"可接取：{Title(e)}");
            runner.OnQuestAccepted += e => AddLog($"已接取：{Title(e)}");
            runner.OnObjectiveProgress += (e, index, count) =>
            {
                var objective = e.Data.objectives[index];
                AddLog($"进度：{Title(e)} / {ObjectiveLabel(objective)} {count}/{Mathf.Max(1, objective.requiredCount)}");
            };
            runner.OnQuestCompleted += e => AddLog($"完成：{Title(e)}");
            runner.OnRewardGranted += (e, reward) => AddLog($"发奖：{Title(e)} → {reward.rewardId} ×{reward.amount}");

            if (asset != null)
                runner.Begin(asset, blackboard);
        }

        void OnDestroy()
        {
            runner?.Detach();
        }

        static string Title(QuestRunner.QuestEntry entry) =>
            string.IsNullOrEmpty(entry.Data.title) ? entry.Data.questId : entry.Data.title;

        static string ObjectiveLabel(QuestObjectiveData objective)
        {
            if (!string.IsNullOrEmpty(objective.description)) return objective.description;
            return string.IsNullOrEmpty(objective.targetId)
                ? objective.objectiveType
                : $"{objective.objectiveType} {objective.targetId}";
        }

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
                EditorGUILayout.HelpBox("请从任务图编辑器工具栏的「模拟」打开本窗口。", MessageType.Info);
                return;
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label($"任务图：{asset.name}", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("重新开始", EditorStyles.toolbarButton, GUILayout.Width(70)))
                    Restart();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawBlackboardPanel();
                DrawQuestPanel();
            }

            DrawLogPanel();
        }

        void DrawBlackboardPanel()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(230)))
            {
                GUILayout.Label("黑板变量", EditorStyles.boldLabel);

                if (asset.blackboard == null)
                {
                    EditorGUILayout.HelpBox("图未配置黑板", MessageType.None);
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

        void DrawQuestPanel()
        {
            using (new EditorGUILayout.VerticalScope())
            {
                GUILayout.Label("任务", EditorStyles.boldLabel);
                questScroll = EditorGUILayout.BeginScrollView(questScroll);

                var ordered = runner.Quests
                    .OrderBy(e => StateOrder(e.State))
                    .ThenBy(e => Title(e))
                    .ToList();

                foreach (var entry in ordered)
                    DrawQuest(entry);

                EditorGUILayout.EndScrollView();
            }
        }

        static int StateOrder(QuestState state)
        {
            switch (state)
            {
                case QuestState.Active: return 0;
                case QuestState.Available: return 1;
                case QuestState.Locked: return 2;
                default: return 3;
            }
        }

        void DrawQuest(QuestRunner.QuestEntry entry)
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(Title(entry), EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();

                    var stateStyle = new GUIStyle(EditorStyles.miniLabel);
                    stateStyle.normal.textColor = StateColor(entry.State);
                    GUILayout.Label(StateLabel(entry.State), stateStyle);

                    if (entry.State == QuestState.Available &&
                        GUILayout.Button("接取", GUILayout.Width(50)))
                        runner.AcceptQuest(entry.Data.questId);

                    if (entry.State != QuestState.Completed &&
                        GUILayout.Button("一键完成", GUILayout.Width(70)))
                        runner.ForceCompleteQuest(entry.Data.questId);
                }

                using (new EditorGUI.DisabledScope(entry.State != QuestState.Active))
                {
                    for (int i = 0; i < entry.Data.objectives.Count; i++)
                    {
                        var objective = entry.Data.objectives[i];
                        int required = Mathf.Max(1, objective.requiredCount);
                        int count = entry.GetObjectiveCount(i);

                        using (new EditorGUILayout.HorizontalScope())
                        {
                            GUILayout.Label($"· {ObjectiveLabel(objective)}  {count}/{required}");
                            GUILayout.FlexibleSpace();
                            if (GUILayout.Button("+1", GUILayout.Width(35)))
                                runner.ReportProgress(objective.objectiveType, objective.targetId, 1);
                        }
                    }
                }
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

        static Color StateColor(QuestState state)
        {
            switch (state)
            {
                case QuestState.Locked: return new Color(0.55f, 0.55f, 0.55f);
                case QuestState.Available: return new Color(0.4f, 0.75f, 1f);
                case QuestState.Active: return new Color(1f, 0.8f, 0.4f);
                default: return new Color(0.5f, 0.9f, 0.5f);
            }
        }

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
