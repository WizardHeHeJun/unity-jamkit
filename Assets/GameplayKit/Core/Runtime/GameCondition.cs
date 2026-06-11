using System;
using System.Collections.Generic;

namespace GameplayKit
{
    public enum ConditionCompareOp
    {
        Equal,
        NotEqual,
        Greater,
        GreaterOrEqual,
        Less,
        LessOrEqual
    }

    public enum ConditionMode
    {
        /// <summary>全部满足（与）。</summary>
        All,

        /// <summary>任一满足（或）。</summary>
        Any
    }

    /// <summary>单条比较：黑板变量 与 固定值 比较。变量不存在时视为不满足。</summary>
    [Serializable]
    public class ConditionClause
    {
        public string variableName;
        public BlackboardVariableType variableType = BlackboardVariableType.Bool;
        public ConditionCompareOp op = ConditionCompareOp.Equal;

        public bool boolValue;
        public int intValue;
        public float floatValue;
        public string stringValue;

        public bool Evaluate(IBlackboardReader reader)
        {
            if (reader == null || string.IsNullOrEmpty(variableName)) return false;

            switch (variableType)
            {
                case BlackboardVariableType.Bool:
                    if (!reader.TryGetBool(variableName, out var b)) return false;
                    return op == ConditionCompareOp.NotEqual ? b != boolValue : b == boolValue;

                case BlackboardVariableType.Int:
                    if (!reader.TryGetInt(variableName, out var i)) return false;
                    return CompareNumber(i.CompareTo(intValue));

                case BlackboardVariableType.Float:
                    if (!reader.TryGetFloat(variableName, out var f)) return false;
                    return CompareNumber(f.CompareTo(floatValue));

                case BlackboardVariableType.String:
                    if (!reader.TryGetString(variableName, out var s)) return false;
                    return op == ConditionCompareOp.NotEqual
                        ? s != stringValue
                        : s == stringValue;

                default:
                    return false;
            }
        }

        bool CompareNumber(int compareResult)
        {
            switch (op)
            {
                case ConditionCompareOp.Equal: return compareResult == 0;
                case ConditionCompareOp.NotEqual: return compareResult != 0;
                case ConditionCompareOp.Greater: return compareResult > 0;
                case ConditionCompareOp.GreaterOrEqual: return compareResult >= 0;
                case ConditionCompareOp.Less: return compareResult < 0;
                case ConditionCompareOp.LessOrEqual: return compareResult <= 0;
                default: return false;
            }
        }
    }

    /// <summary>
    /// 通用条件组：若干条比较按「全部满足 / 任一满足」组合。
    /// 没有任何条目时视为恒满足。任务解锁、条件门、剧情分支等共用。
    /// </summary>
    [Serializable]
    public class GameCondition
    {
        public ConditionMode mode = ConditionMode.All;
        public List<ConditionClause> clauses = new List<ConditionClause>();

        public bool IsEmpty => clauses == null || clauses.Count == 0;

        public bool Evaluate(IBlackboardReader reader)
        {
            if (IsEmpty) return true;

            foreach (var clause in clauses)
            {
                if (clause == null) continue;
                bool ok = clause.Evaluate(reader);
                if (mode == ConditionMode.All && !ok) return false;
                if (mode == ConditionMode.Any && ok) return true;
            }
            return mode == ConditionMode.All;
        }
    }
}
