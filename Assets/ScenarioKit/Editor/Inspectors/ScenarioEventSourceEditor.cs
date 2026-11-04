using System.Collections.Generic;
using ScenarioKit.Events;
using ScenarioKit.Steps;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace ScenarioKit.EditorTools
{
    public abstract class ScenarioEventSourceEditor : Editor
    {
        private const string SCRIPT_FIELD = "m_Script";
        private const string USED_PREFIX = "Используется шагами: ";
        private const string UNUSED_MESSAGE = "Этот ключ не используется ни одним шагом";
        private const string ANY_OBJECT = "(любой объект)";
        private const string NOT_ASSIGNED = "Событие не выбрано";

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
            List<string> options = new List<string>();
            options.Add(ANY_OBJECT);
            options.AddRange(InternalEditorUtility.tags);

            int current = string.IsNullOrEmpty(property.stringValue) ? 0 : options.IndexOf(property.stringValue);

            if (current < 0)
            {
                options.Add(property.stringValue);
                current = options.Count - 1;
            }

            EditorGUI.BeginProperty(rect, content, property);
            EditorGUI.BeginChangeCheck();
            GUIContent[] contents = new GUIContent[options.Count];

            for (int i = 0; i < contents.Length; i++)
            {
                contents[i] = new GUIContent(options[i]);
            }

            int selected = EditorGUI.Popup(rect, content, current, contents);

            if (EditorGUI.EndChangeCheck())
            {
                property.stringValue = selected == 0 ? string.Empty : options[selected];
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
                EditorGUILayout.LabelField(NOT_ASSIGNED, EditorStyles.miniLabel);
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
