using System;
using System.Collections.Generic;
using ScenarioKit.Analysis;
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
    [CustomEditor(typeof(ScenarioMode), true)]
    public class ScenarioModeEditor : Editor
    {
        private const string STATE_PREFIX = "ScenarioKit.ScenarioModeEditor.";
        private const string DISPLAY_NAME_FIELD = "_displayName";
        private const string OUT_OF_ORDER_FIELD = "_outOfOrder";
        private const string ANALYZER_FIELD = "_defaultAnalyzer";
        private const string THRESHOLD_FIELD = "_violationThreshold";

        private const int THRESHOLD_MIN = 1;
        private const int THRESHOLD_MAX = 10;
        private const float LINE_HEIGHT = 18f;
        private const float PADDING = 4f;
        private const float SPACING = 4f;
        private const float CHIP_HEIGHT = 20f;
        private const float CHIP_SPACING = 4f;
        private const float CHIP_MIN_WIDTH = 60f;
        private const float CHIP_MAX_WIDTH = 280f;
        private const float REMOVE_WIDTH = 16f;
        private const float ARROW_HEIGHT = 16f;
        private const float NEW_STAGE_HEIGHT = 26f;
        private const float BUTTON_WIDTH = 22f;
        private const float FOLDOUT_WIDTH = 14f;
        private const float STRIPE_WIDTH = 4f;
        private const float RUNNING_WIDTH = 60f;
        private const float OUTLINE = 2f;
        private const float DRAG_THRESHOLD = 4f;
        private const float DEFAULT_WIDTH = 320f;
        private const float LIST_MARGIN = 40f;
        private const float FRAME_ALPHA = 0.06f;
        private const float GROUP_HUE_STEP = 0.13f;
        private const float GROUP_SATURATION = 0.55f;
        private const float GROUP_VALUE = 0.85f;
        private const string RUNNING_LABEL = "▶ идёт";

        private static readonly int _chipDragHash = "ScenarioKit.ChipDrag".GetHashCode();

        private static readonly GUIContent[] _outOfOrderLabels =
        {
            new GUIContent("Считать нарушением", "Действие раньше своей очереди записывается как нарушение."),
            new GUIContent("Шаг неактивен до своей очереди", "Действие раньше своей очереди просто не засчитывается.")
        };

        private static GUIStyle _greyStyle;
        private static GUIStyle _removeStyle;

        private readonly List<Rect> _chipRects = new List<Rect>();
        private readonly HashSet<Object> _errorContexts = new HashSet<Object>();
        private readonly HashSet<Object> _warningContexts = new HashSet<Object>();

        private ReorderableList _groupsList;
        private Action _pendingEdit;
        private ScenarioRunner _runner;
        private ScenarioSession _session;
        private IReadOnlyList<StepDefinition> _library;
        private Vector2 _dragStart;
        private float _contentWidth;
        private float _groupWidth;

        private ScenarioMode Mode => (ScenarioMode)target;

        private void OnEnable()
        {
            _contentWidth = DEFAULT_WIDTH;
            _groupWidth = _contentWidth - LIST_MARGIN;
            _groupsList = null;

            if (target == null || !ModeLayoutEditing.HasGroupList(serializedObject))
            {
                return;
            }

            _groupsList = new ReorderableList(serializedObject, serializedObject.FindProperty(ModeLayoutEditing.GROUPS_FIELD), true, true, false, false);
            _groupsList.drawHeaderCallback = DrawGroupsHeader;
            _groupsList.drawElementCallback = DrawGroupElement;
            _groupsList.elementHeightCallback = GetGroupElementHeight;
            _groupsList.onReorderCallback = GroupsReorderedHandler;
        }

        private void OnDisable()
        {
            SetRunner(null);
            SetSession(null);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            if (!ModeLayoutEditing.HasSupportedLayout(serializedObject))
            {
                DrawDefaultInspector();
                return;
            }

            if (Event.current.type == EventType.Layout)
            {
                UpdateRuntimeLink();
            }

            _library = GetLibrary();
            DrawValidation();
            DrawSettings();
            DrawPalette();
            DrawGroups();
            serializedObject.ApplyModifiedProperties();
            RunPendingEdit();
        }

        private static GUIStyle GetGreyStyle()
        {
            if (_greyStyle == null)
            {
                _greyStyle = new GUIStyle(EditorStyles.miniLabel);
                _greyStyle.normal.textColor = Color.grey;
                _greyStyle.wordWrap = true;
            }

            return _greyStyle;
        }

        private static GUIStyle GetRemoveStyle()
        {
            if (_removeStyle == null)
            {
                _removeStyle = new GUIStyle(EditorStyles.miniLabel);
                _removeStyle.alignment = TextAnchor.MiddleCenter;
            }

            return _removeStyle;
        }

        private static Color GetGroupColor(int index)
        {
            return Color.HSVToRGB(Mathf.Repeat(index * GROUP_HUE_STEP, 1f), GROUP_SATURATION, GROUP_VALUE);
        }

        private static float GetChipWidth(StepDefinition step, float maxWidth)
        {
            float textWidth = ScenarioKitStyles.ChipStyle.CalcSize(new GUIContent(StepChip.GetChipText(step))).x;
            return Mathf.Clamp(textWidth + REMOVE_WIDTH, Mathf.Min(CHIP_MIN_WIDTH, maxWidth), Mathf.Min(CHIP_MAX_WIDTH, maxWidth));
        }

        private static float LayoutChips(IReadOnlyList<StepDefinition> steps, Rect area, List<Rect> output)
        {
            if (output != null)
            {
                output.Clear();
            }

            float x = area.x;
            float y = area.y;

            foreach (StepDefinition step in steps)
            {
                float width = GetChipWidth(step, area.width);

                if (x > area.x && x + width > area.xMax)
                {
                    x = area.x;
                    y += CHIP_HEIGHT + CHIP_SPACING;
                }

                if (output != null)
                {
                    output.Add(new Rect(x, y, width, CHIP_HEIGHT));
                }

                x += width + CHIP_SPACING;
            }

            return steps.Count == 0 ? CHIP_HEIGHT : y + CHIP_HEIGHT - area.y;
        }

        private static float GetStageHeight(IReadOnlyList<StepDefinition> steps, float width)
        {
            float chips = LayoutChips(steps, new Rect(0f, 0f, width - PADDING * 2f, 0f), null);
            return PADDING + LINE_HEIGHT + PADDING + chips + PADDING;
        }

        private static int GetInsertIndex(List<Rect> chipRects, Vector2 mouse)
        {
            for (int i = 0; i < chipRects.Count; i++)
            {
                Rect rect = chipRects[i];

                if (mouse.y < rect.y || (mouse.y <= rect.yMax && mouse.x < rect.center.x))
                {
                    return i;
                }
            }

            return chipRects.Count;
        }

        private static string GetStageTitle(int stageIndex, int stepCount)
        {
            string title = "Стадия " + (stageIndex + 1);

            if (stepCount == 0)
            {
                return title + " — пустая";
            }

            return stepCount > 1 ? title + " — одновременно (" + stepCount + ")" : title;
        }

        private static bool Contains(IReadOnlyList<ScenarioMode> modes, ScenarioMode mode)
        {
            foreach (ScenarioMode item in modes)
            {
                if (item == mode)
                {
                    return true;
                }
            }

            return false;
        }

        private IReadOnlyList<StepDefinition> GetLibrary()
        {
            IReadOnlyList<ScenarioDefinition> scenarios = ScenarioUsageIndex.GetScenariosUsing(Mode);
            return scenarios.Count > 0 && scenarios[0] != null ? scenarios[0].Steps : null;
        }

        private void DrawValidation()
        {
            _errorContexts.Clear();
            _warningContexts.Clear();

            foreach (ValidationMessage message in ScenarioValidator.GetModeMessages(Mode, _library))
            {
                bool isError = message.Severity == ValidationSeverity.Error;
                EditorGUILayout.HelpBox(message.Text, isError ? MessageType.Error : MessageType.Warning);

                if (message.Context is StepDefinition)
                {
                    (isError ? _errorContexts : _warningContexts).Add(message.Context);
                }
            }
        }

        private void DrawSettings()
        {
            ScenarioKitStyles.Section("Настройки режима");
            EditorGUILayout.PropertyField(serializedObject.FindProperty(DISPLAY_NAME_FIELD), new GUIContent("Название", "Название режима, которое увидит пользователь."));
            DrawOutOfOrderPopup();

            GUIContent analyzerLabel = new GUIContent("Анализатор по умолчанию", "Чем оценивается журнал после прохождения этого режима.");
            AssetPickerField.DrawLayout<ScenarioAnalyzer>(serializedObject.FindProperty(ANALYZER_FIELD), analyzerLabel, ScenarioKitFolders.ANALYZERS);

            SerializedProperty threshold = serializedObject.FindProperty(THRESHOLD_FIELD);

            if (threshold == null)
            {
                return;
            }

            EditorGUILayout.IntSlider(threshold, THRESHOLD_MIN, THRESHOLD_MAX, new GUIContent("Порог нарушений в группе", "После N нарушений оставшиеся шаги группы пропускаются."));
            EditorGUILayout.LabelField("После " + threshold.intValue + " нарушений оставшиеся шаги группы пропускаются", GetGreyStyle());
        }

        private void DrawOutOfOrderPopup()
        {
            SerializedProperty outOfOrder = serializedObject.FindProperty(OUT_OF_ORDER_FIELD);
            GUIContent label = new GUIContent("Действие вне очереди", "Что делать, если пользователь выполнил шаг раньше его очереди.");
            Rect rect = EditorGUILayout.GetControlRect();
            label = EditorGUI.BeginProperty(rect, label, outOfOrder);
            EditorGUI.BeginChangeCheck();
            int selected = EditorGUI.Popup(rect, label, outOfOrder.enumValueIndex, _outOfOrderLabels);

            if (EditorGUI.EndChangeCheck())
            {
                outOfOrder.enumValueIndex = selected;
            }

            EditorGUI.EndProperty();
        }

        private void DrawPalette()
        {
            ScenarioKitStyles.Section("Шаги сценария, которые ещё не в режиме");

            if (_library == null)
            {
                EditorGUILayout.LabelField("Режим не входит ни в один сценарий — перетащите шаги из окна Project", GetGreyStyle());
                return;
            }

            List<StepDefinition> unused = GetUnusedSteps();

            if (unused.Count == 0)
            {
                EditorGUILayout.LabelField("Все шаги сценария распределены", GetGreyStyle());
                return;
            }

            EditorGUILayout.LabelField("Перетащите шаг мышью в нужную стадию", GetGreyStyle());
            float height = LayoutChips(unused, new Rect(0f, 0f, _contentWidth, 0f), null);
            Rect area = GUILayoutUtility.GetRect(_contentWidth, height, GUILayout.ExpandWidth(true));
            StoreWidth(area.width, ref _contentWidth);
            LayoutChips(unused, area, _chipRects);

            for (int i = 0; i < unused.Count; i++)
            {
                DrawChip(_chipRects[i], unused[i], -1, -1, i);
            }
        }

        private List<StepDefinition> GetUnusedSteps()
        {
            HashSet<StepDefinition> used = new HashSet<StepDefinition>(Mode.GetSteps());
            List<StepDefinition> unused = new List<StepDefinition>();

            foreach (StepDefinition step in _library)
            {
                if (step != null && used.Add(step))
                {
                    unused.Add(step);
                }
            }

            return unused;
        }

        private void DrawGroups()
        {
            bool hasGroupList = _groupsList != null;
            ScenarioKitStyles.Section(hasGroupList ? "Группы шагов" : "Порядок шагов");
            EditorGUILayout.LabelField("Стадии идут по очереди, шаги одной стадии активны одновременно. Шаги переносятся мышью.", GetGreyStyle());

            if (hasGroupList)
            {
                _groupsList.DoLayoutList();

                if (GUILayout.Button(new GUIContent("＋ Группа", "Добавить новую группу шагов в конец экзамена")))
                {
                    _pendingEdit = () => ModeLayoutEditing.AddGroup(serializedObject);
                }

                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                if (GUILayout.Button(new GUIContent("⋮ Действия", "Быстрые действия со всеми стадиями"), EditorStyles.miniButton))
                {
                    ShowGroupMenu(0, false);
                }
            }

            float height = GetStagesHeight(0, _contentWidth);
            Rect area = GUILayoutUtility.GetRect(_contentWidth, height, GUILayout.ExpandWidth(true));
            StoreWidth(area.width, ref _contentWidth);
            DrawStages(area, 0);
        }

        private float GetStagesHeight(int groupIndex, float width)
        {
            SerializedProperty stages = ModeLayoutEditing.GetStages(serializedObject, groupIndex);
            float height = 0f;

            for (int i = 0; i < stages.arraySize; i++)
            {
                if (i > 0)
                {
                    height += ARROW_HEIGHT;
                }

                height += GetStageHeight(ModeLayoutEditing.GetSteps(ModeLayoutEditing.GetStageSteps(stages, i)), width);
            }

            return height + SPACING + NEW_STAGE_HEIGHT;
        }

        private void DrawStages(Rect area, int groupIndex)
        {
            SerializedProperty stages = ModeLayoutEditing.GetStages(serializedObject, groupIndex);
            float y = area.y;

            for (int i = 0; i < stages.arraySize; i++)
            {
                if (i > 0)
                {
                    GUI.Label(new Rect(area.x, y, area.width, ARROW_HEIGHT), "↓ затем", EditorStyles.centeredGreyMiniLabel);
                    y += ARROW_HEIGHT;
                }

                List<StepDefinition> steps = ModeLayoutEditing.GetSteps(ModeLayoutEditing.GetStageSteps(stages, i));
                float height = GetStageHeight(steps, area.width);
                DrawStage(new Rect(area.x, y, area.width, height), groupIndex, i, stages.arraySize, steps);
                y += height;
            }

            DrawNewStageZone(new Rect(area.x, y + SPACING, area.width, NEW_STAGE_HEIGHT), groupIndex, stages.arraySize);
        }

        private void DrawStage(Rect rect, int groupIndex, int stageIndex, int stageCount, List<StepDefinition> steps)
        {
            Event current = Event.current;

            if (current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, FRAME_ALPHA));
                bool isEmpty = steps.Count == 0;
                Color border = isEmpty ? ScenarioKitStyles.GetStatusColor(StepStatus.Skipped) : ScenarioKitStyles.GetSeparatorColor();
                ScenarioKitStyles.DrawBorder(rect, border, 1f);
            }

            Rect header = new Rect(rect.x + PADDING, rect.y + PADDING, rect.width - PADDING * 2f, LINE_HEIGHT);
            DrawStageHeader(header, groupIndex, stageIndex, stageCount, steps.Count);

            Rect chipsArea = new Rect(header.x, header.yMax + PADDING, header.width, CHIP_HEIGHT);

            if (steps.Count == 0)
            {
                GUI.Label(chipsArea, "Пустая стадия — перетащите сюда шаг", GetGreyStyle());
                _chipRects.Clear();
            }
            else
            {
                LayoutChips(steps, chipsArea, _chipRects);

                for (int i = 0; i < steps.Count; i++)
                {
                    DrawChip(_chipRects[i], steps[i], groupIndex, stageIndex, i);
                }
            }

            if (StepDragAndDrop.TryAcceptDrop(rect, out List<StepDefinition> dropped, out StepDragData _))
            {
                int index = GetInsertIndex(_chipRects, current.mousePosition);
                _pendingEdit = () => ModeLayoutEditing.MoveSteps(serializedObject, dropped, groupIndex, stageIndex, index);
            }

            if (current.type == EventType.MouseDown && rect.Contains(current.mousePosition))
            {
                current.Use();
            }
        }

        private void DrawStageHeader(Rect header, int groupIndex, int stageIndex, int stageCount, int stepCount)
        {
            Rect up = new Rect(header.x, header.y, BUTTON_WIDTH, header.height);
            Rect down = new Rect(up.xMax, header.y, BUTTON_WIDTH, header.height);
            Rect menu = new Rect(header.xMax - BUTTON_WIDTH, header.y, BUTTON_WIDTH, header.height);
            Rect title = new Rect(down.xMax + SPACING, header.y, menu.x - down.xMax - SPACING * 2f, header.height);

            using (new EditorGUI.DisabledScope(stageIndex == 0))
            {
                if (GUI.Button(up, new GUIContent("▲", "Поднять стадию выше"), EditorStyles.miniButtonLeft))
                {
                    _pendingEdit = () => ModeLayoutEditing.MoveStage(serializedObject, groupIndex, stageIndex, stageIndex - 1);
                }
            }

            using (new EditorGUI.DisabledScope(stageIndex >= stageCount - 1))
            {
                if (GUI.Button(down, new GUIContent("▼", "Опустить стадию ниже"), EditorStyles.miniButtonRight))
                {
                    _pendingEdit = () => ModeLayoutEditing.MoveStage(serializedObject, groupIndex, stageIndex, stageIndex + 1);
                }
            }

            GUI.Label(title, GetStageTitle(stageIndex, stepCount), EditorStyles.boldLabel);

            if (GUI.Button(menu, new GUIContent("⋮", "Действия со стадией"), EditorStyles.miniButton))
            {
                ShowStageMenu(groupIndex, stageIndex, stageCount, stepCount);
            }
        }

        private void DrawNewStageZone(Rect zone, int groupIndex, int stageCount)
        {
            if (Event.current.type == EventType.Repaint)
            {
                ScenarioKitStyles.DrawBorder(zone, ScenarioKitStyles.GetSeparatorColor(), 1f);
            }

            GUI.Label(zone, "＋ Перетащите шаг сюда, чтобы создать новую стадию", EditorStyles.centeredGreyMiniLabel);

            if (StepDragAndDrop.TryAcceptDrop(zone, out List<StepDefinition> dropped, out StepDragData _))
            {
                _pendingEdit = () => ModeLayoutEditing.MoveStepsToNewStage(serializedObject, dropped, groupIndex, stageCount);
            }
        }

        private void DrawChip(Rect rect, StepDefinition step, int groupIndex, int stageIndex, int index)
        {
            StepChip.Draw(rect, step, GetStatus(step), false);
            DrawValidationOutline(rect, step);
            Rect body = rect;

            if (groupIndex >= 0)
            {
                Rect remove = new Rect(rect.xMax - REMOVE_WIDTH, rect.y, REMOVE_WIDTH, rect.height);
                body.width -= REMOVE_WIDTH;

                if (GUI.Button(remove, new GUIContent("✕", "Убрать шаг из стадии — он вернётся в список свободных шагов"), GetRemoveStyle()))
                {
                    _pendingEdit = () => ModeLayoutEditing.RemoveStepAt(serializedObject, groupIndex, stageIndex, index);
                }
            }

            object source = groupIndex >= 0 ? ModeLayoutEditing.GetStageSteps(ModeLayoutEditing.GetStages(serializedObject, groupIndex), stageIndex).propertyPath : null;
            HandleChipDrag(body, step, source, index);
        }

        private void DrawValidationOutline(Rect rect, StepDefinition step)
        {
            if (Event.current.type != EventType.Repaint || step == null)
            {
                return;
            }

            if (_errorContexts.Contains(step))
            {
                ScenarioKitStyles.DrawBorder(rect, ScenarioKitStyles.GetErrorColor(), OUTLINE);
            }
            else if (_warningContexts.Contains(step))
            {
                ScenarioKitStyles.DrawBorder(rect, ScenarioKitStyles.GetStatusColor(StepStatus.Skipped), OUTLINE);
            }
        }

        private void HandleChipDrag(Rect rect, StepDefinition step, object source, int index)
        {
            int controlId = GUIUtility.GetControlID(_chipDragHash, FocusType.Passive, rect);
            Event current = Event.current;

            switch (current.GetTypeForControl(controlId))
            {
                case EventType.MouseDown:
                    if (current.button == 0 && rect.Contains(current.mousePosition) && step != null)
                    {
                        GUIUtility.hotControl = controlId;
                        _dragStart = current.mousePosition;
                        current.Use();
                    }

                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == controlId && Vector2.Distance(_dragStart, current.mousePosition) > DRAG_THRESHOLD)
                    {
                        GUIUtility.hotControl = 0;
                        StepDragAndDrop.BeginDrag(step, source, index);
                        current.Use();
                    }

                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == controlId)
                    {
                        GUIUtility.hotControl = 0;
                        current.Use();
                    }

                    break;
            }
        }

        private void DrawGroupsHeader(Rect rect)
        {
            EditorGUI.LabelField(rect, "Группы идут по очереди: следующая начинается, когда закончена предыдущая");
        }

        private float GetGroupElementHeight(int index)
        {
            float height = PADDING + LINE_HEIGHT + PADDING;

            if (IsGroupExpanded(index))
            {
                height += GetStagesHeight(index, _groupWidth) + PADDING;
            }

            return height;
        }

        private void DrawGroupElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            SerializedProperty group = ModeLayoutEditing.GetGroup(serializedObject, index);
            Rect stripe = new Rect(rect.x, rect.y + PADDING, STRIPE_WIDTH, rect.height - PADDING * 2f);
            EditorGUI.DrawRect(stripe, GetGroupColor(index));

            float contentX = stripe.xMax + SPACING;
            float contentWidth = rect.xMax - contentX;
            Rect header = new Rect(contentX, rect.y + PADDING, contentWidth, LINE_HEIGHT);
            DrawGroupHeader(header, group, index);

            if (!IsGroupExpanded(index))
            {
                return;
            }

            if (Event.current.type == EventType.Repaint)
            {
                StoreWidth(contentWidth, ref _groupWidth);
            }

            Rect area = new Rect(contentX, header.yMax + PADDING, contentWidth, GetStagesHeight(index, contentWidth));
            DrawStages(area, index);
        }

        private void DrawGroupHeader(Rect header, SerializedProperty group, int index)
        {
            Rect foldout = new Rect(header.x, header.y, FOLDOUT_WIDTH, header.height);
            Rect menu = new Rect(header.xMax - BUTTON_WIDTH, header.y, BUTTON_WIDTH, header.height);
            SerializedProperty name = group.FindPropertyRelative(ModeLayoutEditing.NAME_FIELD);
            bool isRunning = IsGroupRunning(name.stringValue);
            float nameRight = menu.x - SPACING - (isRunning ? RUNNING_WIDTH + SPACING : 0f);
            Rect nameRect = new Rect(foldout.xMax, header.y, nameRight - foldout.xMax, header.height);

            bool expanded = IsGroupExpanded(index);
            bool newExpanded = EditorGUI.Foldout(foldout, expanded, GUIContent.none, true);

            if (newExpanded != expanded)
            {
                SessionState.SetBool(GetFoldoutKey(index), newExpanded);
            }

            EditorGUI.PropertyField(nameRect, name, GUIContent.none);

            if (isRunning)
            {
                Rect running = new Rect(nameRect.xMax + SPACING, header.y, RUNNING_WIDTH, header.height);
                Color color = GUI.contentColor;
                GUI.contentColor = ScenarioKitStyles.GetStatusColor(StepStatus.Active);
                GUI.Label(running, RUNNING_LABEL, EditorStyles.boldLabel);
                GUI.contentColor = color;
            }

            if (GUI.Button(menu, new GUIContent("⋮", "Действия с группой"), EditorStyles.miniButton))
            {
                ShowGroupMenu(index, true);
            }
        }

        private bool IsGroupExpanded(int index)
        {
            return SessionState.GetBool(GetFoldoutKey(index), true);
        }

        private string GetFoldoutKey(int index)
        {
            return STATE_PREFIX + target.GetInstanceID() + ".Group." + index;
        }

        private void ShowStageMenu(int groupIndex, int stageIndex, int stageCount, int stepCount)
        {
            GenericMenu menu = new GenericMenu();
            AddMenuItem(menu, "Разбить на отдельные стадии (по очереди)", stepCount > 1, () => ModeLayoutEditing.SplitStage(serializedObject, groupIndex, stageIndex));
            AddMenuItem(menu, "Объединить со следующей (одновременно)", stageIndex + 1 < stageCount, () => ModeLayoutEditing.MergeWithNext(serializedObject, groupIndex, stageIndex));
            menu.AddSeparator(string.Empty);
            AddMenuItem(menu, "Удалить стадию", true, () => ModeLayoutEditing.RemoveStage(serializedObject, groupIndex, stageIndex));
            menu.ShowAsContext();
        }

        private void ShowGroupMenu(int groupIndex, bool canRemove)
        {
            GenericMenu menu = new GenericMenu();
            AddMenuItem(menu, "Все шаги одновременно", true, () => ModeLayoutEditing.SetAllParallel(serializedObject, groupIndex));
            AddMenuItem(menu, "Все шаги по очереди", true, () => ModeLayoutEditing.SetAllSequential(serializedObject, groupIndex));
            AddMenuItem(menu, "Удалить пустые стадии", true, () => ModeLayoutEditing.RemoveEmptyStages(serializedObject, groupIndex));

            if (canRemove)
            {
                menu.AddSeparator(string.Empty);
                AddMenuItem(menu, "Удалить группу", true, () => ModeLayoutEditing.RemoveGroup(serializedObject, groupIndex));
            }

            menu.ShowAsContext();
        }

        private void AddMenuItem(GenericMenu menu, string text, bool isEnabled, Action edit)
        {
            GUIContent content = new GUIContent(text);

            if (!isEnabled)
            {
                menu.AddDisabledItem(content);
                return;
            }

            menu.AddItem(content, false, () =>
            {
                edit();
                Repaint();
            });
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

        private void StoreWidth(float width, ref float stored)
        {
            if (Event.current.type == EventType.Repaint && width > 1f && Mathf.Abs(width - stored) > 0.5f)
            {
                stored = width;
                Repaint();
            }
        }

        private StepStatus? GetStatus(StepDefinition step)
        {
            if (_session == null || step == null)
            {
                return null;
            }

            return _session.GetStatus(step);
        }

        private bool IsGroupRunning(string groupName)
        {
            return _session != null && !_session.IsFinished && string.Equals(_session.CurrentGroupName, groupName, StringComparison.Ordinal);
        }

        private void UpdateRuntimeLink()
        {
            if (!EditorApplication.isPlaying)
            {
                SetRunner(null);
                SetSession(null);
                return;
            }

            SetRunner(FindRunner());
            bool isRunningThisMode = _runner != null && _runner.Session != null && _runner.Session.Mode == Mode;
            SetSession(isRunningThisMode ? _runner.Session : null);
        }

        private ScenarioRunner FindRunner()
        {
            ScenarioRunner fallback = null;

            foreach (ScenarioRunner runner in Object.FindObjectsByType<ScenarioRunner>(FindObjectsSortMode.None))
            {
                if (runner.Session != null && runner.Session.Mode == Mode)
                {
                    return runner;
                }

                if (fallback == null && runner.Scenario != null && Contains(runner.Scenario.Modes, Mode))
                {
                    fallback = runner;
                }
            }

            return fallback;
        }

        private void SetRunner(ScenarioRunner runner)
        {
            if (ReferenceEquals(_runner, runner))
            {
                return;
            }

            if (!ReferenceEquals(_runner, null))
            {
                _runner.SessionStartedEvent -= SessionStartedEventHandler;
            }

            _runner = runner;

            if (!ReferenceEquals(_runner, null))
            {
                _runner.SessionStartedEvent += SessionStartedEventHandler;
            }
        }

        private void SetSession(ScenarioSession session)
        {
            if (ReferenceEquals(_session, session))
            {
                return;
            }

            if (_session != null)
            {
                _session.ChangedEvent -= ChangedEventHandler;
            }

            _session = session;

            if (_session != null)
            {
                _session.ChangedEvent += ChangedEventHandler;
            }

            Repaint();
        }

        private void GroupsReorderedHandler(ReorderableList list)
        {
            ScenarioUsageIndex.Invalidate();
        }

        private void SessionStartedEventHandler(ScenarioSession session)
        {
            SetSession(session != null && session.Mode == Mode ? session : null);
            Repaint();
        }

        private void ChangedEventHandler()
        {
            Repaint();
        }
    }
}
