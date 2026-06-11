# GameplayKit — 通用玩法工具集

面向**非程序人员**的 Unity 编辑器玩法工具集，适用于 Unity 2022.3，无第三方依赖。
与 `Assets/DialogueGraph` 同范式：节点画布、下拉选择不手填、保存即校验可定位、不进 Play Mode 模拟、一键示例。

当前包含三个模块：

| 模块 | 说明 |
|---|---|
| Core（公共底座） | 黑板变量 + 通用条件组 + 条件编辑控件，后续所有玩法工具共用 |
| Quest（任务系统） | 节点式任务链编辑器 + 校验 + 模拟器 + 纯 C# 运行时 QuestRunner |
| Director（导演编排） | 「当 X 发生时做 Y」规则编排：连接对话 / 任务 / 黑板，替代游戏侧手写的事件胶水代码 |

## 目录结构

```
Assets/GameplayKit/
├── Core/
│   ├── Runtime/
│   │   ├── BlackboardAsset.cs      # 黑板：变量声明资产（名称/类型/默认值/备注）
│   │   ├── Blackboard.cs           # 运行时黑板：变量表 + 变更通知 + 快照
│   │   └── GameCondition.cs        # 通用条件组：若干比较按「全部/任一满足」组合
│   └── Editor/
│       ├── BlackboardAssetEditor.cs # 黑板 Inspector（行内编辑 + 重名检查）
│       └── GameConditionView.cs     # 条件编辑控件（变量下拉，所有图工具共用）
├── Quest/
│   ├── Runtime/
│   │   ├── QuestNodeData.cs        # 节点数据 + 目标/奖励 + 目标类型注册表
│   │   ├── QuestGraphAsset.cs      # 任务图资产（ScriptableObject）
│   │   └── QuestRunner.cs          # 执行器（纯 C#，事件驱动，含存档）
│   └── Editor/
│       ├── QuestGraphWindow.cs     # 编辑器窗口（保存/校验/模拟）
│       ├── QuestGraphView.cs       # 画布、保存/加载
│       ├── QuestNodeViews.cs       # 各节点视图
│       ├── QuestGraphValidator.cs  # 静态校验
│       ├── QuestSimulatorWindow.cs # 任务模拟器
│       └── QuestDemoAssetBuilder.cs# 一键示例
└── Director/
    ├── Runtime/
    │   ├── DirectorAsset.cs        # 编排资产：规则 = 触发器 + 附加条件 + 动作列表
    │   └── DirectorRunner.cs       # 执行器（纯 C#，事件驱动，含存档）
    └── Editor/
        ├── DirectorWindow.cs       # 规则列表编辑器（保存/校验/模拟）
        ├── DirectorValidator.cs    # 静态校验
        ├── DirectorSimulatorWindow.cs  # 编排模拟器（可联动任务面板）
        └── DirectorDemoAssetBuilder.cs # 一键示例
```

## 快速开始（给策划）

1. `Tools → 玩法工具 → 创建任务系统示例`，自动生成示例黑板 + 任务图并打开编辑器。
2. 点工具栏「模拟」：左侧改黑板变量（把「等级」改到 5 看条件门解锁），右侧接任务、点目标「+1」推进度，底部看事件流水。
3. 自己的任务链：
   - `Create → 玩法 → 黑板 (Blackboard)`：声明全局变量（等级、是否完成引导……）。
   - `Create → 玩法 → 任务图 (Quest Graph)`：把黑板拖到图的 `blackboard` 字段，双击打开编辑。
4. **改完务必「保存」（Ctrl+S）**：字段实时生效，但节点增删与连线在保存时才写入资产。保存时自动校验，问题列表点击可定位节点。

## 节点类型与解锁规则

| 节点 | 行为 |
|---|---|
| 开始 | 入口，开局即视为通过；每图一个，自动创建不可删除 |
| 任务 | ID/标题/描述/图标 + 目标列表 + 奖励列表 + 额外解锁条件 + 自动/手动接取 |
| 条件门 | 前置满足且黑板条件成立时通过；不成立则挂起，黑板变化后自动复评 |
| 汇合（全部完成） | 所有进入连线都通过后才通过 |

**解锁规则**：任务的前置连线是「任一满足」——多条线连进来，完成任意一条即解锁；
需要「全都做完才解锁」时，中间加一个**汇合**节点。在此之上还可叠加任务自身的「额外解锁条件」。

一个节点的输出口可以连多条线：完成后同时解锁多个后续。

## 导演编排（Director）

「当 X 发生时做 Y」的规则表，把对话、任务、黑板连成一个整体，策划配置代替程序写事件胶水代码。

### 给策划

1. `Tools → 玩法工具 → 创建导演编排示例`（先建任务系统示例可体验任务联动），或 `Create → 玩法 → 导演编排` 后双击打开。
2. 编辑器里把**黑板**和**任务图**拖到顶部引用栏，变量与任务 ID 全部变成下拉。
3. 每条规则 = **当**（触发器）+ **附加条件**（黑板）+ **就做**（动作列表）：

| 触发器 | 触发时机 |
|---|---|
| 事件触发 | 游戏侧 / 对话事件节点上报的 `(事件名, 参数)`，参数留空 = 任意 |
| 黑板条件 | 附加条件从不满足**变为满足的瞬间**（开局即满足也触发） |
| 任务状态 | 指定任务变为可接取 / 被接取 / 完成 |
| 对话结束 | 指定对话图结束，留空 = 任意对话 |

动作：**开始对话**、**修改变量**（设为 / 累加）、**抛游戏事件**（可链式触发其他规则）、**接取任务**、**完成任务**、**上报任务进度**。

