using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace PlatformerKit.Editor
{
    /// <summary>
    /// 平台跳跃关卡编辑器：左侧调色板（图块 / 实体），右侧网格画布。
    /// 左键绘制 / 摆放，右键擦除，矩形工具批量填充；「保存」落盘并校验。
    /// </summary>
    public class LevelEditorWindow : EditorWindow
    {
        enum Tool
        {
            Brush,
            Rect,
            Entity,
            Erase
        }

        const float PaletteWidth = 180f;

        [SerializeField] LevelAsset level;
        [SerializeField] int zoom = 24;

        Tool tool = Tool.Brush;
        string selectedTileId;
        string selectedEntityId;

        Vector2 paletteScroll;
        Vector2 canvasScroll;
        Vector2 issueScroll;
        List<LevelValidator.Issue> issues;

        bool stroking;            // 笔刷 / 橡皮连续绘制中
        bool rectDragging;
        Vector2Int rectStart;
        Vector2Int rectEnd;
        Vector2Int hoverCell = new Vector2Int(-1, -1);

        int pendingWidth = -1;
        int pendingHeight = -1;

        [MenuItem("Tools/关卡工具/关卡编辑器")]
        public static void OpenWindow()
        {
            var window = GetWindow<LevelEditorWindow>("关卡编辑器");
            window.minSize = new Vector2(780f, 460f);
        }

        public static void Open(LevelAsset levelAsset)
        {
            var window = GetWindow<LevelEditorWindow>("关卡编辑器");
            window.minSize = new Vector2(780f, 460f);
            window.level = levelAsset;
            window.pendingWidth = -1;
            window.pendingHeight = -1;
            window.issues = null;
        }

        [OnOpenAsset]
        public static bool OnOpenAsset(int instanceID, int line)
        {
            if (EditorUtility.InstanceIDToObject(instanceID) is LevelAsset levelAsset)
            {
                Open(levelAsset);
                return true;
            }
            return false;
        }

        LevelTileSet TileSet => level != null ? level.tileSet : null;

        void OnEnable()
        {
            wantsMouseMove = true;
        }

        void OnGUI()
        {
            DrawToolbar();

            if (level == null)
            {
                EditorGUILayout.HelpBox("选择或创建一个关卡资产（Create → 关卡 → 关卡），双击资产也可打开本窗口。",
                    MessageType.Info);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawPalette();
                DrawCanvas();
            }

            DrawStatusAndIssues();
        }

        // ---------------- 工具栏 ----------------

        void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                var newLevel = (LevelAsset)EditorGUILayout.ObjectField(level, typeof(LevelAsset), false,
                    GUILayout.Width(180f));
                if (newLevel != level)
                {
                    level = newLevel;
                    pendingWidth = pendingHeight = -1;
                    issues = null;
                }

                if (level != null)
                {
                    if (GUILayout.Button("保存", EditorStyles.toolbarButton, GUILayout.Width(44f)))
                        Save();
                    if (GUILayout.Button("校验", EditorStyles.toolbarButton, GUILayout.Width(44f)))
                        issues = LevelValidator.Validate(level);

                    GUILayout.Space(12f);
                    GUILayout.Label("网格", GUILayout.Width(30f));
                    if (pendingWidth < 0) pendingWidth = level.width;
                    if (pendingHeight < 0) pendingHeight = level.height;
                    pendingWidth = EditorGUILayout.IntField(pendingWidth, GUILayout.Width(44f));
                    GUILayout.Label("×", GUILayout.Width(12f));
                    pendingHeight = EditorGUILayout.IntField(pendingHeight, GUILayout.Width(44f));
                    if (GUILayout.Button("应用", EditorStyles.toolbarButton, GUILayout.Width(40f)))
                        ApplyGridSize();
                }

                GUILayout.FlexibleSpace();
                GUILayout.Label("缩放", GUILayout.Width(30f));
                zoom = (int)GUILayout.HorizontalSlider(zoom, 12f, 48f, GUILayout.Width(100f));
            }
        }

        void Save()
        {
            EditorUtility.SetDirty(level);
            AssetDatabase.SaveAssets();
            issues = LevelValidator.Validate(level);
        }

        void ApplyGridSize()
        {
            int width = Mathf.Clamp(pendingWidth, 1, 1000);
            int height = Mathf.Clamp(pendingHeight, 1, 1000);
            if (width == level.width && height == level.height) return;

            Undo.RecordObject(level, "调整关卡网格");
            level.width = width;
            level.height = height;
            level.PruneOutOfBounds();
            EditorUtility.SetDirty(level);
            pendingWidth = width;
            pendingHeight = height;
        }

        // ---------------- 调色板 ----------------

        void DrawPalette()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(PaletteWidth)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawToolButton(Tool.Brush, "笔刷");
                    DrawToolButton(Tool.Rect, "矩形");
                    DrawToolButton(Tool.Erase, "橡皮");
                }
                GUILayout.Label("右键 = 擦除任意格", EditorStyles.miniLabel);

                if (TileSet == null)
                {
                    EditorGUILayout.HelpBox("关卡未配置图块集：选中关卡资产，在 Inspector 里把图块集拖到 tileSet 字段。",
                        MessageType.Warning);
                    return;
                }

                paletteScroll = EditorGUILayout.BeginScrollView(paletteScroll);

                GUILayout.Label("图块", EditorStyles.boldLabel);
                foreach (var tile in TileSet.tiles)
                {
                    if (tile == null || string.IsNullOrEmpty(tile.id)) continue;
                    bool selected = (tool == Tool.Brush || tool == Tool.Rect) && selectedTileId == tile.id;
                    if (DrawPaletteEntry(tile.displayName, tile.sprite, tile.editorColor, selected))
                    {
                        selectedTileId = tile.id;
                        if (tool != Tool.Brush && tool != Tool.Rect) tool = Tool.Brush;
                    }
                }

                GUILayout.Space(6f);
                GUILayout.Label("实体", EditorStyles.boldLabel);
                foreach (var entity in TileSet.entities)
                {
                    if (entity == null || string.IsNullOrEmpty(entity.id)) continue;
                    bool selected = tool == Tool.Entity && selectedEntityId == entity.id;
                    if (DrawPaletteEntry(entity.displayName, entity.sprite, entity.editorColor, selected))
                    {
                        selectedEntityId = entity.id;
                        tool = Tool.Entity;
                    }
                }

                EditorGUILayout.EndScrollView();
            }
        }

        void DrawToolButton(Tool target, string label)
        {
            bool active = tool == target;
            bool pressed = GUILayout.Toggle(active, label, EditorStyles.miniButton) != active;
            if (pressed) tool = target;
        }

        static bool DrawPaletteEntry(string label, Sprite sprite, Color color, bool selected)
        {
            var rect = GUILayoutUtility.GetRect(0f, 24f, GUILayout.ExpandWidth(true));
            if (selected)
                EditorGUI.DrawRect(rect, new Color(0.25f, 0.45f, 0.7f, 0.6f));

            var swatch = new Rect(rect.x + 4f, rect.y + 3f, 18f, 18f);
            if (sprite != null) DrawSprite(swatch, sprite);
            else EditorGUI.DrawRect(swatch, color);

            GUI.Label(new Rect(swatch.xMax + 6f, rect.y + 4f, rect.width - 30f, 16f),
                string.IsNullOrEmpty(label) ? "(未命名)" : label);

            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition))
            {
                e.Use();
                return true;
            }
            return false;
        }

        // ---------------- 画布 ----------------

        void DrawCanvas()
        {
            float cp = zoom;
            float contentWidth = level.width * cp + 2f;
            float contentHeight = level.height * cp + 2f;

            canvasScroll = EditorGUILayout.BeginScrollView(canvasScroll,
                GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            var canvas = GUILayoutUtility.GetRect(contentWidth, contentHeight,
                GUILayout.Width(contentWidth), GUILayout.Height(contentHeight));

            EditorGUI.DrawRect(canvas, new Color(0.14f, 0.14f, 0.17f));

            // 网格线
            var gridColor = new Color(1f, 1f, 1f, 0.06f);
            for (int x = 0; x <= level.width; x++)
                EditorGUI.DrawRect(new Rect(canvas.x + x * cp, canvas.y, 1f, level.height * cp), gridColor);
            for (int y = 0; y <= level.height; y++)
                EditorGUI.DrawRect(new Rect(canvas.x, canvas.y + y * cp, level.width * cp, 1f), gridColor);

            // 图块
            if (TileSet != null)
            {
                foreach (var cell in level.tiles)
                {
                    if (cell == null || !level.InBounds(cell.x, cell.y)) continue;
                    var def = TileSet.FindTile(cell.tileId);
                    var rect = CellRect(canvas, cell.x, cell.y, cp);
                    if (def == null)
                    {
                        EditorGUI.DrawRect(rect, new Color(1f, 0f, 1f, 0.8f)); // 未知图块：洋红示警
                        continue;
                    }
                    if (def.sprite != null) DrawSprite(rect, def.sprite);
                    else EditorGUI.DrawRect(rect, def.editorColor);
                }

                // 实体（白框区分图块）
                foreach (var instance in level.entities)
                {
                    if (instance == null || !level.InBounds(instance.x, instance.y)) continue;
                    var def = TileSet.FindEntity(instance.entityId);
                    var rect = Shrink(CellRect(canvas, instance.x, instance.y, cp), cp * 0.12f);
                    if (def == null)
                    {
                        EditorGUI.DrawRect(rect, new Color(1f, 0f, 1f, 0.8f));
                        continue;
                    }
                    if (def.sprite != null) DrawSprite(rect, def.sprite);
                    else EditorGUI.DrawRect(rect, def.editorColor);
                    DrawRectOutline(rect, Color.white);
                }
            }

            // 矩形工具预览
            if (rectDragging)
            {
                var min = Vector2Int.Min(rectStart, rectEnd);
                var max = Vector2Int.Max(rectStart, rectEnd);
                var a = CellRect(canvas, min.x, max.y, cp);
                var b = CellRect(canvas, max.x, min.y, cp);
                var preview = Rect.MinMaxRect(a.xMin, a.yMin, b.xMax, b.yMax);
                EditorGUI.DrawRect(preview, new Color(0.3f, 0.7f, 1f, 0.25f));
                DrawRectOutline(preview, new Color(0.3f, 0.7f, 1f));
            }

            // 悬停格
            if (level.InBounds(hoverCell.x, hoverCell.y))
                DrawRectOutline(CellRect(canvas, hoverCell.x, hoverCell.y, cp), new Color(1f, 1f, 1f, 0.5f));

            HandleCanvasMouse(canvas, cp);
            EditorGUILayout.EndScrollView();
        }

        static Rect CellRect(Rect canvas, int x, int y, float cp)
        {
            // 网格 Y 向上，GUI Y 向下
            return new Rect(canvas.x + x * cp + 1f, canvas.y + (canvasHeightCells(canvas, cp) - 1 - y) * cp + 1f,
                cp - 1f, cp - 1f);
        }

        static int canvasHeightCells(Rect canvas, float cp) => Mathf.RoundToInt((canvas.height - 2f) / cp);

        Vector2Int MouseCell(Rect canvas, float cp, Vector2 mouse)
        {
            int x = Mathf.FloorToInt((mouse.x - canvas.x) / cp);
            int row = Mathf.FloorToInt((mouse.y - canvas.y) / cp);
            return new Vector2Int(x, level.height - 1 - row);
        }

        void HandleCanvasMouse(Rect canvas, float cp)
        {
            var e = Event.current;
            if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag)
            {
                var cell = MouseCell(canvas, cp, e.mousePosition);
                if (cell != hoverCell)
                {
                    hoverCell = cell;
                    Repaint();
                }
            }

            switch (e.type)
            {
                case EventType.MouseDown:
                {
                    if (!canvas.Contains(e.mousePosition)) break;
                    var cell = MouseCell(canvas, cp, e.mousePosition);
                    if (!level.InBounds(cell.x, cell.y)) break;

                    if (e.button == 1)
                    {
                        Undo.RecordObject(level, "擦除关卡");
                        EraseCell(cell.x, cell.y);
                        stroking = true;
                    }
                    else if (e.button == 0)
                    {
                        switch (tool)
                        {
                            case Tool.Brush:
                                Undo.RecordObject(level, "绘制图块");
                                PaintTile(cell.x, cell.y);
                                stroking = true;
                                break;

                            case Tool.Erase:
                                Undo.RecordObject(level, "擦除关卡");
                                EraseCell(cell.x, cell.y);
                                stroking = true;
                                break;

                            case Tool.Rect:
                                rectDragging = true;
                                rectStart = rectEnd = cell;
                                break;

                            case Tool.Entity:
                                Undo.RecordObject(level, "摆放实体");
                                PlaceEntity(cell.x, cell.y);
                                break;
                        }
                    }
                    e.Use();
                    Repaint();
                    break;
                }

                case EventType.MouseDrag:
                {
                    var cell = MouseCell(canvas, cp, e.mousePosition);
                    if (rectDragging)
                    {
                        rectEnd = new Vector2Int(Mathf.Clamp(cell.x, 0, level.width - 1),
                            Mathf.Clamp(cell.y, 0, level.height - 1));
                        e.Use();
                        Repaint();
                    }
                    else if (stroking && level.InBounds(cell.x, cell.y))
                    {
                        if (e.button == 1 || tool == Tool.Erase) EraseCell(cell.x, cell.y);
                        else if (tool == Tool.Brush) PaintTile(cell.x, cell.y);
                        e.Use();
                        Repaint();
                    }
                    break;
                }

                case EventType.MouseUp:
                {
                    if (rectDragging && e.button == 0)
                    {
                        Undo.RecordObject(level, "矩形填充");
                        var min = Vector2Int.Min(rectStart, rectEnd);
                        var max = Vector2Int.Max(rectStart, rectEnd);
                        for (int x = min.x; x <= max.x; x++)
                        for (int y = min.y; y <= max.y; y++)
                            PaintTile(x, y);
                        Repaint();
                    }
                    rectDragging = false;
                    stroking = false;
                    break;
                }
            }
        }

        // ---------------- 绘制操作 ----------------

        void PaintTile(int x, int y)
        {
            if (TileSet == null || TileSet.FindTile(selectedTileId) == null) return;
            if (!level.InBounds(x, y)) return;

            var cell = level.GetTile(x, y);
            if (cell != null)
            {
                if (cell.tileId == selectedTileId) return;
                cell.tileId = selectedTileId;
            }
            else
            {
                level.tiles.Add(new TileCell { x = x, y = y, tileId = selectedTileId });
            }
            EditorUtility.SetDirty(level);
        }

        void EraseCell(int x, int y)
        {
            int removed = level.entities.RemoveAll(i => i != null && i.x == x && i.y == y);
            if (removed == 0)
                removed = level.tiles.RemoveAll(t => t != null && t.x == x && t.y == y);
            if (removed > 0) EditorUtility.SetDirty(level);
        }

        void PlaceEntity(int x, int y)
        {
            var def = TileSet != null ? TileSet.FindEntity(selectedEntityId) : null;
            if (def == null || !level.InBounds(x, y)) return;

            // 出生点全图唯一：摆新的等于移动旧的
            if (def.kind == EntityKind.PlayerStart)
            {
                level.entities.RemoveAll(i =>
                {
                    var d = TileSet.FindEntity(i?.entityId);
                    return d != null && d.kind == EntityKind.PlayerStart;
                });
            }

            level.entities.RemoveAll(i => i != null && i.x == x && i.y == y);
            level.entities.Add(new EntityInstance
            {
                id = Guid.NewGuid().ToString("N"),
                x = x,
                y = y,
                entityId = def.id
            });
            EditorUtility.SetDirty(level);
        }

        // ---------------- 状态栏与校验 ----------------

        void DrawStatusAndIssues()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                string selection = tool == Tool.Entity
                    ? $"实体：{TileSet?.FindEntity(selectedEntityId)?.displayName ?? "（未选）"}"
                    : tool == Tool.Erase
                        ? "橡皮"
                        : $"图块：{TileSet?.FindTile(selectedTileId)?.displayName ?? "（未选）"}";
                GUILayout.Label(selection, EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                if (level != null && level.InBounds(hoverCell.x, hoverCell.y))
                    GUILayout.Label($"({hoverCell.x}, {hoverCell.y})", EditorStyles.miniLabel);
            }

            if (issues == null) return;
            if (issues.Count == 0)
            {
                EditorGUILayout.LabelField("✓ 校验通过", EditorStyles.miniLabel);
                return;
            }

            issueScroll = EditorGUILayout.BeginScrollView(issueScroll, GUILayout.Height(80f));
            foreach (var issue in issues)
            {
                var style = new GUIStyle(EditorStyles.miniLabel);
                style.normal.textColor = issue.isError
                    ? new Color(1f, 0.5f, 0.45f)
                    : new Color(1f, 0.8f, 0.4f);
                GUILayout.Label((issue.isError ? "✕ " : "△ ") + issue.message, style);
            }
            EditorGUILayout.EndScrollView();
        }

        // ---------------- 绘制辅助 ----------------

        static void DrawSprite(Rect rect, Sprite sprite)
        {
            var texture = sprite.texture;
            if (texture == null) return;
            var tr = sprite.textureRect;
            var coords = new Rect(tr.x / texture.width, tr.y / texture.height,
                tr.width / texture.width, tr.height / texture.height);
            GUI.DrawTextureWithTexCoords(rect, texture, coords, true);
        }

        static void DrawRectOutline(Rect rect, Color color)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), color);
        }

        static Rect Shrink(Rect rect, float amount) =>
            new Rect(rect.x + amount, rect.y + amount, rect.width - amount * 2f, rect.height - amount * 2f);
    }
}
