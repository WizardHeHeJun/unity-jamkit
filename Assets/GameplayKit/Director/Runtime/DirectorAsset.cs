using System;
using System.Collections.Generic;
using DialogueGraph;
using GameplayKit.Quest;
using UnityEngine;

namespace GameplayKit.Director
{
    // ---------------- 触发器 ----------------

    /// <summary>规则的触发源基类。新增触发器：加子类 + DirectorWindow 注册编辑 UI + DirectorRunner 加匹配分支。</summary>
    [Serializable]
    public abstract class DirectorTrigger
    {
    }

    /// <summary>游戏事件触发：游戏侧 / 对话事件节点 ReportEvent 上报的 (事件名, 参数)。</summary>
    [Serializable]
    public class GameEventTrigger : DirectorTrigger
    {
        [Tooltip("事件名，与 ReportEvent / 对话事件节点的事件名匹配")]
        public string eventName;

        [Tooltip("参数匹配，留空 = 任意参数都触发")]
        public string param;
    }

    /// <summary>黑板条件触发：规则的「附加条件」从不满足变为满足的瞬间触发（边沿触发）。</summary>
    [Serializable]
    public class VariableTrigger : DirectorTrigger
    {
    }

    public enum QuestTriggerPhase
    {
        /// <summary>任务变为可接取。</summary>
        Available,

        /// <summary>任务被接取。</summary>
        Accepted,

        /// <summary>任务完成。</summary>
        Completed
    }

    /// <summary>任务状态触发：挂接的 QuestRunner 中指定任务进入指定阶段时触发。</summary>
    [Serializable]
    public class QuestStateTrigger : DirectorTrigger
    {
        public string questId;
        public QuestTriggerPhase phase = QuestTriggerPhase.Completed;
    }

    /// <summary>对话结束触发：ReportDialogueEnded 上报的对话图结束时触发。</summary>
    [Serializable]
    public class DialogueEndedTrigger : DirectorTrigger
    {
        [Tooltip("留空 = 任意对话结束都触发")]
        public DialogueGraphAsset dialogue;
    }

    // ---------------- 动作 ----------------

    /// <summary>规则触发后执行的动作基类。新增动作：加子类 + DirectorWindow 注册编辑 UI + DirectorRunner.ExecuteAction 加分支。</summary>
    [Serializable]
    public abstract class DirectorAction
    {
    }

    /// <summary>开始对话：通过 OnStartDialogue 通知游戏侧播放（如 VNPlayer）。</summary>
    [Serializable]
    public class StartDialogueAction : DirectorAction
    {
        public DialogueGraphAsset dialogue;
    }

    public enum SetVariableOp
    {
        /// <summary>直接设值。</summary>
        Set,

        /// <summary>在当前值上累加（仅整数 / 小数）。</summary>
        Add
    }

    /// <summary>修改黑板变量（直接执行，可连锁触发其他规则的条件复评）。</summary>
    [Serializable]
    public class SetVariableAction : DirectorAction
    {
        public string variableName;
        public BlackboardVariableType variableType = BlackboardVariableType.Bool;
        public SetVariableOp op = SetVariableOp.Set;

        public bool boolValue;
        public int intValue;
        public float floatValue;
        public string stringValue;
    }

    /// <summary>抛游戏事件：通知游戏侧（OnGameEvent），同时回流为事件触发源，可链式触发其他规则。</summary>
    [Serializable]
    public class RaiseEventAction : DirectorAction
    {
        public string eventName;
        public string param;
    }

    /// <summary>接取任务（需挂接 QuestRunner，任务须处于「可接取」状态）。</summary>
    [Serializable]
    public class AcceptQuestAction : DirectorAction
    {
        public string questId;
    }

    /// <summary>直接完成任务（需挂接 QuestRunner）。</summary>
    [Serializable]
    public class CompleteQuestAction : DirectorAction
    {
        public string questId;
    }

    /// <summary>上报任务进度（需挂接 QuestRunner），等价游戏侧 ReportProgress。</summary>
    [Serializable]
    public class ReportProgressAction : DirectorAction
    {
        [Tooltip("目标类型，与任务目标的类型一致")]
        public string objectiveType = "对话";

        public string targetId;
        public int amount = 1;
    }

    // ---------------- 规则与资产 ----------------

    /// <summary>一条编排规则：触发源 + 附加黑板条件 + 动作列表。</summary>
    [Serializable]
    public class DirectorRule
    {
        [Tooltip("稳定 ID，创建时生成，存档记录触发次数用")]
        public string id;

        [Tooltip("给策划看的规则名，如「击败BOSS后开结局对话」")]
        public string title;

        public bool enabled = true;

        [Tooltip("仅触发一次（计入存档）；关闭后每次满足都触发")]
        public bool once = true;

        [SerializeReference] public DirectorTrigger trigger = new GameEventTrigger();

        [Tooltip("触发时还须满足的黑板条件；「黑板条件」触发器直接以此为触发条件")]
        public GameCondition condition = new GameCondition();

        [SerializeReference] public List<DirectorAction> actions = new List<DirectorAction>();
    }

    /// <summary>
    /// 导演编排表：「当 X 发生时做 Y」的规则集合，连接对话、任务与黑板，
    /// 替代游戏侧手写的事件胶水代码。运行时由 <see cref="DirectorRunner"/> 执行。
    /// </summary>
    [CreateAssetMenu(fileName = "NewDirector", menuName = "玩法/导演编排 (Director)")]
    public class DirectorAsset : ScriptableObject
    {
        [Tooltip("变量引用的黑板（编辑器下拉与校验用）")]
        public BlackboardAsset blackboard;

        [Tooltip("任务引用的任务图（编辑器任务 ID 下拉与校验用）")]
        public QuestGraphAsset questGraph;

        public List<DirectorRule> rules = new List<DirectorRule>();
    }
}
