using System.Collections.Generic;
using ScenarioKit.Events;
using ScenarioKit.Steps;
using UnityEditor;
using UnityEngine;

namespace ScenarioKit.EditorTools
{
    public abstract class ScenarioEventSourceEditor : Editor
    {
        private const string SCRIPT_FIELD = "m_Script";
        private const string USED_PREFIX = "Используется шагами: ";
        private const string UNUSED_MESSAGE = "Этот ключ не используется ни одним шагом";
        private const string UNTAGGED = "Untagged";

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawFields();
            serializedObject.ApplyModifiedProperties();
        }

        protected abstract void DrawFields();

        protected void DrawEventKey(string fieldName, string label, string tooltip)
        {
            SerializedProperty property = serializedObject.FindProperty(fieldName);
            AssetPickerField.DrawLayout<ScenarioEventKey>(property, new GUIContent(label, tooltip), ScenarioKitFolders.EVENTS);
            DrawUsage(ScenarioUsageIndex.GetStepsUsing(property.objectReferenceValue as ScenarioEventKey), property.objectReferenceValue != null);
        }

        protected void DrawStateKey(string fieldName, string label, string tooltip)
        {
            SerializedProperty property = serializedObject.FindProperty(fieldName);
            AssetPickerField.DrawLayout<ScenarioStateKey>(property, new GUIContent(label, tooltip), ScenarioKitFolders.STATES);
            DrawUsage(ScenarioUsageIndex.GetStepsUsing(property.objectReferenceValue as ScenarioStateKey), property.objectReferenceValue != null);
        }

        protected void DrawTag(string fieldName, string label, string tooltip)
        {
            SerializedProperty property = serializedObject.FindProperty(fieldName);
            Rect rect = EditorGUILayout.GetControlRect();
            GUIContent content = new GUIContent(label, tooltip);
            EditorGUI.BeginProperty(rect, content, property);
            EditorGUI.BeginChangeCheck();
            string tag = EditorGUI.TagField(rect, content, string.IsNullOrEmpty(property.stringValue) ? UNTAGGED : property.stringValue);

            if (EditorGUI.EndChangeCheck())
            {
                property.stringValue = tag;
            }

            EditorGUI.EndProperty();
        }

        protected void DrawOtherFields(params string[] drawnFields)
        {
            List<string> excluded = new List<string>(drawnFields);
            excluded.Add(SCRIPT_FIELD);
            DrawPropertiesExcluding(serializedObject, excluded.ToArray());
        }

        private void DrawUsage(IReadOnlyList<StepDefinition> steps, bool isAssigned)
        {
            if (!isAssigned)
            {
                return;
            }

            if (steps.Count == 0)
            {
                EditorGUILayout.HelpBox(UNUSED_MESSAGE, MessageType.Warning);
                return;
            }

            string[] ids = new string[steps.Count];

            for (int i = 0; i < steps.Count; i++)
            {
                ids[i] = steps[i].Id;
            }

            EditorGUILayout.LabelField(USED_PREFIX + string.Join(", ", ids), EditorStyles.miniLabel);
        }
    }
}
