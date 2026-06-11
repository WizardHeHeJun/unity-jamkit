using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameplayKit.Quest.Editor
{
    public class QuestGraphWindow : EditorWindow
    {
        [SerializeField] QuestGraphAsset asset;

        QuestGraphView graphView;
        ObjectField assetField;
        VisualElement issuesPanel;
        Label issuesHeader;
        ScrollView issuesView;

        [MenuItem("Tools/玩法工具/任务图编辑器")]
        public static void OpenWindow()
        {
            GetWindow<QuestGraphWindow>("任务图编辑器");
        }

        public static void Open(QuestGraphAsset graphAsset)
        {
            var window = GetWindow<QuestGraphWindow>("任务图编辑器");
            window.SetAsset(graphAsset);
        }

        [OnOpenAsset]
        public static bool OnOpenGraphAsset(int instanceID, int line)
        {
            if (EditorUtility.InstanceIDToObject(instanceID) is QuestGraphAsset graphAsset)
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
                objectType = typeof(QuestGraphAsset),
                allowSceneObjects = false
            };
            assetField.style.minWidth = 220;
            assetField.RegisterValueChangedCallback(e => SetAsset(e.newValue as QuestGraphAsset));
            toolbar.Add(assetField);

            toolbar.Add(new ToolbarButton(SaveGraph) { text = "保存 (Ctrl+S)" });
            toolbar.Add(new ToolbarButton(RunValidation) { text = "校验" });
            toolbar.Add(new ToolbarSpacer());
            toolbar.Add(new ToolbarButton(OpenSimulator) { text = "模拟" });
            rootVisualElement.Add(toolbar);

            graphView = new QuestGraphView();
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

        public void SetAsset(QuestGraphAsset graphAsset)
        {
            asset = graphAsset;
            if (graphView == null) return; // CreateGUI 尚未执行，等它执行时加载

            assetField.SetValueWithoutNotify(graphAsset);
            graphView.LoadGraph(graphAsset);
            ShowIssues(new List<QuestGraphValidator.Issue>());
        }

        void SaveGraph()
        {
            if (asset == null) return;
            graphView?.Save();

            var issues = QuestGraphValidator.Validate(asset);
            ShowIssues(issues);
            ShowNotification(new GUIContent(issues.Count == 0 ? "已保存" : $"已保存，发现 {issues.Count} 个问题"), 1.0);
        }

        void RunValidation()
        {
            if (asset == null) return;
            graphView?.Save(); // 校验基于最新数据
            var issues = QuestGraphValidator.Validate(asset);
            ShowIssues(issues);
            if (issues.Count == 0)
                ShowNotification(new GUIContent("校验通过"), 1.0);
        }

        void ShowIssues(List<QuestGraphValidator.Issue> issues)
        {
            if (issuesView == null) return;
            issuesView.Clear();

            if (issues.Count == 0)
            {
                issuesPanel.style.display = DisplayStyle.None;
                return;
            }

            issuesPanel.style.display = DisplayStyle.Flex;
            int errors = issues.Count(i => i.severity == QuestGraphValidator.Severity.Error);
            issuesHeader.text = $"校验结果：{errors} 个错误，{issues.Count - errors} 个警告（点击定位节点）";

            foreach (var issue in issues)
            {
                var captured = issue;
                bool isError = issue.severity == QuestGraphValidator.Severity.Error;
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

        void OpenSimulator()
        {
            if (asset == null) return;
            graphView?.Save(); // 模拟基于最新数据
            QuestSimulatorWindow.Open(asset);
        }
    }
}
