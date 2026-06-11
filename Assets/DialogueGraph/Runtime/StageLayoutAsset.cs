using System;
using System.Collections.Generic;
using UnityEngine;

namespace DialogueGraph
{
    /// <summary>
    /// 舞台布局：立绘槽位的位置 / 尺寸 / 缩放，独立成资产供美术调整复用。
    /// 拖到 VNStage.layout 字段后 Awake 时自动应用；
    /// 可在「Tools → 剧情工具 → 舞台布景」中可视化编辑。
    /// </summary>
    [CreateAssetMenu(fileName = "NewStageLayout", menuName = "剧情/舞台布局 (Stage Layout)")]
    public class StageLayoutAsset : ScriptableObject
    {
        [Tooltip("布局基于的参考分辨率，须与场景 CanvasScaler 一致")]
        public Vector2 referenceResolution = new Vector2(1920f, 1080f);

        [Serializable]
        public class SlotLayout
        {
            [Tooltip("槽位锚点（画面比例坐标 0-1，Y=0 为底边；立绘底边中心对齐该点）")]
            public Vector2 anchor = new Vector2(0.5f, 0f);

            [Tooltip("槽位尺寸（参考分辨率像素）")]
            public Vector2 size = new Vector2(350f, 700f);

            [Tooltip("槽位整体缩放（角色自身的校准缩放会再叠加）")]
            public float scale = 1f;

            /// <summary>把布局写到槽位的 RectTransform（底边中心为轴心）。</summary>
            public void ApplyTo(RectTransform rectTransform)
            {
                rectTransform.anchorMin = rectTransform.anchorMax = anchor;
                rectTransform.pivot = new Vector2(0.5f, 0f);
                rectTransform.anchoredPosition = Vector2.zero;
                rectTransform.sizeDelta = size;
                rectTransform.localScale = Vector3.one * scale;
            }
        }

        [Tooltip("顺序与站位枚举一致：左 / 中 / 右")]
        public List<SlotLayout> slots = new List<SlotLayout>
        {
            new SlotLayout { anchor = new Vector2(0.22f, 0f) },
            new SlotLayout { anchor = new Vector2(0.5f, 0f) },
            new SlotLayout { anchor = new Vector2(0.78f, 0f) }
        };

        public SlotLayout GetSlot(int index)
        {
            return (index >= 0 && index < slots.Count) ? slots[index] : null;
        }
    }
}
