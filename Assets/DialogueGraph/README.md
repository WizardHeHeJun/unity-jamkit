# Dialogue Graph — 视觉小说剧情工具链

基于 Unity 内置 GraphView 的节点式剧情编辑器 + 视觉小说运行时框架，适用于 **Unity 2022.3**（在 2022.3.62f3c1 下开发）。运行时 UI 采用 UGUI + TextMeshPro，除 TMP 外无第三方依赖。

> 本仓库还包含两个同范式的工具包：[GameplayKit](../GameplayKit/README.md)（黑板 / 任务系统 / 导演编排）与 [PlatformerKit](../PlatformerKit/README.md)（平台跳跃关卡编辑器），总览见[仓库根 README](../../README.md)。

## 功能总览

| 模块 | 入口 | 说明 |
|---|---|---|
| 对话图编辑器 | 双击对话图资产 / `Tools → 剧情工具 → 对话图编辑器` | 可视化节点编辑：对话、选项、条件、事件、演出、子图 |
| 剧情预览 | `Tools → 剧情工具 → 剧情预览`（或编辑器工具栏「预览」） | 不进 Play Mode 文字试跑，可改变量、可从选中节点开始 |
| 图校验 | 保存时自动 / 工具栏「校验」 | 断头、不可达、空文案、缺表情、缺 Loc Key 等，点击定位节点 |
| 舞台布景 | `Tools → 剧情工具 → 舞台布景` | 给美术：不进 Play Mode 预览背景/立绘摆位，拖拽槽位、校准立绘、一键生成演出指令 |
| 文案 CSV | `Tools → 剧情工具 → 文案导出导入 (CSV)` | 导出给翻译/校对/配音，改完导回 |
| 演示场景 | `Tools → 剧情工具 → 创建VN演示场景` | 一键生成可运行的完整 VN 场景（占位资源） |
| 运行时框架 | `VNPlayer` / `VNStage` / `VNDialogueUI` 组件 | 立绘/背景/音频演出、打字机对话框、选项、Auto/Skip、存档 |

## 目录结构

```
Assets/DialogueGraph/
├── Runtime/
│   ├── DialogueGraphAsset.cs       # 对话图资产（ScriptableObject）
│   ├── NodeData.cs                 # 节点数据（SerializeReference 多态）
│   ├── CharacterAsset.cs           # 角色：显示名/名字颜色/表情差分/立绘校准
│   ├── StageCommands.cs            # 演出指令：立绘/背景/BGM/音效/等待
│   ├── StageLayoutAsset.cs         # 舞台布局：立绘槽位位置/尺寸/缩放
│   ├── LocalizationTable.cs        # 本地化表
│   ├── DialogueVariables.cs        # 变量接口 + 内存实现（含快照）
│   ├── DialogueRunner.cs           # 执行器（纯 C#，含子图栈/存档/已读）
│   └── Presentation/
│       ├── VNStage.cs              # 舞台：背景交叉淡入/立绘槽位/音频
│       ├── VNDialogueUI.cs         # 对话框：打字机/名字板/选项
│       └── VNPlayer.cs             # 门面：串接 Runner 与表现层
└── Editor/
    ├── DialogueGraphWindow.cs      # 编辑器窗口（工具栏/校验面板）
    ├── DialogueGraphView.cs        # GraphView 画布、保存/加载
    ├── NodeViews.cs                # 各节点视图
    ├── StageCommandListView.cs     # 演出指令列表控件
    ├── GraphValidator.cs           # 静态校验规则
    ├── DialoguePreviewWindow.cs    # 编辑器内剧情预览
    ├── VNStageSetupWindow.cs       # 舞台布景窗口（美术摆位/校准/导出指令）
    ├── StageCommandClipboard.cs    # 演出指令剪贴板（布景窗口 → 节点粘贴）
    ├── DialogueCsvTool.cs          # 文案 CSV 导出导入
    ├── VNDemoSceneBuilder.cs       # 演示场景一键搭建
    └── DialogueGraphEditor.uss     # 网格背景样式
```

