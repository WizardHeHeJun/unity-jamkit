using System;
using System.Collections;
using UnityEngine;

namespace DialogueGraph
{
    public enum VNPlayMode
    {
        Normal,

        /// <summary>打字完成 + 语音播完后自动推进。</summary>
        Auto,

        /// <summary>已读台词快进（演出瞬时完成、不打字、极短停顿）。</summary>
        Skip
    }

    /// <summary>
    /// 视觉小说播放器门面：持有 DialogueRunner，把剧情事件接到 VNStage 与 VNDialogueUI。
    /// 协程顺序：演出指令 → 表情/高亮/语音 → 打字机。
    /// </summary>
    public class VNPlayer : MonoBehaviour
    {
        public DialogueGraphAsset graph;
        public VNStage stage;
        public VNDialogueUI ui;
        public bool playOnStart = true;
        public string language = "zh-CN";

        [Header("Auto / Skip")]
        public VNPlayMode playMode = VNPlayMode.Normal;
        public float autoDelaySeconds = 1.2f;
        public float skipDelaySeconds = 0.05f;

        public DialogueRunner Runner { get; private set; }
        public DialogueVariableStore Variables { get; } = new DialogueVariableStore();

        /// <summary>事件节点转发：(事件名, 参数)。接游戏逻辑：给道具、解锁CG、切场景等。</summary>
        public event Action<string, string> OnGameEvent;

        public event Action OnDialogueEnded;

        enum WaitState
        {
            None,
            Line,
            Choice,
            Stage
        }

        WaitState waiting;
        Coroutine lineRoutine;
        Coroutine autoRoutine;

        void Awake()
        {
            Runner = new DialogueRunner
            {
                Language = language,
                Variables = Variables
            };
            Runner.OnLine += HandleLine;
            Runner.OnChoices += HandleChoices;
            Runner.OnStage += HandleStage;
            Runner.OnEvent += (eventName, param) => OnGameEvent?.Invoke(eventName, param);
            Runner.OnEnded += HandleEnded;

            if (ui != null)
            {
                ui.OnAdvanceClicked += HandleAdvanceClicked;
                ui.OnChoiceSelected += HandleChoiceSelected;
            }
        }

        void Start()
        {
            if (playOnStart && graph != null) Play();
        }

        public void Play() => Runner.Begin(graph);

        public void PlayFrom(string nodeGuid) => Runner.BeginFrom(graph, nodeGuid);

        // ---------------- Runner 事件 ----------------

        void HandleLine(DialogueLine line)
        {
            waiting = WaitState.None;
            StopAuto();
            if (lineRoutine != null) StopCoroutine(lineRoutine);
            lineRoutine = StartCoroutine(PlayLine(line));
        }

        IEnumerator PlayLine(DialogueLine line)
        {
            bool fastForward = playMode == VNPlayMode.Skip && line.wasRead;

            if (stage != null)
            {
                yield return stage.Execute(line.commands, fastForward);
                stage.SetExpression(line.character, line.expression);
                stage.HighlightSpeaker(line.character);
                if (!fastForward) stage.PlayVoice(line.voiceClip);
            }

            ui?.ShowLine(
                line.speakerName,
                line.character != null ? line.character.nameColor : Color.white,
                line.text,
                instant: fastForward);

            waiting = WaitState.Line;
            lineRoutine = null;

            if (fastForward)
            {
                yield return new WaitForSeconds(skipDelaySeconds);
                AdvanceLine();
            }
            else if (playMode == VNPlayMode.Auto)
            {
                autoRoutine = StartCoroutine(AutoAdvance());
            }
        }

        IEnumerator AutoAdvance()
        {
            while (ui != null && ui.IsTyping) yield return null;
            while (stage != null && stage.IsVoicePlaying) yield return null;
            yield return new WaitForSeconds(autoDelaySeconds);
            autoRoutine = null;
            AdvanceLine();
        }

        void HandleChoices(ChoicePrompt prompt)
        {
            waiting = WaitState.Choice;
            StopAuto();

            if (ui != null)
            {
                if (!string.IsNullOrEmpty(prompt.promptText))
                {
                    ui.ShowLine(
                        prompt.speakerName,
                        prompt.character != null ? prompt.character.nameColor : Color.white,
                        prompt.promptText);
                }

                var texts = new string[prompt.options.Count];
                for (int i = 0; i < prompt.options.Count; i++)
                    texts[i] = prompt.options[i].text;
                ui.ShowChoices(texts);
            }

            stage?.HighlightSpeaker(prompt.character);
        }

        void HandleStage(System.Collections.Generic.IReadOnlyList<StageCommand> commands)
        {
            waiting = WaitState.Stage;
            StartCoroutine(PlayStage(commands));
        }

        IEnumerator PlayStage(System.Collections.Generic.IReadOnlyList<StageCommand> commands)
        {
            if (stage != null)
                yield return stage.Execute(commands, playMode == VNPlayMode.Skip);

            if (waiting == WaitState.Stage)
            {
                waiting = WaitState.None;
                Runner.Continue();
            }
        }

        void HandleEnded()
        {
            waiting = WaitState.None;
            StopAuto();
            ui?.HideAll();
            stage?.HighlightSpeaker(null);
            OnDialogueEnded?.Invoke();
        }

        // ---------------- 玩家输入 ----------------

        void HandleAdvanceClicked()
        {
            if (waiting != WaitState.Line) return;

            if (ui != null && ui.IsTyping)
            {
                ui.CompleteTyping();
                return;
            }
            AdvanceLine();
        }

        void HandleChoiceSelected(int index)
        {
            if (waiting != WaitState.Choice) return;
            waiting = WaitState.None;
            Runner.SelectChoice(index);
        }

        void AdvanceLine()
        {
            if (waiting != WaitState.Line) return;
            waiting = WaitState.None;
            StopAuto();
            Runner.Continue();
        }

        void StopAuto()
        {
            if (autoRoutine != null)
            {
                StopCoroutine(autoRoutine);
                autoRoutine = null;
            }
        }

        /// <summary>切换 Auto 模式（UI 按钮可直接绑定）。</summary>
        public void ToggleAuto()
        {
            playMode = playMode == VNPlayMode.Auto ? VNPlayMode.Normal : VNPlayMode.Auto;
            if (playMode == VNPlayMode.Auto && waiting == WaitState.Line && autoRoutine == null)
                autoRoutine = StartCoroutine(AutoAdvance());
            else if (playMode != VNPlayMode.Auto)
                StopAuto();
        }

        /// <summary>切换 Skip 模式（仅快进已读台词）。</summary>
        public void ToggleSkip()
        {
            playMode = playMode == VNPlayMode.Skip ? VNPlayMode.Normal : VNPlayMode.Skip;
        }

        // ---------------- 存档 ----------------

        /// <summary>捕获当前剧情状态为 JSON（含变量与已读记录），写进游戏存档即可。</summary>
        public string CaptureSaveJson()
        {
            var state = Runner.GetState();
            return state != null ? JsonUtility.ToJson(state) : null;
        }

        /// <summary>从 JSON 恢复剧情（会重新触发当前台词/选项以重建 UI）。</summary>
        public bool RestoreFromJson(string json)
        {
            if (string.IsNullOrEmpty(json) || graph == null) return false;
            var state = JsonUtility.FromJson<DialogueRunnerState>(json);
            return Runner.RestoreState(graph, state);
        }
    }
}
