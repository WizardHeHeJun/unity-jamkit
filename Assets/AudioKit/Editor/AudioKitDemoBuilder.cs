using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AudioKit.Editor
{
    /// <summary>
    /// 一键生成可玩的音频示例：程序合成几个占位音效与一段循环 BGM（无需外部素材），
    /// 建好音频库，并在当前场景放入 AudioDirector + 演示控制器。进 Play 即可点按钮试听、拖滑条调音量。
    /// </summary>
    public static class AudioKitDemoBuilder
    {
        const string Folder = "Assets/AudioKitDemo";
        const int SampleRate = 44100;

        [MenuItem("Tools/音频工具/创建音频示例")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets", "AudioKitDemo");

            // 1) 合成占位音频 → wav 文件
            WriteWav($"{Folder}/jump.wav", Beep(660f, 0.18f, 9f));
            WriteWav($"{Folder}/coin.wav", TwoTone(880f, 1320f, 0.09f));
            WriteWav($"{Folder}/click.wav", Beep(1000f, 0.06f, 18f));
            WriteWav($"{Folder}/hit.wav", Beep(150f, 0.22f, 12f));
            WriteWav($"{Folder}/bgm.wav", Melody());
            AssetDatabase.Refresh();

            // 2) 建音频库
            var lib = ScriptableObject.CreateInstance<AudioLibrary>();
            lib.entries.Add(MakeEntry("jump", AudioBus.Sfx, 0.9f, false, 0.97f, 1.05f));
            lib.entries.Add(MakeEntry("coin", AudioBus.Sfx, 0.8f, false, 1f, 1.08f));
            lib.entries.Add(MakeEntry("click", AudioBus.Ui, 0.7f, false, 1f, 1f));
            lib.entries.Add(MakeEntry("hit", AudioBus.Sfx, 1f, false, 0.92f, 1f));
            lib.entries.Add(MakeEntry("bgm", AudioBus.Music, 0.6f, true, 1f, 1f));
            AssetDatabase.CreateAsset(lib, $"{Folder}/示例音频库.asset");
            AssetDatabase.SaveAssets();

            // 3) 场景里放 AudioDirector + 演示控制器
            var existing = Object.FindObjectOfType<AudioDirector>();
            if (existing == null)
            {
                var dirGo = new GameObject("AudioDirector");
                var dir = dirGo.AddComponent<AudioDirector>();
                dir.library = lib;
                dir.persistAcrossScenes = false; // 单场景 demo
                Undo.RegisterCreatedObjectUndo(dirGo, "Create AudioDirector");
            }
            else if (existing.library == null)
            {
                existing.library = lib;
            }

            if (Object.FindObjectOfType<AudioKitDemoController>() == null)
            {
                var demoGo = new GameObject("AudioKitDemo");
                demoGo.AddComponent<AudioKitDemoController>();
                Undo.RegisterCreatedObjectUndo(demoGo, "Create AudioKitDemo");
            }

            Selection.activeObject = lib;
            EditorGUIUtility.PingObject(lib);
            Debug.Log("音频示例已生成：Assets/AudioKitDemo。点 Play 后用左上角按钮试听音效 / BGM，拖滑条调音量。");
        }

        static AudioEntry MakeEntry(string id, AudioBus bus, float volume, bool loop, float pitchMin, float pitchMax)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Folder}/{id}.wav");
            return new AudioEntry
            {
                id = id,
                clip = clip,
                bus = bus,
                volume = volume,
                loop = loop,
                pitchMin = pitchMin,
                pitchMax = pitchMax
            };
        }

        // ---------- 占位音频合成 ----------

        /// <summary>带指数衰减的单音 beep。</summary>
        static float[] Beep(float freq, float duration, float decay)
        {
            int n = (int)(SampleRate * duration);
            var s = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Exp(-decay * t);
                s[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.6f;
            }
            return s;
        }

        /// <summary>两段音连奏（拾取「叮咚」上扬感）。</summary>
        static float[] TwoTone(float freqA, float freqB, float each)
        {
            var a = Beep(freqA, each, 10f);
            var b = Beep(freqB, each, 10f);
            var s = new float[a.Length + b.Length];
            a.CopyTo(s, 0);
            b.CopyTo(s, a.Length);
            return s;
        }

        /// <summary>一段可循环的极简旋律（C-E-G-E），首尾各音加淡入淡出避免爆音。</summary>
        static float[] Melody()
        {
            float[] notes = { 523.25f, 659.25f, 783.99f, 659.25f };
            const float noteDur = 0.32f;
            var samples = new List<float>();
            foreach (var f in notes)
            {
                int n = (int)(SampleRate * noteDur);
                for (int i = 0; i < n; i++)
                {
                    float t = (float)i / SampleRate;
                    float env = Mathf.Min(1f, Mathf.Min(t * 25f, (noteDur - t) * 25f));
                    samples.Add(Mathf.Sin(2f * Mathf.PI * f * t) * env * 0.32f);
                }
            }
            return samples.ToArray();
        }

        // ---------- 16-bit PCM 单声道 WAV 写出 ----------

        static void WriteWav(string path, float[] samples)
        {
            using var fs = new FileStream(path, FileMode.Create);
            using var bw = new BinaryWriter(fs);
            int dataBytes = samples.Length * 2;

            WriteTag(bw, "RIFF");
            bw.Write(36 + dataBytes);
            WriteTag(bw, "WAVE");
            WriteTag(bw, "fmt ");
            bw.Write(16);                 // fmt chunk size
            bw.Write((short)1);           // PCM
            bw.Write((short)1);           // mono
            bw.Write(SampleRate);
            bw.Write(SampleRate * 2);     // byte rate
            bw.Write((short)2);           // block align
            bw.Write((short)16);          // bits per sample
            WriteTag(bw, "data");
            bw.Write(dataBytes);
            foreach (var f in samples)
                bw.Write((short)(Mathf.Clamp(f, -1f, 1f) * short.MaxValue));
        }

        static void WriteTag(BinaryWriter bw, string tag)
        {
            foreach (var c in tag)
                bw.Write((byte)c);
        }
    }
}
