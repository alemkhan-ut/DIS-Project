using System;
using System.Collections.Generic;
using ScenarioKit.Execution;
using ScenarioKit.Modes;
using ScenarioKit.Steps;
using ScenarioKit.Validation;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ScenarioKit.EditorTools
{
    [CustomEditor(typeof(ScenarioDefinition))]
    public class ScenarioDefinitionEditor : Editor
    {
        private const string STATE_PREFIX = "ScenarioKit.ScenarioDefinitionEditor.";
        private const string STEP_STATE = ".SelectedStep";
        private const string MODE_STATE = ".SelectedMode";
        private const string SCENARIO_ID_FIELD = "_scenarioId";
        private const string STEPS_FIELD = "_steps";
        private const string MODES_FIELD = "_modes";
        private const string MODE_NAME_FIELD = "_displayName";
        private const string UNDO_CREATE_STEP = "Создание шага";
        private const string UNDO_CREATE_MODE = "Создание режима";
        private const string ERROR_ICON = "console.erroricon.sml";
        private const string WARNING_ICON = "console.warnicon.sml";

        private const float ROW_PADDING = 2f;
        private const float ROW_HEIGHT = 20f;
        private const float ICON_WIDTH = 18f;
        private const float SPACING = 4f;
        private const float CHIP_WIDTH_RATIO = 0.55f;
        private const float MODE_NAME_RATIO = 0.45f;
        private const float MODE_TYPE_WIDTH = 90f;
        private const float OUTLINE = 2f;
        private const float SHOW_BUTTON_WIDTH = 80f;

        private static GUIStyle _greyStyle;

        private readonly List<ValidationMessage> _messages = new List<ValidationMessage>();
        private readonly HashSet<Object> _errorContexts = new HashSet<Object>();
        private readonly HashSet<Object> _warningContexts = new HashSet<Object>();
        private readonly HashSet<ScenarioMode> _scenarioModes = new HashSet<ScenarioMode>();

        private ReorderableList _stepsList;
        private ReorderableList _modesList;
        private Editor _stepEditor;
        private Editor _modeEditor;
        private StepDefinition _shownStep;
        private ScenarioMode _shownMode;
        private Action _pendingEdit;
        private Action<string> _pendingCreate;
        private string _pendingName;
        private int _errorCount;
        private int _warningCount;

        private ScenarioDefinition Scenario => (ScenarioDefinition)target;

        private void OnEnable()
        {
            if (target == null)
            {
                return;
            }

            _stepsList = new ReorderableList(serializedObject, serializedObject.FindProperty(STEPS_FIELD), true, true, false, true);
            _stepsList.drawHeaderCallback = DrawStepsHeader;
            _stepsList.drawElementCallback = DrawStepElement;
            _stepsList.elementHeight = ROW_HEIGHT + ROW_PADDING * 2f;
            _stepsList.onRemoveCallback = StepRemovedHandler;
            _stepsList.onSelectCallback = StepSelectedHandler;
            _stepsList.onReorderCallback = ListReorderedHandler;
            _stepsList.index = SessionState.GetInt(GetStateKey(STEP_STATE), -1);

            _modesList = new ReorderableList(serializedObject, serializedObject.FindProperty(MODES_FIELD), true, true, false, true);
            _modesList.drawHeaderCallback = DrawModesHeader;
            _modesList.drawElementCallback = DrawModeElement;
            _modesList.elementHeight = ROW_HEIGHT + ROW_PADDING * 2f;
            _modesList.onRemoveCallback = ModeRemovedHandler;
            _modesList.onSelectCallback = ModeSelectedHandler;
            _modesList.onReorderCallback = ListReorderedHandler;
            _modesList.index = SessionState.GetInt(GetStateKey(MODE_STATE), -1);
        }

        private void OnDisable()
        {
            if (_stepEditor != null)
            {
                DestroyImmediate(_stepEditor);
            }

            if (_modeEditor != null)
            {
                DestroyImmediate(_modeEditor);
            }
        }

        public override void OnInspectorGUI()
        {
            if (_stepsList == null)
            {
                return;
            }

            serializedObject.Update();

            if (Event.current.type == EventType.Layout)
            {
                RefreshSnapshot();
            }

            DrawScenarioHeader();
            DrawValidation();

            DrawSteps();
            serializedObject.ApplyModifiedProperties();
            DrawEmbedded(_shownStep, ref _stepEditor, _shownStep != null ? _shownStep.Id : string.Empty);

            DrawModes();
            serializedObject.ApplyModifiedProperties();
            DrawEmbedded(_shownMode, ref _modeEditor, ModeLabels.GetName(_shownMode));

            ShowPendingPopup();
            RunPendingEdit();
        }

        private static GUIStyle GetGreyStyle()
        {
            if (_greyStyle == null)
            {
                _greyStyle = new GUIStyle(EditorStyles.miniLabel);
                _greyStyle.normal.textColor = Color.grey;
                _greyStyle.alignment = TextAnchor.MiddleLeft;
            }

            return _greyStyle;
        }

        private static string GetCountText(int count, string one, string few, string many)
        {
            int lastTwo = count % 100;
            int last = count % 10;

            if (lastTwo >= 11 && lastTwo <= 14)
            {
                return count + " " + many;
            }

            if (last == 1)
            {
                return count + " " + one;
            }

            return last >= 2 && last <= 4 ? count + " " + few : count + " " + many;
        }

        private static int IndexOf(SerializedProperty list, Object value)
        {
            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == value)
                {
                    return i;
                }
            }

            return -1;
        }

        private static Object GetElement(ReorderableList list)
        {
            SerializedProperty property = list.serializedProperty;

            if (list.index < 0 || list.index >= property.arraySize)
            {
                return null;
            }

            return property.GetArrayElementAtIndex(list.index).objectReferenceValue;
        }

        private static void RemoveElement(ReorderableList list)
        {
            SerializedProperty property = list.serializedProperty;
            int index = list.index;

            if (index < 0 || index >= property.arraySize)
            {
                return;
            }

            int size = property.arraySize;
            property.DeleteArrayElementAtIndex(index);

            if (property.arraySize == size)
            {
                property.DeleteArrayElementAtIndex(index);
            }

            property.serializedObject.ApplyModifiedProperties();
            list.index = Mathf.Min(index, property.arraySize - 1);
            ScenarioUsageIndex.Invalidate();
        }

        private void RefreshSnapshot()
        {
            _messages.Clear();
            _messages.AddRange(ScenarioValidator.GetMessages(Scenario));
            _errorContexts.Clear();
            _warningContexts.Clear();
            _errorCount = 0;
            _warningCount = 0;

            foreach (ValidationMessage message in _messages)
            {
                bool isError = message.Severity == ValidationSeverity.Error;

                if (isError)
                {
                    _errorCount++;
                }
                else
                {
                    _warningCount++;
                }

                if (message.Context != null && message.Context != target)
                {
                    (isError ? _errorContexts : _warningContexts).Add(message.Context);
                }
            }

            _scenarioModes.Clear();

            foreach (ScenarioMode mode in Scenario.Modes)
            {
                if (mode != null)
                {
                    _scenarioModes.Add(mode);
                }
            }

            _shownStep = GetElement(_stepsList) as StepDefinition;
            _shownMode = GetElement(_modesList) as ScenarioMode;
        }

        private void DrawScenarioHeader()
        {
            ScenarioKitStyles.Section("Сценарий");
            GUIContent idLabel = new GUIContent("Идентификатор", "Идентификатор сценария, попадает в журнал.");
            EditorGUILayout.PropertyField(serializedObject.FindProperty(SCENARIO_ID_FIELD), idLabel);

            string steps = GetCountText(_stepsList.serializedProperty.arraySize, "шаг", "шага", "шагов");
            string modes = GetCountText(_modesList.serializedProperty.arraySize, "режим", "режима", "режимов");
            EditorGUILayout.LabelField(steps + " · " + modes, GetGreyStyle());

            bool hasProblems = _errorCount > 0 || _warningCount > 0;
            string status = hasProblems ? "Ошибок: " + _errorCount + " · Предупреждений: " + _warningCount : "Ошибок не найдено";
            Color color = GUI.contentColor;
            GUI.contentColor = _errorCount > 0 ? ScenarioKitStyles.GetErrorColor() : _warningCount > 0 ? ScenarioKitStyles.GetStatusColor(StepStatus.Skipped) : ScenarioKitStyles.GetStatusColor(StepStatus.Completed);
            EditorGUILayout.LabelField(status, EditorStyles.boldLabel);
            GUI.contentColor = color;
        }

        private void DrawValidation()
        {
            if (_messages.Count == 0)
            {
                return;
            }

            ScenarioKitStyles.Section("Проверка сценария");

            foreach (ValidationMessage message in _messages)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    MessageType type = message.Severity == ValidationSeverity.Error ? MessageType.Error : MessageType.Warning;
                    EditorGUILayout.HelpBox(message.Text, type);

                    using (new EditorGUI.DisabledScope(message.Context == null))
                    {
                        GUIContent show = new GUIContent("Показать", "Выделить объект, к которому относится сообщение");

                        if (GUILayout.Button(show, GUILayout.Width(SHOW_BUTTON_WIDTH), GUILayout.ExpandHeight(true)))
                        {
                            Object context = message.Context;
                            _pendingEdit = () => Reveal(context);
                        }
                    }
                }
            }
        }

        private void Reveal(Object context)
        {
            int stepIndex = IndexOf(_stepsList.serializedProperty, context);
            int modeIndex = IndexOf(_modesList.serializedProperty, context);

            if (context is StepDefinition && stepIndex >= 0)
            {
                SelectStep(stepIndex);
            }
            else if (context is ScenarioMode && modeIndex >= 0)
            {
                SelectMode(modeIndex);
            }

            EditorGUIUtility.PingObject(context);
        }

        private void DrawSteps()
        {
            ScenarioKitStyles.Section("Шаги сценария (библиотека)");
            EditorGUILayout.LabelField("Перетащите шаги из окна Project в список. «–» убирает шаг только из сценария, файл шага остаётся.", GetGreyStyle());
            _stepsList.DoLayoutList();
            Rect listRect = GUILayoutUtility.GetLastRect();

            if (StepDragAndDrop.TryAcceptDrop(listRect, out List<StepDefinition> dropped, out StepDragData _))
            {
                _pendingEdit = () => AddSteps(dropped);
            }

            if (GUILayout.Button(new GUIContent("＋ Новый шаг", "Создать новый шаг и добавить его в сценарий")))
            {
                ShowStepTypeMenu();
            }
        }

        private void DrawStepsHeader(Rect rect)
        {
            EditorGUI.LabelField(rect, "Шаги — выберите шаг, чтобы отредактировать его ниже");
        }

        private void DrawStepElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            StepDefinition step = _stepsList.serializedProperty.GetArrayElementAtIndex(index).objectReferenceValue as StepDefinition;
            Rect row = new Rect(rect.x, rect.y + ROW_PADDING, rect.width, ROW_HEIGHT);
            Rect chip = new Rect(row.x, row.y, row.width * CHIP_WIDTH_RATIO, row.height);
            Rect icon = new Rect(chip.xMax + SPACING, row.y, ICON_WIDTH, row.height);
            Rect usage = new Rect(icon.xMax + SPACING, row.y, row.xMax - icon.xMax - SPACING, row.height);

            StepChip.Draw(chip, step, null, isActive);
            DrawProblem(chip, icon, step);
            GUI.Label(usage, GetUsageText(step), GetGreyStyle());
        }

        private string GetUsageText(StepDefinition step)
        {
            if (step == null)
            {
                return "пустая ячейка — удалите её кнопкой «–»";
            }

            List<string> names = new List<string>();

            foreach (ModeUsage usage in ScenarioUsageIndex.GetModesUsing(step))
            {
                string name = ModeLabels.GetName(usage.Mode);

                if (usage.Mode != null && _scenarioModes.Contains(usage.Mode) && !names.Contains(name))
                {
                    names.Add(name);
                }
            }

            return names.Count == 0 ? "не входит ни в один режим" : "в режимах: " + string.Join(", ", names);
        }

        private void DrawProblem(Rect outline, Rect icon, Object item)
        {
            if (item == null)
            {
                return;
            }

            bool isError = _errorContexts.Contains(item);
            bool isWarning = !isError && _warningContexts.Contains(item);

            if (!isError && !isWarning)
            {
                return;
            }

            if (Event.current.type == EventType.Repaint)
            {
                Color color = isError ? ScenarioKitStyles.GetErrorColor() : ScenarioKitStyles.GetStatusColor(StepStatus.Skipped);
                ScenarioKitStyles.DrawBorder(outline, color, OUTLINE);
            }

            GUIContent content = new GUIContent(EditorGUIUtility.IconContent(isError ? ERROR_ICON : WARNING_ICON));
            content.tooltip = isError ? "Есть ошибки — см. сообщения вверху" : "Есть предупреждения — см. сообщения вверху";
            GUI.Label(icon, content);
        }

        private void ShowStepTypeMenu()
        {
            GenericMenu menu = new GenericMenu();

            foreach (Type type in TypeCache.GetTypesDerivedFrom<StepDefinition>())
            {
                if (type.IsAbstract || type.IsGenericType)
                {
                    continue;
                }

                Type stepType = type;
                menu.AddItem(new GUIContent(AssetPickerField.GetTypeLabel(stepType)), false, () => RequestCreate(stepType, ScenarioKitFolders.STEPS, STEPS_FIELD, UNDO_CREATE_STEP));
            }

            if (menu.GetItemCount() == 0)
            {
                menu.AddDisabledItem(new GUIContent("Нет доступных типов шагов"));
            }

            menu.ShowAsContext();
        }

        private void DrawModes()
        {
            ScenarioKitStyles.Section("Режимы");
            EditorGUILayout.LabelField("Выберите режим, чтобы настроить его ниже. «–» убирает режим только из сценария.", GetGreyStyle());
            _modesList.DoLayoutList();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("＋ Тренировка", "Создать новый режим тренировки")))
                {
                    OpenCreateModePopup(typeof(TrainingMode));
                }

                if (GUILayout.Button(new GUIContent("＋ Экзамен", "Создать новый режим экзамена")))
                {
                    OpenCreateModePopup(typeof(ExamMode));
                }

                if (GUILayout.Button(new GUIContent("＋ Существующий", "Добавить режим, который уже есть в проекте")))
                {
                    ShowExistingModesMenu();
                }
            }
        }

        private void DrawModesHeader(Rect rect)
        {
            EditorGUI.LabelField(rect, "Режимы прохождения сценария");
        }

        private void DrawModeElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            ScenarioMode mode = _modesList.serializedProperty.GetArrayElementAtIndex(index).objectReferenceValue as ScenarioMode;
            Rect row = new Rect(rect.x, rect.y + ROW_PADDING, rect.width, ROW_HEIGHT);
            Rect name = new Rect(row.x, row.y, row.width * MODE_NAME_RATIO, row.height);
            Rect type = new Rect(name.xMax + SPACING, row.y, MODE_TYPE_WIDTH, row.height);
            Rect icon = new Rect(type.xMax + SPACING, row.y, ICON_WIDTH, row.height);
            Rect groups = new Rect(icon.xMax + SPACING, row.y, row.xMax - icon.xMax - SPACING, row.height);

            if (mode == null)
            {
                GUI.Label(name, "(пусто) — удалите кнопкой «–»", ScenarioKitStyles.ChipErrorStyle);
                return;
            }

            GUI.Label(name, ModeLabels.GetName(mode), EditorStyles.boldLabel);
            GUI.Label(type, ModeLabels.GetTypeName(mode));
            DrawProblem(row, icon, mode);
            GUI.Label(groups, "групп: " + mode.Groups.Count, GetGreyStyle());
        }

        private void ShowExistingModesMenu()
        {
            GenericMenu menu = new GenericMenu();
            SerializedProperty modes = _modesList.serializedProperty;

            foreach (string path in AssetPickerField.GetAssetPaths(typeof(ScenarioMode)))
            {
                ScenarioMode mode = AssetDatabase.LoadAssetAtPath<ScenarioMode>(path);

                if (mode == null || IndexOf(modes, mode) >= 0)
                {
                    continue;
                }

                string label = ModeLabels.GetName(mode) + " — " + ModeLabels.GetTypeName(mode) + "    (" + mode.name + ")";
                menu.AddItem(new GUIContent(label), false, () => AddExistingMode(mode));
            }

            if (menu.GetItemCount() == 0)
            {
                menu.AddDisabledItem(new GUIContent("Все режимы проекта уже в сценарии"));
            }

            menu.ShowAsContext();
        }

        private void AddExistingMode(ScenarioMode mode)
        {
            int index = AddObjects(MODES_FIELD, new Object[] { mode });
            SelectMode(index);
            Repaint();
        }

        private void AddSteps(List<StepDefinition> steps)
        {
            int index = AddObjects(STEPS_FIELD, steps.ToArray());
            SelectStep(index);
        }

        private int AddObjects(string field, Object[] items)
        {
            if (target == null)
            {
                return -1;
            }

            serializedObject.Update();
            SerializedProperty list = serializedObject.FindProperty(field);
            int lastIndex = -1;

            foreach (Object item in items)
            {
                if (item == null)
                {
                    continue;
                }

                lastIndex = IndexOf(list, item);

                if (lastIndex >= 0)
                {
                    continue;
                }

                lastIndex = list.arraySize;
                list.InsertArrayElementAtIndex(lastIndex);
                list.GetArrayElementAtIndex(lastIndex).objectReferenceValue = item;
            }

            serializedObject.ApplyModifiedProperties();
            ScenarioUsageIndex.Invalidate();
            return lastIndex;
        }

        private void OpenCreateModePopup(Type type)
        {
            string defaultName = ScenarioKitFolders.GetDefaultName(ScenarioKitFolders.MODES);
            Rect activator = new Rect(Event.current.mousePosition.x, Event.current.mousePosition.y, 1f, 1f);
            PopupWindow.Show(activator, new NamePromptPopup(defaultName, name => CreateAndAdd(type, ScenarioKitFolders.MODES, name, MODES_FIELD, UNDO_CREATE_MODE)));
        }

        private void RequestCreate(Type type, string folder, string field, string undoName)
        {
            _pendingName = ScenarioKitFolders.GetDefaultName(folder);
            _pendingCreate = assetName => CreateAndAdd(type, folder, assetName, field, undoName);
            Repaint();
        }

        // Окно ввода имени открываем из OnGUI инспектора: из колбэка GenericMenu контекст GUI уже потерян.
        private void ShowPendingPopup()
        {
            if (_pendingCreate == null || Event.current.type != EventType.Repaint)
            {
                return;
            }

            Action<string> create = _pendingCreate;
            _pendingCreate = null;

            Rect activator = new Rect(Event.current.mousePosition.x, Event.current.mousePosition.y, 1f, 1f);
            PopupWindow.Show(activator, new NamePromptPopup(_pendingName, create));
        }

        private void CreateAndAdd(Type type, string folder, string assetName, string field, string undoName)
        {
            if (target == null)
            {
                return;
            }

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            ScriptableObject asset = AssetPickerField.CreateAsset(type, folder, assetName);

            if (asset == null)
            {
                return;
            }

            if (asset is ScenarioMode)
            {
                SerializedObject mode = new SerializedObject(asset);
                mode.FindProperty(MODE_NAME_FIELD).stringValue = assetName;
                mode.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssetIfDirty(asset);
            }

            int index = AddObjects(field, new Object[] { asset });
            Undo.SetCurrentGroupName(undoName);
            Undo.CollapseUndoOperations(group);

            if (field == STEPS_FIELD)
            {
                SelectStep(index);
            }
            else
            {
                SelectMode(index);
            }

            EditorGUIUtility.PingObject(asset);
            Repaint();
        }

        private void DrawEmbedded(Object item, ref Editor cachedEditor, string title)
        {
            if (item == null)
            {
                return;
            }

            ScenarioKitStyles.Section("Редактирование: " + title);
            CreateCachedEditor(item, null, ref cachedEditor);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                cachedEditor.OnInspectorGUI();
            }
        }

        private void SelectStep(int index)
        {
            _stepsList.index = index;
            SessionState.SetInt(GetStateKey(STEP_STATE), index);
        }

        private void SelectMode(int index)
        {
            _modesList.index = index;
            SessionState.SetInt(GetStateKey(MODE_STATE), index);
        }

        private string GetStateKey(string suffix)
        {
            return STATE_PREFIX + target.GetInstanceID() + suffix;
        }

        private void RunPendingEdit()
        {
            if (_pendingEdit == null)
            {
                return;
            }

            Action edit = _pendingEdit;
            _pendingEdit = null;
            edit();
            Repaint();
        }

        private void StepRemovedHandler(ReorderableList list)
        {
            RemoveElement(list);
            SelectStep(list.index);
        }

        private void ModeRemovedHandler(ReorderableList list)
        {
            RemoveElement(list);
            SelectMode(list.index);
        }

        private void StepSelectedHandler(ReorderableList list)
        {
            SelectStep(list.index);
        }

        private void ModeSelectedHandler(ReorderableList list)
        {
            SelectMode(list.index);
        }

        private void ListReorderedHandler(ReorderableList list)
        {
            ScenarioUsageIndex.Invalidate();
            SelectStep(_stepsList.index);
            SelectMode(_modesList.index);
        }
    }
}