## 快速开始

1. 把 `Assets/DialogueGraph` 拷进 Unity 2022.3 工程（或直接在本目录创建工程）。
2. 导入 TMP 资源：`Window → TextMeshPro → Import TMP Essential Resources`。
3. 执行 `Tools → 剧情工具 → 创建VN演示场景`，点 Play 即可体验完整流程（演出→对话→选项→子图→条件分支）。
4. 自己的剧情：`Create → 剧情 → 对话图`，双击打开编辑。
5. 美术摆位：`Tools → 剧情工具 → 舞台布景`，拖拽调整立绘站位、校准角色原画，摆好后一键生成演出指令。

## 编辑器使用

- 画布空白处**右键 → 新建节点**；拖动端口连线；`Delete` 删除；滚轮缩放。
- **改完务必「保存」（Ctrl+S）**：字段实时生效，但节点增删与连线在保存时才写入资产。保存时自动校验，问题列表点击可定位节点。
- 工具栏「从选中节点预览」：选中画布上任意节点 → 从该节点开始文字试跑（条件分支用预览窗口左侧的变量表驱动）。

### 节点类型

| 节点 | 行为 |
|---|---|
| 开始 | 入口，每图一个，自动创建不可删除 |
| 对话 | 角色（留空=旁白）+ 表情 + 文案 + 语音 + 台词前演出指令；展示后等 `Continue()` |
| 选项分支 | 可选提示文案 + 任意数量选项（每个选项有稳定 ID 与独立输出口） |
| 条件判断 | 变量比较（布尔/整数/小数/文本），走「满足/不满足」分支，自动执行 |
| 事件触发 | 抛 `(事件名, 参数)` 给游戏侧（给道具、解锁CG等），自动继续 |
| 演出 | 无台词的演出段落（切背景、换BGM、立绘调度），播完后推进 |
| 子图跳转 | 执行子图至其结束节点后返回，继续走「返回后」连线；可嵌套 |
| 结束 | 终点（子图中表示「返回上级图」） |

### 演出指令

挂在对话节点（台词显示前执行）或演出节点上，按顺序执行，支持上移/下移：
**显示立绘**（角色/表情/左中右站位/过渡）、**隐藏立绘**（留空=清场）、**切换背景**、**播放/停止BGM**（交叉淡入）、**播放音效**、**等待**。

逐条手填之外，也可以在舞台布景窗口摆好画面后点「生成演出指令」，回到节点上点「**粘贴布景**」批量填入（见下节）。

### 舞台布景（给美术）

`Tools → 剧情工具 → 舞台布景`，不进 Play Mode 所见即所得地配置场景：

1. **布局**：工具栏指定或「新建布局」舞台布局资产（`Create → 剧情 → 舞台布局`），画布上**拖拽槽位**调整站位，点选后右侧可微调锚点/尺寸/缩放。布局拖到 `VNStage.layout` 字段（或点「应用到场景」）后运行时自动生效，多个场景可复用同一布局。
2. **立绘校准**：右侧给槽位选角色与表情即可预览；选中槽位后调整该角色的**缩放/偏移**，校准保存在角色资产上，一次调好全剧情生效——不同画师、不同尺寸的原画用这个对齐（运行时立绘按保持宽高比显示，不会拉伸变形）。
3. **生成演出指令**：摆好背景与立绘后点「生成演出指令」，再到对话图的对话/演出节点演出指令区点「**粘贴布景**」，即可得到等价的切背景+显示立绘指令序列，无需逐条手填。

布局的锚点是画面比例坐标（0-1，Y=0 为底边，立绘底边中心对齐锚点），尺寸为参考分辨率（默认 1920×1080，须与 CanvasScaler 一致）像素。

### 角色与本地化

