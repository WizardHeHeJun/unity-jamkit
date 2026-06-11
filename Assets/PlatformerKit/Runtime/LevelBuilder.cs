using System;
using UnityEngine;

namespace PlatformerKit
{
    /// <summary>
    /// 关卡搭建器：把关卡资产生成为场景物体——
    /// 实心 / 单向平台 / 危险的碰撞体、收集品 / 终点 / 检查点的触发器，
    /// 出生点与敌人点位通过事件交给游戏侧。
    /// </summary>
    public class LevelBuilder : MonoBehaviour
    {
        public LevelAsset level;
        public bool buildOnAwake = true;

        [Tooltip("可选：搭建完成后把该物体移动到出生点（拖玩家进来即可）")]
        public Transform player;

        /// <summary>出生点世界坐标（搭建后有效；没摆出生点时为自身位置）。</summary>
        public Vector3 PlayerSpawnPosition { get; private set; }

        /// <summary>每个实体生成时通知：(定义, 摆放数据, 生成的物体——纯事件型实体为 null)。</summary>
        public event Action<EntityDefinition, EntityInstance, GameObject> OnEntitySpawned;

        Transform root;

        void Awake()
        {
            if (buildOnAwake) Build();
        }

        public void Build()
        {
            Clear();
            PlayerSpawnPosition = transform.position;
            if (level == null || level.tileSet == null)
            {
                Debug.LogWarning("LevelBuilder：未配置关卡资产或图块集。");
                return;
            }

            root = new GameObject("Level").transform;
            root.SetParent(transform, false);

            foreach (var cell in level.tiles)
            {
                var def = level.tileSet.FindTile(cell?.tileId);
                if (def == null || !level.InBounds(cell.x, cell.y)) continue;
                BuildTile(cell, def);
            }

            foreach (var instance in level.entities)
            {
                var def = level.tileSet.FindEntity(instance?.entityId);
                if (def == null || !level.InBounds(instance.x, instance.y)) continue;
                BuildEntity(instance, def);
            }

            if (player != null)
                player.position = PlayerSpawnPosition;
        }

        public void Clear()
        {
            if (root == null)
            {
                var existing = transform.Find("Level");
                root = existing != null ? existing : null;
            }
            if (root == null) return;

            if (Application.isPlaying) Destroy(root.gameObject);
            else DestroyImmediate(root.gameObject);
            root = null;
        }

        public Vector3 CellToWorld(int x, int y) => transform.position + level.CellToLocal(x, y);

        // ---------------- 图块 ----------------

        void BuildTile(TileCell cell, TileDefinition def)
        {
            var go = CreateCellObject($"Tile_{def.id}_{cell.x}_{cell.y}", cell.x, cell.y, def.sprite, def.editorColor);

            float size = level.cellSize;
            switch (def.collision)
            {
                case TileCollisionType.Solid:
                {
                    var box = go.AddComponent<BoxCollider2D>();
                    box.size = new Vector2(size, size);
                    break;
                }
                case TileCollisionType.OneWay:
                {
                    var box = go.AddComponent<BoxCollider2D>();
                    box.size = new Vector2(size, size * 0.5f);
                    box.offset = new Vector2(0f, size * 0.25f);
                    box.usedByEffector = true;
                    go.AddComponent<PlatformEffector2D>(); // 默认单向（只挡从上方）
                    break;
                }
                case TileCollisionType.Hazard:
                {
                    var box = go.AddComponent<BoxCollider2D>();
                    box.size = new Vector2(size * 0.8f, size * 0.8f);
                    box.isTrigger = true;
                    go.AddComponent<LevelHazard>();
                    break;
                }
                // Decoration：只有图像
            }
        }

        // ---------------- 实体 ----------------

        void BuildEntity(EntityInstance instance, EntityDefinition def)
        {
            var position = CellToWorld(instance.x, instance.y);

            if (def.kind == EntityKind.PlayerStart)
            {
                PlayerSpawnPosition = position;
                OnEntitySpawned?.Invoke(def, instance, null);
                return;
            }

            if (def.prefab != null)
            {
                var prefabInstance = Instantiate(def.prefab, position, Quaternion.identity, root);
                AttachMarker(prefabInstance, instance, def);
                OnEntitySpawned?.Invoke(def, instance, prefabInstance);
                return;
            }

            // 纯事件型实体：交给游戏侧生成
            if (def.kind == EntityKind.EnemySpawn || def.kind == EntityKind.Custom)
            {
                OnEntitySpawned?.Invoke(def, instance, null);
                return;
            }

            var go = CreateCellObject($"Entity_{def.id}_{instance.x}_{instance.y}",
                instance.x, instance.y, def.sprite, def.editorColor);
            var trigger = go.AddComponent<BoxCollider2D>();
            trigger.size = new Vector2(level.cellSize * 0.8f, level.cellSize * 0.8f);
            trigger.isTrigger = true;
            AttachMarker(go, instance, def);
            OnEntitySpawned?.Invoke(def, instance, go);
        }

        static void AttachMarker(GameObject go, EntityInstance instance, EntityDefinition def)
        {
            switch (def.kind)
            {
                case EntityKind.Goal:
                    if (go.GetComponent<LevelGoal>() == null) go.AddComponent<LevelGoal>();
                    break;

                case EntityKind.Checkpoint:
                    if (go.GetComponent<LevelCheckpoint>() == null) go.AddComponent<LevelCheckpoint>();
                    break;

                case EntityKind.Collectible:
                    var collectible = go.GetComponent<LevelCollectible>();
                    if (collectible == null) collectible = go.AddComponent<LevelCollectible>();
                    collectible.entityId = def.id;
                    collectible.instanceId = instance.id;
                    break;
            }
        }

        // ---------------- 公共 ----------------

        GameObject CreateCellObject(string objectName, int x, int y, Sprite sprite, Color fallbackColor)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(root, false);
            go.transform.localPosition = level.CellToLocal(x, y);

            var renderer = go.AddComponent<SpriteRenderer>();
            if (sprite != null)
            {
                renderer.sprite = sprite;
                // 把任意尺寸的图缩放到一格大小
                var bounds = sprite.bounds.size;
                if (bounds.x > 0f && bounds.y > 0f)
                    go.transform.localScale = new Vector3(level.cellSize / bounds.x, level.cellSize / bounds.y, 1f);
            }
            else
            {
                renderer.sprite = FallbackSprite;
                renderer.color = fallbackColor;
                go.transform.localScale = Vector3.one * level.cellSize;
            }
            return go;
        }

        static Sprite fallbackSprite;

        /// <summary>没配图时用的 1×1 白色方块（运行时生成，所有占位共用）。</summary>
        static Sprite FallbackSprite
        {
            get
            {
                if (fallbackSprite == null)
                {
                    var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                    var pixels = new Color[16];
                    for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
                    texture.SetPixels(pixels);
                    texture.Apply();
                    fallbackSprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
                    fallbackSprite.name = "PlatformerKit_Fallback";
                }
                return fallbackSprite;
            }
        }
    }
}
