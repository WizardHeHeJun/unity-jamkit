using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameplayKit.Quest.Editor
{
    /// <summary>
    /// 任务图画布。编辑过程中节点字段实时写回内存数据，
    /// 节点增删与连线只存在于视图中，点击保存时统一序列化回资产。
    /// </summary>
    public class QuestGraphView : GraphView
    {
        QuestGraphAsset asset;

        public QuestGraphAsset Asset => asset;

        public QuestGraphView()
        {
            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();

            var sheetGuids = AssetDatabase.FindAssets("QuestGraphEditor t:StyleSheet");
            if (sheetGuids.Length > 0)
            {
                var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                    AssetDatabase.GUIDToAssetPath(sheetGuids[0]));
                if (sheet != null) styleSheets.Add(sheet);
            }
        }

        public void LoadGraph(QuestGraphAsset graphAsset)
        {
            asset = graphAsset;
            DeleteElements(graphElements.ToList());
            if (asset == null) return;

            asset.nodes.RemoveAll(n => n == null);
            if (asset.GetStartNode() == null)
            {
                asset.nodes.Add(new QuestStartNodeData
                {
                    guid = Guid.NewGuid().ToString("N"),
                    position = new Vector2(100, 200)
                });
                EditorUtility.SetDirty(asset);
            }

            var viewByGuid = new Dictionary<string, BaseQuestNodeView>();
            foreach (var data in asset.nodes)
            {
                var view = CreateNodeView(data);
                if (view != null) viewByGuid[data.guid] = view;
            }

            foreach (var view in viewByGuid.Values)
            {
                foreach (var (port, targetGuid) in view.EnumerateOutputLinks())
                {
                    if (string.IsNullOrEmpty(targetGuid)) continue;
                    if (!viewByGuid.TryGetValue(targetGuid, out var target) || target.Input == null) continue;
                    AddElement(port.ConnectTo(target.Input));
                }
            }
        }

        BaseQuestNodeView CreateNodeView(QuestBaseNodeData data)
        {
            BaseQuestNodeView view = data switch
            {
                QuestStartNodeData d => new QuestStartNodeView(this, d),
                QuestNodeData d => new QuestNodeView(this, d),
                ConditionGateNodeData d => new ConditionGateNodeView(this, d),
                JoinAllNodeData d => new JoinAllNodeView(this, d),
                _ => null
            };
            if (view == null) return null;

            view.SetPosition(new Rect(data.position, Vector2.zero));
            AddElement(view);
            return view;
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            if (asset != null && evt.target is GraphView)
            {
                Vector2 graphPosition = contentViewContainer.WorldToLocal(evt.mousePosition);
                evt.menu.AppendAction("新建节点/任务",
                    _ => AddNewNode(new QuestNodeData { questId = NewQuestId() }, graphPosition));
                evt.menu.AppendAction("新建节点/条件门", _ => AddNewNode(new ConditionGateNodeData(), graphPosition));
                evt.menu.AppendAction("新建节点/汇合（全部完成）", _ => AddNewNode(new JoinAllNodeData(), graphPosition));
                evt.menu.AppendSeparator();
            }
            base.BuildContextualMenu(evt);
        }

        static string NewQuestId()
        {
            // 给一个可读的默认 ID，策划可改成业务 ID
            return "quest_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        void AddNewNode(QuestBaseNodeData data, Vector2 position)
        {
            data.guid = Guid.NewGuid().ToString("N");
            data.position = position;
            CreateNodeView(data);
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            return ports.ToList().Where(port =>
                port != startPort &&
                port.node != startPort.node &&
                port.direction != startPort.direction).ToList();
        }

        public void Save()
        {
            if (asset == null) return;
            Undo.RecordObject(asset, "保存任务图");

            var views = nodes.ToList().OfType<BaseQuestNodeView>().ToList();
            foreach (var view in views)
                view.SyncToData();

            foreach (var edge in edges.ToList())
            {
                if (edge.output?.userData is Action<string> addTargetGuid &&
                    edge.input?.node is BaseQuestNodeView target)
                {
                    addTargetGuid(target.Data.guid);
                }
            }

            asset.nodes = views.Select(v => v.Data).ToList();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
        }

        /// <summary>定位并选中指定节点（校验问题列表点击跳转用）。</summary>
        public void FrameAndSelectNode(string guid)
        {
            var view = nodes.ToList().OfType<BaseQuestNodeView>().FirstOrDefault(v => v.Data.guid == guid);
            if (view == null) return;
            ClearSelection();
            AddToSelection(view);
            FrameSelection();
        }
    }
}
