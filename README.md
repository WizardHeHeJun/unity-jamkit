# unity-jamkit — Game Jam 通用工具集

为 Game Jam 准备的 Unity 工具集：**剧情对话、任务系统、事件编排、平台跳跃关卡**，开箱即用。
适用于 **Unity 2022.3**，除 TextMeshPro（仅剧情模块的演示 UI 使用）外无任何第三方依赖。

所有工具遵循同一套范式，目标是让策划 / 美术在 Jam 的 48 小时里不等程序：

- **面向非程序人员**：能下拉选择的绝不手填，资产驱动，改完保存即生效；
- **保存即校验**：断头连线、缺出生点、引用失效 ID 等当场报出来，问题可定位；
- **不进 Play Mode 验证**：剧情文字试跑、任务/事件模拟器、关卡画布所见即所得；
- **一键示例**：每个模块都有菜单一键生成可运行的完整示例，照着改最快。

## 模块总览

| 模块 | 一句话 | 一键示例入口 |
|---|---|---|
| [DialogueGraph](Assets/DialogueGraph/README.md) | 节点式剧情编辑器 + 视觉小说运行时（立绘演出 / 选项分支 / 本地化 / 存档），含给美术的舞台布景工具 | `Tools → 剧情工具 → 创建VN演示场景` |
| [GameplayKit](Assets/GameplayKit/README.md) | 黑板变量底座 + 节点式任务链 + 「当 X 发生时做 Y」导演编排，三者互通 | `Tools → 玩法工具 → 创建任务系统示例` / `创建导演编排示例` |
| [PlatformerKit](Assets/PlatformerKit/README.md) | 平台跳跃关卡编辑器（网格画布绘制）+ 运行时一键搭建 + 演示控制器 | `Tools → 关卡工具 → 创建平台跳跃示例` |

模块之间可以联动也可以单独拆走：对话事件 / 对话结束可以触发导演编排规则，导演规则可以开对话、推任务；
关卡的收集 / 通关事件接到黑板或任务上报即可。每个模块目录都是自包含的（含 asmdef），
只想要其中一个就只拷那个文件夹（GameplayKit 的 Director 依赖 Core、Quest 与 DialogueGraph）。

## 快速开始

1. 新建或打开 Unity 2022.3 工程，把 `Assets/` 下需要的模块文件夹拷进去；
   用 DialogueGraph 的话先导入 TMP：`Window → TextMeshPro → Import TMP Essential Resources`。
2. 跑上表任意一个「一键示例」，点 Play 体验完整流程。
3. 各模块的详细用法（给策划 / 给美术 / 给程序）见各自目录的 README。

## Jam 场景速查

- **做视觉小说 / 剧情向**：DialogueGraph（+ 舞台布景给美术摆位）；
- **做有任务 / 解锁链的玩法**：GameplayKit 的黑板 + Quest，模拟器里就能验数值门槛；
- **剧情和玩法要联动**（打完 BOSS 开对话、对话完给任务）：加 Director，策划自己连，不用程序写胶水；
- **做平台跳跃**：PlatformerKit，美术先填图块集，策划开画，控制器先用演示的后面再换手感。

## 协作约定

- 工程请开启 `Edit → Project Settings → Editor → Asset Serialization = Force Text`，否则多人改同一资产没法合并。
- 各资产里的 ID（任务 ID、图块 ID、Loc Key、事件名……）是存档与引用的键，定了就别改。

## License

MIT
