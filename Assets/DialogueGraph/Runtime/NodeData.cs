using System;
using System.Collections.Generic;
using UnityEngine;

namespace DialogueGraph
{
    /// <summary>
    /// 可本地化文本：key 非空且本地化表中能查到时使用译文，否则使用 fallbackText。
    /// </summary>
    [Serializable]
    public class LocalizedText
    {
        [Tooltip("本地化 Key，留空则直接使用 fallbackText")]
        public string key;

        [TextArea(2, 6)]
        public string fallbackText;
    }

    public enum VariableType
    {
        Bool,
        Int,
        Float,
        String
    }

    public enum CompareOp
    {
        Equal,
        NotEqual,
        Greater,
        GreaterOrEqual,
        Less,
        LessOrEqual
    }

    [Serializable]
    public class VariableCondition
    {
        public string variableName;
        public VariableType variableType = VariableType.Bool;
        public CompareOp op = CompareOp.Equal;

        public bool boolValue;
        public int intValue;
        public float floatValue;
        public string stringValue;
    }

    [Serializable]
    public abstract class BaseNodeData
    {
        public string guid;
        public Vector2 position;
    }

    /// <summary>剧情入口，每张图有且只有一个。</summary>
    [Serializable]
    public class StartNodeData : BaseNodeData
    {
        public string nextGuid;
    }

    /// <summary>
    /// 一句对话：角色 + 表情 + 文案 + 语音 + 台词显示前执行的演出指令。
    /// character 为 null 表示旁白。台词的稳定行 ID 即节点 guid。
    /// </summary>
    [Serializable]
    public class DialogueNodeData : BaseNodeData
    {
        public CharacterAsset character;
        public string expression;
        public LocalizedText line = new LocalizedText();
        public AudioClip voiceClip;

        [SerializeReference]
        public List<StageCommand> commands = new List<StageCommand>();

        public string nextGuid;
    }

    [Serializable]
    public class ChoiceOptionData
    {
        [Tooltip("稳定行 ID，创建时生成，供已读记录 / 配音 / 文案导出使用")]
        public string id;

        public LocalizedText text = new LocalizedText();
        public string nextGuid;
    }

    /// <summary>玩家选项分支。</summary>
    [Serializable]
    public class ChoiceNodeData : BaseNodeData
    {
        public CharacterAsset character;
        public string expression;
        public LocalizedText prompt = new LocalizedText();
        public List<ChoiceOptionData> options = new List<ChoiceOptionData>();
    }

    /// <summary>条件判断：按变量比较结果走「满足 / 不满足」分支。</summary>
    [Serializable]
    public class ConditionNodeData : BaseNodeData
    {
        public VariableCondition condition = new VariableCondition();
        public string trueGuid;
        public string falseGuid;
    }

    /// <summary>事件触发：通知游戏侧执行逻辑（给道具、解锁CG等），自动继续。</summary>
    [Serializable]
    public class EventNodeData : BaseNodeData
    {
        public string eventName;
        public string stringParam;
        public string nextGuid;
    }

    /// <summary>纯演出节点：无台词的演出段落（切背景、换 BGM、立绘调度等）。</summary>
    [Serializable]
    public class StageNodeData : BaseNodeData
    {
        [SerializeReference]
        public List<StageCommand> commands = new List<StageCommand>();

        public string nextGuid;
    }

    /// <summary>子图跳转：执行子图至其结束节点后返回，继续走 nextGuid。</summary>
    [Serializable]
    public class SubGraphNodeData : BaseNodeData
    {
        public DialogueGraphAsset graph;
        public string nextGuid;
    }

    [Serializable]
    public class EndNodeData : BaseNodeData
    {
    }
}
