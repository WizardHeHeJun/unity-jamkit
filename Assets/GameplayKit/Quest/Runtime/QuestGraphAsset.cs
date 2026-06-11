using System.Collections.Generic;
using UnityEngine;

namespace GameplayKit.Quest
{
    [CreateAssetMenu(fileName = "NewQuestGraph", menuName = "玩法/任务图 (Quest Graph)")]
    public class QuestGraphAsset : ScriptableObject
    {
        [Tooltip("解锁条件 / 条件门引用的黑板。强烈建议配置，否则条件只能手填变量名")]
        public BlackboardAsset blackboard;

        [SerializeReference]
        public List<QuestBaseNodeData> nodes = new List<QuestBaseNodeData>();

        public QuestBaseNodeData FindNode(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return null;
            return nodes.Find(n => n != null && n.guid == guid);
        }

        public QuestStartNodeData GetStartNode()
        {
            return nodes.Find(n => n is QuestStartNodeData) as QuestStartNodeData;
        }

        /// <summary>guid → 所有指向它的前置节点 guid。运行时与校验共用。</summary>
        public Dictionary<string, List<string>> BuildPredecessorMap()
        {
            var map = new Dictionary<string, List<string>>();
            foreach (var node in nodes)
            {
                if (node == null) continue;
                foreach (var next in node.nextGuids)
                {
                    if (string.IsNullOrEmpty(next)) continue;
                    if (!map.TryGetValue(next, out var list))
                        map[next] = list = new List<string>();
                    if (!list.Contains(node.guid))
                        list.Add(node.guid);
                }
            }
            return map;
        }
    }
}
