using System;
using System.Collections.Generic;
using System.Text;
using ScenarioKit.Events;
using ScenarioKit.Steps;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ScenarioKit.EditorTools
{
    [CustomEditor(typeof(EventStep))]
    public class EventStepEditor : Editor
    {
        private const string STATE_PREFIX = "ScenarioKit.EventStepEditor.";
        private const string SECTION_STEP = "Step";
        private const string SECTION_SEQUENCE = "Sequence";
        private const string SECTION_STATES = "States";
        private const string SECTION_VIOLATIONS = "Violations";
        private const string SECTION_SUMMARY = "Summary";
        private const string SECTION_USAGE = "Usage";

        private const string ID_FIELD = "_id";
        private const string INSTRUCTION_FIELD = "_instruction";
        private const string SEQUENCE_FIELD = "_sequence";
        private const string STRICT_ORDER_FIELD = "_strictOrder";
        private const string WRONG_ORDER_FIELD = "_wrongOrderMessage";
        private const string STATES_FIELD = "_requiredStates";
        private const string STATE_MESSAGE_FIELD = "_stateMessage";
        private const string VIOLATIONS_FIELD = "_violations";
        private const string KEY_FIELD = "_key";
        private const string EXPECTED_FIELD = "_expected";
        private const string MESSAGE_FIELD = "_message";

        private const float ROW_PADDING = 2f;
        private const float NUMBER_WIDTH = 24f;
        private const float POPUP_WIDTH = 100f;
        private const float SPACING = 4f;
        private const float CAPTION_HEIGHT = 14f;
        private const float SECTION_SPACE = 6f;
        private const float ID_BUTTON_WIDTH = 120f;
        private const float EMPTY_ALPHA = 0.15f;
        private const float VIOLATION_PICKER_RATIO = 0.45f;
        private const int INSTRUCTION_LINES = 3;
        private const string NOT_SELECTED = "(не выбрано)";

        private static readonly string[] _expectedLabels = { "включено", "выключено" };

        private static GUIStyle _greyStyle;

        private ReorderableList _sequenceList;
        private ReorderableList _statesList;
        private ReorderableList _violationsList;

        private void OnEnable()
        {
            _sequenceList = CreateList(SEQUENCE_FIELD, DrawSequenceElement, GetSequenceElementHeight);
            _statesList = CreateList(STATES_FIELD, DrawStateElement, GetRowHeight);
            _violationsList = CreateList(VIOLATIONS_FIELD, DrawViolationElement, GetRowHeight);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            if (DrawSectionHeader(SECTION_STEP, "Шаг"))
            {
                DrawStepSection();
            }

            if (DrawSectionHeader(SECTION_SEQUENCE, "Шаг выполнен, когда"))
            {
                DrawSequenceSection();
            }

            if (DrawSectionHeader(SECTION_STATES, "Только если"))
            {
                DrawStatesSection();
            }

            if (DrawSectionHeader(SECTION_VIOLATIONS, "Считается нарушением"))
            {
                DrawViolationsSection();
            }

            if (DrawSectionHeader(SECTION_SUMMARY, "Итог"))
            {
                DrawSummarySection();
            }

            if (DrawSectionHeader(SECTION_USAGE, "Где используется"))
            {
                DrawUsageSection();
            }

            serializedObject.ApplyModifiedProperties();
        }

        private ReorderableList CreateList(string fieldName, ReorderableList.ElementCallbackDelegate draw, ReorderableList.ElementHeightCallbackDelegate height)
        {
            ReorderableList list = new ReorderableList(serializedObject, serializedObject.FindProperty(fieldName), true, false, true, true);
            list.drawElementCallback = draw;
            list.elementHeightCallback = height;
            return list;
        }

        private bool DrawSectionHeader(string key, string title)
        {
            string stateKey = STATE_PREFIX + key;
            bool expanded = SessionState.GetBool(stateKey, true);
            EditorGUILayout.Space(SECTION_SPACE);
            Rect rect = EditorGUILayout.GetControlRect();
            bool newValue = EditorGUI.Foldout(rect, expanded, title, true, EditorStyles.foldoutHeader);

            if (newValue != expanded)
            {
                SessionState.SetBool(stateKey, newValue);
            }

            return newValue;
        }

        private void DrawStepSection()
        {
            SerializedProperty id = serializedObject.FindProperty(ID_FIELD);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(id, new GUIContent("Идентификатор", "Уникальное имя шага. Попадает в журнал."));

                if (GUILayout.Button(new GUIContent("↺ из имени файла", "Записать в идентификатор имя этого ассета"), GUILayout.Width(ID_BUTTON_WIDTH)))
                {
                    id.stringValue = target.name;
                }
            }

            SerializedProperty instruction = serializedObject.FindProperty(INSTRUCTION_FIELD);
            EditorGUILayout.LabelField("Текст задания для пользователя");
            float height = EditorGUIUtility.singleLineHeight * INSTRUCTION_LINES;
            string text = EditorGUILayout.TextArea(instruction.stringValue, GUILayout.MinHeight(height));

            if (!string.Equals(text, instruction.stringValue, StringComparison.Ordinal))
            {
                instruction.stringValue = text;
            }
        }

        private void DrawSequenceSection()
        {
            _sequenceList.DoLayoutList();
            HandleKeyDrop<ScenarioEventKey>(GUILayoutUtility.GetLastRect(), AddSequenceKey);

            if (_sequenceList.serializedProperty.arraySize >= 2)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty(STRICT_ORDER_FIELD), new GUIContent("Строгий порядок", "События нужно выполнять строго в порядке списка."));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(WRONG_ORDER_FIELD), new GUIContent("Сообщение при нарушении порядка"));
            }
        }

        private void DrawStatesSection()
        {
            _statesList.DoLayoutList();
            HandleKeyDrop<ScenarioStateKey>(GUILayoutUtility.GetLastRect(), AddStateKey);

            if (_statesList.serializedProperty.arraySize > 0)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty(STATE_MESSAGE_FIELD), new GUIContent("Сообщение, если условие не выполнено"));
            }
        }

        private void DrawViolationsSection()
        {
            _violationsList.DoLayoutList();
            HandleKeyDrop<ScenarioEventKey>(GUILayoutUtility.GetLastRect(), AddViolationKey);
        }

        private void DrawSummarySection()
        {
            if (_sequenceList.serializedProperty.arraySize == 0)
            {
                EditorGUILayout.HelpBox("Не указано событие выполнения", MessageType.Error);
                return;
            }

            EditorGUILayout.HelpBox(GetSummary(), MessageType.None);
        }

        private void DrawUsageSection()
        {
            IReadOnlyList<ModeUsage> usages = ScenarioUsageIndex.GetModesUsing((StepDefinition)target);

            if (usages.Count == 0)
            {
                EditorGUILayout.LabelField("Шаг не входит ни в один режим", GetGreyStyle());
                return;
            }

            foreach (ModeUsage usage in usages)
            {
                string modeName = usage.Mode != null ? usage.Mode.name : "(нет режима)";
                string text = modeName + " → " + usage.GroupName + " → стадия " + (usage.StageIndex + 1);

                if (GUILayout.Button(new GUIContent(text, "Выделить режим в окне Project"), EditorStyles.linkLabel) && usage.Mode != null)
                {
                    Selection.activeObject = usage.Mode;
                    EditorGUIUtility.PingObject(usage.Mode);
                }
            }
        }

        private GUIStyle GetGreyStyle()
        {
            if (_greyStyle == null)
            {
                _greyStyle = new GUIStyle(EditorStyles.miniLabel);
                _greyStyle.normal.textColor = Color.grey;
            }

            return _greyStyle;
        }

        private string GetSummary()
        {
            StringBuilder builder = new StringBuilder("Шаг выполнен, когда: ");
            SerializedProperty sequence = _sequenceList.serializedProperty;
            bool strict = serializedObject.FindProperty(STRICT_ORDER_FIELD).boolValue;
            string separator = strict ? " → " : ", ";

            for (int i = 0; i < sequence.arraySize; i++)
            {
                if (i > 0)
                {
                    builder.Append(separator);
                }

                builder.Append(GetAssetName(sequence.GetArrayElementAtIndex(i)));
            }

            if (!strict && sequence.arraySize >= 2)
            {
                builder.Append(" (в любом порядке)");
            }

            builder.Append('.');

            SerializedProperty states = _statesList.serializedProperty;

            if (states.arraySize > 0)
            {
                builder.Append(" Только если: ");

                for (int i = 0; i < states.arraySize; i++)
                {
                    SerializedProperty element = states.GetArrayElementAtIndex(i);

                    if (i > 0)
                    {
                        builder.Append(", ");
                    }

                    bool expected = element.FindPropertyRelative(EXPECTED_FIELD).boolValue;
                    builder.Append(GetAssetName(element.FindPropertyRelative(KEY_FIELD))).Append(" = ").Append(expected ? _expectedLabels[0] : _expectedLabels[1]);
                }

                builder.Append('.');
            }

            SerializedProperty violations = _violationsList.serializedProperty;

            if (violations.arraySize > 0)
            {
                builder.Append(" Нарушение: ");

                for (int i = 0; i < violations.arraySize; i++)
                {
                    SerializedProperty element = violations.GetArrayElementAtIndex(i);

                    if (i > 0)
                    {
                        builder.Append("; ");
                    }

                    builder.Append(GetAssetName(element.FindPropertyRelative(KEY_FIELD)));
                    string message = element.FindPropertyRelative(MESSAGE_FIELD).stringValue;

                    if (!string.IsNullOrEmpty(message))
                    {
                        builder.Append(" — «").Append(message).Append('»');
                    }
                }

                builder.Append('.');
            }

            return builder.ToString();
        }

        private string GetAssetName(SerializedProperty reference)
        {
            Object asset = reference.objectReferenceValue;
            return asset != null ? asset.name : NOT_SELECTED;
        }

        private float GetRowHeight(int index)
        {
            return EditorGUIUtility.singleLineHeight + ROW_PADDING * 2f;
        }

        private float GetSequenceElementHeight(int index)
        {
            return GetRowHeight(index) + (index > 0 ? CAPTION_HEIGHT : 0f);
        }

        private void DrawSequenceElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            SerializedProperty element = _sequenceList.serializedProperty.GetArrayElementAtIndex(index);

            if (index > 0)
            {
                EditorGUI.LabelField(new Rect(rect.x, rect.y, rect.width, CAPTION_HEIGHT), "затем", EditorStyles.centeredGreyMiniLabel);
                rect.y += CAPTION_HEIGHT;
                rect.height -= CAPTION_HEIGHT;
            }

            Rect row = GetPaddedRow(rect);
            HighlightIfEmpty(row, element);
            EditorGUI.LabelField(new Rect(row.x, row.y, NUMBER_WIDTH, row.height), (index + 1) + ".");
            Rect picker = new Rect(row.x + NUMBER_WIDTH, row.y, row.width - NUMBER_WIDTH, row.height);
            AssetPickerField.Draw<ScenarioEventKey>(picker, element, GUIContent.none, ScenarioKitFolders.EVENTS);
        }

        private void DrawStateElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            SerializedProperty element = _statesList.serializedProperty.GetArrayElementAtIndex(index);
            SerializedProperty key = element.FindPropertyRelative(KEY_FIELD);
            SerializedProperty expected = element.FindPropertyRelative(EXPECTED_FIELD);
            Rect row = GetPaddedRow(rect);
            HighlightIfEmpty(row, key);
            Rect picker = new Rect(row.x, row.y, row.width - POPUP_WIDTH - SPACING, row.height);
            Rect popup = new Rect(row.xMax - POPUP_WIDTH, row.y, POPUP_WIDTH, row.height);
            AssetPickerField.Draw<ScenarioStateKey>(picker, key, GUIContent.none, ScenarioKitFolders.STATES);

            EditorGUI.BeginProperty(popup, GUIContent.none, expected);
            EditorGUI.BeginChangeCheck();
            int selected = EditorGUI.Popup(popup, expected.boolValue ? 0 : 1, _expectedLabels);

            if (EditorGUI.EndChangeCheck())
            {
                expected.boolValue = selected == 0;
            }

            EditorGUI.EndProperty();
        }

        private void DrawViolationElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            SerializedProperty element = _violationsList.serializedProperty.GetArrayElementAtIndex(index);
            SerializedProperty key = element.FindPropertyRelative(KEY_FIELD);
            SerializedProperty message = element.FindPropertyRelative(MESSAGE_FIELD);
            Rect row = GetPaddedRow(rect);
            HighlightIfEmpty(row, key);
            float pickerWidth = row.width * VIOLATION_PICKER_RATIO;
            Rect picker = new Rect(row.x, row.y, pickerWidth, row.height);
            Rect text = new Rect(picker.xMax + SPACING, row.y, row.width - pickerWidth - SPACING, row.height);
            AssetPickerField.Draw<ScenarioEventKey>(picker, key, GUIContent.none, ScenarioKitFolders.EVENTS);
            EditorGUI.PropertyField(text, message, GUIContent.none);
        }

        private Rect GetPaddedRow(Rect rect)
        {
            return new Rect(rect.x, rect.y + ROW_PADDING, rect.width, EditorGUIUtility.singleLineHeight);
        }

        private void HighlightIfEmpty(Rect row, SerializedProperty reference)
        {
            if (reference.objectReferenceValue == null)
            {
                Color color = ScenarioKitStyles.GetErrorColor();
                color.a = EMPTY_ALPHA;
                EditorGUI.DrawRect(new Rect(row.x - 2f, row.y - 1f, row.width + 4f, row.height + 2f), color);
            }
        }

        private void HandleKeyDrop<T>(Rect zone, Action<T> add) where T : ScriptableObject
        {
            Event current = Event.current;

            if ((current.type != EventType.DragUpdated && current.type != EventType.DragPerform) || !zone.Contains(current.mousePosition))
            {
                return;
            }

            List<T> keys = new List<T>();

            foreach (Object reference in DragAndDrop.objectReferences)
            {
                if (reference is T key)
                {
                    keys.Add(key);
                }
            }

            if (keys.Count == 0)
            {
                return;
            }

            DragAndDrop.visualMode = DragAndDropVisualMode.Link;

            if (current.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();

                foreach (T key in keys)
                {
                    add(key);
                }

                GUI.changed = true;
            }

            current.Use();
        }

        private void AddSequenceKey(ScenarioEventKey key)
        {
            SerializedProperty array = _sequenceList.serializedProperty;
            int index = array.arraySize;
            array.arraySize++;
            array.GetArrayElementAtIndex(index).objectReferenceValue = key;
        }

        private void AddStateKey(ScenarioStateKey key)
        {
            SerializedProperty array = _statesList.serializedProperty;
            int index = array.arraySize;
            array.arraySize++;
            array.GetArrayElementAtIndex(index).FindPropertyRelative(KEY_FIELD).objectReferenceValue = key;
        }

        private void AddViolationKey(ScenarioEventKey key)
        {
            SerializedProperty array = _violationsList.serializedProperty;
            int index = array.arraySize;
            array.arraySize++;
            SerializedProperty element = array.GetArrayElementAtIndex(index);
            element.FindPropertyRelative(KEY_FIELD).objectReferenceValue = key;
            element.FindPropertyRelative(MESSAGE_FIELD).stringValue = string.Empty;
        }
    }
}
