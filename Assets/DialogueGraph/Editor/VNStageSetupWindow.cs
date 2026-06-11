using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DialogueGraph.Editor
{
    /// <summary>
    /// 舞台布景窗口（给美术）：不进 Play Mode 预览背景与立绘摆位。
    /// 画布上拖拽槽位写入舞台布局资产；选中槽位后可微调布局与角色立绘校准；
    /// 「生成演出指令」把当前布景放入剪贴板，节点演出指令区「粘贴布景」即可使用。
    /// </summary>
    public class VNStageSetupWindow : EditorWindow
    {
        const float RightPanelWidth = 280f;
        const float CanvasAspect = 16f / 9f;
        static readonly string[] SlotNames = { "左", "中", "右" };

        [SerializeField] StageLayoutAsset layout;
        [SerializeField] Sprite background;
        [SerializeField] CharacterAsset[] slotCharacters = new CharacterAsset[3];
        [SerializeField] string[] slotExpressions = new string[3];

        int selectedSlot = -1;
        int draggingSlot = -1;
        Vector2 panelScroll;

        [MenuItem("Tools/剧情工具/舞台布景")]
        public static void OpenWindow()
        {
            var window = GetWindow<VNStageSetupWindow>("舞台布景");
            window.minSize = new Vector2(760f, 420f);
        }

        void OnGUI()
        {
            DrawToolbar();

            EditorGUILayout.BeginHorizontal();
            var canvasArea = GUILayoutUtility.GetRect(0f, 0f,
                GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            DrawCanvas(canvasArea);
            DrawRightPanel();
            EditorGUILayout.EndHorizontal();
        }

        // ---------------- 工具栏 ----------------

        void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            var newLayout = (StageLayoutAsset)EditorGUILayout.ObjectField(
                layout, typeof(StageLayoutAsset), false, GUILayout.Width(220f));
            if (newLayout != layout)
            {
                layout = newLayout;
                selectedSlot = -1;
            }

            if (GUILayout.Button("新建布局", EditorStyles.toolbarButton, GUILayout.Width(64f)))
                CreateLayoutAsset();

            GUILayout.FlexibleSpace();

            using (new EditorGUI.DisabledScope(layout == null))
            {
                if (GUILayout.Button("应用到场景", EditorStyles.toolbarButton, GUILayout.Width(80f)))
                    ApplyToScene();
            }
            if (GUILayout.Button("生成演出指令", EditorStyles.toolbarButton, GUILayout.Width(90f)))
                ExportCommands();

            EditorGUILayout.EndHorizontal();
        }

        void CreateLayoutAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "新建舞台布局", "StageLayout", "asset", "选择布局资产的保存位置");
            if (string.IsNullOrEmpty(path)) return;

            var asset = CreateInstance<StageLayoutAsset>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            layout = asset;
            selectedSlot = -1;
        }

        void ApplyToScene()
        {
            var stage = FindObjectOfType<VNStage>();
            if (stage == null)
            {
                EditorUtility.DisplayDialog("场景中没有 VNStage",
                    "请先打开包含舞台的场景（或用 Tools → 剧情工具 → 创建VN演示场景）。", "好");
                return;
            }

            Undo.RecordObject(stage, "应用舞台布局");
            stage.layout = layout;
            for (int i = 0; i < stage.characterSlots.Length; i++)
            {
                var image = stage.characterSlots[i];
                var slot = layout.GetSlot(i);
                if (image == null || slot == null) continue;
                Undo.RecordObject(image.rectTransform, "应用舞台布局");
                slot.ApplyTo(image.rectTransform);
            }
            EditorUtility.SetDirty(stage);
        }

        void ExportCommands()
        {
            var commands = new List<StageCommand>();
            if (background != null)
            {
                commands.Add(new SetBackgroundCommand
                {
                    background = background,
                    transition = StageTransition.Fade,
                    duration = 0.5f
                });
            }
            for (int i = 0; i < slotCharacters.Length; i++)
            {
                if (slotCharacters[i] == null) continue;
                commands.Add(new ShowCharacterCommand
                {
                    character = slotCharacters[i],
                    expression = slotExpressions[i],
                    slot = (StageSlot)i,
                    transition = StageTransition.Fade,
                    duration = 0.3f
                });
            }

            if (commands.Count == 0)
            {
                EditorUtility.DisplayDialog("没有可生成的内容", "请先在右侧面板设置背景或立绘。", "好");
                return;
            }

            StageCommandClipboard.Set(commands);
            EditorUtility.DisplayDialog("已生成演出指令",
                $"共 {commands.Count} 条。打开对话图，在对话 / 演出节点的演出指令区点「粘贴布景」即可。", "好");
        }

        // ---------------- 预览画布 ----------------

        void DrawCanvas(Rect area)
        {
            EditorGUI.DrawRect(area, new Color(0.16f, 0.16f, 0.16f));
            var canvas = FitCanvas(area);
            EditorGUI.DrawRect(canvas, new Color(0.09f, 0.09f, 0.12f));

            if (background != null)
                DrawSprite(canvas, background);

            if (layout == null)
            {
                var hint = new Rect(area.x, area.center.y - 10f, area.width, 20f);
                GUI.Label(hint, "请在左上角指定或「新建布局」舞台布局资产", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            for (int i = 0; i < SlotNames.Length; i++)
            {
                var slotRect = GetSlotRect(canvas, i);
                var character = slotCharacters[i];
                var sprite = character != null ? character.GetExpression(GetExpression(i)) : null;
                if (sprite != null)
                    DrawSpriteFit(GetCharacterRect(canvas, i), sprite);

                var outlineColor = i == selectedSlot
                    ? new Color(0.30f, 0.70f, 1f)
                    : new Color(1f, 1f, 1f, 0.25f);
                DrawRectOutline(slotRect, outlineColor);

                var label = new Rect(slotRect.x, slotRect.yMax - 18f, slotRect.width, 18f);
                GUI.Label(label, SlotNames[i], EditorStyles.centeredGreyMiniLabel);
            }

            HandleCanvasMouse(canvas);
        }

        void HandleCanvasMouse(Rect canvas)
        {
            var e = Event.current;
            switch (e.type)
            {
                case EventType.MouseDown:
                    if (!canvas.Contains(e.mousePosition)) break;
                    draggingSlot = -1;
                    for (int i = SlotNames.Length - 1; i >= 0; i--)
                    {
                        if (!GetSlotRect(canvas, i).Contains(e.mousePosition)) continue;
                        selectedSlot = i;
                        draggingSlot = i;
                        Undo.RecordObject(layout, "移动立绘槽位");
                        e.Use();
                        Repaint();
                        break;
                    }
                    break;

                case EventType.MouseDrag:
                    if (draggingSlot < 0) break;
                    var slot = layout.GetSlot(draggingSlot);
                    if (slot != null)
                    {
                        slot.anchor.x = Mathf.Clamp01(slot.anchor.x + e.delta.x / canvas.width);
                        slot.anchor.y = Mathf.Clamp01(slot.anchor.y - e.delta.y / canvas.height);
                        EditorUtility.SetDirty(layout);
                    }
                    e.Use();
                    Repaint();
                    break;

                case EventType.MouseUp:
                    draggingSlot = -1;
                    break;
            }
        }

        /// <summary>槽位在画布上的矩形（布局锚点 / 尺寸 / 缩放，底边中心对齐锚点）。</summary>
        Rect GetSlotRect(Rect canvas, int index)
        {
            var slot = layout.GetSlot(index);
            if (slot == null) return new Rect();

            float k = canvas.width / layout.referenceResolution.x;
            float w = slot.size.x * k * slot.scale;
            float h = slot.size.y * k * slot.scale;
            float centerX = canvas.x + slot.anchor.x * canvas.width;
            float bottom = canvas.yMax - slot.anchor.y * canvas.height;
            return new Rect(centerX - w / 2f, bottom - h, w, h);
        }

        /// <summary>在槽位矩形上叠加角色校准（缩放绕底边中心、像素偏移），与运行时 VNStage 一致。</summary>
        Rect GetCharacterRect(Rect canvas, int index)
        {
            var slotRect = GetSlotRect(canvas, index);
            var character = slotCharacters[index];
            if (character == null) return slotRect;

            float k = canvas.width / layout.referenceResolution.x;
            float w = slotRect.width * character.spriteScale;
            float h = slotRect.height * character.spriteScale;
            float centerX = slotRect.center.x + character.spriteOffset.x * k;
            float bottom = slotRect.yMax - character.spriteOffset.y * k;
            return new Rect(centerX - w / 2f, bottom - h, w, h);
        }

        string GetExpression(int index)
        {
            return index < slotExpressions.Length ? slotExpressions[index] : null;
        }

        // ---------------- 右侧面板 ----------------

        void DrawRightPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(RightPanelWidth));
            panelScroll = EditorGUILayout.BeginScrollView(panelScroll);

            EditorGUILayout.LabelField("布景", EditorStyles.boldLabel);
            background = (Sprite)EditorGUILayout.ObjectField("背景", background, typeof(Sprite), false);

            for (int i = 0; i < SlotNames.Length; i++)
                DrawSlotSection(i);

            if (layout != null && selectedSlot >= 0)
            {
                DrawSlotLayoutFields(selectedSlot);
                DrawCharacterCalibration(selectedSlot);
            }
            else
            {
                EditorGUILayout.Space(10f);
                EditorGUILayout.HelpBox("在画布上点击槽位可编辑布局与立绘校准，拖拽可移动位置。", MessageType.Info);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        void DrawSlotSection(int index)
        {
            EditorGUILayout.Space(6f);
            string title = SlotNames[index] + "槽位" + (index == selectedSlot ? "（选中）" : "");
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

            slotCharacters[index] = (CharacterAsset)EditorGUILayout.ObjectField(
                "角色", slotCharacters[index], typeof(CharacterAsset), false);

            var character = slotCharacters[index];
            if (character == null) return;

            var names = character.GetExpressionNames();
            if (names.Count == 0) return;
            int current = Mathf.Max(0, names.IndexOf(slotExpressions[index] ?? string.Empty));
            int picked = EditorGUILayout.Popup("表情", current, names.ToArray());
            slotExpressions[index] = names[Mathf.Clamp(picked, 0, names.Count - 1)];
        }

        void DrawSlotLayoutFields(int index)
        {
            var slot = layout.GetSlot(index);
            if (slot == null) return;

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField($"槽位布局 — {SlotNames[index]}", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            var anchor = EditorGUILayout.Vector2Field("锚点 (0-1)", slot.anchor);
            var size = EditorGUILayout.Vector2Field("尺寸 (px)", slot.size);
            float scale = EditorGUILayout.FloatField("缩放", slot.scale);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(layout, "编辑槽位布局");
                slot.anchor = new Vector2(Mathf.Clamp01(anchor.x), Mathf.Clamp01(anchor.y));
                slot.size = size;
                slot.scale = Mathf.Max(0.01f, scale);
                EditorUtility.SetDirty(layout);
            }
        }

        void DrawCharacterCalibration(int index)
        {
            var character = slotCharacters[index];
            if (character == null) return;

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField($"立绘校准 — {character.name}", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            float scale = EditorGUILayout.FloatField("缩放", character.spriteScale);
            var offset = EditorGUILayout.Vector2Field("偏移 (px)", character.spriteOffset);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(character, "校准立绘");
                character.spriteScale = Mathf.Max(0.01f, scale);
                character.spriteOffset = offset;
                EditorUtility.SetDirty(character);
            }
            EditorGUILayout.HelpBox("校准保存在角色资产上，对所有用到该角色的剧情生效。", MessageType.None);
        }

        // ---------------- 绘制辅助 ----------------

        static Rect FitCanvas(Rect area)
        {
            const float margin = 8f;
            area = new Rect(area.x + margin, area.y + margin,
                area.width - margin * 2f, area.height - margin * 2f);
            float w = area.width;
            float h = w / CanvasAspect;
            if (h > area.height)
            {
                h = area.height;
                w = h * CanvasAspect;
            }
            return new Rect(area.x + (area.width - w) / 2f, area.y + (area.height - h) / 2f, w, h);
        }

        static void DrawSprite(Rect rect, Sprite sprite)
        {
            var texture = sprite.texture;
            if (texture == null) return;
            var tr = sprite.textureRect;
            var coords = new Rect(tr.x / texture.width, tr.y / texture.height,
                tr.width / texture.width, tr.height / texture.height);
            GUI.DrawTextureWithTexCoords(rect, texture, coords, true);
        }

        /// <summary>保持宽高比居中绘制（与运行时 Image.preserveAspect 一致）。</summary>
        static void DrawSpriteFit(Rect rect, Sprite sprite)
        {
            if (rect.width <= 0f || rect.height <= 0f) return;
            float spriteAspect = sprite.rect.width / sprite.rect.height;
            float rectAspect = rect.width / rect.height;
            Rect fitted;
            if (spriteAspect > rectAspect)
            {
                float h = rect.width / spriteAspect;
                fitted = new Rect(rect.x, rect.y + (rect.height - h) / 2f, rect.width, h);
            }
            else
            {
                float w = rect.height * spriteAspect;
                fitted = new Rect(rect.x + (rect.width - w) / 2f, rect.y, w, rect.height);
            }
            DrawSprite(fitted, sprite);
        }

        static void DrawRectOutline(Rect rect, Color color)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), color);
        }
    }
}
