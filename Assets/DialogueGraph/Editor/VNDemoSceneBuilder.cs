using System;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace DialogueGraph.Editor
{
    /// <summary>
    /// 一键生成 VN 演示场景：占位立绘/背景、两个角色、覆盖全节点类型的演示图、
    /// 中文动态 TMP 字体（微软雅黑），以及接好全部引用的舞台 + UI 层级。
    /// </summary>
    public static class VNDemoSceneBuilder
    {
        const string Folder = "Assets/DialogueGraphDemo";

        // 演示文案集中定义，同时用于给动态字体预烘焙字形
        const string TxtLine1 = "你来啦！今天想聊点什么？";
        const string TxtLine2 = "我也在哦，别只顾着和她说话。";
        const string TxtPrompt = "选个话题吧。";
        const string TxtOptA = "聊聊天气";
        const string TxtOptB = "谈谈未来";
        const string TxtLineA = "今天天气真好，适合出去走走呢。";
        const string TxtSubLine1 = "未来的事，谁知道呢。";
        const string TxtSubLine2 = "但我相信，一定会越来越好的。";
        const string TxtTrue = "对了……谢谢你一直陪着我。";
        const string TxtFalse = "……她似乎还想说什么，但最终没有开口。（提示：好感度≥10 时走另一分支）";

        [MenuItem("Tools/剧情工具/创建VN演示场景")]
        public static void CreateDemoScene()
        {
            if (TMP_Settings.instance == null)
            {
                EditorUtility.DisplayDialog("缺少 TextMeshPro 资源",
                    "请先执行 Window → TextMeshPro → Import TMP Essential Resources，然后重试。", "好");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets", "DialogueGraphDemo");

            // ---- 占位资源 ----
            var bgDay = CreateColorSprite("BG_Day", new Color(0.55f, 0.75f, 0.95f), 480, 270);
            var bgNight = CreateColorSprite("BG_Night", new Color(0.10f, 0.12f, 0.30f), 480, 270);
            var aliceNormal = CreateColorSprite("Alice_Normal", new Color(0.95f, 0.60f, 0.70f), 200, 400);
            var aliceHappy = CreateColorSprite("Alice_Happy", new Color(1.00f, 0.75f, 0.82f), 200, 400);
            var kaiNormal = CreateColorSprite("Kai_Normal", new Color(0.40f, 0.55f, 0.85f), 200, 400);
            var kaiHappy = CreateColorSprite("Kai_Happy", new Color(0.55f, 0.70f, 0.95f), 200, 400);

            var alice = CreateCharacter("alice", "艾莉丝", new Color(0.95f, 0.55f, 0.65f), aliceNormal, aliceHappy);
            var kai = CreateCharacter("kai", "凯", new Color(0.45f, 0.60f, 0.95f), kaiNormal, kaiHappy);

            var subGraph = CreateSubGraph(alice, kai);
            var mainGraph = CreateMainGraph(alice, kai, bgDay, bgNight, subGraph);

            var font = CreateChineseFontAsset();
            AssetDatabase.SaveAssets();

            // ---- 场景 ----
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var camera = Camera.main;
            if (camera != null)
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
            }

            if (UnityEngine.Object.FindObjectOfType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            var canvasGO = new GameObject("VNCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            // ---- 舞台 ----
            var stageGO = CreateUIObject("Stage", canvasGO.transform);
            Stretch((RectTransform)stageGO.transform);
            var stage = stageGO.AddComponent<VNStage>();

            stage.backgroundA = CreateStretchedImage("BackgroundA", stageGO.transform, new Color(1f, 1f, 1f, 0f));
            stage.backgroundB = CreateStretchedImage("BackgroundB", stageGO.transform, new Color(1f, 1f, 1f, 0f));

            stage.characterSlots = new[]
            {
                CreateCharacterSlot("Slot_Left", stageGO.transform, 0.22f),
                CreateCharacterSlot("Slot_Center", stageGO.transform, 0.5f),
                CreateCharacterSlot("Slot_Right", stageGO.transform, 0.78f)
            };
            stage.layout = CreateStageLayout();

            stage.bgmSourceA = AddAudioSource(stageGO, loop: true);
            stage.bgmSourceB = AddAudioSource(stageGO, loop: true);
            stage.sfxSource = AddAudioSource(stageGO, loop: false);
            stage.voiceSource = AddAudioSource(stageGO, loop: false);

            // ---- 对话 UI ----
            var ui = canvasGO.AddComponent<VNDialogueUI>();

            var dialoguePanel = CreateUIObject("DialoguePanel", canvasGO.transform);
            var panelRT = (RectTransform)dialoguePanel.transform;
            panelRT.anchorMin = new Vector2(0.05f, 0.02f);
            panelRT.anchorMax = new Vector2(0.95f, 0.28f);
            panelRT.offsetMin = panelRT.offsetMax = Vector2.zero;
            var panelImage = dialoguePanel.AddComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0.65f);
            panelImage.raycastTarget = false;

            var namePlate = CreateUIObject("NamePlate", dialoguePanel.transform);
            var nameRT = (RectTransform)namePlate.transform;
            nameRT.anchorMin = nameRT.anchorMax = new Vector2(0f, 1f);
            nameRT.pivot = new Vector2(0f, 0f);
            nameRT.anchoredPosition = new Vector2(40f, 0f);
            nameRT.sizeDelta = new Vector2(260f, 56f);
            var nameImage = namePlate.AddComponent<Image>();
            nameImage.color = new Color(0f, 0f, 0f, 0.8f);
            nameImage.raycastTarget = false;

            var nameText = CreateText("NameText", namePlate.transform, font, 30f, Color.white,
                TextAlignmentOptions.Center);
            Stretch(nameText.rectTransform);

            var bodyText = CreateText("BodyText", dialoguePanel.transform, font, 32f, Color.white,
                TextAlignmentOptions.TopLeft);
            Stretch(bodyText.rectTransform);
            bodyText.rectTransform.offsetMin = new Vector2(30f, 20f);
            bodyText.rectTransform.offsetMax = new Vector2(-30f, -20f);

            // 全屏透明点击层（在对话框之上、选项之下）
            var clickCatcher = CreateUIObject("ClickCatcher", canvasGO.transform);
            Stretch((RectTransform)clickCatcher.transform);
            var catcherImage = clickCatcher.AddComponent<Image>();
            catcherImage.color = Color.clear;
            var catcherButton = clickCatcher.AddComponent<Button>();
            catcherButton.transition = Selectable.Transition.None;

            // 选项面板
            var choicePanel = CreateUIObject("ChoicePanel", canvasGO.transform);
            var choiceRT = (RectTransform)choicePanel.transform;
            choiceRT.anchorMin = choiceRT.anchorMax = new Vector2(0.5f, 0.55f);
            choiceRT.pivot = new Vector2(0.5f, 0.5f);
            choiceRT.sizeDelta = new Vector2(700f, 100f);
            var choiceLayout = choicePanel.AddComponent<VerticalLayoutGroup>();
            choiceLayout.spacing = 14f;
            choiceLayout.childControlWidth = true;
            choiceLayout.childControlHeight = true;
            choiceLayout.childForceExpandWidth = true;
            choiceLayout.childForceExpandHeight = false;
            var fitter = choicePanel.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var template = CreateUIObject("ChoiceButtonTemplate", choicePanel.transform);
            var templateImage = template.AddComponent<Image>();
            templateImage.color = new Color(0.12f, 0.12f, 0.20f, 0.92f);
            var templateButton = template.AddComponent<Button>();
            template.AddComponent<LayoutElement>().preferredHeight = 64f;
            var templateLabel = CreateText("Label", template.transform, font, 28f, Color.white,
                TextAlignmentOptions.Center);
            Stretch(templateLabel.rectTransform);
            template.SetActive(false);
            choicePanel.SetActive(false);

            ui.dialoguePanel = dialoguePanel;
            ui.namePlate = namePlate;
            ui.nameText = nameText;
            ui.bodyText = bodyText;
            ui.choicePanel = choicePanel;
            ui.choiceButtonTemplate = templateButton;
            dialoguePanel.SetActive(false);

            // ---- 播放器 ----
            var playerGO = new GameObject("VNPlayer");
            var player = playerGO.AddComponent<VNPlayer>();
            player.graph = mainGraph;
            player.stage = stage;
            player.ui = ui;

            // Auto / Skip 按钮（右上角）
            var autoButton = CreateModeButton("AUTO", canvasGO.transform, font, new Vector2(-150f, -20f));
            var skipButton = CreateModeButton("SKIP", canvasGO.transform, font, new Vector2(-20f, -20f));

            UnityEventTools.AddPersistentListener(catcherButton.onClick, ui.NotifyAdvanceClicked);
            UnityEventTools.AddPersistentListener(autoButton.onClick, player.ToggleAuto);
            UnityEventTools.AddPersistentListener(skipButton.onClick, player.ToggleSkip);

            EditorSceneManager.SaveScene(scene, Folder + "/VNDemoScene.unity");
            Selection.activeObject = playerGO;

            EditorUtility.DisplayDialog("演示场景已创建",
                $"场景与演示资源已生成到 {Folder}。\n直接点 Play 运行；" +
                "条件分支可在剧情预览窗口（Tools → 剧情工具 → 剧情预览）里加变量「好感度」试跑。", "好");
        }

        // ---------------- 资产生成 ----------------

        static Sprite CreateColorSprite(string spriteName, Color color, int width, int height)
        {
            string path = $"{Folder}/{spriteName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null) return existing;

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
            texture.SetPixels(pixels);
            texture.Apply();
            texture.name = spriteName;
            AssetDatabase.CreateAsset(texture, path);

            var sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = spriteName + "_Sprite";
            AssetDatabase.AddObjectToAsset(sprite, texture);
            return sprite;
        }

        static StageLayoutAsset CreateStageLayout()
        {
            string path = $"{Folder}/StageLayout.asset";
            var existing = AssetDatabase.LoadAssetAtPath<StageLayoutAsset>(path);
            if (existing != null) return existing;

            // 默认槽位（左/中/右 0.22/0.5/0.78，350×700）与场景搭建值一致
            var layoutAsset = ScriptableObject.CreateInstance<StageLayoutAsset>();
            AssetDatabase.CreateAsset(layoutAsset, path);
            return layoutAsset;
        }

        static CharacterAsset CreateCharacter(string id, string displayName, Color nameColor,
            Sprite normal, Sprite happy)
        {
            string path = $"{Folder}/Char_{id}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<CharacterAsset>(path);
            if (existing != null) return existing;

            var character = ScriptableObject.CreateInstance<CharacterAsset>();
            character.id = id;
            character.displayName.fallbackText = displayName;
            character.nameColor = nameColor;
            character.expressions.Add(new CharacterAsset.Expression { name = "普通", sprite = normal });
            character.expressions.Add(new CharacterAsset.Expression { name = "开心", sprite = happy });
            AssetDatabase.CreateAsset(character, path);
            return character;
        }

        static DialogueGraphAsset CreateSubGraph(CharacterAsset alice, CharacterAsset kai)
        {
            string path = $"{Folder}/VNDemoSubGraph.asset";
            var existing = AssetDatabase.LoadAssetAtPath<DialogueGraphAsset>(path);
            if (existing != null) return existing;

            var graph = ScriptableObject.CreateInstance<DialogueGraphAsset>();

            var start = new StartNodeData { guid = NewGuid(), position = new Vector2(0, 200) };
            var line1 = MakeLine(kai, "普通", TxtSubLine1, new Vector2(240, 200));
            var line2 = MakeLine(alice, "开心", TxtSubLine2, new Vector2(560, 200));
            var end = new EndNodeData { guid = NewGuid(), position = new Vector2(880, 200) };

            start.nextGuid = line1.guid;
            line1.nextGuid = line2.guid;
            line2.nextGuid = end.guid;

            graph.nodes.AddRange(new BaseNodeData[] { start, line1, line2, end });
            AssetDatabase.CreateAsset(graph, path);
            return graph;
        }

        static DialogueGraphAsset CreateMainGraph(CharacterAsset alice, CharacterAsset kai,
            Sprite bgDay, Sprite bgNight, DialogueGraphAsset subGraph)
        {
            string path = $"{Folder}/VNDemoGraph.asset";
            var existing = AssetDatabase.LoadAssetAtPath<DialogueGraphAsset>(path);
            if (existing != null) return existing;

            var graph = ScriptableObject.CreateInstance<DialogueGraphAsset>();

            var start = new StartNodeData { guid = NewGuid(), position = new Vector2(0, 300) };

            var opening = new StageNodeData { guid = NewGuid(), position = new Vector2(220, 300) };
            opening.commands.Add(new SetBackgroundCommand
            {
                background = bgDay,
                transition = StageTransition.Fade,
                duration = 0.6f
            });
            opening.commands.Add(new ShowCharacterCommand
            {
                character = alice,
                expression = "普通",
                slot = StageSlot.Left,
                transition = StageTransition.Fade,
                duration = 0.4f
            });

            var line1 = MakeLine(alice, "普通", TxtLine1, new Vector2(560, 300));

            var line2 = MakeLine(kai, "普通", TxtLine2, new Vector2(900, 300));
            line2.commands.Add(new ShowCharacterCommand
            {
                character = kai,
                expression = "普通",
                slot = StageSlot.Right,
                transition = StageTransition.Fade,
                duration = 0.4f
            });

            var choice = new ChoiceNodeData
            {
                guid = NewGuid(),
                position = new Vector2(1240, 300),
                character = alice,
                expression = "普通"
            };
            choice.prompt.fallbackText = TxtPrompt;
            var optionA = new ChoiceOptionData { id = NewGuid() };
            optionA.text.fallbackText = TxtOptA;
            var optionB = new ChoiceOptionData { id = NewGuid() };
            optionB.text.fallbackText = TxtOptB;
            choice.options.Add(optionA);
            choice.options.Add(optionB);

            var lineA = MakeLine(alice, "开心", TxtLineA, new Vector2(1600, 140));
            var eventNode = new EventNodeData
            {
                guid = NewGuid(),
                position = new Vector2(1940, 140),
                eventName = "UnlockCG",
                stringParam = "cg_demo"
            };

            var subNode = new SubGraphNodeData
            {
                guid = NewGuid(),
                position = new Vector2(1600, 460),
                graph = subGraph
            };

            var condition = new ConditionNodeData { guid = NewGuid(), position = new Vector2(2280, 300) };
            condition.condition.variableName = "好感度";
            condition.condition.variableType = VariableType.Int;
            condition.condition.op = CompareOp.GreaterOrEqual;
            condition.condition.intValue = 10;

            var nightStage = new StageNodeData { guid = NewGuid(), position = new Vector2(2620, 140) };
            nightStage.commands.Add(new SetBackgroundCommand
            {
                background = bgNight,
                transition = StageTransition.Fade,
                duration = 0.8f
            });

            var lineTrue = MakeLine(alice, "开心", TxtTrue, new Vector2(2960, 140));
            var lineFalse = MakeLine(null, null, TxtFalse, new Vector2(2620, 460));
            var end = new EndNodeData { guid = NewGuid(), position = new Vector2(3300, 300) };

            start.nextGuid = opening.guid;
            opening.nextGuid = line1.guid;
            line1.nextGuid = line2.guid;
            line2.nextGuid = choice.guid;
            optionA.nextGuid = lineA.guid;
            optionB.nextGuid = subNode.guid;
            lineA.nextGuid = eventNode.guid;
            eventNode.nextGuid = condition.guid;
            subNode.nextGuid = condition.guid;
            condition.trueGuid = nightStage.guid;
            condition.falseGuid = lineFalse.guid;
            nightStage.nextGuid = lineTrue.guid;
            lineTrue.nextGuid = end.guid;
            lineFalse.nextGuid = end.guid;

            graph.nodes.AddRange(new BaseNodeData[]
            {
                start, opening, line1, line2, choice, lineA, eventNode, subNode,
                condition, nightStage, lineTrue, lineFalse, end
            });
            AssetDatabase.CreateAsset(graph, path);
            return graph;
        }

        static DialogueNodeData MakeLine(CharacterAsset character, string expression, string text,
            Vector2 position)
        {
            var node = new DialogueNodeData
            {
                guid = NewGuid(),
                position = position,
                character = character,
                expression = expression
            };
            node.line.fallbackText = text;
            return node;
        }

        static string NewGuid() => Guid.NewGuid().ToString("N");

        // ---------------- 字体 ----------------

        static TMP_FontAsset CreateChineseFontAsset()
        {
            string path = $"{Folder}/MSYH_Demo_SDF.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null) return existing;

            try
            {
                var osFont = Font.CreateDynamicFontFromOSFont("Microsoft YaHei", 32);
                if (osFont == null) return TMP_Settings.defaultFontAsset;

                var fontAsset = TMP_FontAsset.CreateFontAsset(osFont, 32, 4, GlyphRenderMode.SDFAA,
                    1024, 1024, AtlasPopulationMode.Dynamic, true);
                if (fontAsset == null) return TMP_Settings.defaultFontAsset;

                fontAsset.name = "MSYH_Demo_SDF";

                // 预烘焙演示文案用到的字形
                string allText = TxtLine1 + TxtLine2 + TxtPrompt + TxtOptA + TxtOptB + TxtLineA +
                                 TxtSubLine1 + TxtSubLine2 + TxtTrue + TxtFalse +
                                 "艾莉丝凯旁白AUTOSKIP0123456789";
                fontAsset.TryAddCharacters(allText);

                osFont.name = "MSYH_Demo_Source";
                AssetDatabase.CreateAsset(fontAsset, path);
                AssetDatabase.AddObjectToAsset(osFont, fontAsset);
                if (fontAsset.material != null)
                {
                    fontAsset.material.name = "MSYH_Demo_SDF Material";
                    AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
                }
                if (fontAsset.atlasTexture != null)
                {
                    fontAsset.atlasTexture.name = "MSYH_Demo_SDF Atlas";
                    AssetDatabase.AddObjectToAsset(fontAsset.atlasTexture, fontAsset);
                }
                return fontAsset;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[VNDemo] 创建中文字体失败，回退到 TMP 默认字体（中文可能显示为方块）：{e.Message}");
                return TMP_Settings.defaultFontAsset;
            }
        }

        // ---------------- UI 构建辅助 ----------------

        static GameObject CreateUIObject(string objectName, Transform parent)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static Image CreateStretchedImage(string objectName, Transform parent, Color color)
        {
            var go = CreateUIObject(objectName, parent);
            Stretch((RectTransform)go.transform);
            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static Image CreateCharacterSlot(string objectName, Transform parent, float anchorX)
        {
            var go = CreateUIObject(objectName, parent);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(anchorX, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(350f, 700f);
            var image = go.AddComponent<Image>();
            image.color = Color.white;
            image.raycastTarget = false;
            go.SetActive(false);
            return image;
        }

        static AudioSource AddAudioSource(GameObject go, bool loop)
        {
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            return source;
        }

        static TextMeshProUGUI CreateText(string objectName, Transform parent, TMP_FontAsset font,
            float size, Color color, TextAlignmentOptions alignment)
        {
            var go = CreateUIObject(objectName, parent);
            var text = go.AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            return text;
        }

        static Button CreateModeButton(string label, Transform parent, TMP_FontAsset font,
            Vector2 anchoredPosition)
        {
            var go = CreateUIObject("Btn_" + label, parent);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = new Vector2(110f, 48f);

            var image = go.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.5f);
            var button = go.AddComponent<Button>();

            var text = CreateText("Label", go.transform, font, 24f, Color.white, TextAlignmentOptions.Center);
            Stretch(text.rectTransform);
            text.text = label;
            return button;
        }
    }
}