「仅一次」规则触发后永久失效（计入存档）；关闭后每次满足都触发（黑板条件规则需先回到不满足再重新满足）。
工具栏「模拟」不进 Play Mode 试跑：改黑板、上报事件、结束对话、操作任务，观察规则触发与连锁反应。

### 给程序

```csharp
var director = new DirectorRunner();
director.OnStartDialogue += g => { vnPlayer.graph = g; vnPlayer.Play(); }; // 开始对话动作
director.OnGameEvent += (name, param) => { /* 抛游戏事件动作 */ };

director.AttachQuestRunner(questRunner); // 任务状态触发器生效，任务类动作可执行
director.Attach(vnPlayer);               // 对话事件/对话结束自动回流为触发源
director.Begin(directorAsset, blackboard);

director.ReportEvent("引导完成");         // 游戏侧上报事件（区域进入、按钮点击……）

// 存档（黑板快照另行保存，与任务系统共用同一个黑板时只存一份）
string json = director.CaptureSaveJson();
director.RestoreFromJson(directorAsset, blackboard, json); // 恢复期间不触发任何规则
```

## 运行时接入（给程序）

```csharp
using GameplayKit;
using GameplayKit.Quest;

public class QuestSystem : MonoBehaviour
{
    public QuestGraphAsset graph;

    Blackboard blackboard;
    QuestRunner runner;

    void Start()
    {
        blackboard = Blackboard.CreateFrom(graph.blackboard);
        runner = new QuestRunner();

        runner.OnQuestAvailable += e => questUI.ShowNewQuestMark(e);
        runner.OnQuestAccepted  += e => questUI.AddTracker(e);
        runner.OnObjectiveProgress += (e, i, count) => questUI.RefreshTracker(e);
        runner.OnQuestCompleted += e => questUI.PlayCompleteFx(e);
        runner.OnRewardGranted  += (e, reward) => inventory.Add(reward.rewardId, reward.amount);

        runner.Begin(graph, blackboard);
    }

    // 游戏侧上报：杀怪 / 拾取 / 到达 / 对话…（类型字符串与编辑器下拉一致）
    public void OnMonsterKilled(string monsterId) => runner.ReportProgress("击杀", monsterId);
    public void OnItemGained(string itemId)      => runner.ReportProgress("收集", itemId);

    // 升级时写黑板，挂起的条件门 / 解锁条件自动复评
    public void OnLevelUp(int level) => blackboard.SetInt("等级", level);

    public void Save()
    {
        PlayerPrefs.SetString("quests", runner.CaptureSaveJson());
        PlayerPrefs.SetString("board", JsonUtility.ToJson(blackboard.CaptureState()));
    }

    public void Load()
    {
        blackboard = Blackboard.CreateFrom(graph.blackboard);
        blackboard.ApplyState(JsonUtility.FromJson<BlackboardState>(PlayerPrefs.GetString("board")));
        runner.RestoreFromJson(graph, blackboard, PlayerPrefs.GetString("quests")); // 恢复期间不抛事件
        questUI.RefreshAll(runner.Quests); // 恢复后整体刷新 UI
    }
}
```

- **目标匹配规则**：`ReportProgress(类型, 对象ID)` 会推进所有进行中任务里「类型相同且（对象 ID 相同或目标未填对象 ID）」的目标。
- **扩展目标类型**：游戏启动时 `QuestObjectiveTypes.All.Add("护送")`，编辑器下拉即出现。
- **GM 指令**：`runner.ForceCompleteQuest(questId)` 直接完成任意任务。
- 黑板也可不用 `Blackboard` 而接自己的存档系统：实现 `IBlackboardReader` 即可参与条件求值（但失去自动复评，需自行在数值变化后驱动）。

## 扩展指南

**新增任务图节点类型**（共 4 处）：
1. `QuestNodeData.cs` 加数据类（继承 `QuestBaseNodeData`）；
2. `QuestNodeViews.cs` 加视图类；
3. `QuestGraphView.cs` 的 `CreateNodeView` switch 与右键菜单各加一行；
4. `QuestRunner.EvaluateNode` 加解锁分支；（可选）`QuestGraphValidator` 加校验规则。

**新增导演触发器**（共 3 处）：
1. `DirectorAsset.cs` 加 `DirectorTrigger` 子类；
2. `DirectorWindow.TriggerTypes` 注册 + `BuildTriggerSection` 加编辑 UI；
3. `DirectorRunner` 加匹配入口（参照 `ReportEvent`）。

**新增导演动作**（共 3 处）：
1. `DirectorAsset.cs` 加 `DirectorAction` 子类；
2. `DirectorWindow.ActionTypes` 注册 + `BuildActionFields` 加分支；
3. `DirectorRunner.ExecuteAction` 加执行分支。

**其他玩法工具复用底座**：条件编辑直接 `new GameConditionView(condition, () => blackboard)`；
运行时求值 `condition.Evaluate(blackboard)`；挂起复评订阅 `blackboard.OnValueChanged`。

## 注意事项

- 任务 ID 是存档与代码引用的唯一标识，发布后不要改；图内重复会被校验拦截。
- 改版任务图后读旧档：已删除任务的记录自动丢弃；门/汇合的通过状态按当前黑板与已完成任务重算。
- 模拟器基于编辑器内存数据运行，点「模拟」时会先自动保存当前图。
- 导演规则的稳定 ID 在创建时生成（存档按它记录触发次数），编辑器里看不到也不用管；复制资产文件再改出新编排时规则 ID 不变，属正常。
- 读旧档时导演不触发任何规则；恢复时条件已满足但还没触发过的「黑板条件」规则（如改版新增的），会在下一次黑板变化时补触发。
- 「抛游戏事件」可以链式触发其他规则，循环成环时执行器会在深度上限处报错中断——校验不会检查环，请用模拟器验证规则链。
