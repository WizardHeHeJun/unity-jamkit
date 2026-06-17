using System;
using System.Collections.Generic;
using UnityEngine;

namespace AudioKit
{
    /// <summary>
    /// 一条音频登记：唯一 ID（代码与编排中通过它播放，下拉选择不手填）、音频片段、
    /// 所属总线、基础音量、是否循环、随机音调范围。
    /// </summary>
    [Serializable]
    public class AudioEntry
    {
        [Tooltip("唯一标识，代码 / 导演编排中通过它播放，定了就别改（存档与引用键）")]
        public string id;

        public AudioClip clip;

        [Tooltip("所属总线，决定受哪个音量滑条控制")]
        public AudioBus bus = AudioBus.Sfx;

        [Range(0f, 1f)]
        [Tooltip("这条音频的基础音量，最终音量 = 此值 × 所属总线音量 × Master")]
        public float volume = 1f;

        [Tooltip("是否循环。BGM / 环境音通常勾上；一次性音效不勾")]
        public bool loop;

        [Tooltip("播放时在 [音调下限, 音调上限] 间随机取音调，两者相等=不随机。轻微随机能让重复音效不腻")]
        public float pitchMin = 1f;

        public float pitchMax = 1f;
    }

    /// <summary>
    /// 音频库：美术 / 音频在这里登记所有 BGM 与音效（ID + 片段 + 总线 + 音量），
    /// 程序与导演编排通过 ID 播放，无需关心具体文件。改完保存即生效，可在 Inspector 直接试听。
    /// 运行时由 <see cref="AudioDirector"/> 持有并播放。
    /// </summary>
    [CreateAssetMenu(fileName = "NewAudioLibrary", menuName = "音频/音频库 (Audio Library)")]
    public class AudioLibrary : ScriptableObject
    {
        public List<AudioEntry> entries = new List<AudioEntry>();

        /// <summary>按 ID 查找条目，找不到返回 null。</summary>
        public AudioEntry Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return entries.Find(e => e != null && e.id == id);
        }

        public bool Has(string id) => Find(id) != null;

        /// <summary>全部 ID（编辑器下拉用）。</summary>
        public List<string> GetIds()
        {
            var ids = new List<string>();
            foreach (var e in entries)
            {
                if (e != null && !string.IsNullOrEmpty(e.id))
                    ids.Add(e.id);
            }
            return ids;
        }

        /// <summary>某条总线下的全部 ID。</summary>
        public List<string> GetIds(AudioBus bus)
        {
            var ids = new List<string>();
            foreach (var e in entries)
            {
                if (e != null && !string.IsNullOrEmpty(e.id) && e.bus == bus)
                    ids.Add(e.id);
            }
            return ids;
        }
    }
}
