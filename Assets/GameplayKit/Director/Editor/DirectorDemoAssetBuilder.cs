using System;
using GameplayKit.Quest;
using UnityEditor;
using UnityEngine;

namespace GameplayKit.Director.Editor
{
    /// <summary>
    /// 一键生成导演编排示例：复用任务系统示例的黑板与任务图（没有则自动创建黑板），
    /// 生成覆盖四类触发器的规则集并打开编辑器，配合「模拟」按钮即可完整体验。
    /// </summary>
    public static class DirectorDemoAssetBuilder
    {
        const string Folder = "Assets/GameplayKitDemo";

        [MenuItem("Tools/玩法工具/创建导演编排示例")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets", "GameplayKitDemo");

            // 黑板：优先复用任务系统示例的
            var blackboard = AssetDatabase.LoadAssetAtPath<BlackboardAsset>($"{Folder}/示例黑板.asset");
            if (blackboard == null)
            {
                blackboard = ScriptableObject.CreateInstance<BlackboardAsset>();
                blackboard.variables.Add(new BlackboardVariableDefinition
                {
                    name = "等级",
                    type = BlackboardVariableType.Int,
                    defaultInt = 1,
                    note = "玩家等级，升级系统写入"
                });
                blackboard.variables.Add(new BlackboardVariableDefinition
                {
                    name = "完成新手引导",
                    type = BlackboardVariableType.Bool,
                    defaultBool = false,
                    note = "新手引导结束时置真"
                });
                AssetDatabase.CreateAsset(blackboard, $"{Folder}/示例黑板.asset");
            }

            // 任务图：任务系统示例存在则联动（任务状态触发器生效）
            var questGraph = AssetDatabase.LoadAssetAtPath<QuestGraphAsset>($"{Folder}/示例任务图.asset");

            var director = ScriptableObject.CreateInstance<DirectorAsset>();
            director.blackboard = blackboard;
            director.questGraph = questGraph;

            // 规则 1：事件触发 → 改黑板 + 抛事件
            var guideDone = new DirectorRule
            {
                id = NewGuid(),
                title = "引导结束：标记完成并提示",
                trigger = new GameEventTrigger { eventName = "引导完成" }
            };
            guideDone.actions.Add(new SetVariableAction
            {
                variableName = "完成新手引导",
                variableType = BlackboardVariableType.Bool,
                boolValue = true
            });
            guideDone.actions.Add(new RaiseEventAction { eventName = "播放提示", param = "引导完成" });
            director.rules.Add(guideDone);

            // 规则 2：黑板条件触发（等级 ≥ 5 的瞬间）
            var levelUnlock = new DirectorRule
            {
                id = NewGuid(),
                title = "等级达到 5：解锁进阶玩法",
                trigger = new VariableTrigger()
            };
            levelUnlock.condition.clauses.Add(new ConditionClause
            {
                variableName = "等级",
                variableType = BlackboardVariableType.Int,
                op = ConditionCompareOp.GreaterOrEqual,
                intValue = 5
            });
            levelUnlock.actions.Add(new RaiseEventAction { eventName = "解锁进阶玩法" });
            director.rules.Add(levelUnlock);

            // 规则 3：对话结束触发（重复规则，每次 +1 经验）
            var talkExp = new DirectorRule
            {
                id = NewGuid(),
                title = "每次对话结束：等级 +1（演示重复规则）",
                once = false,
                trigger = new DialogueEndedTrigger()
            };
            talkExp.actions.Add(new SetVariableAction
            {
                variableName = "等级",
                variableType = BlackboardVariableType.Int,
                op = SetVariableOp.Add,
                intValue = 1
            });
            director.rules.Add(talkExp);

            // 规则 4：任务状态触发（仅在联动任务系统示例时生成）
            if (questGraph != null)
            {
                var questDone = new DirectorRule
                {
                    id = NewGuid(),
                    title = "新手训练完成：村庄广播",
                    trigger = new QuestStateTrigger
                    {
                        questId = "main_001",
                        phase = QuestTriggerPhase.Completed
                    }
                };
                questDone.actions.Add(new RaiseEventAction { eventName = "村庄广播", param = "新手毕业" });
                director.rules.Add(questDone);
            }

            AssetDatabase.CreateAsset(director, $"{Folder}/示例导演编排.asset");
            AssetDatabase.SaveAssets();

            Selection.activeObject = director;
            EditorGUIUtility.PingObject(director);
            DirectorWindow.Open(director);

            Debug.Log("导演编排示例已生成：Assets/GameplayKitDemo。点编辑器工具栏「模拟」试跑：" +
                      "上报事件「引导完成」、改黑板「等级」到 5、结束一个对话，分别观察规则触发。" +
                      (questGraph == null ? "（先执行「创建任务系统示例」再重新生成，可体验任务状态触发）" : ""));
        }

        static string NewGuid() => Guid.NewGuid().ToString("N");
    }
}
