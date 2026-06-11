using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DialogueGraph
{
    /// <summary>
    /// 视觉小说舞台：背景交叉淡入、三个立绘槽位、BGM / 音效 / 语音。
    /// 负责解释执行 StageCommand。所有引用由演示场景搭建工具或手动接好。
    /// </summary>
    public class VNStage : MonoBehaviour
    {
        [Header("背景（B 层须在 A 层之上）")]
        public Image backgroundA;
        public Image backgroundB;

        [Header("立绘槽位（顺序：左 / 中 / 右）")]
        public Image[] characterSlots = new Image[3];

        [Header("舞台布局（可选；Awake 时应用到立绘槽位，美术在「舞台布景」窗口调整）")]
        public StageLayoutAsset layout;

        [Header("音频")]
        public AudioSource bgmSourceA;
        public AudioSource bgmSourceB;
        public AudioSource sfxSource;
        public AudioSource voiceSource;

        [Header("说话人高亮（其他立绘压暗的亮度）")]
        [Range(0f, 1f)]
        public float dimBrightness = 0.55f;

        readonly CharacterAsset[] slotOccupants = new CharacterAsset[3];
        Vector2[] slotBasePositions;
        Vector3[] slotBaseScales;
        bool bgmUseB;

        public bool IsVoicePlaying => voiceSource != null && voiceSource.isPlaying;

        void Awake()
        {
            ApplyLayout();
        }

        /// <summary>把布局资产应用到立绘槽位（运行时与编辑器工具共用）。</summary>
        public void ApplyLayout()
        {
            slotBasePositions = null;
            if (layout == null) return;
            for (int i = 0; i < characterSlots.Length; i++)
            {
                var slot = layout.GetSlot(i);
                var image = GetSlot(i);
                if (slot != null && image != null)
                    slot.ApplyTo(image.rectTransform);
            }
        }

        /// <summary>记录槽位基准位置 / 缩放，供角色校准在其上叠加。</summary>
        void EnsureSlotBase()
        {
            if (slotBasePositions != null) return;
            slotBasePositions = new Vector2[characterSlots.Length];
            slotBaseScales = new Vector3[characterSlots.Length];
            for (int i = 0; i < characterSlots.Length; i++)
            {
                var image = GetSlot(i);
                if (image == null) continue;
                slotBasePositions[i] = image.rectTransform.anchoredPosition;
                slotBaseScales[i] = image.rectTransform.localScale;
            }
        }

        /// <summary>顺序执行一组演出指令。instant 为 true 时全部瞬时完成（Skip 模式）。</summary>
        public IEnumerator Execute(IReadOnlyList<StageCommand> commands, bool instant = false)
        {
            if (commands == null) yield break;
            for (int i = 0; i < commands.Count; i++)
                yield return Execute(commands[i], instant);
        }

        public IEnumerator Execute(StageCommand command, bool instant = false)
        {
            switch (command)
            {
                case ShowCharacterCommand show:
                    yield return ShowCharacter(show, instant);
                    break;
                case HideCharacterCommand hide:
                    yield return HideCharacter(hide, instant);
                    break;
                case SetBackgroundCommand bg:
                    yield return SetBackground(bg, instant);
                    break;
                case PlayBgmCommand bgm:
                    yield return CrossFadeBgm(bgm.clip, bgm.loop, instant ? 0f : bgm.fadeSeconds);
                    break;
                case StopBgmCommand stop:
                    yield return CrossFadeBgm(null, false, instant ? 0f : stop.fadeSeconds);
                    break;
                case PlaySfxCommand sfx:
                    if (!instant && sfxSource != null && sfx.clip != null)
                        sfxSource.PlayOneShot(sfx.clip, sfx.volume);
                    break;
                case WaitCommand wait:
                    if (!instant) yield return new WaitForSeconds(wait.seconds);
                    break;
            }
        }

        // ---------------- 立绘 ----------------

        IEnumerator ShowCharacter(ShowCharacterCommand cmd, bool instant)
        {
            if (cmd.character == null) yield break;
            int slotIndex = (int)cmd.slot;
            var image = GetSlot(slotIndex);
            if (image == null) yield break;

            // 同一角色已在其他槽位时先移除
            for (int i = 0; i < slotOccupants.Length; i++)
            {
                if (i != slotIndex && slotOccupants[i] == cmd.character)
                    ClearSlot(i);
            }

            EnsureSlotBase();
            slotOccupants[slotIndex] = cmd.character;
            image.sprite = cmd.character.GetExpression(cmd.expression);
            image.preserveAspect = true;
            image.rectTransform.anchoredPosition = slotBasePositions[slotIndex] + cmd.character.spriteOffset;
            image.rectTransform.localScale = slotBaseScales[slotIndex] * cmd.character.spriteScale;
            image.gameObject.SetActive(true);

            float duration = (instant || cmd.transition == StageTransition.Cut) ? 0f : cmd.duration;
            yield return FadeImageAlpha(image, 0f, 1f, duration);
        }

        IEnumerator HideCharacter(HideCharacterCommand cmd, bool instant)
        {
            float duration = (instant || cmd.transition == StageTransition.Cut) ? 0f : cmd.duration;

            // character 为 null 表示清空全部立绘
            for (int i = 0; i < slotOccupants.Length; i++)
            {
                if (slotOccupants[i] == null) continue;
                if (cmd.character != null && slotOccupants[i] != cmd.character) continue;

                var image = GetSlot(i);
                if (image != null && image.gameObject.activeSelf)
                    yield return FadeImageAlpha(image, image.color.a, 0f, duration);
                ClearSlot(i);
            }
        }

        /// <summary>角色在台上时直接切换表情（说话时自动调用）。</summary>
        public void SetExpression(CharacterAsset character, string expression)
        {
            if (character == null) return;
            for (int i = 0; i < slotOccupants.Length; i++)
            {
                if (slotOccupants[i] != character) continue;
                var sprite = character.GetExpression(expression);
                var image = GetSlot(i);
                if (image != null && sprite != null)
                    image.sprite = sprite;
            }
        }

        /// <summary>高亮说话角色并压暗其他立绘；传 null（旁白）恢复全部正常亮度。</summary>
        public void HighlightSpeaker(CharacterAsset speaker)
        {
            for (int i = 0; i < slotOccupants.Length; i++)
            {
                if (slotOccupants[i] == null) continue;
                var image = GetSlot(i);
                if (image == null) continue;

                float brightness = (speaker == null || slotOccupants[i] == speaker) ? 1f : dimBrightness;
                var c = image.color;
                image.color = new Color(brightness, brightness, brightness, c.a);
            }
        }

        Image GetSlot(int index)
        {
            return (characterSlots != null && index >= 0 && index < characterSlots.Length)
                ? characterSlots[index]
                : null;
        }

        void ClearSlot(int index)
        {
            slotOccupants[index] = null;
            var image = GetSlot(index);
            if (image != null)
            {
                image.gameObject.SetActive(false);
                var c = image.color;
                image.color = new Color(1f, 1f, 1f, c.a);
            }
        }

        // ---------------- 背景 ----------------

        IEnumerator SetBackground(SetBackgroundCommand cmd, bool instant)
        {
            if (backgroundA == null || backgroundB == null) yield break;
            float duration = (instant || cmd.transition == StageTransition.Cut) ? 0f : cmd.duration;

            // B 层在 A 层之上：换到 B 时 B 淡入，换到 A 时 A 直接置满、B 淡出
            bool currentIsB = backgroundB.color.a > 0.5f && backgroundB.gameObject.activeSelf;
            if (!currentIsB)
            {
                backgroundB.sprite = cmd.background;
                backgroundB.gameObject.SetActive(true);
                yield return FadeImageAlpha(backgroundB, 0f, 1f, duration);
            }
            else
            {
                backgroundA.sprite = cmd.background;
                backgroundA.gameObject.SetActive(true);
                SetImageAlpha(backgroundA, 1f);
                yield return FadeImageAlpha(backgroundB, 1f, 0f, duration);
            }
        }

        // ---------------- 音频 ----------------

        IEnumerator CrossFadeBgm(AudioClip clip, bool loop, float fadeSeconds)
        {
            var from = bgmUseB ? bgmSourceB : bgmSourceA;
            var to = bgmUseB ? bgmSourceA : bgmSourceB;
            if (from == null || to == null) yield break;

            if (clip != null)
            {
                bgmUseB = !bgmUseB;
                to.clip = clip;
                to.loop = loop;
                to.volume = 0f;
                to.Play();
            }

            if (fadeSeconds <= 0f)
            {
                if (clip != null) to.volume = 1f;
                from.Stop();
                from.volume = 0f;
                yield break;
            }

            float t = 0f;
            float fromStart = from.volume;
            while (t < fadeSeconds)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / fadeSeconds);
                from.volume = Mathf.Lerp(fromStart, 0f, k);
                if (clip != null) to.volume = k;
                yield return null;
            }
            from.Stop();
        }

        public void PlayVoice(AudioClip clip)
        {
            if (voiceSource == null) return;
            voiceSource.Stop();
            if (clip != null)
            {
                voiceSource.clip = clip;
                voiceSource.Play();
            }
        }

        // ---------------- 工具 ----------------

        static void SetImageAlpha(Image image, float alpha)
        {
            var c = image.color;
            image.color = new Color(c.r, c.g, c.b, alpha);
        }

        static IEnumerator FadeImageAlpha(Image image, float from, float to, float duration)
        {
            if (duration <= 0f)
            {
                SetImageAlpha(image, to);
                yield break;
            }

            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                SetImageAlpha(image, Mathf.Lerp(from, to, Mathf.Clamp01(t / duration)));
                yield return null;
            }
            SetImageAlpha(image, to);
        }
    }
}
