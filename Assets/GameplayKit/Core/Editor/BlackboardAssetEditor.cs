using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GameplayKit.Editor
{
    /// <summary>
    /// 黑板变量行内编辑：一行显示「变量名 | 类型 | 默认值」，第二行备注。
    /// </summary>
    [CustomPropertyDrawer(typeof(BlackboardVariableDefinition))]
    public class BlackboardVariableDefinitionDrawer : PropertyDrawer
    {
        const float Gap = 4f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight * 2 + EditorGUIUtility.standardVerticalSpacing * 3;
        }

        public override void OnGUI(Rect rect, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(rect, label, property);

            var nameProp = property.FindPropertyRelative("name");
            var typeProp = property.FindPropertyRelative("type");
            var noteProp = property.FindPropertyRelative("note");

            float line = EditorGUIUtility.singleLineHeight;
            var row1 = new Rect(rect.x, rect.y + EditorGUIUtility.standardVerticalSpacing, rect.width, line);
            var row2 = new Rect(rect.x, row1.yMax + EditorGUIUtility.standardVerticalSpacing, rect.width, line);

            float nameWidth = rect.width * 0.4f;
            float typeWidth = 52f;
            float valueWidth = rect.width - nameWidth - typeWidth - Gap * 2;

            var nameRect = new Rect(row1.x, row1.y, nameWidth, line);
            var typeRect = new Rect(nameRect.xMax + Gap, row1.y, typeWidth, line);
            var valueRect = new Rect(typeRect.xMax + Gap, row1.y, valueWidth, line);

            EditorGUI.PropertyField(nameRect, nameProp, GUIContent.none);
            EditorGUI.PropertyField(typeRect, typeProp, GUIContent.none);

            var type = (BlackboardVariableType)typeProp.enumValueIndex;
            string defaultField = type switch
            {
                BlackboardVariableType.Bool => "defaultBool",
                BlackboardVariableType.Int => "defaultInt",
                BlackboardVariableType.Float => "defaultFloat",
                _ => "defaultString"
            };
            EditorGUI.PropertyField(valueRect, property.FindPropertyRelative(defaultField), GUIContent.none);

            EditorGUI.PropertyField(row2, noteProp, GUIContent.none);
            if (string.IsNullOrEmpty(noteProp.stringValue))
            {
                var hint = new GUIStyle(EditorStyles.label) { fontStyle = FontStyle.Italic };
                hint.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
                EditorGUI.LabelField(new Rect(row2.x + 4, row2.y, row2.width - 4, line), "备注（可选）", hint);
            }

            EditorGUI.EndProperty();
        }
    }

    /// <summary>黑板 Inspector：默认列表之上叠加重名 / 空名检查提示。</summary>
    [CustomEditor(typeof(BlackboardAsset))]
    public class BlackboardAssetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var asset = (BlackboardAsset)target;

            var problems = CollectProblems(asset);
            if (problems.Count > 0)
                EditorGUILayout.HelpBox(string.Join("\n", problems), MessageType.Warning);
            else
                EditorGUILayout.HelpBox($"共 {asset.variables.Count} 个变量。任务图、条件门中通过下拉引用这些变量。",
                    MessageType.Info);

            DrawDefaultInspector();
        }

        static List<string> CollectProblems(BlackboardAsset asset)
        {
            var problems = new List<string>();
            if (asset.variables.Any(v => v == null || string.IsNullOrEmpty(v.name)))
                problems.Add("存在未命名的变量");

            var duplicates = asset.variables
                .Where(v => v != null && !string.IsNullOrEmpty(v.name))
                .GroupBy(v => v.name)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();
            foreach (var name in duplicates)
                problems.Add($"变量「{name}」重名，引用它的工具行为不可预期");

            return problems;
        }
    }
}
