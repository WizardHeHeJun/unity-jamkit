using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameplayKit
{
    public enum BlackboardVariableType
    {
        Bool,
        Int,
        Float,
        String
    }

    /// <summary>一个黑板变量的声明：名称、类型、默认值与备注。</summary>
    [Serializable]
    public class BlackboardVariableDefinition
    {
        [Tooltip("变量名，所有工具中通过下拉选择引用，请勿重名")]
        public string name;

        public BlackboardVariableType type = BlackboardVariableType.Bool;

        public bool defaultBool;
        public int defaultInt;
        public float defaultFloat;
        public string defaultString;

        [Tooltip("给策划看的备注：这个变量是干什么的、谁负责写入")]
        public string note;
    }

    /// <summary>
    /// 黑板：策划在这里声明全局变量（名称 + 类型 + 默认值），
    /// 任务 / 条件门等所有工具引用变量时都从这里下拉选择，避免手填拼错。
    /// 运行时用 <see cref="Blackboard.CreateFrom"/> 创建实例。
    /// </summary>
    [CreateAssetMenu(fileName = "NewBlackboard", menuName = "玩法/黑板 (Blackboard)")]
    public class BlackboardAsset : ScriptableObject
    {
        public List<BlackboardVariableDefinition> variables = new List<BlackboardVariableDefinition>();

        public BlackboardVariableDefinition Find(string variableName)
        {
            if (string.IsNullOrEmpty(variableName)) return null;
            return variables.Find(v => v != null && v.name == variableName);
        }

        public bool Has(string variableName) => Find(variableName) != null;

        /// <summary>全部变量名（编辑器下拉用）。</summary>
        public List<string> GetNames()
        {
            var names = new List<string>();
            foreach (var v in variables)
            {
                if (v != null && !string.IsNullOrEmpty(v.name))
                    names.Add(v.name);
            }
            return names;
        }
    }
}
