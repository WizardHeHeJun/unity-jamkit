using System.Collections.Generic;

namespace PlatformerKit.Editor
{
    /// <summary>关卡静态校验。保存时自动执行，问题显示在编辑器底部。</summary>
    public static class LevelValidator
    {
        public class Issue
        {
            public string message;
            public bool isError = true;
        }

        public static List<Issue> Validate(LevelAsset level)
        {
            var issues = new List<Issue>();
            if (level == null) return issues;

            if (level.tileSet == null)
            {
                Error(issues, "未配置图块集（tileSet），画布无法使用");
                return issues;
            }

            ValidateTileSet(issues, level.tileSet);
            ValidateTiles(issues, level);
            ValidateEntities(issues, level);
            return issues;
        }

        static void ValidateTileSet(List<Issue> issues, LevelTileSet set)
        {
            var seen = new HashSet<string>();
            foreach (var tile in set.tiles)
            {
                if (tile == null) continue;
                if (string.IsNullOrEmpty(tile.id))
                    Error(issues, $"图块集：图块「{tile.displayName}」没有 ID");
                else if (!seen.Add("t:" + tile.id))
                    Error(issues, $"图块集：图块 ID「{tile.id}」重复");
            }
            foreach (var entity in set.entities)
            {
                if (entity == null) continue;
                if (string.IsNullOrEmpty(entity.id))
                    Error(issues, $"图块集：实体「{entity.displayName}」没有 ID");
                else if (!seen.Add("e:" + entity.id))
                    Error(issues, $"图块集：实体 ID「{entity.id}」重复");
            }
        }

        static void ValidateTiles(List<Issue> issues, LevelAsset level)
        {
            var occupied = new HashSet<(int, int)>();
            foreach (var cell in level.tiles)
            {
                if (cell == null) continue;
                if (!level.InBounds(cell.x, cell.y))
                {
                    Error(issues, $"图块 ({cell.x},{cell.y}) 在网格之外（保存时可在编辑器里缩放网格自动清理）");
                    continue;
                }
                if (!occupied.Add((cell.x, cell.y)))
                    Error(issues, $"格子 ({cell.x},{cell.y}) 上有重叠图块");
                if (level.tileSet.FindTile(cell.tileId) == null)
                    Error(issues, $"图块 ({cell.x},{cell.y}) 引用的图块 ID「{cell.tileId}」不在图块集中");
            }
        }

        static void ValidateEntities(List<Issue> issues, LevelAsset level)
        {
            int playerStarts = 0;
            int goals = 0;
            var occupied = new HashSet<(int, int)>();

            foreach (var instance in level.entities)
            {
                if (instance == null) continue;
                if (!level.InBounds(instance.x, instance.y))
                {
                    Error(issues, $"实体 ({instance.x},{instance.y}) 在网格之外");
                    continue;
                }
                if (!occupied.Add((instance.x, instance.y)))
                    Error(issues, $"格子 ({instance.x},{instance.y}) 上有重叠实体");
                if (string.IsNullOrEmpty(instance.id))
                    Error(issues, $"实体 ({instance.x},{instance.y}) 缺少稳定 ID（请删除后重新摆放）");

                var def = level.tileSet.FindEntity(instance.entityId);
                if (def == null)
                {
                    Error(issues, $"实体 ({instance.x},{instance.y}) 引用的实体 ID「{instance.entityId}」不在图块集中");
                    continue;
                }

                if (def.kind == EntityKind.PlayerStart) playerStarts++;
                if (def.kind == EntityKind.Goal) goals++;

                var tile = level.GetTile(instance.x, instance.y);
                var tileDef = tile != null ? level.tileSet.FindTile(tile.tileId) : null;
                if (tileDef != null && tileDef.collision == TileCollisionType.Solid)
                    Warn(issues, $"实体「{def.displayName}」({instance.x},{instance.y}) 被实心图块埋住");
            }

            if (playerStarts == 0)
                Error(issues, "没有摆放玩家出生点");
            else if (playerStarts > 1)
                Error(issues, $"有 {playerStarts} 个玩家出生点（应只有一个）");

            if (goals == 0)
                Warn(issues, "没有摆放终点");
        }

        static void Error(List<Issue> issues, string message) =>
            issues.Add(new Issue { message = message, isError = true });

        static void Warn(List<Issue> issues, string message) =>
            issues.Add(new Issue { message = message, isError = false });
    }
}
