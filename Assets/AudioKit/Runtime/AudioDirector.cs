using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AudioKit
{
    /// <summary>
    /// 全局音频管理器：持有一份 <see cref="AudioLibrary"/>，按 ID 播放 BGM（双源交叉淡入淡出）
    /// 与音效（AudioSource 池，可随机音调），并维护各总线音量（自动存读 PlayerPrefs，接设置面板）。
    /// 单例常驻，场景里放一个即可；其他脚本用 <see cref="Instance"/> 或静态便捷方法播放。
    /// </summary>
    public class AudioDirector : MonoBehaviour
    {
        /// <summary>当前实例（场景里放了 AudioDirector 后可用）。</summary>
        public static AudioDirector Instance { get; private set; }

        [Tooltip("音频库资产，登记所有 BGM 与音效")]
        public AudioLibrary library;

        [Tooltip("跨场景常驻（DontDestroyOnLoad）。做单场景 demo 可关掉")]
        public bool persistAcrossScenes = true;

        [Tooltip("启动时从 PlayerPrefs 读回上次的音量设置")]
        public bool loadVolumeOnAwake = true;

        const string PrefPrefix = "AudioKit.";

        AudioSource musicA;
        AudioSource musicB;
        bool activeIsA;
        AudioEntry currentMusic;
        Coroutine musicFade;

        readonly List<AudioSource> sfxSources = new List<AudioSource>();

        float masterVolume = 1f;
        readonly Dictionary<AudioBus, float> busVolumes = new Dictionary<AudioBus, float>();

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (persistAcrossScenes)
                DontDestroyOnLoad(gameObject);

            foreach (AudioBus bus in Enum.GetValues(typeof(AudioBus)))
                busVolumes[bus] = 1f;

            musicA = CreateSource("Music A", true);
            musicB = CreateSource("Music B", true);

            if (loadVolumeOnAwake)
                LoadVolumePrefs();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        AudioSource CreateSource(string label, bool loop)
        {
            var go = new GameObject(label);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = loop;
            src.spatialBlend = 0f; // 2D
            return src;
        }

        // ---------- BGM ----------

        /// <summary>播放 BGM（按库里 ID）。与当前同曲则忽略；fadeSeconds≤0 立即切换。</summary>
        public void PlayMusic(string id, float fadeSeconds = 1f)
        {
            var entry = library != null ? library.Find(id) : null;
            if (entry == null || entry.clip == null)
            {
                Debug.LogWarning($"[AudioKit] 找不到 BGM「{id}」或其片段为空。", this);
                return;
            }
            if (currentMusic != null && currentMusic.id == entry.id && ActiveMusic().isPlaying)
                return;

            currentMusic = entry;
            var from = ActiveMusic();
            var to = activeIsA ? musicB : musicA;
            activeIsA = !activeIsA;

            to.clip = entry.clip;
            to.loop = entry.loop;
            to.pitch = 1f;
            float target = MusicVolume(entry);

            if (fadeSeconds <= 0f || !isActiveAndEnabled)
            {
                if (musicFade != null) { StopCoroutine(musicFade); musicFade = null; }
                from.Stop();
                from.volume = 0f;
                to.volume = target;
                to.Play();
                return;
            }

            to.volume = 0f;
            to.Play();
            if (musicFade != null) StopCoroutine(musicFade);
            musicFade = StartCoroutine(FadeMusic(from, to, target, fadeSeconds));
        }

        /// <summary>停止 BGM（淡出）。</summary>
        public void StopMusic(float fadeSeconds = 1f)
        {
            currentMusic = null;
            var active = ActiveMusic();
            if (fadeSeconds <= 0f || !isActiveAndEnabled)
            {
                if (musicFade != null) { StopCoroutine(musicFade); musicFade = null; }
                active.Stop();
                active.volume = 0f;
                return;
            }
            if (musicFade != null) StopCoroutine(musicFade);
            musicFade = StartCoroutine(FadeMusic(active, null, 0f, fadeSeconds));
        }

        IEnumerator FadeMusic(AudioSource from, AudioSource to, float toVolume, float seconds)
        {
            float t = 0f;
            float fromStart = from != null ? from.volume : 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime; // 暂停（timeScale=0）时仍能淡入淡出
                float k = Mathf.Clamp01(t / seconds);
                if (from != null) from.volume = Mathf.Lerp(fromStart, 0f, k);
                if (to != null) to.volume = Mathf.Lerp(0f, toVolume, k);
                yield return null;
            }
            if (from != null) { from.Stop(); from.volume = 0f; }
            if (to != null) to.volume = toVolume;
            musicFade = null;
        }

        AudioSource ActiveMusic() => activeIsA ? musicA : musicB;

        float MusicVolume(AudioEntry entry) => entry.volume * GetBusVolume(AudioBus.Music) * masterVolume;

        // ---------- 音效 ----------

        /// <summary>播放音效（按库里 ID），返回所用的 AudioSource（一般无需理会）。</summary>
        public AudioSource PlaySfx(string id)
        {
            var entry = library != null ? library.Find(id) : null;
            if (entry == null || entry.clip == null)
            {
                Debug.LogWarning($"[AudioKit] 找不到音效「{id}」或其片段为空。", this);
                return null;
            }
            return PlaySfx(entry);
        }

        /// <summary>播放一条已取到的音频（按其总线音量与随机音调）。</summary>
        public AudioSource PlaySfx(AudioEntry entry)
        {
            if (entry == null || entry.clip == null) return null;
            var src = GetFreeSfxSource();
            src.clip = entry.clip;
            src.loop = false;
            src.pitch = entry.pitchMax > entry.pitchMin
                ? UnityEngine.Random.Range(entry.pitchMin, entry.pitchMax)
                : entry.pitchMin;
            src.volume = entry.volume * GetBusVolume(entry.bus) * masterVolume;
            src.Play();
            return src;
        }

        AudioSource GetFreeSfxSource()
        {
            for (int i = 0; i < sfxSources.Count; i++)
            {
                if (!sfxSources[i].isPlaying)
                    return sfxSources[i];
            }
            var src = CreateSource($"SFX {sfxSources.Count}", false);
            sfxSources.Add(src);
            return src;
        }

        // ---------- 音量（接设置面板）----------

        public float GetMasterVolume() => masterVolume;

        public void SetMasterVolume(float value)
        {
            masterVolume = Mathf.Clamp01(value);
            ApplyMusicVolume();
            SavePref("Master", masterVolume);
        }

        public float GetBusVolume(AudioBus bus) => busVolumes.TryGetValue(bus, out var v) ? v : 1f;

        public void SetBusVolume(AudioBus bus, float value)
        {
            busVolumes[bus] = Mathf.Clamp01(value);
            ApplyMusicVolume();
            SavePref("Bus." + bus, busVolumes[bus]);
        }

        void ApplyMusicVolume()
        {
            // 正在淡入淡出时不打断，由淡入目标音量决定；稳定播放时实时跟随滑条。
            if (currentMusic != null && musicFade == null)
            {
                var active = ActiveMusic();
                if (active.isPlaying)
                    active.volume = MusicVolume(currentMusic);
            }
        }

        void LoadVolumePrefs()
        {
            masterVolume = PlayerPrefs.GetFloat(PrefPrefix + "Master", 1f);
            foreach (AudioBus bus in Enum.GetValues(typeof(AudioBus)))
                busVolumes[bus] = PlayerPrefs.GetFloat(PrefPrefix + "Bus." + bus, 1f);
        }

        void SavePref(string key, float value)
        {
            PlayerPrefs.SetFloat(PrefPrefix + key, value);
        }

        // ---------- 静态便捷（Instance 不存在时静默，方便随手调用）----------

        /// <summary>播放音效，没有 AudioDirector 时静默无副作用。</summary>
        public static void Sfx(string id)
        {
            if (Instance != null) Instance.PlaySfx(id);
        }

        /// <summary>播放 BGM，没有 AudioDirector 时静默无副作用。</summary>
        public static void Bgm(string id, float fadeSeconds = 1f)
        {
            if (Instance != null) Instance.PlayMusic(id, fadeSeconds);
        }
    }
}
