using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameplayKit.Quest
{
    /// <summary>
    /// 任务目标类型注册表。程序可在游戏启动时 Add 扩展，
    /// 编辑器中目标类型下拉读取这里的列表。
    /// </summary>
    public static class QuestObjectiveTypes
    {
        public static readonly List<string> All = new List<string>
        {
            "击杀",
            "收集",
            "到达",
            "对话",
            "自定义事件"
        };
    }

    /// <summary>一条任务目标：类型 + 目标对象 + 需求数量。进度由游戏侧 ReportProgress 驱动。</summary>
    [Serializable]
    public class QuestObjectiveData
    {
        [Tooltip("稳定 ID，创建时生成，存档用")]
        public string id;

        [Tooltip("目标类型，与 ReportProgress 上报的类型匹配")]
        public string objectiveType = "击杀";

        [Tooltip("目标对象 ID（怪物表ID / 道具ID / 地点ID…）。留空 = 该类型任意对象都计数")]
        public string targetId;

        public int requiredCount = 1;

        [Tooltip("任务追踪 UI 上显示的文案，如「击败训练假人 0/3」")]
        public string description;
    }

    /// <summary>一条任务奖励。实际发放由游戏侧监听 OnRewardGranted 执行。</summary>
    [Serializable]
    public class QuestRewardData
    {
        [Tooltip("奖励 ID（道具ID / 货币ID…），游戏侧解析")]
        public string rewardId;

        public int amount = 1;
    }

    [Serializable]
    public abstract class QuestBaseNodeData
    {
        public string guid;
        public Vector2 position;

        [Tooltip("本节点完成 / 通过后解锁的后续节点")]
        public List<string> nextGuids = new List<string>();
    }

    /// <summary>任务链入口，游戏开局即视为通过。每图有且只有一个。</summary>
    [Serializable]
    public class QuestStartNodeData : QuestBaseNodeData
    {
    }

    /// <summary>
    /// 一个任务：前置连线（任一满足）+ 额外解锁条件都通过后变为可接取。
    /// 全部目标达成后自动完成并发放奖励、解锁后续连线。
    /// </summary>
    [Serializable]
    public class QuestNodeData : QuestBaseNodeData
    {
        [Tooltip("任务 ID，存档与代码引用用，图内不可重复")]
        public string questId;

        public string title;

        [TextArea(2, 5)]
        public string description;

        public Sprite icon;

        [Tooltip("解锁后是否自动接取（否则停留在「可接取」等待玩家手动接）")]
        public bool autoAccept = true;

        [Tooltip("在前置连线之上额外要求的黑板条件，留空 = 无额外条件")]
        public GameCondition unlockCondition = new GameCondition();

        public List<QuestObjectiveData> objectives = new List<QuestObjectiveData>();
        public List<QuestRewardData> rewards = new List<QuestRewardData>();
    }

    /// <summary>条件门：前置连线满足且黑板条件成立时通过；条件不成立则挂起，黑板变化时自动复评。</summary>
    [Serializable]
    public class ConditionGateNodeData : QuestBaseNodeData
    {
        public GameCondition condition = new GameCondition();
    }

    /// <summary>汇合（全部完成）：所有进入连线都满足后才通过。任务默认任一前置满足即解锁，需要「全都做完」时用它。</summary>
    [Serializable]
    public class JoinAllNodeData : QuestBaseNodeData
    {
    }
}
