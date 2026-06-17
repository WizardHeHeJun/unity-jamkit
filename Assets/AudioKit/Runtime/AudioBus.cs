namespace AudioKit
{
    /// <summary>
    /// 音频总线：每个音频条目归属一条总线，运行时各总线有独立音量，统一再受 Master 叠乘。
    /// 设置面板里通常给每条总线一个滑条（音乐 / 音效 / 界面 / 环境 / 语音），玩家各自调节。
    /// </summary>
    public enum AudioBus
    {
        /// <summary>背景音乐（BGM），走交叉淡入淡出通道。</summary>
        Music,

        /// <summary>音效（脚步、打击、爆炸……）。</summary>
        Sfx,

        /// <summary>界面音（按钮、提示、翻页）。</summary>
        Ui,

        /// <summary>环境音（风声、雨声、人群）。</summary>
        Ambience,

        /// <summary>语音 / 配音。</summary>
        Voice
    }
}
