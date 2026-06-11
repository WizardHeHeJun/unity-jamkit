using System;
using System.Collections.Generic;
using System.Linq;
using DialogueGraph;
using GameplayKit.Editor;
using GameplayKit.Quest;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameplayKit.Director.Editor
{
    /// <summary>
    /// 导演编排编辑器：「当 X 发生时做 Y」的规则列表。
    /// 字段实时写入资产内存，「保存」落盘并校验；「模拟」不进 Play Mode 试跑规则。
    /// </summary>
    public class DirectorWindow : EditorWindow
    {
        // 顺序与下拉一一对应
        static readonly (string label, Func<DirectorTrigger> create, Type type)[] TriggerTypes =
        {
            ("事件触发", () => new GameEventTrigger(), typeof(GameEventTrigger)),
            ("黑板条件", () => new VariableTrigger(), typeof(VariableTrigger)),
            ("任务状态", () => new QuestStateTrigger(), typeof(QuestStateTrigger)),
            ("对话结束", () => new DialogueEndedTrigger(), typeof(DialogueEndedTrigger))
        };

        static readonly (string label, Func<DirectorAction> create)[] ActionTypes =
        {
            ("开始对话", () => (DirectorAction)new StartDialogueAction()),
            ("修改变量", () => (DirectorAction)new SetVariableAction()),
            ("抛游戏事件", () => (DirectorAction)new RaiseEventAction()),
            ("接取任务", () => (DirectorAction)new AcceptQuestAction()),
            ("完成任务", () => (DirectorAction)new CompleteQuestAction()),
            ("上报任务进度", () => (DirectorAction)new ReportProgressAction())
        };

        static readonly List<string> PhaseNames = new List<string> { "变为可接取", "被接取", "完成" };
        static readonly List<string> SetOpNames = new List<string> { "设为", "累加" };

        [SerializeField] DirectorAsset asset;

        ObjectField assetField;
        ScrollView rulesView;
        VisualElement issuesPanel;
        readonly List<VisualElement> ruleCards = new List<VisualElement>();

        [MenuItem("Tools/玩法工具/导演编排")]
        public static void OpenWindow()
        {
            GetWindow<DirectorWindow>("导演编排");
        }

        public static void Open(DirectorAsset directorAsset)
        {
            var window = GetWindow<DirectorWindow>("导演编排");
            window.asset = directorAsset;
            window.assetField?.SetValueWithoutNotify(directorAsset);
            window.RebuildAll();
        }

        [OnOpenAsset]
        public static bool OnOpenAsset(int instanceID, int line)
        {
            if (EditorUtility.InstanceIDToObject(instanceID) is DirectorAsset directorAsset)
            {
                Open(directorAsset);
                return true;
            }
            return false;
        }

        void CreateGUI()
        {
            var toolbar = new Toolbar();

            assetField = new ObjectField
            {
                objectType = typeof(DirectorAsset),
                allowSceneObjects = false,
                value = asset
            };
            assetField.style.minWidth = 200;
            assetField.RegisterValueChangedCallback(e =>
            {
                asset = e.newValue as DirectorAsset;
                RebuildAll();
            });
            toolbar.Add(assetField);

            toolbar.Add(new ToolbarButton(Save) { text = "保存" });
            toolbar.Add(new ToolbarButton(ShowIssues) { text = "校验" });
            toolbar.Add(new ToolbarButton(() =>
            {
                if (asset != null) DirectorSimulatorWindow.Open(asset);
            }) { text = "模拟" });
            rootVisualElement.Add(toolbar);

            rulesView = new ScrollView();
            rulesView.style.flexGrow = 1;
            rootVisualElement.Add(rulesView);

            issuesPanel = new VisualElement();
            issuesPanel.style.maxHeight = 140;
            issuesPanel.style.borderTopWidth = 1;
            issuesPanel.style.borderTopColor = new Color(0.1f, 0.1f, 0.1f);
            rootVisualElement.Add(issuesPanel);

            RebuildAll();
        }

        void Touch()
        {
            if (asset != null) EditorUtility.SetDirty(asset);
        }

        void Save()
        {
            if (asset == null) return;
            Touch();
            AssetDatabase.SaveAssets();
            ShowIssues();
        }

        // ---------------- 整体重建 ----------------

        void RebuildAll()
        {
            if (rulesView == null) return;
            rulesView.Clear();
            ruleCards.Clear();
            issuesPanel?.Clear();

            if (asset == null)
            {
                var hint = new Label("选择或创建一个「导演编排」资产（Create → 玩法 → 导演编排）");
                hint.style.marginTop = 12;
                hint.style.unityTextAlign = TextAnchor.MiddleCenter;
                hint.style.color = new Color(0.6f, 0.6f, 0.6f);
                rulesView.Add(hint);
                return;
            }

            // 引用配置
            var refsBox = new VisualElement();
            refsBox.style.flexDirection = FlexDirection.Row;
            refsBox.style.marginTop = 4;
            refsBox.style.marginBottom = 2;

            var boardField = new ObjectField("黑板")
            {
                objectType = typeof(BlackboardAsset),
                allowSceneObjects = false,
                value = asset.blackboard
            };
            boardField.style.flexGrow = 1;
            boardField.RegisterValueChangedCallback(e =>
            {
                asset.blackboard = e.newValue as BlackboardAsset;
                Touch();
                RebuildAll(); // 变量下拉变化
            });
            refsBox.Add(boardField);

            var questField = new ObjectField("任务图")
            {
                objectType = typeof(QuestGraphAsset),
                allowSceneObjects = false,
                value = asset.questGraph
            };
            questField.style.flexGrow = 1;
            questField.RegisterValueChangedCallback(e =>
            {
                asset.questGraph = e.newValue as QuestGraphAsset;
                Touch();
                RebuildAll(); // 任务下拉变化
            });
            refsBox.Add(questField);
            rulesView.Add(refsBox);

            foreach (var rule in asset.rules)
            {
                var card = BuildRuleCard(rule);
                ruleCards.Add(card);
                rulesView.Add(card);
            }

            var addButton = new Button(() =>
            {
                asset.rules.Add(new DirectorRule
                {
                    id = Guid.NewGuid().ToString("N"),
                    title = $"规则 {asset.rules.Count + 1}"
                });
                Touch();
                RebuildAll();
            }) { text = "＋ 添加规则" };
            addButton.style.marginTop = 6;
            addButton.style.marginBottom = 10;
            rulesView.Add(addButton);
        }

        // ---------------- 规则卡片 ----------------

        VisualElement BuildRuleCard(DirectorRule rule)
        {
            var card = new VisualElement();
            card.style.marginTop = 6;
            card.style.marginLeft = 4;
            card.style.marginRight = 4;
            card.style.paddingLeft = 6;
            card.style.paddingRight = 4;
            card.style.paddingBottom = 6;
            card.style.borderLeftWidth = 3;
            card.style.borderLeftColor = rule.enabled
                ? new Color(0.35f, 0.7f, 0.45f)
                : new Color(0.5f, 0.5f, 0.5f);
            card.style.backgroundColor = new Color(0f, 0f, 0f, 0.12f);

            // 头部
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;

            var enabledToggle = new Toggle { value = rule.enabled, tooltip = "停用后规则不参与触发" };
            enabledToggle.RegisterValueChangedCallback(e =>
            {
                rule.enabled = e.newValue;
                Touch();
                card.style.borderLeftColor = e.newValue
                    ? new Color(0.35f, 0.7f, 0.45f)
                    : new Color(0.5f, 0.5f, 0.5f);
            });
            header.Add(enabledToggle);

            var titleField = new TextField { value = rule.title ?? "" };
            titleField.style.flexGrow = 1;
            titleField.RegisterValueChangedCallback(e =>
            {
                rule.title = e.newValue;
                Touch();
            });
            header.Add(titleField);

            var onceToggle = new Toggle("仅一次") { value = rule.once, tooltip = "只触发一次（计入存档）；关闭后每次满足都触发" };
            onceToggle.RegisterValueChangedCallback(e =>
            {
                rule.once = e.newValue;
                Touch();
            });
            header.Add(onceToggle);

            header.Add(new Button(() => MoveRule(rule, -1)) { text = "↑", tooltip = "上移" });
            header.Add(new Button(() => MoveRule(rule, +1)) { text = "↓", tooltip = "下移" });
            header.Add(new Button(() =>
            {
                asset.rules.Remove(rule);
                Touch();
                RebuildAll();
            }) { text = "✕", tooltip = "删除规则" });
            card.Add(header);

            // 触发器
            var triggerBox = new VisualElement();
            card.Add(triggerBox);
            BuildTriggerSection(rule, triggerBox);

            // 附加条件
            card.Add(new GameConditionView(rule.condition, () => asset.blackboard, "附加条件（黑板）"));

            // 动作
            card.Add(BuildActionsSection(rule));
            return card;
        }

        void MoveRule(DirectorRule rule, int delta)
        {
            int from = asset.rules.IndexOf(rule);
            int to = from + delta;
            if (from < 0 || to < 0 || to >= asset.rules.Count) return;
            (asset.rules[from], asset.rules[to]) = (asset.rules[to], asset.rules[from]);
            Touch();
            RebuildAll();
        }

        // ---------------- 触发器编辑 ----------------

        void BuildTriggerSection(DirectorRule rule, VisualElement container)
        {
            container.Clear();

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 4;

            var label = new Label("当");
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.minWidth = 24;
            row.Add(label);

            int currentIndex = Mathf.Max(0, Array.FindIndex(TriggerTypes,
                t => rule.trigger != null && t.type == rule.trigger.GetType()));
            var typeField = new DropdownField(TriggerTypes.Select(t => t.label).ToList(), currentIndex);
            typeField.RegisterValueChangedCallback(_ =>
            {
                int picked = Mathf.Max(0, typeField.index);
                if (rule.trigger == null || rule.trigger.GetType() != TriggerTypes[picked].type)
                {
                    rule.trigger = TriggerTypes[picked].create();
                    Touch();
                    BuildTriggerSection(rule, container);
                }
            });
            row.Add(typeField);
            container.Add(row);

            switch (rule.trigger)
            {
                case GameEventTrigger e:
                {
                    AddText(container, "事件名", e.eventName, v => e.eventName = v);
                    var paramField = AddText(container, "参数", e.param, v => e.param = v);
                    paramField.tooltip = "留空 = 任意参数都触发";
                    break;
                }
                case VariableTrigger _:
                {
                    var hint = new Label("下方「附加条件」从不满足变为满足的瞬间触发；开局即满足也会触发。");
                    hint.style.color = new Color(0.6f, 0.6f, 0.6f);
                    hint.style.whiteSpace = WhiteSpace.Normal;
                    container.Add(hint);
                    break;
                }
                case QuestStateTrigger q:
                {
                    AddQuestIdField(container, q.questId, v => q.questId = v);
                    var phaseField = new DropdownField("阶段", PhaseNames,
                        Mathf.Clamp((int)q.phase, 0, PhaseNames.Count - 1));
                    TightLabel(phaseField);
                    phaseField.RegisterValueChangedCallback(_ =>
                    {
                        q.phase = (QuestTriggerPhase)Mathf.Max(0, phaseField.index);
                        Touch();
                    });
                    container.Add(phaseField);
                    break;
                }
                case DialogueEndedTrigger d:
                {
                    var field = new ObjectField("对话图")
                    {
                        objectType = typeof(DialogueGraphAsset),
                        allowSceneObjects = false,
                        value = d.dialogue,
                        tooltip = "留空 = 任意对话结束都触发"
                    };
                    TightLabel(field);
                    field.RegisterValueChangedCallback(e =>
                    {
                        d.dialogue = e.newValue as DialogueGraphAsset;
                        Touch();
                    });
                    container.Add(field);
                    break;
                }
            }
        }

        // ---------------- 动作编辑 ----------------

        VisualElement BuildActionsSection(DirectorRule rule)
        {
            var section = new VisualElement();

            var header = new Label("就做");
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginTop = 4;
            section.Add(header);

            var rowsContainer = new VisualElement();
            section.Add(rowsContainer);

            foreach (var action in rule.actions)
                rowsContainer.Add(BuildActionRow(rule, action, rowsContainer));

            var addMenu = new ToolbarMenu { text = "＋ 添加动作" };
            foreach (var actionType in ActionTypes)
            {
                var create = actionType.create;
                addMenu.menu.AppendAction(actionType.label, _ =>
                {
                    var action = create();
                    rule.actions.Add(action);
                    Touch();
                    rowsContainer.Add(BuildActionRow(rule, action, rowsContainer));
                });
            }
            section.Add(addMenu);
            return section;
        }

        VisualElement BuildActionRow(DirectorRule rule, DirectorAction action, VisualElement rowsContainer)
        {
            var box = new VisualElement();
            box.style.marginTop = 3;
            box.style.paddingLeft = 4;
            box.style.borderLeftWidth = 2;
            box.style.borderLeftColor = new Color(0.85f, 0.6f, 0.3f);

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;

            var title = new Label(GetActionLabel(action));
            title.style.flexGrow = 1;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.Add(title);

            header.Add(new Button(() => MoveAction(rule, action, box, rowsContainer, -1)) { text = "↑", tooltip = "上移" });
            header.Add(new Button(() => MoveAction(rule, action, box, rowsContainer, +1)) { text = "↓", tooltip = "下移" });
            header.Add(new Button(() =>
            {
                rule.actions.Remove(action);
                Touch();
                box.RemoveFromHierarchy();
            }) { text = "✕", tooltip = "删除动作" });
            box.Add(header);

            var fields = new VisualElement();
            box.Add(fields);
            BuildActionFields(action, fields);
            return box;
        }

        void MoveAction(DirectorRule rule, DirectorAction action, VisualElement box,
            VisualElement rowsContainer, int delta)
        {
            int from = rule.actions.IndexOf(action);
            int to = from + delta;
            if (from < 0 || to < 0 || to >= rule.actions.Count) return;
            (rule.actions[from], rule.actions[to]) = (rule.actions[to], rule.actions[from]);
            Touch();
            box.RemoveFromHierarchy();
            rowsContainer.Insert(to, box);
        }

        static string GetActionLabel(DirectorAction action)
        {
            switch (action)
            {
                case StartDialogueAction _: return "开始对话";
                case SetVariableAction _: return "修改变量";
                case RaiseEventAction _: return "抛游戏事件";
                case AcceptQuestAction _: return "接取任务";
                case CompleteQuestAction _: return "完成任务";
                case ReportProgressAction _: return "上报任务进度";
                default: return action != null ? action.GetType().Name : "(空动作)";
            }
        }

        void BuildActionFields(DirectorAction action, VisualElement container)
        {
            container.Clear();
            switch (action)
            {
                case StartDialogueAction d:
                {
                    var field = new ObjectField("对话图")
                    {
                        objectType = typeof(DialogueGraphAsset),
                        allowSceneObjects = false,
                        value = d.dialogue
                    };
                    TightLabel(field);
                    field.RegisterValueChangedCallback(e =>
                    {
                        d.dialogue = e.newValue as DialogueGraphAsset;
                        Touch();
                    });
                    container.Add(field);
                    break;
                }
                case SetVariableAction v:
                {
                    BuildSetVariableFields(v, container);
                    break;
                }
                case RaiseEventAction r:
                {
                    AddText(container, "事件名", r.eventName, value => r.eventName = value);
                    AddText(container, "参数", r.param, value => r.param = value);
                    break;
                }
                case AcceptQuestAction a:
                {
                    AddQuestIdField(container, a.questId, value => a.questId = value);
                    break;
                }
                case CompleteQuestAction c:
                {
                    AddQuestIdField(container, c.questId, value => c.questId = value);
                    break;
                }
                case ReportProgressAction p:
                {
                    var typeField = new DropdownField("目标类型", QuestObjectiveTypes.All,
                        Mathf.Max(0, QuestObjectiveTypes.All.IndexOf(p.objectiveType)));
                    TightLabel(typeField);
                    typeField.RegisterValueChangedCallback(e =>
                    {
                        p.objectiveType = e.newValue;
                        Touch();
                    });
                    container.Add(typeField);

                    var targetField = AddText(container, "目标ID", p.targetId, value => p.targetId = value);
                    targetField.tooltip = "与任务目标的对象 ID 匹配，留空 = 匹配未填对象的目标";

                    var amountField = new IntegerField("数量") { value = Mathf.Max(1, p.amount) };
                    TightLabel(amountField);
                    amountField.RegisterValueChangedCallback(e =>
                    {
                        p.amount = Mathf.Max(1, e.newValue);
                        Touch();
                    });
                    container.Add(amountField);
                    break;
                }
            }
        }

        void BuildSetVariableFields(SetVariableAction v, VisualElement container)
        {
            container.Clear();

            var board = asset != null ? asset.blackboard : null;
            if (board != null)
            {
                var names = board.GetNames();
                if (!string.IsNullOrEmpty(v.variableName) && !names.Contains(v.variableName))
                    names.Insert(0, v.variableName); // 已不存在的旧引用，保留显示，由校验报错

                if (names.Count == 0)
                {
                    var hint = new Label("黑板中没有变量");
                    hint.style.color = new Color(1f, 0.6f, 0.4f);
                    container.Add(hint);
                    return;
                }

                int index = Mathf.Max(0, names.IndexOf(v.variableName));
                v.variableName = names[index];
                var def = board.Find(v.variableName);
                if (def != null) v.variableType = def.type;

                var variableField = new DropdownField("变量", names, index);
                TightLabel(variableField);
                variableField.RegisterValueChangedCallback(e =>
                {
                    v.variableName = e.newValue;
                    var d = asset.blackboard != null ? asset.blackboard.Find(e.newValue) : null;
                    if (d != null) v.variableType = d.type;
                    Touch();
                    BuildSetVariableFields(v, container); // 类型可能变化
                });
                container.Add(variableField);
            }
            else
            {
                var nameField = AddText(container, "变量", v.variableName, value => v.variableName = value);
                nameField.tooltip = "变量名（未配置黑板，需手填）";
            }

            bool numeric = v.variableType == BlackboardVariableType.Int ||
                           v.variableType == BlackboardVariableType.Float;
            if (numeric)
            {
                var opField = new DropdownField("方式", SetOpNames, Mathf.Clamp((int)v.op, 0, SetOpNames.Count - 1));
                TightLabel(opField);
                opField.RegisterValueChangedCallback(_ =>
                {
                    v.op = (SetVariableOp)Mathf.Max(0, opField.index);
                    Touch();
                });
                container.Add(opField);
            }
            else
            {
                v.op = SetVariableOp.Set;
            }

            switch (v.variableType)
            {
                case BlackboardVariableType.Bool:
                    var toggle = new Toggle("值") { value = v.boolValue };
                    TightLabel(toggle);
                    toggle.RegisterValueChangedCallback(e => { v.boolValue = e.newValue; Touch(); });
                    container.Add(toggle);
                    break;

                case BlackboardVariableType.Int:
                    var intField = new IntegerField("值") { value = v.intValue };
                    TightLabel(intField);
                    intField.RegisterValueChangedCallback(e => { v.intValue = e.newValue; Touch(); });
                    container.Add(intField);
                    break;

                case BlackboardVariableType.Float:
                    var floatField = new FloatField("值") { value = v.floatValue };
                    TightLabel(floatField);
                    floatField.RegisterValueChangedCallback(e => { v.floatValue = e.newValue; Touch(); });
                    container.Add(floatField);
                    break;

                default:
                    var textField = new TextField("值") { value = v.stringValue ?? "" };
                    TightLabel(textField);
                    textField.RegisterValueChangedCallback(e => { v.stringValue = e.newValue; Touch(); });
                    container.Add(textField);
                    break;
            }
        }

        // ---------------- 校验面板 ----------------

        void ShowIssues()
        {
            if (issuesPanel == null) return;
            issuesPanel.Clear();
            if (asset == null) return;

            var issues = DirectorValidator.Validate(asset);
            if (issues.Count == 0)
            {
                var ok = new Label("✓ 校验通过");
                ok.style.color = new Color(0.5f, 0.9f, 0.5f);
                ok.style.marginLeft = 6;
                ok.style.marginTop = 3;
                issuesPanel.Add(ok);
                return;
            }

            var scroll = new ScrollView();
            scroll.style.maxHeight = 134;
            foreach (var issue in issues)
            {
                var row = new Label((issue.isError ? "✕ " : "△ ") + issue.message);
                row.style.color = issue.isError
                    ? new Color(1f, 0.5f, 0.45f)
                    : new Color(1f, 0.8f, 0.4f);
                row.style.marginLeft = 6;

                int ruleIndex = issue.ruleIndex;
                if (ruleIndex >= 0)
                {
                    row.RegisterCallback<MouseDownEvent>(_ =>
                    {
                        if (ruleIndex < ruleCards.Count)
                            rulesView.ScrollTo(ruleCards[ruleIndex]);
                    });
                }
                scroll.Add(row);
            }
            issuesPanel.Add(scroll);
        }

        // ---------------- 字段辅助 ----------------

        static void TightLabel(VisualElement field)
        {
            var label = field.Q<Label>(className: "unity-base-field__label");
            if (label != null) label.style.minWidth = 56;
        }

        TextField AddText(VisualElement parent, string label, string value, Action<string> onChange)
        {
            var field = new TextField(label) { value = value ?? "" };
            TightLabel(field);
            field.RegisterValueChangedCallback(e =>
            {
                onChange(e.newValue);
                Touch();
            });
            parent.Add(field);
            return field;
        }

        /// <summary>任务 ID 字段：配了任务图时下拉选择，否则手填。</summary>
        void AddQuestIdField(VisualElement parent, string current, Action<string> onChange)
        {
            var questIds = DirectorValidator.CollectQuestIds(asset != null ? asset.questGraph : null);
            if (questIds != null && questIds.Count > 0)
            {
                var names = questIds.OrderBy(id => id).ToList();
                if (!string.IsNullOrEmpty(current) && !names.Contains(current))
                    names.Insert(0, current); // 已不存在的旧引用，保留显示，由校验报错

                int index = Mathf.Max(0, names.IndexOf(current));
                onChange(names[index]);

                var field = new DropdownField("任务", names, index);
                TightLabel(field);
                field.RegisterValueChangedCallback(e =>
                {
                    onChange(e.newValue);
                    Touch();
                });
                parent.Add(field);
            }
            else
            {
                var field = AddText(parent, "任务ID", current, onChange);
                field.tooltip = "未配置任务图，需手填任务 ID";
            }
        }
    }
}
