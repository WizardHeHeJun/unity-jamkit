using UnityEngine;

namespace PlatformerKit
{
    /// <summary>危险物标记（尖刺 / 岩浆）：角色触碰后回到重生点。</summary>
    public class LevelHazard : MonoBehaviour
    {
    }

    /// <summary>收集品标记：角色触碰后由控制器销毁并抛事件。</summary>
    public class LevelCollectible : MonoBehaviour
    {
        [Tooltip("实体定义 ID（金币 / 宝石……），统计与存档用")]
        public string entityId;

        [Tooltip("该摆放点的稳定 ID（关卡资产生成），存档记录「已捡过」用")]
        public string instanceId;
    }

    /// <summary>终点标记。</summary>
    public class LevelGoal : MonoBehaviour
    {
    }

    /// <summary>检查点标记：角色触碰后重生点更新到这里。</summary>
    public class LevelCheckpoint : MonoBehaviour
    {
    }
}
