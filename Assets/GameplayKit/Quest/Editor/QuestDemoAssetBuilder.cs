using System;
using GameplayKit;
using UnityEditor;
using UnityEngine;

namespace GameplayKit.Quest.Editor
{
    /// <summary>
    /// 一键生成可玩的任务系统示例：黑板 + 任务图（主线三连 + 条件门 + 条件解锁的支线），
    /// 生成后自动打开任务图编辑器，配合「模拟」按钮即可完整体验流程。
    /// </summary>
    public static class QuestDemoAssetBuilder
    {
        const string Folder = "Assets/GameplayKitDemo";

        [MenuItem("Tools/玩法工具/创建任务系统示例")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets", "GameplayKitDemo");

            // 黑板
            var blackboard = ScriptableObject.CreateInstance<BlackboardAsset>();
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

            // 任务图
            var graph = ScriptableObject.CreateInstance<QuestGraphAsset>();
            graph.blackboard = blackboard;

            var start = new QuestStartNodeData { guid = NewGuid(), position = new Vector2(60, 260) };

            var training = new QuestNodeData
            {
                guid = NewGuid(),
                position = new Vector2(320, 80),
                questId = "main_001",
                title = "新手训练",
                description = "在训练场击败 3 个训练假人。",
                autoAccept = true
            };
            training.objectives.Add(new QuestObjectiveData
            {
                id = NewGuid(),
                objectiveType = "击杀",
                targetId = "训练假人",
                requiredCount = 3,
                description = "击败训练假人"
            });
            training.rewards.Add(new QuestRewardData { rewardId = "金币", amount = 100 });

            var equip = new QuestNodeData
            {
                guid = NewGuid(),
                position = new Vector2(700, 80),
                questId = "main_002",
                title = "装备入门",
                description = "找铁匠领取一把木剑。",
                autoAccept = false
            };
            equip.objectives.Add(new QuestObjectiveData
            {
                id = NewGuid(),
                objectiveType = "收集",
                targetId = "木剑",
                requiredCount = 1,
                description = "获得木剑"
            });
            equip.rewards.Add(new QuestRewardData { rewardId = "金币", amount = 50 });

            var gate = new ConditionGateNodeData
            {
                guid = NewGuid(),
                position = new Vector2(1080, 80)
            };
            gate.condition.clauses.Add(new ConditionClause
            {
                variableName = "等级",
                variableType = BlackboardVariableType.Int,
                op = ConditionCompareOp.GreaterOrEqual,
                intValue = 5
            });

            var trial = new QuestNodeData
            {
                guid = NewGuid(),
                position = new Vector2(1400, 80),
                questId = "main_003",
                title = "进阶试炼",
                description = "等级达到 5 级后开启：清剿村外的哥布林。",
                autoAccept = true
            };
            trial.objectives.Add(new QuestObjectiveData
            {
                id = NewGuid(),
                objectiveType = "击杀",
                targetId = "哥布林",
                requiredCount = 10,
                description = "击杀哥布林"
            });
            trial.rewards.Add(new QuestRewardData { rewardId = "金币", amount = 500 });
            trial.rewards.Add(new QuestRewardData { rewardId = "铁剑", amount = 1 });

            var gift = new QuestNodeData
            {
                guid = NewGuid(),
                position = new Vector2(320, 460),
                questId = "side_001",
                title = "支线·村长的谢礼",
                description = "完成新手引导后，去找村长聊聊。",
                autoAccept = false
            };
            gift.unlockCondition.clauses.Add(new ConditionClause
            {
                variableName = "完成新手引导",
                variableType = BlackboardVariableType.Bool,
                op = ConditionCompareOp.Equal,
                boolValue = true
            });
            gift.objectives.Add(new QuestObjectiveData
            {
                id = NewGuid(),
                objectiveType = "对话",
                targetId = "村长",
                requiredCount = 1,
                description = "与村长对话"
            });
            gift.rewards.Add(new QuestRewardData { rewardId = "面包", amount = 5 });

            start.nextGuids.Add(training.guid);
            start.nextGuids.Add(gift.guid);
            training.nextGuids.Add(equip.guid);
            equip.nextGuids.Add(gate.guid);
            gate.nextGuids.Add(trial.guid);

            graph.nodes.Add(start);
            graph.nodes.Add(training);
            graph.nodes.Add(equip);
            graph.nodes.Add(gate);
            graph.nodes.Add(trial);
            graph.nodes.Add(gift);

            AssetDatabase.CreateAsset(graph, $"{Folder}/示例任务图.asset");
            AssetDatabase.SaveAssets();

            Selection.activeObject = graph;
            EditorGUIUtility.PingObject(graph);
            QuestGraphWindow.Open(graph);

            Debug.Log("任务系统示例已生成：Assets/GameplayKitDemo。点编辑器工具栏「模拟」即可试跑（改黑板「等级」到 5 看条件门解锁）。");
        }

        static string NewGuid() => Guid.NewGuid().ToString("N");
    }
}
