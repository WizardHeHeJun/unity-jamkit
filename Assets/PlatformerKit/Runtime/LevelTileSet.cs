using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlatformerKit
{
    public enum TileCollisionType
    {
        /// <summary>实心：四面碰撞（地面 / 墙壁）。</summary>
        Solid,

        /// <summary>单向平台：只挡从上往下，可从下方跳穿。</summary>
        OneWay,

        /// <summary>危险：触碰即死（尖刺 / 岩浆），无实体碰撞。</summary>
        Hazard,

        /// <summary>装饰：只有图像，无碰撞。</summary>
        Decoration
    }

    /// <summary>一种图块的定义：编辑器调色板与运行时搭建共用。</summary>
    [Serializable]
    public class TileDefinition
    {
        [Tooltip("图块 ID，关卡数据按它引用，发布后不要改")]
        public string id;

        [Tooltip("调色板上显示的名字，如「地面」「尖刺」")]
        public string displayName;

        public Sprite sprite;

        [Tooltip("没配 Sprite 时编辑器画布与运行时用此颜色显示")]
        public Color editorColor = Color.white;

        public TileCollisionType collision = TileCollisionType.Solid;
    }

    public enum EntityKind
    {
        /// <summary>玩家出生点，每关一个。</summary>
        PlayerStart,

        /// <summary>终点。</summary>
        Goal,

        /// <summary>收集品（金币等）。</summary>
        Collectible,

        /// <summary>检查点：碰到后重生点更新到这里。</summary>
        Checkpoint,

        /// <summary>敌人出生点：运行时只抛事件，由游戏侧生成。</summary>
        EnemySpawn,

        /// <summary>自定义：运行时只抛事件。</summary>
        Custom
    }

    /// <summary>一种实体的定义（出生点 / 终点 / 收集品 / 敌人……）。</summary>
    [Serializable]
    public class EntityDefinition
    {
        [Tooltip("实体 ID，关卡数据按它引用，发布后不要改")]
        public string id;

        [Tooltip("调色板上显示的名字，如「金币」「史莱姆」")]
        public string displayName;

        public EntityKind kind = EntityKind.Collectible;

        public Sprite sprite;

        [Tooltip("没配 Sprite 时编辑器画布与运行时用此颜色显示")]
        public Color editorColor = Color.yellow;

        [Tooltip("可选：运行时直接实例化此预制体（出生点 / 终点等内置行为仍生效）")]
        public GameObject prefab;
    }

    /// <summary>
    /// 图块集：关卡编辑器的调色板。美术在这里登记图块与实体（ID + 图 + 碰撞类型），
    /// 策划画关卡时从调色板选择，多个关卡可共用同一图块集。
    /// </summary>
    [CreateAssetMenu(fileName = "NewTileSet", menuName = "关卡/图块集 (Tile Set)")]
    public class LevelTileSet : ScriptableObject
    {
        public List<TileDefinition> tiles = new List<TileDefinition>();
        public List<EntityDefinition> entities = new List<EntityDefinition>();

        public TileDefinition FindTile(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return tiles.Find(t => t != null && t.id == id);
        }

        public EntityDefinition FindEntity(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return entities.Find(e => e != null && e.id == id);
        }
    }
}
