# AudioKit — 全局音频工具

面向**非程序人员**的 Unity 音频工具：美术 / 音频登记一份音频库（BGM 与音效），程序与导演编排只用 ID 播放。
适用于 Unity 2022.3，无第三方依赖。与 `DialogueGraph` / `GameplayKit` / `PlatformerKit` 同范式：资产驱动、下拉不手填、Inspector 直接试听、一键示例。

| 模块 | 入口 | 说明 |
|---|---|---|
| 音频库 | `Create → 音频 → 音频库 (Audio Library)` | 登记每条音频：ID / 片段 / 总线 / 音量 / 循环 / 随机音调；Inspector 里 ▶ 直接试听、保存即校验 |
| 音频管理器 | `AudioDirector` 组件 | 单例常驻：按 ID 播 BGM（双源交叉淡入淡出）/ 音效（AudioSource 池、随机音调）、各总线音量（自动存读 PlayerPrefs） |
| 一键示例 | `Tools → 音频工具 → 创建音频示例` | 程序合成占位音效 + 循环 BGM，建好库并放好场景，进 Play 即可试听调音量 |

## 目录结构

```
Assets/AudioKit/
├── Runtime/
│   ├── AudioBus.cs                # 总线枚举（音乐/音效/界面/环境/语音）
│   ├── AudioLibrary.cs            # 音频库资产（ScriptableObject）+ 单条 AudioEntry
│   ├── AudioDirector.cs           # 运行时管理器（单例，BGM 淡入淡出 / SFX 池 / 总线音量）
│   └── AudioKitDemoController.cs  # 演示控制器（OnGUI 试听按钮，占位）
└── Editor/
    ├── AudioLibraryEditor.cs      # 库 Inspector：校验 + 逐条编辑 + ▶ 试听
    └── AudioKitDemoBuilder.cs     # 一键示例（含占位音频合成 + WAV 写出）
```

## 快速开始

1. 把 `Assets/AudioKit` 拷进 Unity 2022.3 工程。
2. `Tools → 音频工具 → 创建音频示例`，点 Play：左上角按钮试听音效 / BGM，拖滑条调 Master 与各总线音量。
3. 自己的音频：
   - `Create → 音频 → 音频库`，逐条登记 ID / 拖入片段 / 选总线，Inspector 里 ▶ 试听核对；
   - 场景里建一个空物体挂 `AudioDirector`，把库拖到 `library` 字段（跨场景常驻默认开）。

## 总线与音量

每条音频归属一条**总线**（音乐 / 音效 / 界面 / 环境 / 语音）。某条音频的最终音量 =
`条目音量 × 所属总线音量 × Master`。总线音量与 Master 通过 `AudioDirector` 调节并自动存进 PlayerPrefs，
正好对接设置面板的一排滑条：

```csharp
// 设置面板里：滑条值变化时调用
AudioDirector.Instance.SetMasterVolume(v);
AudioDirector.Instance.SetBusVolume(AudioBus.Music, v);
AudioDirector.Instance.SetBusVolume(AudioBus.Sfx, v);

// 读回当前值给滑条初始化（启动时已自动从 PlayerPrefs 载入）
float music = AudioDirector.Instance.GetBusVolume(AudioBus.Music);
```

## 运行时接入（给程序）

```csharp
using AudioKit;

// 任意脚本里（场景中已放 AudioDirector）：
AudioDirector.Instance.PlaySfx("jump");      // 一行播音效（按库里 ID）
AudioDirector.Instance.PlayMusic("battle");  // 切 BGM，默认 1s 交叉淡入
AudioDirector.Instance.PlayMusic("town", 2f);// 指定淡入时长
AudioDirector.Instance.StopMusic();          // 淡出停止

// 嫌取 Instance 麻烦时用静态便捷（没有 AudioDirector 时静默，不报错）：
AudioDirector.Sfx("coin");
AudioDirector.Bgm("title");
```

- **音效池**：`PlaySfx` 自动复用空闲 `AudioSource`，不够时扩容，重复音效不互相打断。
- **随机音调**：条目里把 `音调下限/上限` 设成不等（如 0.95~1.05），重复音效自动轻微变调，不腻。
- **暂停友好**：BGM 淡入淡出用 `unscaledDeltaTime`，`Time.timeScale = 0`（暂停菜单）时仍正常淡入淡出。

## 与其他模块联动

- **DialogueGraph**：剧情演出里已有 BGM / 音效指令；想统一走 AudioKit 时，在事件节点抛 `(播放音效, id)`，
  游戏侧 `player.OnGameEvent += (n, p) => { if (n == "播放音效") AudioDirector.Sfx(p); }`。
- **GameplayKit / Director**：导演规则的「抛游戏事件」动作约定一个事件名（如 `播放音效`），
  在 `director.OnGameEvent` 里转给 `AudioDirector.Sfx(param)`，策划即可在编排里配音效，无需程序改代码。
- **PlatformerKit**：拾取 / 受伤 / 通关回调里 `AudioDirector.Sfx("coin")` 即可。

## 扩展指南

- **新增总线**：`AudioBus` 加枚举值即可，`AudioDirector` 自动为其维护音量并存读 PlayerPrefs。
- **3D 音效**：当前演示按 2D（`spatialBlend = 0`）。需要空间音时，给 `PlaySfx` 重载传世界坐标并设 `spatialBlend = 1`。
- **音频混响 / 混音器**：要接 `AudioMixer` 时，把各 `AudioSource` 路由到对应 Mixer Group，
  总线音量改为驱动 Mixer 的 exposed 参数即可（接口不变）。

## 注意事项

- 音频 ID 是代码与编排的引用键，发布后不要改；库内重复 ID 会被 Inspector 校验拦下。
- 一键示例的音效为程序合成的占位 beep，仅供跑通流程，正式项目请替换为真实素材。
- `AudioDirector` 默认 `DontDestroyOnLoad` 跨场景常驻；做单场景 demo 可在 Inspector 关掉 `persistAcrossScenes`。
- 编辑器内 ▶ 试听通过反射调用 Unity 内部 `AudioUtil`，仅编辑器可用、与 Play Mode 无关；若 Unity 版本差异导致试听失效，不影响运行时播放。
