using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace AudioKit.Editor
{
    /// <summary>
    /// 音频库 Inspector：顶部叠加校验（空 ID / 重名 / 缺片段），逐条紧凑编辑，
    /// 每条带「试听 ▶」按钮——不进 Play Mode 就能在编辑器里听，给美术 / 音频核对用。
    /// </summary>
    [CustomEditor(typeof(AudioLibrary))]
    public class AudioLibraryEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var lib = (AudioLibrary)target;
            serializedObject.Update();

            var problems = CollectProblems(lib);
            if (problems.Count > 0)
                EditorGUILayout.HelpBox(string.Join("\n", problems), MessageType.Warning);
            else
                EditorGUILayout.HelpBox($"共 {lib.entries.Count} 条音频。代码 / 导演编排通过 ID 播放，下方 ▶ 可直接试听。",
                    MessageType.Info);

            var listProp = serializedObject.FindProperty("entries");
            for (int i = 0; i < listProp.arraySize; i++)
                DrawEntry(listProp, i);

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("+ 添加音频"))
                    listProp.InsertArrayElementAtIndex(listProp.arraySize);
                if (GUILayout.Button("■ 停止试听", GUILayout.Width(90)))
                    StopPreview();
            }

            serializedObject.ApplyModifiedProperties();
        }

        void DrawEntry(SerializedProperty listProp, int index)
        {
            var entry = listProp.GetArrayElementAtIndex(index);
            var idProp = entry.FindPropertyRelative("id");
            var clipProp = entry.FindPropertyRelative("clip");
            var busProp = entry.FindPropertyRelative("bus");
            var volumeProp = entry.FindPropertyRelative("volume");
            var loopProp = entry.FindPropertyRelative("loop");
            var pitchMinProp = entry.FindPropertyRelative("pitchMin");
            var pitchMaxProp = entry.FindPropertyRelative("pitchMax");

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var clip = clipProp.objectReferenceValue as AudioClip;
                    using (new EditorGUI.DisabledScope(clip == null))
                    {
                        if (GUILayout.Button("▶", GUILayout.Width(28)))
                            PlayPreview(clip, loopProp.boolValue);
                    }
                    EditorGUILayout.PropertyField(idProp, GUIContent.none);
                    EditorGUILayout.PropertyField(busProp, GUIContent.none, GUILayout.Width(90));
                    if (GUILayout.Button("✕", GUILayout.Width(24)))
                    {
                        listProp.DeleteArrayElementAtIndex(index);
                        return;
                    }
                }
                EditorGUILayout.PropertyField(clipProp);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(volumeProp, GUIContent.none);
                    loopProp.boolValue = GUILayout.Toggle(loopProp.boolValue, "循环", GUILayout.Width(50));
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PrefixLabel("随机音调");
                    EditorGUILayout.PropertyField(pitchMinProp, GUIContent.none);
                    EditorGUILayout.PropertyField(pitchMaxProp, GUIContent.none);
                }
            }
        }

        static List<string> CollectProblems(AudioLibrary lib)
        {
            var problems = new List<string>();
            if (lib.entries.Any(e => e == null || string.IsNullOrEmpty(e.id)))
                problems.Add("存在未命名（空 ID）的音频条目");
            if (lib.entries.Any(e => e != null && !string.IsNullOrEmpty(e.id) && e.clip == null))
                problems.Add("有条目缺少音频片段（clip）");

            var duplicates = lib.entries
                .Where(e => e != null && !string.IsNullOrEmpty(e.id))
                .GroupBy(e => e.id)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key);
            foreach (var id in duplicates)
                problems.Add($"ID「{id}」重名，按 ID 播放时行为不可预期");

            return problems;
        }

        // ---------- 编辑器内试听（反射调用内部 AudioUtil，兼容 Unity 2022.3）----------

        static void PlayPreview(AudioClip clip, bool loop)
        {
            if (clip == null) return;
            StopPreview();
            var audioUtil = GetAudioUtil();
            var play = audioUtil?.GetMethod("PlayPreviewClip",
                new[] { typeof(AudioClip), typeof(int), typeof(bool) });
            play?.Invoke(null, new object[] { clip, 0, loop });
        }

        static void StopPreview()
        {
            var audioUtil = GetAudioUtil();
            var stop = audioUtil?.GetMethod("StopAllPreviewClips", BindingFlags.Static | BindingFlags.Public);
            stop?.Invoke(null, null);
        }

        static System.Type GetAudioUtil()
        {
            return typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
        }
    }
}
