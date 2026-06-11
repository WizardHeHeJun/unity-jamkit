using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DialogueGraph.Editor
{
    public class DialogueGraphWindow : EditorWindow
    {
        [SerializeField] DialogueGraphAsset asset;

        DialogueGraphView graphView;
        ObjectField assetField;
        VisualElement issuesPanel;
        Label issuesHeader;
        ScrollView issuesView;

        [MenuItem("Tools/剧情工具/对话图编辑器")]
        public static void OpenWindow()
        {
            GetWindow<DialogueGraphWindow>("对话图编辑器");
        }

        public static void Open(DialogueGraphAsset graphAsset)
        {
            var window = GetWindow<DialogueGraphWindow>("对话图编辑器");
            window.SetAsset(graphAsset);
        }

        [OnOpenAsset]
        public static bool OnOpenGraphAsset(int instanceID, int line)
        {
            if (EditorUtility.InstanceIDToObject(instanceID) is DialogueGraphAsset graphAsset)
            {
                Open(graphAsset);
                return true;
            }
            return false;
        }

        void CreateGUI()
        {
            var toolbar = new Toolbar();

            assetField = new ObjectField
            {
                objectType = typeof(DialogueGraphAsset),
                allowSceneObjects = false
            };
            assetField.style.minWidth = 220;
            assetField.RegisterValueChangedCallback(e => SetAsset(e.newValue as DialogueGraphAsset));
            toolbar.Add(assetField);

            toolbar.Add(new ToolbarButton(SaveGraph) { text = "保存 (Ctrl+S)" });
            toolbar.Add(new ToolbarButton(RunValidation) { text = "校验" });
            toolbar.Add(new ToolbarSpacer());
            toolbar.Add(new ToolbarButton(() => OpenPreview(false)) { text = "预览" });
            toolbar.Add(new ToolbarButton(() => OpenPreview(true)) { text = "从选中节点预览" });
            rootVisualElement.Add(toolbar);

            graphView = new DialogueGraphView();
            graphView.style.flexGrow = 1;
            rootVisualElement.Add(graphView);

            issuesPanel = new VisualElement();
            issuesPanel.style.display = DisplayStyle.None;
            issuesPanel.style.maxHeight = 150;
            issuesPanel.style.borderTopWidth = 1;
            issuesPanel.style.borderTopColor = new Color(0.1f, 0.1f, 0.1f);

            issuesHeader = new Label();
            issuesHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            issuesHeader.style.marginLeft = 4;
            issuesPanel.Add(issuesHeader);

            issuesView = new ScrollView();
            issuesPanel.Add(issuesView);
            rootVisualElement.Add(issuesPanel);

            rootVisualElement.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.ctrlKey && e.keyCode == KeyCode.S)
                {
                    SaveGraph();
                    e.StopPropagation();
                }
            }, TrickleDown.TrickleDown);

            if (asset != null)
            {
                assetField.SetValueWithoutNotify(asset);
                graphView.LoadGraph(asset);
            }
        }

        public void SetAsset(DialogueGraphAsset graphAsset)
        {
            asset = graphAsset;
            if (graphView == null) return; // CreateGUI 尚未执行，等它执行时加载

            assetField.SetValueWithoutNotify(graphAsset);
            graphView.LoadGraph(graphAsset);
            ShowIssues(new List<GraphValidator.Issue>());
        }

        void SaveGraph()
        {
            if (asset == null) return;
            graphView?.Save();

            var issues = GraphValidator.Validate(asset);
            ShowIssues(issues);
            ShowNotification(new GUIContent(issues.Count == 0 ? "已保存" : $"已保存，发现 {issues.Count} 个问题"), 1.0);
        }

        void RunValidation()
        {
            if (asset == null) return;
            graphView?.Save(); // 校验基于最新数据
            var issues = GraphValidator.Validate(asset);
            ShowIssues(issues);
            if (issues.Count == 0)
                ShowNotification(new GUIContent("校验通过"), 1.0);
        }

        void ShowIssues(List<GraphValidator.Issue> issues)
        {
            if (issuesView == null) return;
            issuesView.Clear();

            if (issues.Count == 0)
            {
                issuesPanel.style.display = DisplayStyle.None;
                return;
            }

            issuesPanel.style.display = DisplayStyle.Flex;
            int errors = issues.Count(i => i.severity == GraphValidator.Severity.Error);
            issuesHeader.text = $"校验结果：{errors} 个错误，{issues.Count - errors} 个警告（点击定位节点）";

            foreach (var issue in issues)
            {
                var captured = issue;
                bool isError = issue.severity == GraphValidator.Severity.Error;
                var row = new Button(() =>
                {
                    if (!string.IsNullOrEmpty(captured.nodeGuid))
                        graphView.FrameAndSelectNode(captured.nodeGuid);
                })
                {
                    text = (isError ? "[错误] " : "[警告] ") + issue.message
                };
                row.style.unityTextAlign = TextAnchor.MiddleLeft;
                row.style.backgroundColor = Color.clear;
                row.style.color = isError
                    ? new Color(1f, 0.45f, 0.45f)
                    : new Color(1f, 0.8f, 0.4f);
                issuesView.Add(row);
            }
        }

        void OpenPreview(bool fromSelection)
        {
            if (asset == null) return;

            string startGuid = null;
            if (fromSelection)
            {
                startGuid = graphView?.GetFirstSelectedNodeGuid();
                if (startGuid == null)
                {
                    ShowNotification(new GUIContent("请先在画布中选中一个节点"), 1.5);
                    return;
                }
            }

            graphView?.Save(); // 预览基于最新数据
            DialoguePreviewWindow.Open(asset, startGuid);
        }
    }
}
