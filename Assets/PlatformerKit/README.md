# PlatformerKit — 平台跳跃关卡工具

面向**非程序人员**的 2D 平台跳跃关卡编辑器 + 运行时搭建器，适用于 Unity 2022.3，无第三方依赖。
与 `DialogueGraph` / `GameplayKit` 同范式：资产驱动、调色板下拉不手填、保存即校验、一键可玩示例。

| 模块 | 入口 | 说明 |
|---|---|---|
| 关卡编辑器 | 双击关卡资产 / `Tools → 关卡工具 → 关卡编辑器` | 网格画布：笔刷 / 矩形填充 / 实体摆放 / 右键擦除 |
| 图块集 | `Create → 关卡 → 图块集 (Tile Set)` | 美术登记图块（实心 / 单向平台 / 危险 / 装饰）与实体（出生点 / 终点 / 金币 / 敌人…） |
| 运行时搭建 | `LevelBuilder` 组件 | 资产 → 场景：自动生成碰撞体、单向平台、尖刺与收集品触发器 |
| 演示 | `Tools → 关卡工具 → 创建平台跳跃示例` | 一键生成图块集 + 示例关卡 + 可玩场景（A/D 移动、空格跳） |

## 目录结构

```
Assets/PlatformerKit/
├── Runtime/
│   ├── LevelTileSet.cs             # 图块集：图块/实体定义（调色板）
│   ├── LevelAsset.cs               # 关卡资产：网格 + 图块层 + 实体层
│   ├── LevelBuilder.cs             # 搭建器：资产 → 场景物体与碰撞
│   ├── LevelMarkers.cs             # 标记组件：危险/收集品/终点/检查点
│   └── SimplePlatformerController.cs # 演示级角色控制器 + 跟随相机
└── Editor/
    ├── LevelEditorWindow.cs        # 关卡编辑器（画布/调色板/校验）
    ├── LevelValidator.cs           # 静态校验
    └── LevelDemoBuilder.cs         # 一键示例
```

## 快速开始

1. `Tools → 关卡工具 → 创建平台跳跃示例`，点 Play 游玩：A/D 移动、空格跳；碰尖刺回到出生点 / 检查点，吃金币、踩单向平台、到终点。
2. 双击 `Assets/PlatformerKitDemo/示例关卡` 继续编辑，改完**保存**后重新 Play 生效。
3. 自己的关卡：
   - 美术：`Create → 关卡 → 图块集`，登记图块（ID、图、碰撞类型）与实体；
   - 策划：`Create → 关卡 → 关卡`，把图块集拖到 `tileSet` 字段，双击开画。

## 编辑器使用

- **左键**绘制 / 摆放，**右键**任意工具下擦除（先擦实体，再擦图块）；**矩形**工具拖框批量填充。
- 调色板点图块自动进笔刷，点实体自动进实体摆放；出生点全图唯一，重复摆放等于移动。
- 工具栏改网格尺寸后点「应用」，缩小会自动清掉界外内容（可撤销）。
- 「保存」落盘并校验：缺出生点、实体被实心块埋住、引用了图块集里不存在的 ID 等会列在底部。

### 图块碰撞类型

| 类型 | 行为 |
|---|---|
| 实心 | 四面碰撞（地面 / 墙壁 / 砖块） |
| 单向平台 | 只挡从上方落下，可从下方跳穿（PlatformEffector2D） |
| 危险 | 无实体碰撞，角色触碰回到重生点（尖刺 / 岩浆） |
| 装饰 | 只有图像 |

### 实体类型

出生点（每关一个）、终点、收集品、检查点为内置行为；**敌人出生点**与**自定义**不生成物体，
只抛 `OnEntitySpawned` 事件由游戏侧生成。任何实体配了 `prefab` 字段则直接实例化该预制体（内置标记仍会附加）。

## 运行时接入（给程序）

场景里放一个 `LevelBuilder`，拖上关卡资产即可（`buildOnAwake` 默认开）。演示控制器可整套换掉，
只要继续响应 `LevelHazard` / `LevelCollectible` / `LevelGoal` / `LevelCheckpoint` 标记组件：

```csharp
public class GameMode : MonoBehaviour
{
    public LevelBuilder builder;
    public SimplePlatformerController player;

    void Start()
    {
        builder.OnEntitySpawned += (def, instance, go) =>
        {
            if (def.kind == EntityKind.EnemySpawn)
                SpawnEnemy(def.id, builder.CellToWorld(instance.x, instance.y));
        };

        player.OnCollected += c => AddCoin(c.entityId, c.instanceId); // instanceId 可记入存档去重
        player.OnGoalReached += () => LoadNextLevel();
        player.OnDied += () => PlayHurtFx();
    }
}
```

与 GameplayKit 联动：在上述回调里写黑板 / 上报任务进度即可，例如
`questRunner.ReportProgress("收集", c.entityId)`、`blackboard.SetBool("通关第一关", true)`。

## 扩展指南

- **新增图块碰撞行为**：`TileCollisionType` 加枚举 + `LevelBuilder.BuildTile` 加分支（编辑器无需改动）。
- **新增实体内置行为**：`EntityKind` 加枚举 + `LevelBuilder.AttachMarker` 加分支 + 新标记组件。
- 编辑器画布按图块集的 Sprite / 颜色显示，美术只改图块集即可全局换皮。

## 注意事项

- 图块 / 实体 ID 是关卡数据与存档的引用键，发布后不要改；改名请改 `displayName`。
- 实体摆放点的稳定 ID（`EntityInstance.id`）在摆放时生成，收集品「已捡过」按它存档。
- 每格一个碰撞体的搭建方式适合中小关卡；超大关卡（数千实心块）建议后续做碰撞合并优化。
- 演示控制器为占位实现（移动 / 跳跃 / 土狼时间），正式项目请替换为自己的手感方案。
