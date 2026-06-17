using UnityEngine;

namespace AudioKit
{
    /// <summary>
    /// 演示用控制器（占位）：用 OnGUI 摆几个按钮和滑条，进 Play 后即可试播一键示例生成的音效 / BGM、
    /// 拖动音量滑条听总线效果。正式项目用自己的 UI，按钮里调 <see cref="AudioDirector"/> 即可。
    /// </summary>
    public class AudioKitDemoController : MonoBehaviour
    {
        [Tooltip("示例库里的音效 ID（与一键示例生成的一致）")]
        public string[] sfxIds = { "jump", "coin", "click", "hit" };

        [Tooltip("示例库里的 BGM ID")]
        public string bgmId = "bgm";

        void OnGUI()
        {
            var dir = AudioDirector.Instance;
            const int w = 260;
            GUILayout.BeginArea(new Rect(16, 16, w, 460), GUI.skin.box);
            GUILayout.Label("<b>AudioKit 演示</b>", new GUIStyle(GUI.skin.label) { richText = true, fontSize = 14 });

            if (dir == null)
            {
                GUILayout.Label("场景里没有 AudioDirector。");
                GUILayout.EndArea();
                return;
            }

            GUILayout.Space(6);
            GUILayout.Label("音效：");
            foreach (var id in sfxIds)
            {
                if (GUILayout.Button($"播放音效  {id}"))
                    dir.PlaySfx(id);
            }

            GUILayout.Space(6);
            GUILayout.Label("背景音乐：");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("播放 BGM")) dir.PlayMusic(bgmId);
            if (GUILayout.Button("停止 BGM")) dir.StopMusic();
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.Label($"Master 总音量：{dir.GetMasterVolume():0.00}");
            dir.SetMasterVolume(GUILayout.HorizontalSlider(dir.GetMasterVolume(), 0f, 1f));

            GUILayout.Label($"音乐总线：{dir.GetBusVolume(AudioBus.Music):0.00}");
            dir.SetBusVolume(AudioBus.Music, GUILayout.HorizontalSlider(dir.GetBusVolume(AudioBus.Music), 0f, 1f));

            GUILayout.Label($"音效总线：{dir.GetBusVolume(AudioBus.Sfx):0.00}");
            dir.SetBusVolume(AudioBus.Sfx, GUILayout.HorizontalSlider(dir.GetBusVolume(AudioBus.Sfx), 0f, 1f));

            GUILayout.EndArea();
        }
    }
}
