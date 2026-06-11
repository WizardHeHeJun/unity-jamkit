using System;
using System.Collections.Generic;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DialogueGraph.Editor
{
    /// <summary>
    /// 演出指令列表编辑控件，嵌入对话节点与演出节点。
    /// 直接读写传入的 List&lt;StageCommand&gt;（保存时随节点数据序列化）。
    /// 新增指令类型：在 CommandTypes 注册 + BuildFields 加分支。
    /// </summary>
    public class StageCommandListView : VisualElement
    {
        static readonly (string label, Func<StageCommand> create)[] CommandTypes =
        {
            ("显示立绘", () => (StageCommand)new ShowCharacterCommand()),
            ("隐藏立绘", () => (StageCommand)new HideCharacterCommand()),
            ("切换背景", () => (StageCommand)new SetBackgroundCommand()),
            ("播放BGM", () => (StageCommand)new PlayBgmCommand()),
            ("停止BGM", () => (StageCommand)new StopBgmCommand()),
            ("播放音效", () => (StageCommand)new PlaySfxCommand()),
            ("等待", () => (StageCommand)new WaitCommand())
        };

        // 顺序与 StageSlot / StageTransition 枚举一一对应
        static readonly List<string> SlotNames = new List<string> { "左", "中", "右" };
        static readonly List<string> TransitionNames = new List<string> { "瞬切", "淡入淡出" };

        readonly List<StageCommand> commands;
        readonly VisualElement rowsContainer;

        public StageCommandListView(List<StageCommand> commands)
        {
            this.commands = commands;

            var header = new Label("演出指令");
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginTop = 4;
            Add(header);

            rowsContainer = new VisualElement();
            Add(rowsContainer);

            var buttonsRow = new VisualElement();
            buttonsRow.style.flexDirection = FlexDirection.Row;

            var addMenu = new ToolbarMenu { text = "＋ 添加指令" };
            foreach (var commandType in CommandTypes)
            {
                var create = commandType.create;
                addMenu.menu.AppendAction(commandType.label, _ =>
                {
                    var command = create();
                    commands.Add(command);
                    rowsContainer.Add(BuildRow(command));
                });
            }
            buttonsRow.Add(addMenu);

            buttonsRow.Add(new Button(() =>
            {
                foreach (var command in StageCommandClipboard.CloneAll())
                {
                    commands.Add(command);
                    rowsContainer.Add(BuildRow(command));
                }
            })
            {
                text = "粘贴布景",
                tooltip = "粘贴「舞台布景」窗口（Tools → 剧情工具 → 舞台布景）生成的演出指令"
            });

            Add(buttonsRow);

            foreach (var command in commands)
                rowsContainer.Add(BuildRow(command));
        }

        VisualElement BuildRow(StageCommand command)
        {
            var box = new VisualElement();
            box.style.marginBottom = 3;
            box.style.paddingLeft = 4;
            box.style.borderLeftWidth = 2;
            box.style.borderLeftColor = new Color(0.35f, 0.55f, 0.85f);

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;

            var title = new Label(GetCommandLabel(command));
            title.style.flexGrow = 1;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.Add(title);

            header.Add(new Button(() => MoveCommand(command, box, -1)) { text = "↑", tooltip = "上移" });
            header.Add(new Button(() => MoveCommand(command, box, +1)) { text = "↓", tooltip = "下移" });
            header.Add(new Button(() =>
            {
                commands.Remove(command);
                box.RemoveFromHierarchy();
            }) { text = "✕", tooltip = "删除指令" });

            box.Add(header);
            BuildFields(command, box);
            return box;
        }

        void MoveCommand(StageCommand command, VisualElement box, int delta)
        {
            int from = commands.IndexOf(command);
            int to = from + delta;
            if (from < 0 || to < 0 || to >= commands.Count) return;

            (commands[from], commands[to]) = (commands[to], commands[from]);
            box.RemoveFromHierarchy();
            rowsContainer.Insert(to, box);
        }

        static string GetCommandLabel(StageCommand command)
        {
            switch (command)
            {
                case ShowCharacterCommand _: return "显示立绘";
                case HideCharacterCommand _: return "隐藏立绘";
                case SetBackgroundCommand _: return "切换背景";
                case PlayBgmCommand _: return "播放BGM";
                case StopBgmCommand _: return "停止BGM";
                case PlaySfxCommand _: return "播放音效";
                case WaitCommand _: return "等待";
                default: return command != null ? command.GetType().Name : "(空指令)";
            }
        }

        static void BuildFields(StageCommand command, VisualElement box)
        {
            switch (command)
            {
                case ShowCharacterCommand show:
                {
                    DropdownField expressionField = null;
                    AddObject<CharacterAsset>(box, "角色", show.character, v =>
                    {
                        show.character = v;
                        expressionField.choices = v != null ? v.GetExpressionNames() : new List<string>();
                    });
                    expressionField = AddExpressionDropdown(box, show.character, show.expression,
                        v => show.expression = v);
                    AddIndexDropdown(box, "站位", SlotNames, (int)show.slot, i => show.slot = (StageSlot)i);
                    AddIndexDropdown(box, "过渡", TransitionNames, (int)show.transition,
                        i => show.transition = (StageTransition)i);
                    AddFloat(box, "时长", show.duration, v => show.duration = v);
                    break;
                }
                case HideCharacterCommand hide:
                {
                    var field = AddObject<CharacterAsset>(box, "角色", hide.character, v => hide.character = v);
                    field.tooltip = "留空 = 清空全部立绘";
                    AddIndexDropdown(box, "过渡", TransitionNames, (int)hide.transition,
                        i => hide.transition = (StageTransition)i);
                    AddFloat(box, "时长", hide.duration, v => hide.duration = v);
                    break;
                }
                case SetBackgroundCommand bg:
                {
                    AddObject<Sprite>(box, "背景图", bg.background, v => bg.background = v);
                    AddIndexDropdown(box, "过渡", TransitionNames, (int)bg.transition,
                        i => bg.transition = (StageTransition)i);
                    AddFloat(box, "时长", bg.duration, v => bg.duration = v);
                    break;
                }
                case PlayBgmCommand bgm:
                {
                    AddObject<AudioClip>(box, "音乐", bgm.clip, v => bgm.clip = v);
                    AddFloat(box, "淡入秒", bgm.fadeSeconds, v => bgm.fadeSeconds = v);
                    var loopToggle = new Toggle("循环") { value = bgm.loop };
                    TightLabel(loopToggle);
                    loopToggle.RegisterValueChangedCallback(e => bgm.loop = e.newValue);
                    box.Add(loopToggle);
                    break;
                }
                case StopBgmCommand stop:
                {
                    AddFloat(box, "淡出秒", stop.fadeSeconds, v => stop.fadeSeconds = v);
                    break;
                }
                case PlaySfxCommand sfx:
                {
                    AddObject<AudioClip>(box, "音效", sfx.clip, v => sfx.clip = v);
                    AddFloat(box, "音量", sfx.volume, v => sfx.volume = Mathf.Clamp01(v));
                    break;
                }
                case WaitCommand wait:
                {
                    AddFloat(box, "秒数", wait.seconds, v => wait.seconds = v);
                    break;
                }
            }
        }

        // ---------------- 字段构建辅助 ----------------

        static void TightLabel(VisualElement field)
        {
            var label = field.Q<Label>(className: "unity-base-field__label");
            if (label != null) label.style.minWidth = 48;
        }

        static ObjectField AddObject<T>(VisualElement parent, string label, UnityEngine.Object value,
            Action<T> onChange) where T : UnityEngine.Object
        {
            var field = new ObjectField(label)
            {
                objectType = typeof(T),
                allowSceneObjects = false,
                value = value
            };
            TightLabel(field);
            field.RegisterValueChangedCallback(e => onChange(e.newValue as T));
            parent.Add(field);
            return field;
        }

        static void AddFloat(VisualElement parent, string label, float value, Action<float> onChange)
        {
            var field = new FloatField(label) { value = value };
            TightLabel(field);
            field.RegisterValueChangedCallback(e => onChange(e.newValue));
            parent.Add(field);
        }

        static void AddIndexDropdown(VisualElement parent, string label, List<string> names, int index,
            Action<int> onChange)
        {
            var field = new DropdownField(label, names, Mathf.Clamp(index, 0, names.Count - 1));
            TightLabel(field);
            field.RegisterValueChangedCallback(_ => onChange(Mathf.Max(0, field.index)));
            parent.Add(field);
        }

        static DropdownField AddExpressionDropdown(VisualElement parent, CharacterAsset character,
            string current, Action<string> onChange)
        {
            var field = new DropdownField("表情")
            {
                value = current ?? string.Empty,
                choices = character != null ? character.GetExpressionNames() : new List<string>()
            };
            TightLabel(field);
            field.RegisterValueChangedCallback(e => onChange(e.newValue));
            parent.Add(field);
            return field;
        }
    }
}
