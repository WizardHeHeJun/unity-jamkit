using System;
using System.Collections.Generic;
using System.Linq;
using GameplayKit.Editor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameplayKit.Quest.Editor
{
    /// <summary>
    /// 任务图节点视图基类。字段编辑直接写回内存中的 Data；
    /// 输出口允许连多条线（一个节点完成后可同时解锁多个后续），
    /// 连线指向在保存时由 QuestGraphView 按当前边重建到 nextGuids。
    /// </summary>
    public abstract class BaseQuestNodeView : Node
    {
        public QuestBaseNodeData Data { get; }
        public Port Input { get; private set; }
        public Port Output { get; private set; }

        protected readonly QuestGraphView graphView;

        protected BaseQuestNodeView(QuestGraphView graphView, QuestBaseNodeData data)
        {
            this.graphView = graphView;
            Data = data;
            style.minWidth = 220;
        }

        protected void AddInputPort(string label = "前置")
        {
            Input = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
            Input.portName = label;
            inputContainer.Add(Input);
        }

        protected void AddOutputPort(string label)
        {
            Output = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(bool));
            Output.portName = label;
            // 保存时每条边调用一次，把目标 guid 追加进 nextGuids
            Output.userData = (Action<string>)(guid =>
            {
                if (!Data.nextGuids.Contains(guid))
                    Data.nextGuids.Add(guid);
            });
            outputContainer.Add(Output);
        }

        public IEnumerable<(Port port, string targetGuid)> EnumerateOutputLinks()
        {
            if (Output == null) yield break;
            foreach (var guid in Data.nextGuids)
                yield return (Output, guid);
        }

        public void SyncToData()
        {
            Data.position = GetPosition().position;
            Data.nextGuids.Clear(); // 保存时由当前连线重建
        }

        protected TextField AddTextField(string label, string initial, Action<string> onChange,
            bool multiline = false, VisualElement container = null)
        {
            var field = new TextField(label) { value = initial ?? string.Empty, multiline = multiline };
            field.labelElement.style.minWidth = 52;
            field.RegisterValueChangedCallback(e => onChange(e.newValue));
            (container ?? extensionContainer).Add(field);
            return field;
        }

        protected Label AddSectionLabel(string text)
        {
            var label = new Label(text);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginTop = 6;
            extensionContainer.Add(label);
            return label;
        }

        protected void FinishLayout()
        {
            expanded = true;
            RefreshExpandedState();
            RefreshPorts();
        }
    }

    public sealed class QuestStartNodeView : BaseQuestNodeView
    {
        public QuestStartNodeView(QuestGraphView graphView, QuestStartNodeData data) : base(graphView, data)
        {
            title = "开始（开局解锁）";
            capabilities &= ~(Capabilities.Deletable | Capabilities.Copiable);
            AddOutputPort("解锁");
            FinishLayout();
        }
    }

    public sealed class QuestNodeView : BaseQuestNodeView
    {
        readonly QuestNodeData data;
        readonly VisualElement objectivesContainer;
        readonly VisualElement rewardsContainer;

        public QuestNodeView(QuestGraphView graphView, QuestNodeData data) : base(graphView, data)
        {
            this.data = data;
            style.minWidth = 320;
            UpdateTitle();

            AddInputPort();
            AddOutputPort("完成后");

            AddTextField("任务 ID", data.questId, v => data.questId = v);
            AddTextField("标题", data.title, v =>
            {
                data.title = v;
                UpdateTitle();
            });
            AddTextField("描述", data.description, v => data.description = v, multiline: true);

            var iconField = new UnityEditor.UIElements.ObjectField("图标")
            {
                objectType = typeof(Sprite),
                allowSceneObjects = false,
                value = data.icon
            };
            iconField.labelElement.style.minWidth = 52;
            iconField.RegisterValueChangedCallback(e => data.icon = e.newValue as Sprite);
            extensionContainer.Add(iconField);

            var autoAcceptToggle = new Toggle("自动接取") { value = data.autoAccept };
            autoAcceptToggle.labelElement.style.minWidth = 52;
            autoAcceptToggle.tooltip = "解锁后自动开始；关闭则停在「可接取」等玩家手动接";
            autoAcceptToggle.RegisterValueChangedCallback(e => data.autoAccept = e.newValue);
            extensionContainer.Add(autoAcceptToggle);

            extensionContainer.Add(new GameConditionView(data.unlockCondition,
                () => graphView.Asset != null ? graphView.Asset.blackboard : null, "额外解锁条件"));

            AddSectionLabel("目标（全部达成即完成）");
            objectivesContainer = new VisualElement();
            extensionContainer.Add(objectivesContainer);
            extensionContainer.Add(new Button(AddNewObjective) { text = "＋ 添加目标" });
            foreach (var objective in data.objectives)
            {
                if (string.IsNullOrEmpty(objective.id))
                    objective.id = Guid.NewGuid().ToString("N"); // 旧数据补 ID
                AddObjectiveBox(objective);
            }

            AddSectionLabel("奖励");
            rewardsContainer = new VisualElement();
            extensionContainer.Add(rewardsContainer);
            extensionContainer.Add(new Button(AddNewReward) { text = "＋ 添加奖励" });
            foreach (var reward in data.rewards)
                AddRewardRow(reward);

            FinishLayout();
        }

        void UpdateTitle()
        {
            title = string.IsNullOrEmpty(data.title) ? "任务" : $"任务：{data.title}";
        }

        void AddNewObjective()
        {
            var objective = new QuestObjectiveData { id = Guid.NewGuid().ToString("N") };
            data.objectives.Add(objective);
            AddObjectiveBox(objective);
            FinishLayout();
        }

        void AddObjectiveBox(QuestObjectiveData objective)
        {
            var box = new VisualElement();
            box.style.borderBottomWidth = 1;
            box.style.borderBottomColor = new Color(0.3f, 0.3f, 0.3f);
            box.style.marginBottom = 3;
            box.style.paddingBottom = 3;

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;

            var types = new List<string>(QuestObjectiveTypes.All);
            if (!string.IsNullOrEmpty(objective.objectiveType) && !types.Contains(objective.objectiveType))
                types.Insert(0, objective.objectiveType);
            int typeIndex = Mathf.Max(0, types.IndexOf(objective.objectiveType));
            var typeField = new PopupField<string>(types, typeIndex) { tooltip = "目标类型" };
            typeField.RegisterValueChangedCallback(e => objective.objectiveType = e.newValue);
            row.Add(typeField);

            var targetField = new TextField
            {
                value = objective.targetId ?? "",
                tooltip = "目标对象 ID（怪物/道具/地点…）。留空 = 该类型任意对象都计数"
            };
            targetField.style.flexGrow = 1;
            targetField.style.minWidth = 70;
            targetField.RegisterValueChangedCallback(e => objective.targetId = e.newValue);
            row.Add(targetField);

            var countField = new IntegerField { value = objective.requiredCount, tooltip = "需求数量" };
            countField.style.width = 40;
            countField.RegisterValueChangedCallback(e => objective.requiredCount = e.newValue);
            row.Add(countField);

            row.Add(new Button(() =>
            {
                data.objectives.Remove(objective);
                box.RemoveFromHierarchy();
            }) { text = "✕", tooltip = "删除该目标" });
            box.Add(row);

            var descField = new TextField
            {
                value = objective.description ?? "",
                tooltip = "任务追踪 UI 上显示的文案，如「击败训练假人」"
            };
            descField.RegisterValueChangedCallback(e => objective.description = e.newValue);
            box.Add(descField);

            objectivesContainer.Add(box);
        }

        void AddNewReward()
        {
            var reward = new QuestRewardData();
            data.rewards.Add(reward);
            AddRewardRow(reward);
            FinishLayout();
        }

        void AddRewardRow(QuestRewardData reward)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 2;

            var idField = new TextField { value = reward.rewardId ?? "", tooltip = "奖励 ID（道具/货币…）" };
            idField.style.flexGrow = 1;
            idField.style.minWidth = 80;
            idField.RegisterValueChangedCallback(e => reward.rewardId = e.newValue);
            row.Add(idField);

            var amountField = new IntegerField { value = reward.amount, tooltip = "数量" };
            amountField.style.width = 50;
            amountField.RegisterValueChangedCallback(e => reward.amount = e.newValue);
            row.Add(amountField);

            row.Add(new Button(() =>
            {
                data.rewards.Remove(reward);
                row.RemoveFromHierarchy();
            }) { text = "✕", tooltip = "删除该奖励" });

            rewardsContainer.Add(row);
        }
    }

    public sealed class ConditionGateNodeView : BaseQuestNodeView
    {
        public ConditionGateNodeView(QuestGraphView graphView, ConditionGateNodeData data) : base(graphView, data)
        {
            title = "条件门";
            style.minWidth = 280;
            AddInputPort();
            AddOutputPort("通过后");

            extensionContainer.Add(new GameConditionView(data.condition,
                () => graphView.Asset != null ? graphView.Asset.blackboard : null, "通过条件"));
            FinishLayout();
        }
    }

    public sealed class JoinAllNodeView : BaseQuestNodeView
    {
        public JoinAllNodeView(QuestGraphView graphView, JoinAllNodeData data) : base(graphView, data)
        {
            title = "汇合（全部完成）";
            AddInputPort("全部前置");
            AddOutputPort("之后");

            var hint = new Label("所有进入连线都完成后才通过");
            hint.style.color = new Color(0.6f, 0.6f, 0.6f);
            hint.style.marginTop = 4;
            extensionContainer.Add(hint);
            FinishLayout();
        }
    }
}
