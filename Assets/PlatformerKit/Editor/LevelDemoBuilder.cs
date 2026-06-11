using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PlatformerKit.Editor
{
    /// <summary>
    /// 一键生成可玩的平台跳跃示例：占位图块集 + 示例关卡 + 接好引用的场景
    /// （搭建器 / 玩家 / 跟随相机），点 Play 即可游玩（A/D 移动，空格跳）。
    /// </summary>
    public static class LevelDemoBuilder
    {
        const string Folder = "Assets/PlatformerKitDemo";

        [MenuItem("Tools/关卡工具/创建平台跳跃示例")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets", "PlatformerKitDemo");

            var tileSet = CreateTileSet();
            var level = CreateLevel(tileSet);
            AssetDatabase.SaveAssets();

            CreateScene(level);

            Selection.activeObject = level;
            EditorGUIUtility.PingObject(level);
            LevelEditorWindow.Open(level);

            Debug.Log("平台跳跃示例已生成：Assets/PlatformerKitDemo。点 Play 游玩（A/D 移动，空格跳，尖刺会回到出生点/检查点）；" +
                      "双击示例关卡可继续编辑，改完保存后重新 Play 生效。");
        }

        // ---------------- 图块集 ----------------

        static LevelTileSet CreateTileSet()
        {
            string path = $"{Folder}/示例图块集.asset";
            var existing = AssetDatabase.LoadAssetAtPath<LevelTileSet>(path);
            if (existing != null) return existing;

            var set = ScriptableObject.CreateInstance<LevelTileSet>();

            set.tiles.Add(new TileDefinition
            {
                id = "ground",
                displayName = "地面",
                sprite = CreateColorSprite("Tile_Ground", new Color(0.45f, 0.30f, 0.18f)),
                collision = TileCollisionType.Solid
            });
            set.tiles.Add(new TileDefinition
            {
                id = "brick",
                displayName = "砖块",
                sprite = CreateColorSprite("Tile_Brick", new Color(0.55f, 0.55f, 0.60f)),
                collision = TileCollisionType.Solid
            });
            set.tiles.Add(new TileDefinition
            {
                id = "platform",
                displayName = "单向平台",
                sprite = CreateColorSprite("Tile_Platform", new Color(0.85f, 0.70f, 0.30f)),
                collision = TileCollisionType.OneWay
            });
            set.tiles.Add(new TileDefinition
            {
                id = "spike",
                displayName = "尖刺",
                sprite = CreateColorSprite("Tile_Spike", new Color(0.85f, 0.25f, 0.25f)),
                collision = TileCollisionType.Hazard
            });
            set.tiles.Add(new TileDefinition
            {
                id = "bush",
                displayName = "灌木（装饰）",
                sprite = CreateColorSprite("Tile_Bush", new Color(0.30f, 0.60f, 0.30f)),
                collision = TileCollisionType.Decoration
            });

            set.entities.Add(new EntityDefinition
            {
                id = "start",
                displayName = "出生点",
                kind = EntityKind.PlayerStart,
                sprite = CreateColorSprite("Entity_Start", new Color(0.35f, 0.65f, 1f))
            });
            set.entities.Add(new EntityDefinition
            {
                id = "goal",
                displayName = "终点",
                kind = EntityKind.Goal,
                sprite = CreateColorSprite("Entity_Goal", new Color(0.30f, 0.85f, 0.45f))
            });
            set.entities.Add(new EntityDefinition
            {
                id = "coin",
                displayName = "金币",
                kind = EntityKind.Collectible,
                sprite = CreateColorSprite("Entity_Coin", new Color(1f, 0.85f, 0.25f))
            });
            set.entities.Add(new EntityDefinition
            {
                id = "checkpoint",
                displayName = "检查点",
                kind = EntityKind.Checkpoint,
                sprite = CreateColorSprite("Entity_Checkpoint", new Color(0.70f, 0.45f, 0.90f))
            });

            AssetDatabase.CreateAsset(set, path);
            return set;
        }

        // ---------------- 示例关卡 ----------------

        static LevelAsset CreateLevel(LevelTileSet tileSet)
        {
            string path = $"{Folder}/示例关卡.asset";
            var existing = AssetDatabase.LoadAssetAtPath<LevelAsset>(path);
            if (existing != null) return existing;

            var level = ScriptableObject.CreateInstance<LevelAsset>();
            level.tileSet = tileSet;
            level.width = 44;
            level.height = 14;

            // 地面两行，挖两个尖刺坑
            for (int x = 0; x < level.width; x++)
            {
                bool pit = (x >= 14 && x <= 16) || (x >= 28 && x <= 30);
                if (pit)
                {
                    Tile(level, x, 0, "spike");
                }
                else
                {
                    Tile(level, x, 0, "ground");
                    Tile(level, x, 1, "ground");
                }
            }

            // 单向平台（跳跃路线）
            Platform(level, 8, 10, 4);
            Platform(level, 12, 13, 6);
            Platform(level, 18, 20, 5);
            Platform(level, 24, 26, 7);
            Platform(level, 32, 34, 4);

            // 终点前的砖块台阶
            Tile(level, 38, 2, "brick");
            Tile(level, 39, 2, "brick");
            Tile(level, 39, 3, "brick");
            Tile(level, 40, 2, "brick");
            Tile(level, 40, 3, "brick");
            Tile(level, 40, 4, "brick");

            // 装饰
            Tile(level, 5, 2, "bush");
            Tile(level, 22, 2, "bush");
            Tile(level, 36, 2, "bush");

            // 实体
            Entity(level, 2, 2, "start");
            Entity(level, 42, 2, "goal");
            Entity(level, 9, 6, "coin");
            Entity(level, 12, 8, "coin");
            Entity(level, 19, 7, "coin");
            Entity(level, 25, 9, "coin");
            Entity(level, 33, 6, "coin");
            Entity(level, 21, 2, "checkpoint");

            AssetDatabase.CreateAsset(level, path);
            return level;
        }

        static void Tile(LevelAsset level, int x, int y, string tileId) =>
            level.tiles.Add(new TileCell { x = x, y = y, tileId = tileId });

        static void Platform(LevelAsset level, int fromX, int toX, int y)
        {
            for (int x = fromX; x <= toX; x++)
                Tile(level, x, y, "platform");
        }

        static void Entity(LevelAsset level, int x, int y, string entityId) =>
            level.entities.Add(new EntityInstance
            {
                id = Guid.NewGuid().ToString("N"),
                x = x,
                y = y,
                entityId = entityId
            });

        // ---------------- 场景 ----------------

        static void CreateScene(LevelAsset level)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var camera = Camera.main;
            if (camera != null)
            {
                camera.orthographic = true;
                camera.orthographicSize = 6f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.16f, 0.20f, 0.32f);
            }

            // 玩家
            var player = new GameObject("Player");
            var renderer = player.AddComponent<SpriteRenderer>();
            renderer.sprite = CreateColorSprite("Player", new Color(0.30f, 0.85f, 0.85f));
            renderer.sortingOrder = 10;
            player.transform.localScale = new Vector3(0.85f, 0.95f, 1f);
            player.AddComponent<SimplePlatformerController>(); // RequireComponent 自动补 Rigidbody2D + BoxCollider2D

            // 搭建器
            var builderGO = new GameObject("LevelBuilder");
            var builder = builderGO.AddComponent<LevelBuilder>();
            builder.level = level;
            builder.player = player.transform;

            // 相机跟随
            if (camera != null)
            {
                var follow = camera.gameObject.AddComponent<SimpleCameraFollow>();
                follow.target = player.transform;
            }

            EditorSceneManager.SaveScene(scene, $"{Folder}/PlatformerDemoScene.unity");
        }

        // ---------------- 占位 Sprite ----------------

        static Sprite CreateColorSprite(string spriteName, Color color)
        {
            string path = $"{Folder}/{spriteName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null) return existing;

            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
            texture.SetPixels(pixels);
            texture.Apply();
            texture.name = spriteName;
            AssetDatabase.CreateAsset(texture, path);

            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            sprite.name = spriteName + "_Sprite";
            AssetDatabase.AddObjectToAsset(sprite, texture);
            return sprite;
        }
    }
}
