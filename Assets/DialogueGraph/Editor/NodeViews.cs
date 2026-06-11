using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DialogueGraph.Editor
{
    /// <summary>
    /// 节点视图基类。字段编辑直接写回内存中的 Data；
    /// 连线指向在保存时由 DialogueGraphView 按当前边重建。
    /// </summary>
    public abstract class NodeView : Node
    {
        public BaseNodeData Data { get; }
        public Port Input { get; private set; }

        protected readonly DialogueGraphView graphView;
        readonly List<(Port port, Func<string> getGuid)> outputLinks = new List<(Port, Func<string>)>();

        protected NodeView(DialogueGraphView graphView, BaseNodeData data)
        {
            this.graphView = graphView;
            Data = data;
            style.minWidth = 220;
        }

        protected void AddInputPort(string label = "进入")
        {
            Input = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
            Input.portName = label;
            inputContainer.Add(Input);
        }

        /// <summary>
        /// 输出口携带 setGuid（保存时写入连线目标）与 getGuid（加载时还原连线）。
        /// </summary>
        protected Port AddOutputPort(string label, Func<string> getGuid, Action<string> setGuid,
            VisualElement container = null)
        {
            var port = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
            port.portName = label;
            port.userData = setGuid;
            outputLinks.Add((port, getGuid));
            (container ?? outputContainer).Add(port);
            return port;
        }

        protected void RemoveOutputPort(Port port)
        {
            outputLinks.RemoveAll(l => l.port == port);
            graphView.DeleteElements(port.connections.ToList());
            port.RemoveFromHierarchy();
        }

        public IEnumerable<(Port port, string targetGuid)> EnumerateOutputLinks()
        {
            foreach (var (port, getGuid) in outputLinks)
                yield return (port, getGuid());
        }

        public void SyncToData()
        {
            Data.position = GetPosition().position;
            ClearOutputGuids();
        }

        protected abstract void ClearOutputGuids();

        protected TextField AddTextField(string label, string initial, Action<string> onChange,
            bool multiline = false, VisualElement container = null)
        {
            var field = new TextField(label) { value = initial ?? string.Empty, multiline = multiline };
            field.labelElement.style.minWidth = 52;
            field.RegisterValueChangedCallback(e => onChange(e.newValue));
            (container ?? extensionContainer).Add(field);
            return field;
        }

        protected void AddLocalizedTextFields(string label, LocalizedText text)
        {
            AddTextField(label, text.fallbackText, v => text.fallbackText = v, multiline: true);
            AddTextField("Loc Key", text.key, v => text.key = v);
        }

        /// <summary>角色引用 + 表情下拉（选项随角色资产刷新）。</summary>
        protected void AddSpeakerFields(Func<CharacterAsset> getCharacter, Action<CharacterAsset> setCharacter,
            string initialExpression, Action<string> setExpression)
        {
            var expressionField = new DropdownField("表情")
            {
                value = initialExpression ?? string.Empty,
                choices = getCharacter() != null ? getCharacter().GetExpressionNames() : new List<string>()
            };
            expressionField.labelElement.style.minWidth = 52;
            expressionField.RegisterValueChangedCallback(e => setExpression(e.newValue));

            var characterField = new ObjectField("角色")
            {
                objectType = typeof(CharacterAsset),
                allowSceneObjects = false,
                value = getCharacter(),
                tooltip = "留空 = 旁白"
            };
            characterField.labelElement.style.minWidth = 52;
            characterField.RegisterValueChangedCallback(e =>
            {
                setCharacter(e.newValue as CharacterAsset);
                var character = getCharacter();
                expressionField.choices = character != null ? character.GetExpressionNames() : new List<string>();
            });

            extensionContainer.Add(characterField);
            extensionContainer.Add(expressionField);
        }

        protected void AddObjectField<T>(string label, UnityEngine.Object initial, Action<T> onChange)
            where T : UnityEngine.Object
        {
            var field = new ObjectField(label)
            {
                objectType = typeof(T),
                allowSceneObjects = false,
                value = initial
            };
            field.labelElement.style.minWidth = 52;
            field.RegisterValueChangedCallback(e => onChange(e.newValue as T));
            extensionContainer.Add(field);
        }

        protected void FinishLayout()
        {
            expanded = true;
            RefreshExpandedState();
            RefreshPorts();
        }
    }

    public sealed class StartNodeView : NodeView
    {
        public StartNodeView(DialogueGraphView graphView, StartNodeData data) : base(graphView, data)
        {
            title = "开始";
            capabilities &= ~(Capabilities.Deletable | Capabilities.Copiable);
            AddOutputPort("下一步", () => data.nextGuid, g => data.nextGuid = g);
            FinishLayout();
        }

        protected override void ClearOutputGuids() => ((StartNodeData)Data).nextGuid = null;
    }

    public sealed class DialogueNodeView : NodeView
    {
        public DialogueNodeView(DialogueGraphView graphView, DialogueNodeData data) : base(graphView, data)
        {
            title = "对话";
            style.minWidth = 280;
            AddInputPort();
            AddOutputPort("下一步", () => data.nextGuid, g => data.nextGuid = g);

            AddSpeakerFields(() => data.character, v => data.character = v,
                data.expression, v => data.expression = v);
            AddLocalizedTextFields("文案", data.line);
            AddObjectField<AudioClip>("语音", data.voiceClip, v => data.voiceClip = v);

            extensionContainer.Add(new StageCommandListView(data.commands));
            FinishLayout();
        }

        protected override void ClearOutputGuids() => ((DialogueNodeData)Data).nextGuid = null;
    }

    public sealed class ChoiceNodeView : NodeView
    {
        readonly ChoiceNodeData data;
        readonly VisualElement optionsContainer;
        readonly Dictionary<ChoiceOptionData, (VisualElement row, Port port)> optionRows =
            new Dictionary<ChoiceOptionData, (VisualElement, Port)>();

        public ChoiceNodeView(DialogueGraphView graphView, ChoiceNodeData data) : base(graphView, data)
        {
            this.data = data;
            title = "选项分支";
            style.minWidth = 300;
            AddInputPort();

            AddSpeakerFields(() => data.character, v => data.character = v,
                data.expression, v => data.expression = v);
            AddLocalizedTextFields("提示文案", data.prompt);

            optionsContainer = new VisualElement();
            extensionContainer.Add(optionsContainer);
            extensionContainer.Add(new Button(AddNewOption) { text = "＋ 添加选项" });

            foreach (var option in data.options)
            {
                if (string.IsNullOrEmpty(option.id))
                    option.id = Guid.NewGuid().ToString("N"); // 旧数据补 ID
                AddOptionRow(option);
            }

            FinishLayout();
        }

        void AddNewOption()
        {
            var option = new ChoiceOptionData { id = Guid.NewGuid().ToString("N") };
            data.options.Add(option);
            AddOptionRow(option);
            FinishLayout();
        }

        void AddOptionRow(ChoiceOptionData option)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;

            var textField = new TextField { value = option.text.fallbackText ?? "", tooltip = "选项文案" };
            textField.style.flexGrow = 1;
            textField.style.minWidth = 80;
            textField.RegisterValueChangedCallback(e => option.text.fallbackText = e.newValue);

            var keyField = new TextField { value = option.text.key ?? "", tooltip = "本地化 Key（可选）" };
            keyField.style.width = 70;
            keyField.RegisterValueChangedCallback(e => option.text.key = e.newValue);

            var deleteButton = new Button(() => RemoveOption(option)) { text = "✕", tooltip = "删除该选项" };

            row.Add(textField);
            row.Add(keyField);
            row.Add(deleteButton);
            optionsContainer.Add(row);

            var port = AddOutputPort(string.Empty, () => option.nextGuid, g => option.nextGuid = g, row);
            optionRows[option] = (row, port);
        }

        void RemoveOption(ChoiceOptionData option)
        {
            if (!optionRows.TryGetValue(option, out var entry)) return;
            optionRows.Remove(option);
            data.options.Remove(option);
            RemoveOutputPort(entry.port);
            entry.row.RemoveFromHierarchy();
        }

        protected override void ClearOutputGuids()
        {
            foreach (var option in data.options)
                option.nextGuid = null;
        }
    }

    public sealed class ConditionNodeView : NodeView
    {
        // 顺序与 VariableType / CompareOp 枚举一一对应
        static readonly List<string> TypeNames = new List<string> { "布尔", "整数", "小数", "文本" };
        static readonly List<string> OpSymbols = new List<string> { "==", "!=", ">", ">=", "<", "<=" };

        readonly ConditionNodeData data;
        readonly VisualElement valueContainer;

        public ConditionNodeView(DialogueGraphView graphView, ConditionNodeData data) : base(graphView, data)
        {
            this.data = data;
            title = "条件判断";
            AddInputPort();
            AddOutputPort("满足", () => data.trueGuid, g => data.trueGuid = g);
            AddOutputPort("不满足", () => data.falseGuid, g => data.falseGuid = g);

            AddTextField("变量名", data.condition.variableName, v => data.condition.variableName = v);

            var typeField = new PopupField<string>("类型", TypeNames, (int)data.condition.variableType);
            typeField.labelElement.style.minWidth = 52;
            typeField.RegisterValueChangedCallback(_ =>
            {
                data.condition.variableType = (VariableType)TypeNames.IndexOf(typeField.value);
                RebuildValueField();
            });
            extensionContainer.Add(typeField);

            var opField = new PopupField<string>("比较", OpSymbols, (int)data.condition.op);
            opField.labelElement.style.minWidth = 52;
            opField.RegisterValueChangedCallback(_ =>
                data.condition.op = (CompareOp)OpSymbols.IndexOf(opField.value));
            extensionContainer.Add(opField);

            valueContainer = new VisualElement();
            extensionContainer.Add(valueContainer);
            RebuildValueField();

            FinishLayout();
        }

        void RebuildValueField()
        {
            valueContainer.Clear();
            var c = data.condition;
            switch (c.variableType)
            {
                case VariableType.Bool:
                    var toggle = new Toggle("值") { value = c.boolValue };
                    toggle.labelElement.style.minWidth = 52;
                    toggle.RegisterValueChangedCallback(e => c.boolValue = e.newValue);
                    valueContainer.Add(toggle);
                    break;
                case VariableType.Int:
                    var intField = new IntegerField("值") { value = c.intValue };
                    intField.labelElement.style.minWidth = 52;
                    intField.RegisterValueChangedCallback(e => c.intValue = e.newValue);
                    valueContainer.Add(intField);
                    break;
                case VariableType.Float:
                    var floatField = new FloatField("值") { value = c.floatValue };
                    floatField.labelElement.style.minWidth = 52;
                    floatField.RegisterValueChangedCallback(e => c.floatValue = e.newValue);
                    valueContainer.Add(floatField);
                    break;
                case VariableType.String:
                    var textField = new TextField("值") { value = c.stringValue ?? "" };
                    textField.labelElement.style.minWidth = 52;
                    textField.RegisterValueChangedCallback(e => c.stringValue = e.newValue);
                    valueContainer.Add(textField);
                    break;
            }
        }

        protected override void ClearOutputGuids()
        {
            data.trueGuid = null;
            data.falseGuid = null;
        }
    }

    public sealed class EventNodeView : NodeView
    {
        public EventNodeView(DialogueGraphView graphView, EventNodeData data) : base(graphView, data)
        {
            title = "事件触发";
            AddInputPort();
            AddOutputPort("下一步", () => data.nextGuid, g => data.nextGuid = g);
            AddTextField("事件名", data.eventName, v => data.eventName = v);
            AddTextField("参数", data.stringParam, v => data.stringParam = v);
            FinishLayout();
        }

        protected override void ClearOutputGuids() => ((EventNodeData)Data).nextGuid = null;
    }

    public sealed class StageNodeView : NodeView
    {
        public StageNodeView(DialogueGraphView graphView, StageNodeData data) : base(graphView, data)
        {
            title = "演出";
            style.minWidth = 280;
            AddInputPort();
            AddOutputPort("下一步", () => data.nextGuid, g => data.nextGuid = g);
            extensionContainer.Add(new StageCommandListView(data.commands));
            FinishLayout();
        }

        protected override void ClearOutputGuids() => ((StageNodeData)Data).nextGuid = null;
    }

    public sealed class SubGraphNodeView : NodeView
    {
        public SubGraphNodeView(DialogueGraphView graphView, SubGraphNodeData data) : base(graphView, data)
        {
            title = "子图跳转";
            AddInputPort();
            AddOutputPort("返回后", () => data.nextGuid, g => data.nextGuid = g);

            AddObjectField<DialogueGraphAsset>("子图", data.graph, v => data.graph = v);
            extensionContainer.Add(new Button(() =>
            {
                if (data.graph != null) DialogueGraphWindow.Open(data.graph);
            }) { text = "打开子图" });

            FinishLayout();
        }

        protected override void ClearOutputGuids() => ((SubGraphNodeData)Data).nextGuid = null;
    }

    public sealed class EndNodeView : NodeView
    {
        public EndNodeView(DialogueGraphView graphView, EndNodeData data) : base(graphView, data)
        {
            title = "结束";
            AddInputPort();
            FinishLayout();
        }

        protected override void ClearOutputGuids()
        {
        }
    }
}
