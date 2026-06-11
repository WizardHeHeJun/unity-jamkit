using System;
using System.Collections.Generic;
using UnityEngine;

namespace DialogueGraph
{
    /// <summary>
    /// 角色数据库条目：显示名、名字颜色、立绘表情差分。
    /// </summary>
    [CreateAssetMenu(fileName = "NewCharacter", menuName = "剧情/角色 (Character)")]
    public class CharacterAsset : ScriptableObject
    {
        [Tooltip("角色唯一 ID，供存档 / 脚本引用")]
        public string id;

        public LocalizedText displayName = new LocalizedText();
        public Color nameColor = Color.white;

        [Serializable]
        public class Expression
        {
            public string name;
            public Sprite sprite;
        }

        [Tooltip("表情差分列表，第一项视为默认表情")]
        public List<Expression> expressions = new List<Expression>();

        [Header("立绘校准（适配不同尺寸的原画，可在「舞台布景」窗口可视化调整）")]
        [Tooltip("整体缩放，叠加在槽位缩放之上")]
        public float spriteScale = 1f;

        [Tooltip("相对槽位的像素偏移（X 向右、Y 向上为正）")]
        public Vector2 spriteOffset = Vector2.zero;

        /// <summary>按表情名取立绘；名字为空返回默认（第一项），找不到返回 null。</summary>
        public Sprite GetExpression(string expressionName)
        {
            if (expressions.Count == 0) return null;
            if (string.IsNullOrEmpty(expressionName)) return expressions[0].sprite;
            var entry = expressions.Find(e => e.name == expressionName);
            return entry != null ? entry.sprite : null;
        }

        public bool HasExpression(string expressionName)
        {
            return expressions.Exists(e => e.name == expressionName);
        }

        public List<string> GetExpressionNames()
        {
            return expressions.ConvertAll(e => e.name);
        }
    }
}
