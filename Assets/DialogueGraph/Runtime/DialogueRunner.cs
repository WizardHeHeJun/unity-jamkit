using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DialogueGraph
{
    /// <summary>一句台词的完整信息，由 OnLine 事件下发。</summary>
    public struct DialogueLine
    {
        /// <summary>稳定行 ID（= 对话节点 guid），供已读记录 / 配音对照使用。</summary>
        public string lineId;

        /// <summary>说话角色，null 表示旁白。</summary>
        public CharacterAsset character;

        /// <summary>已按当前语言解析的显示名，旁白为空字符串。</summary>
        public string speakerName;

        public string expression;
        public string text;
        public AudioClip voiceClip;

        /// <summary>台词显示前需执行的演出指令。</summary>
        public IReadOnlyList<StageCommand> commands;

        /// <summary>本句在此次显示之前是否已读过（供 Skip 模式判断）。</summary>
        public bool wasRead;
    }

    public struct ChoiceItem
    {
        /// <summary>稳定行 ID（= 选项 id）。</summary>
        public string id;

        public string text;
    }

    public struct ChoicePrompt
    {
        public CharacterAsset character;
        public string speakerName;
        public string promptText;
        public IReadOnlyList<ChoiceItem> options;
    }

    /// <summary>存档状态快照，可直接 JsonUtility 序列化。</summary>
    [Serializable]
    public class DialogueRunnerState
    {
        [Serializable]
        public class StackFrame
        {
            public string graphName;
            public string returnGuid;
        }

        public string currentGraphName;
        public string currentNodeGuid;

        /// <summary>子图调用栈，自栈底向栈顶排列。</summary>
        public List<StackFrame> callStack = new List<StackFrame>();

        public VariableStoreState variables;
        public List<string> readLines = new List<string>();
    }

    /// <summary>
    /// 对话图运行器：纯 C# 类，由游戏侧驱动。
    /// 流程：Begin() 开始 → OnLine 展示台词后调 Continue()；
    /// OnChoices 展示选项后调 SelectChoice(index)；
    /// OnStage 播完演出后调 Continue()；
    /// 条件 / 事件 / 子图节点自动执行；到达结束节点触发 OnEnded。
    /// </summary>
    public class DialogueRunner
    {
        public event Action<DialogueLine> OnLine;
        public event Action<ChoicePrompt> OnChoices;

        /// <summary>纯演出节点：表现层执行完指令后须调 Continue() 推进。</summary>
        public event Action<IReadOnlyList<StageCommand>> OnStage;

        /// <summary>剧情事件：(事件名, 字符串参数)</summary>
        public event Action<string, string> OnEvent;

        public event Action OnEnded;

        /// <summary>当前语言代码，对应本地化表 languages 中的项。</summary>
        public string Language = "zh-CN";

        /// <summary>条件节点的变量来源，为 null 时所有条件按「不满足」处理。</summary>
        public IDialogueVariableProvider Variables;

        public bool IsRunning { get; private set; }

        const int MaxSteps = 10000;
        const int MaxSubGraphDepth = 64;

        DialogueGraphAsset rootGraph;
        DialogueGraphAsset graph;
        BaseNodeData current;
        ChoiceNodeData pendingChoice;

        readonly Stack<(DialogueGraphAsset graph, string returnGuid)> callStack =
            new Stack<(DialogueGraphAsset, string)>();

        readonly HashSet<string> readLines = new HashSet<string>();

        public void Begin(DialogueGraphAsset graphAsset)
        {
            BeginFrom(graphAsset, null);
        }

        /// <summary>从指定节点开始执行；nodeGuid 为空则从开始节点执行。</summary>
        public void BeginFrom(DialogueGraphAsset graphAsset, string nodeGuid)
        {
            rootGraph = graphAsset;
            graph = graphAsset;
            var start = graph != null ? graph.GetStartNode() : null;
            if (start == null)
            {
                Debug.LogError("[DialogueRunner] 对话图为空或缺少开始节点");
                return;
            }

            IsRunning = true;
            pendingChoice = null;
            callStack.Clear();

            var entry = string.IsNullOrEmpty(nodeGuid)
                ? graph.FindNode(start.nextGuid)
                : graph.FindNode(nodeGuid);
            Run(entry);
        }

        /// <summary>对话台词展示完毕、或纯演出节点演出播放完毕后调用。</summary>
        public void Continue()
        {
            if (!IsRunning) return;
            switch (current)
            {
                case DialogueNodeData d:
                    Run(graph.FindNode(d.nextGuid));
                    break;
                case StageNodeData s:
                    Run(graph.FindNode(s.nextGuid));
                    break;
            }
        }

        /// <summary>玩家选择第 index 个选项后调用。</summary>
        public void SelectChoice(int index)
        {
            if (!IsRunning || pendingChoice == null) return;
            if (index < 0 || index >= pendingChoice.options.Count) return;

            var nextGuid = pendingChoice.options[index].nextGuid;
            pendingChoice = null;
            Run(graph.FindNode(nextGuid));
        }

        public void Stop()
        {
            IsRunning = false;
            current = null;
            pendingChoice = null;
            callStack.Clear();
        }

        /// <summary>该行是否已读（Skip 模式 / 回想用）。</summary>
        public bool IsRead(string lineId) => readLines.Contains(lineId);

        public void ClearReadLog() => readLines.Clear();

        void Run(BaseNodeData node)
        {
            current = node;
            int safety = 0;
            while (IsRunning)
            {
                if (++safety > MaxSteps)
                {
                    Debug.LogError("[DialogueRunner] 节点推进超过上限，疑似条件/事件节点构成死循环，已中断");
                    Finish();
                    return;
                }

                switch (current)
                {
                    case null:
                    case EndNodeData _:
                        if (callStack.Count > 0)
                        {
                            var frame = callStack.Pop();
                            graph = frame.graph;
                            current = graph.FindNode(frame.returnGuid);
                            break;
                        }
                        Finish();
                        return;

                    case StartNodeData start:
                        current = graph.FindNode(start.nextGuid);
                        break;

                    case DialogueNodeData dialogue:
                        EmitLine(dialogue);
                        return;

                    case ChoiceNodeData choice:
                        EmitChoices(choice);
                        return;

                    case StageNodeData stage:
                        OnStage?.Invoke(stage.commands);
                        return;

                    case ConditionNodeData condition:
                        current = graph.FindNode(
                            Evaluate(condition.condition) ? condition.trueGuid : condition.falseGuid);
                        break;

                    case EventNodeData evt:
                        OnEvent?.Invoke(evt.eventName, evt.stringParam);
                        current = graph.FindNode(evt.nextGuid);
                        break;

                    case SubGraphNodeData sub:
                        if (sub.graph == null || sub.graph.GetStartNode() == null)
                        {
                            Debug.LogWarning($"[DialogueRunner] 子图节点引用无效，已跳过（{graph.name}）");
                            current = graph.FindNode(sub.nextGuid);
                            break;
                        }
                        if (callStack.Count >= MaxSubGraphDepth)
                        {
                            Debug.LogError("[DialogueRunner] 子图嵌套超过上限，疑似递归引用，已中断");
                            Finish();
                            return;
                        }
                        callStack.Push((graph, sub.nextGuid));
                        graph = sub.graph;
                        current = graph.FindNode(graph.GetStartNode().nextGuid);
                        break;

                    default:
                        Finish();
                        return;
                }
            }
        }

        void EmitLine(DialogueNodeData dialogue)
        {
            bool wasRead = readLines.Contains(dialogue.guid);
            readLines.Add(dialogue.guid);
            OnLine?.Invoke(new DialogueLine
            {
                lineId = dialogue.guid,
                character = dialogue.character,
                speakerName = ResolveSpeakerName(dialogue.character),
                expression = dialogue.expression,
                text = Resolve(dialogue.line),
                voiceClip = dialogue.voiceClip,
                commands = dialogue.commands,
                wasRead = wasRead
            });
        }

        void EmitChoices(ChoiceNodeData choice)
        {
            pendingChoice = choice;
            var items = new List<ChoiceItem>(choice.options.Count);
            foreach (var option in choice.options)
                items.Add(new ChoiceItem { id = option.id, text = Resolve(option.text) });

            OnChoices?.Invoke(new ChoicePrompt
            {
                character = choice.character,
                speakerName = ResolveSpeakerName(choice.character),
                promptText = Resolve(choice.prompt),
                options = items
            });
        }

        void Finish()
        {
            IsRunning = false;
            current = null;
            pendingChoice = null;
            callStack.Clear();
            OnEnded?.Invoke();
        }

        string ResolveSpeakerName(CharacterAsset character)
        {
            return character != null ? Resolve(character.displayName) : string.Empty;
        }

        string Resolve(LocalizedText text)
        {
            if (text == null) return string.Empty;
            if (!string.IsNullOrEmpty(text.key) && graph != null && graph.localizationTable != null)
            {
                var localized = graph.localizationTable.GetText(text.key, Language);
                if (localized != null) return localized;
            }
            return text.fallbackText ?? string.Empty;
        }

        bool Evaluate(VariableCondition c)
        {
            if (c == null || Variables == null || string.IsNullOrEmpty(c.variableName)) return false;

            switch (c.variableType)
            {
                case VariableType.Bool:
                    return Variables.TryGetBool(c.variableName, out var b)
                           && CompareEquality(b == c.boolValue, c.op);
                case VariableType.Int:
                    return Variables.TryGetInt(c.variableName, out var i)
                           && CompareNumeric(i.CompareTo(c.intValue), c.op);
                case VariableType.Float:
                    return Variables.TryGetFloat(c.variableName, out var f)
                           && CompareNumeric(f.CompareTo(c.floatValue), c.op);
                case VariableType.String:
                    return Variables.TryGetString(c.variableName, out var s)
                           && CompareEquality(string.Equals(s, c.stringValue), c.op);
                default:
                    return false;
            }
        }

        // 布尔 / 文本只支持 == 和 !=，其余比较符一律视为不满足
        static bool CompareEquality(bool equal, CompareOp op)
        {
            switch (op)
            {
                case CompareOp.Equal: return equal;
                case CompareOp.NotEqual: return !equal;
                default: return false;
            }
        }

        static bool CompareNumeric(int cmp, CompareOp op)
        {
            switch (op)
            {
                case CompareOp.Equal: return cmp == 0;
                case CompareOp.NotEqual: return cmp != 0;
                case CompareOp.Greater: return cmp > 0;
                case CompareOp.GreaterOrEqual: return cmp >= 0;
                case CompareOp.Less: return cmp < 0;
                case CompareOp.LessOrEqual: return cmp <= 0;
                default: return false;
            }
        }

        // ---------------- 存档 ----------------

        /// <summary>
        /// 捕获当前状态。仅在停在对话 / 选项 / 演出节点时有效（自动节点不会停留）。
        /// 变量快照仅当 Variables 是 DialogueVariableStore 时包含。
        /// </summary>
        public DialogueRunnerState GetState()
        {
            if (!IsRunning || current == null)
            {
                Debug.LogWarning("[DialogueRunner] 当前未在运行，无法捕获状态");
                return null;
            }

            var state = new DialogueRunnerState
            {
                currentGraphName = graph.name,
                currentNodeGuid = current.guid,
                variables = (Variables as DialogueVariableStore)?.CaptureState()
            };

            // Stack 枚举自顶向底，存档按栈底→栈顶排列
            foreach (var frame in callStack.Reverse())
            {
                state.callStack.Add(new DialogueRunnerState.StackFrame
                {
                    graphName = frame.graph.name,
                    returnGuid = frame.returnGuid
                });
            }

            state.readLines.AddRange(readLines);
            return state;
        }

        /// <summary>
        /// 从快照恢复并重新触发当前节点事件（OnLine / OnChoices / OnStage）以重建 UI。
        /// 子图按资产名解析：默认从 rootGraph 的子图引用递归收集；
        /// 图不在引用树内时可传自定义 graphResolver。
        /// </summary>
        public bool RestoreState(DialogueGraphAsset graphAsset, DialogueRunnerState state,
            Func<string, DialogueGraphAsset> graphResolver = null)
        {
            if (graphAsset == null || state == null) return false;

            var graphMap = new Dictionary<string, DialogueGraphAsset>();
            CollectGraphs(graphAsset, graphMap);

            DialogueGraphAsset ResolveGraph(string graphName)
            {
                if (graphMap.TryGetValue(graphName, out var found)) return found;
                return graphResolver?.Invoke(graphName);
            }

            var currentGraph = ResolveGraph(state.currentGraphName);
            var currentNode = currentGraph != null ? currentGraph.FindNode(state.currentNodeGuid) : null;
            if (currentNode == null)
            {
                Debug.LogError($"[DialogueRunner] 恢复失败：找不到图「{state.currentGraphName}」中的节点 {state.currentNodeGuid}");
                return false;
            }

            rootGraph = graphAsset;
            callStack.Clear();
            foreach (var frame in state.callStack)
            {
                var frameGraph = ResolveGraph(frame.graphName);
                if (frameGraph == null)
                {
                    Debug.LogError($"[DialogueRunner] 恢复失败：找不到子图调用栈中的图「{frame.graphName}」");
                    return false;
                }
                callStack.Push((frameGraph, frame.returnGuid));
            }

            readLines.Clear();
            readLines.UnionWith(state.readLines);
            if (state.variables != null && Variables is DialogueVariableStore store)
                store.ApplyState(state.variables);

            graph = currentGraph;
            current = currentNode;
            pendingChoice = null;
            IsRunning = true;

            // 重新触发当前节点事件以重建 UI
            switch (current)
            {
                case DialogueNodeData dialogue:
                    EmitLine(dialogue);
                    break;
                case ChoiceNodeData choice:
                    EmitChoices(choice);
                    break;
                case StageNodeData stage:
                    OnStage?.Invoke(stage.commands);
                    break;
                default:
                    Run(current);
                    break;
            }
            return true;
        }

        static void CollectGraphs(DialogueGraphAsset g, Dictionary<string, DialogueGraphAsset> map)
        {
            if (g == null || map.ContainsKey(g.name)) return;
            map[g.name] = g;
            foreach (var node in g.nodes)
            {
                if (node is SubGraphNodeData sub && sub.graph != null)
                    CollectGraphs(sub.graph, map);
            }
        }
    }
}
