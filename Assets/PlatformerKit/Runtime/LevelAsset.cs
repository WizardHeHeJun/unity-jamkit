using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlatformerKit
{
    /// <summary>一格图块：网格坐标（左下角为原点，Y 向上）+ 图块 ID。</summary>
    [Serializable]
    public class TileCell
    {
        public int x;
        public int y;
        public string tileId;
    }

    /// <summary>一个摆放的实体：稳定 ID（存档 / 游戏侧引用）+ 网格坐标 + 实体定义 ID。</summary>
    [Serializable]
    public class EntityInstance
    {
        [Tooltip("稳定 ID，摆放时生成。收集品存档、敌人标识等按它引用")]
        public string id;

        public int x;
        public int y;
        public string entityId;
    }

    /// <summary>
    /// 关卡资产：网格尺寸 + 图块层 + 实体层（稀疏存储）。
    /// 双击在关卡编辑器中绘制；运行时交给 <see cref="LevelBuilder"/> 搭建。
    /// </summary>
    [CreateAssetMenu(fileName = "NewLevel", menuName = "关卡/关卡 (Level)")]
    public class LevelAsset : ScriptableObject
    {
        [Tooltip("图块与实体的调色板")]
        public LevelTileSet tileSet;

        [Min(1)] public int width = 40;
        [Min(1)] public int height = 12;

        [Tooltip("一格对应的世界单位")]
        public float cellSize = 1f;

        public List<TileCell> tiles = new List<TileCell>();
        public List<EntityInstance> entities = new List<EntityInstance>();

        public bool InBounds(int x, int y) => x >= 0 && x < width && y >= 0 && y < height;

        public TileCell GetTile(int x, int y) =>
            tiles.Find(t => t != null && t.x == x && t.y == y);

        public EntityInstance GetEntity(int x, int y) =>
            entities.Find(e => e != null && e.x == x && e.y == y);

        /// <summary>格子中心的本地坐标（LevelBuilder 加上自身位置后即世界坐标）。</summary>
        public Vector3 CellToLocal(int x, int y) =>
            new Vector3((x + 0.5f) * cellSize, (y + 0.5f) * cellSize, 0f);

        /// <summary>删掉网格外的图块与实体（编辑器缩小网格时调用）。</summary>
        public void PruneOutOfBounds()
        {
            tiles.RemoveAll(t => t == null || !InBounds(t.x, t.y));
            entities.RemoveAll(e => e == null || !InBounds(e.x, e.y));
        }
    }
}
