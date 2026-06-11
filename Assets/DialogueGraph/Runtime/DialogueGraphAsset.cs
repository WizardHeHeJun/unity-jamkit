using System.Collections.Generic;
using UnityEngine;

namespace DialogueGraph
{
    [CreateAssetMenu(fileName = "NewDialogueGraph", menuName = "剧情/对话图 (Dialogue Graph)")]
    public class DialogueGraphAsset : ScriptableObject
    {
        [Tooltip("可选：本地化表。节点文案填了 Loc Key 时从这里取译文")]
        public LocalizationTable localizationTable;

        [SerializeReference]
        public List<BaseNodeData> nodes = new List<BaseNodeData>();

        public BaseNodeData FindNode(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return null;
            return nodes.Find(n => n != null && n.guid == guid);
        }

        public StartNodeData GetStartNode()
        {
            return nodes.Find(n => n is StartNodeData) as StartNodeData;
        }
    }
}