- `Create → 剧情 → 角色`：ID、显示名、名字颜色、表情差分列表（第一项为默认表情）、立绘校准（缩放/偏移，建议在舞台布景窗口可视化调整）。节点上选了角色后表情变成下拉框。
- `Create → 剧情 → 本地化表`：`languages` 填语言代码（如 `zh-CN`、`en-US`），把表拖到对话图的 `localizationTable` 字段。文案的 Loc Key 命中时用译文，否则用节点里直接填的文案——**纯中文项目可完全忽略 Key**。
- 批量翻译流程：CSV 工具勾选「自动生成并回写 Loc Key」导出 → 翻译填表 → 导入。

### 文案 CSV（翻译 / 配音）

列：`行ID, 类型, 说话人, LocKey, 原文, [每语言一列]`。行 ID 稳定不变（对话=节点 guid，选项=选项 id），**配音文件直接按行 ID 命名**即可对照；UTF-8 带 BOM，Excel 直接打开。

## 运行时接入

### 用 VNPlayer（推荐）

场景照演示场景的层级搭好（或直接改演示场景），把对话图拖到 `VNPlayer.graph`：

```csharp
public class GameFlow : MonoBehaviour
{
    public VNPlayer player;

    void Start()
    {
        player.Variables.SetInt("好感度", 12);          // 条件节点读这里
        player.OnGameEvent += (name, param) =>          // 事件节点
        {
            if (name == "UnlockCG") UnlockCG(param);
        };
        player.OnDialogueEnded += () => Debug.Log("剧情结束");
    }

    public void Save() => PlayerPrefs.SetString("vn_save", player.CaptureSaveJson());
    public void Load() => player.RestoreFromJson(PlayerPrefs.GetString("vn_save"));
}
```

- **Auto / Skip**：`player.ToggleAuto()` / `ToggleSkip()`（演示场景右上角按钮已接好）。Skip 只快进已读台词（演出瞬时完成、不打字）。
- **存档**：`CaptureSaveJson()` 含当前节点、子图调用栈、变量、已读记录；`RestoreFromJson()` 恢复后会重新触发当前台词/选项重建 UI。

### 只用 DialogueRunner（自己做表现层）

```csharp
var runner = new DialogueRunner { Language = "zh-CN", Variables = myVariableProvider };
runner.OnLine += line => { /* line.speakerName / text / voiceClip / commands / wasRead */ };
runner.OnChoices += prompt => { /* prompt.options[i].text → SelectChoice(i) */ };
runner.OnStage += commands => { /* 播完演出后调 runner.Continue() */ };
runner.OnEvent += (name, param) => { };
runner.OnEnded += () => { };
runner.Begin(graph);            // 或 BeginFrom(graph, nodeGuid)
```

`IDialogueVariableProvider` 可直接实现到你的存档/任务系统上。

## 扩展指南

**新增节点类型**（共 5 处）：
1. `NodeData.cs` 加数据类；2. `NodeViews.cs` 加视图类；3. `DialogueGraphView.cs` 的 `CreateNodeView` switch 与右键菜单各加一行；4. `DialogueRunner.Run` 加执行分支；5.（可选）`GraphValidator` 加校验规则。

**新增演出指令**（共 3 处）：
1. `StageCommands.cs` 加子类；2. `StageCommandListView.cs` 的 `CommandTypes` 注册 + `BuildFields` 加编辑 UI；3. `VNStage.Execute` 加执行分支。

## 注意事项

- 演示用的中文字体由系统「微软雅黑」动态生成（`DialogueGraphDemo/MSYH_Demo_SDF`），正式项目请自制 TMP 中文字体资产并替换。
- 每张图可配独立本地化表；子图执行期间使用子图自己的表。
- 子图按**资产名**在存档中定位，请保证同一引用树内的图资产不重名。
- 演出节点在编辑器预览窗口中以日志行显示并自动继续，实际演出效果需进 Play Mode 看（摆位效果可在舞台布景窗口不进 Play Mode 预览）。
- 舞台布局在 `VNStage.Awake` 时应用；运行中改了布局资产需调用 `stage.ApplyLayout()` 才会生效。
