using System;
using UnityEditor;
using UnityEngine;

namespace ScenarioKit.EditorTools
{
    public class NamePromptPopup : PopupWindowContent
    {
        private const string FIELD_CONTROL_NAME = "ScenarioKitNameField";
        private const float WINDOW_WIDTH = 280f;
        private const float WINDOW_HEIGHT = 86f;
        private const float MARGIN = 8f;

        private readonly Action<string> _confirmed;

        private string _name;
        private bool _focusRequested;

        public NamePromptPopup(string defaultName, Action<string> confirmed)
        {
            _name = defaultName;
            _confirmed = confirmed;
        }

        public override Vector2 GetWindowSize()
        {
            return new Vector2(WINDOW_WIDTH, WINDOW_HEIGHT);
        }

        public override void OnGUI(Rect rect)
        {
            Event current = Event.current;

            if (current.type == EventType.KeyDown)
            {
                if (current.keyCode == KeyCode.Return || current.keyCode == KeyCode.KeypadEnter)
                {
                    current.Use();
                    Confirm();
                    return;
                }

                if (current.keyCode == KeyCode.Escape)
                {
                    current.Use();
                    editorWindow.Close();
                    return;
                }
            }

            GUILayout.BeginArea(new Rect(MARGIN, MARGIN, rect.width - MARGIN * 2f, rect.height - MARGIN * 2f));
            EditorGUILayout.LabelField("Название");
            GUI.SetNextControlName(FIELD_CONTROL_NAME);
            _name = EditorGUILayout.TextField(_name);

            if (!_focusRequested)
            {
                _focusRequested = true;
                EditorGUI.FocusTextInControl(FIELD_CONTROL_NAME);
            }

            EditorGUILayout.BeginHorizontal();

            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_name)))
            {
                if (GUILayout.Button("Создать"))
                {
                    Confirm();
                }
            }

            if (GUILayout.Button("Отмена"))
            {
                editorWindow.Close();
            }

            EditorGUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void Confirm()
        {
            if (string.IsNullOrWhiteSpace(_name))
            {
                return;
            }

            string result = _name.Trim();
            editorWindow.Close();
            _confirmed?.Invoke(result);
            GUIUtility.ExitGUI();
        }
    }
}
