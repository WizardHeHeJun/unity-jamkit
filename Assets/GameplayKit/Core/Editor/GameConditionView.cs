using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameplayKit.Editor
{
    /// <summary>
    /// 通用条件组编辑控件（UIElements），任务节点、条件门等共用。
    /// 配了黑板时变量为下拉选择（选中后类型自动跟随声明），未配黑板时退化为手填。
    /// 字段实时写回内存中的 <see cref="GameCondition"/>，由宿主负责保存。
    /// </summary>
    public class GameConditionView : VisualElement
    {
        // 顺序与 ConditionMode / ConditionCompareOp / BlackboardVariableType 枚举一一对应
        static readonly List<string> ModeNames = new List<string> { "全部满足", "任一满足" };
        static readonly List<string> NumberOps = new List<string> { "==", "!=", ">", ">=", "<", "<=" };
        static readonly List<string> EqualityOps = new List<string> { "==", "!=" };
        static readonly List<string> TypeNames = new List<string> { "布尔", "整数", "小数", "文本" };

        readonly GameCondition condition;
        readonly Func<BlackboardAsset> getBlackboard;
        readonly VisualElement rowsContainer;
        readonly PopupField<string> modeField;

        public GameConditionView(GameCondition condition, Func<BlackboardAsset> getBlackboard, string title = "条件")
        {
            this.condition = condition;
            this.getBlackboard = getBlackboard;

            style.marginTop = 4;

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;

            var titleLabel = new Label(title);
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.flexGrow = 1;
            header.Add(titleLabel);

            modeField = new PopupField<string>(ModeNames, (int)condition.mode);
            modeField.tooltip = "多条比较的组合方式";
            modeField.RegisterValueChangedCallback(_ =>
                condition.mode = (ConditionMode)ModeNames.IndexOf(modeField.value));
            header.Add(modeField);

            header.Add(new Button(AddClause) { text = "＋", tooltip = "添加一条比较" });
            Add(header);

            rowsContainer = new VisualElement();
            Add(rowsContainer);

            Rebuild();
        }

        void AddClause()
        {
            var clause = new ConditionClause();
            var board = getBlackboard?.Invoke();
            var firstName = board != null && board.GetNames().Count > 0 ? board.GetNames()[0] : null;
            if (!string.IsNullOrEmpty(firstName))
            {
                clause.variableName = firstName;
                clause.variableType = board.Find(firstName).type;
            }
            condition.clauses.Add(clause);
            Rebuild();
        }

        void Rebuild()
        {
            rowsContainer.Clear();

            modeField.style.display = condition.clauses.Count > 1 ? DisplayStyle.Flex : DisplayStyle.None;

            if (condition.clauses.Count == 0)
            {
                var empty = new Label("（无条件 = 恒满足）");
                empty.style.color = new Color(0.6f, 0.6f, 0.6f);
                rowsContainer.Add(empty);
                return;
            }

            foreach (var clause in condition.clauses)
                rowsContainer.Add(BuildRow(clause));
        }

        VisualElement BuildRow(ConditionClause clause)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 2;

            var board = getBlackboard?.Invoke();
            if (board != null)
            {
                var names = board.GetNames();
                if (!string.IsNullOrEmpty(clause.variableName) && !names.Contains(clause.variableName))
                    names.Insert(0, clause.variableName); // 黑板里已不存在的旧引用，保留显示，由校验报错

                if (names.Count == 0)
                {
                    var hint = new Label("黑板中没有变量");
                    hint.style.color = new Color(1f, 0.6f, 0.4f);
                    hint.style.flexGrow = 1;
                    row.Add(hint);
                }
                else
                {
                    int index = Mathf.Max(0, names.IndexOf(clause.variableName));
                    clause.variableName = names[index];
                    var def = board.Find(clause.variableName);
                    if (def != null) clause.variableType = def.type;

                    var variableField = new PopupField<string>(names, index);
                    variableField.style.flexGrow = 1;
                    variableField.style.minWidth = 70;
                    variableField.RegisterValueChangedCallback(e =>
                    {
                        clause.variableName = e.newValue;
                        var d = getBlackboard?.Invoke()?.Find(e.newValue);
                        if (d != null) clause.variableType = d.type;
                        Rebuild(); // 类型可能变化，重建比较符与值字段
                    });
                    row.Add(variableField);
                }
            }
            else
            {
                // 未配黑板：手填变量名 + 手选类型
                var nameField = new TextField { value = clause.variableName ?? "", tooltip = "变量名（图未配置黑板，需手填）" };
                nameField.style.flexGrow = 1;
                nameField.style.minWidth = 60;
                nameField.RegisterValueChangedCallback(e => clause.variableName = e.newValue);
                row.Add(nameField);

                var typeField = new PopupField<string>(TypeNames, (int)clause.variableType);
                typeField.RegisterValueChangedCallback(_ =>
                {
                    clause.variableType = (BlackboardVariableType)TypeNames.IndexOf(typeField.value);
                    Rebuild();
                });
                row.Add(typeField);
            }

            bool equalityOnly = clause.variableType == BlackboardVariableType.Bool ||
                                clause.variableType == BlackboardVariableType.String;
            var ops = equalityOnly ? EqualityOps : NumberOps;
            if ((int)clause.op >= ops.Count) clause.op = ConditionCompareOp.Equal;

            var opField = new PopupField<string>(ops, (int)clause.op);
            opField.RegisterValueChangedCallback(_ =>
                clause.op = (ConditionCompareOp)ops.IndexOf(opField.value));
            row.Add(opField);

            row.Add(BuildValueField(clause));
            row.Add(new Button(() =>
            {
                condition.clauses.Remove(clause);
                Rebuild();
            }) { text = "✕", tooltip = "删除该条比较" });

            return row;
        }

        VisualElement BuildValueField(ConditionClause clause)
        {
            switch (clause.variableType)
            {
                case BlackboardVariableType.Bool:
                    var toggle = new Toggle { value = clause.boolValue };
                    toggle.RegisterValueChangedCallback(e => clause.boolValue = e.newValue);
                    return toggle;

                case BlackboardVariableType.Int:
                    var intField = new IntegerField { value = clause.intValue };
                    intField.style.minWidth = 50;
                    intField.RegisterValueChangedCallback(e => clause.intValue = e.newValue);
                    return intField;

                case BlackboardVariableType.Float:
                    var floatField = new FloatField { value = clause.floatValue };
                    floatField.style.minWidth = 50;
                    floatField.RegisterValueChangedCallback(e => clause.floatValue = e.newValue);
                    return floatField;

                default:
                    var textField = new TextField { value = clause.stringValue ?? "" };
                    textField.style.minWidth = 50;
                    textField.RegisterValueChangedCallback(e => clause.stringValue = e.newValue);
                    return textField;
            }
        }
    }
}
