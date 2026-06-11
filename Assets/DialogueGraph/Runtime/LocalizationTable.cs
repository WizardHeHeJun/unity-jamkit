using System;
using System.Collections.Generic;
using UnityEngine;

namespace DialogueGraph
{
    [CreateAssetMenu(fileName = "NewLocalizationTable", menuName = "剧情/本地化表 (Localization Table)")]
    public class LocalizationTable : ScriptableObject
    {
        [Tooltip("语言代码列表，例如 zh-CN / en-US。每条文本的译文按相同顺序排列")]
        public List<string> languages = new List<string> { "zh-CN" };

        [Serializable]
        public class Entry
        {
            public string key;
            public List<string> texts = new List<string>();
        }

        public List<Entry> entries = new List<Entry>();

        /// <summary>找不到对应译文时返回 null，调用方应回退到 fallbackText。</summary>
        public string GetText(string key, string language)
        {
            if (string.IsNullOrEmpty(key)) return null;
            int langIndex = languages.IndexOf(language);
            if (langIndex < 0) return null;

            var entry = entries.Find(e => e.key == key);
            if (entry == null || langIndex >= entry.texts.Count) return null;

            var text = entry.texts[langIndex];
            return string.IsNullOrEmpty(text) ? null : text;
        }
    }
}
