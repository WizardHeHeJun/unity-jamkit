using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace DialogueGraph.Editor
{
    /// <summary>
    /// 对话图画布。编辑过程中节点字段实时写回内存数据，
    /// 节点增删与连线只存在于视图中，点击保存时统一序列化回资产。
    /// </summary>
    public class DialogueGraphView : GraphView
    {
        DialogueGraphAsset asset;

        public DialogueGraphAsset Asset => asset;

        public DialogueGraphView()
        {
            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();

            var sheetGuids = AssetDatabase.FindAssets("DialogueGraphEditor t:StyleSheet");
            if (sheetGuids.Length > 0)
            {
                var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                    AssetDatabase.GUIDToAssetPath(sheetGuids[0]));
                if (sheet != null) styleSheets.Add(sheet);
            }
        }

        public void LoadGraph(DialogueGraphAsset graphAsset)
        {
            asset = graphAsset;
            DeleteElements(graphElements.ToList());
            if (asset == null) return;

            asset.nodes.RemoveAll(n => n == null);
            if (asset.GetStartNode() == null)
            {
                asset.nodes.Add(new StartNodeData
                {
                    guid = Guid.NewGuid().ToString("N"),
                    position = new Vector2(100, 200)
                });
                EditorUtility.SetDirty(asset);
            }

            var viewByGuid = new Dictionary<string, NodeView>();
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

        NodeView CreateNodeView(BaseNodeData data)
        {
            NodeView view = data switch
            {
                StartNodeData d => new StartNodeView(this, d),
                DialogueNodeData d => new DialogueNodeView(this, d),
                ChoiceNodeData d => new ChoiceNodeView(this, d),
                ConditionNodeData d => new ConditionNodeView(this, d),
                EventNodeData d => new EventNodeView(this, d),
                StageNodeData d => new StageNodeView(this, d),
                SubGraphNodeData d => new SubGraphNodeView(this, d),
                EndNodeData d => new EndNodeView(this, d),
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
                evt.menu.AppendAction("新建节点/对话", _ => AddNewNode(new DialogueNodeData(), graphPosition));
                evt.menu.AppendAction("新建节点/选项分支", _ => AddNewNode(new ChoiceNodeData(), graphPosition));
                evt.menu.AppendAction("新建节点/条件判断", _ => AddNewNode(new ConditionNodeData(), graphPosition));
                evt.menu.AppendAction("新建节点/事件触发", _ => AddNewNode(new EventNodeData(), graphPosition));
                evt.menu.AppendAction("新建节点/演出", _ => AddNewNode(new StageNodeData(), graphPosition));
                evt.menu.AppendAction("新建节点/子图跳转", _ => AddNewNode(new SubGraphNodeData(), graphPosition));
                evt.menu.AppendAction("新建节点/结束", _ => AddNewNode(new EndNodeData(), graphPosition));
                evt.menu.AppendSeparator();
            }
            base.BuildContextualMenu(evt);
        }

        void AddNewNode(BaseNodeData data, Vector2 position)
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
            Undo.RecordObject(asset, "保存对话图");

            var views = nodes.ToList().OfType<NodeView>().ToList();
            foreach (var view in views)
                view.SyncToData();

            foreach (var edge in edges.ToList())
            {
                if (edge.output?.userData is Action<string> setTargetGuid &&
                    edge.input?.node is NodeView target)
                {
                    setTargetGuid(target.Data.guid);
                }
            }

            asset.nodes = views.Select(v => v.Data).ToList();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
        }

        /// <summary>定位并选中指定节点（校验问题列表点击跳转用）。</summary>
        public void FrameAndSelectNode(string guid)
        {
            var view = nodes.ToList().OfType<NodeView>().FirstOrDefault(v => v.Data.guid == guid);
            if (view == null) return;
            ClearSelection();
            AddToSelection(view);
            FrameSelection();
        }

        /// <summary>当前选中的第一个节点 guid，无选中返回 null（「从选中节点预览」用）。</summary>
        public string GetFirstSelectedNodeGuid()
        {
            return selection.OfType<NodeView>().FirstOrDefault()?.Data.guid;
        }
    }
}
