using System;
using System.Collections.Generic;
using System.IO;
using ScenarioKit.Analysis;
using ScenarioKit.Events;
using ScenarioKit.Execution;
using ScenarioKit.Logging;
using ScenarioKit.Modes;
using ScenarioKit.Steps;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ScenarioKit.EditorTools
{
    [CustomEditor(typeof(ScenarioRunner))]
    public class ScenarioRunnerEditor : Editor
    {
        private const string SCENARIO_FIELD = "_scenario";
        private const string LOGGER_FIELD = "_logger";
        private const string ANALYZER_FIELD = "_analyzerOverride";
        private const string ANALYZERS_FIELD = "_analyzers";
        private const string AUTO_START_FIELD = "_autoStartMode";
        private const string BEFORE_START_FIELD = "_onBeforeSessionStart";
        private const string AUTO_START_NONE = "(выбор на экране)";
        private const string ANALYZER_NONE = "(анализатор режима)";
        private const string NOT_IN_SCENARIO = " (не из сценария)";

        private const int TAPE_SIZE = 10;
        private const float ROW_PADDING = 2f;
        private const float CHIP_HEIGHT = 20f;
        private const float MENU_BUTTON_WIDTH = 22f;
        private const float SEND_BUTTON_WIDTH = 90f;

        private static GUIStyle _greyStyle;

        private readonly List<ScenarioAnalyzer> _projectAnalyzers = new List<ScenarioAnalyzer>();
        private readonly List<StepDefinition> _activeSteps = new List<StepDefinition>();
        private readonly List<string> _tape = new List<string>();
        private readonly List<string> _loadWarnings = new List<string>();

        private ReorderableList _analyzersList;
        private ScenarioSession _session;
        private ScenarioEventKey _simulatedKey;
        private ScenarioLog _loadedLog;
        private string _loadedPath;
        private AnalysisResult _lastResult;
        private AnalysisResult _reviewResult;
        private Action _pendingAction;
        private string _groupName;
        private int _reviewAnalyzerIndex;
        private bool _isSubscribed;
        private bool _isAnalyzerCacheValid;

        private ScenarioRunner Runner => (ScenarioRunner)target;

        private void OnEnable()
        {
            if (target == null)
            {
                return;
            }

            _analyzersList = new ReorderableList(serializedObject, serializedObject.FindProperty(ANALYZERS_FIELD), true, true, true, true);
            _analyzersList.drawHeaderCallback = DrawAnalyzersHeader;
            _analyzersList.drawElementCallback = DrawAnalyzerElement;
            _analyzersList.elementHeight = EditorGUIUtility.singleLineHeight + ROW_PADDING * 2f;
            _analyzersList.onAddCallback = AnalyzerAddedHandler;

            EditorApplication.playModeStateChanged += PlayModeStateChangedHandler;
            EditorApplication.projectChanged += ProjectChangedHandler;
            SyncRuntimeLink();
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= PlayModeStateChangedHandler;
            EditorApplication.projectChanged -= ProjectChangedHandler;
            SetSubscribed(false);
            SetSession(null);
        }

        public override void OnInspectorGUI()
        {
            if (_analyzersList == null)
            {
                return;
            }

            serializedObject.Update();

            if (Event.current.type == EventType.Layout)
            {
                SyncRuntimeLink();
                RefreshSnapshot();
            }

            DrawSetup();
            serializedObject.ApplyModifiedProperties();
            DrawControl();
            DrawResults();
            RunPendingAction();
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

        private static void DrawObjectPopup(SerializedProperty property, GUIContent label, List<Object> options, List<string> labels)
        {
            Object current = property.objectReferenceValue;

            if (current != null && !options.Contains(current))
            {
                options.Add(current);
                labels.Add(current.name + NOT_IN_SCENARIO);
            }

            GUIContent[] contents = new GUIContent[labels.Count];

            for (int i = 0; i < labels.Count; i++)
            {
                contents[i] = new GUIContent(labels[i]);
            }

            Rect rect = EditorGUILayout.GetControlRect();
            label = EditorGUI.BeginProperty(rect, label, property);
            EditorGUI.BeginChangeCheck();
            int selected = EditorGUI.Popup(rect, label, Mathf.Max(0, options.IndexOf(current)), contents);

            if (EditorGUI.EndChangeCheck())
            {
                property.objectReferenceValue = options[selected];
            }

            EditorGUI.EndProperty();
        }

        private static string GetAnalyzerLabel(ScenarioAnalyzer analyzer)
        {
            return analyzer.DisplayName == analyzer.name ? analyzer.name : analyzer.DisplayName + " (" + analyzer.name + ")";
        }

        private void SyncRuntimeLink()
        {
            if (!EditorApplication.isPlaying || target == null)
            {
                SetSubscribed(false);
                SetSession(null);
                return;
            }

            SetSubscribed(true);
            SetSession(Runner.Session);
        }

        private void RefreshSnapshot()
        {
            _activeSteps.Clear();
            _tape.Clear();
            _groupName = null;
            _lastResult = EditorApplication.isPlaying ? Runner.LastResult : null;

            if (_session != null)
            {
                _activeSteps.AddRange(_session.ActiveSteps);
                _groupName = _session.CurrentGroupName;
            }

            ScenarioLog log = EditorApplication.isPlaying ? Runner.CurrentLog ?? Runner.LastLog : null;

            if (log == null || log.Records == null)
            {
                return;
            }

            for (int i = Mathf.Max(0, log.Records.Count - TAPE_SIZE); i < log.Records.Count; i++)
            {
                _tape.Add(ReportFormatter.GetRecordText(log.Records[i]));
            }
        }

        private void DrawSetup()
        {
            ScenarioKitStyles.Section("Настройка");

            SerializedProperty scenario = serializedObject.FindProperty(SCENARIO_FIELD);
            AssetPickerField.DrawLayout<ScenarioDefinition>(scenario, new GUIContent("Сценарий", "Сценарий, который проходит пользователь."), ScenarioKitFolders.ROOT);
            DrawAutoStartPopup(scenario.objectReferenceValue as ScenarioDefinition);

            GUIContent loggerLabel = new GUIContent("Логгер", "Куда записывать журнал. Пусто — журнал хранится только в памяти.");
            AssetPickerField.DrawLayout<ScenarioLoggerAsset>(serializedObject.FindProperty(LOGGER_FIELD), loggerLabel, ScenarioKitFolders.LOGGERS);
            DrawAnalyzerPopup();

            EditorGUILayout.Space();
            _analyzersList.DoLayoutList();

            GUIContent beforeLabel = new GUIContent("Перед стартом сессии", "Вызывается перед стартом каждой сессии, до сброса состояния мира.");
            EditorGUILayout.PropertyField(serializedObject.FindProperty(BEFORE_START_FIELD), beforeLabel);
        }

        private void DrawAutoStartPopup(ScenarioDefinition scenario)
        {
            List<Object> options = new List<Object> { null };
            List<string> labels = new List<string> { AUTO_START_NONE };

            if (scenario != null)
            {
                foreach (ScenarioMode mode in scenario.Modes)
                {
                    if (mode != null && !options.Contains(mode))
                    {
                        options.Add(mode);
                        labels.Add(ModeLabels.GetName(mode) + " — " + ModeLabels.GetTypeName(mode));
                    }
                }
            }

            GUIContent label = new GUIContent("Режим при запуске", "Режим, который запускается сразу при старте сцены. «Выбор на экране» — пользователь выбирает сам.");
            DrawObjectPopup(serializedObject.FindProperty(AUTO_START_FIELD), label, options, labels);
        }

        private void DrawAnalyzerPopup()
        {
            List<Object> options = new List<Object> { null };
            List<string> labels = new List<string> { ANALYZER_NONE };

            foreach (ScenarioAnalyzer analyzer in GetProjectAnalyzers())
            {
                options.Add(analyzer);
                labels.Add(GetAnalyzerLabel(analyzer));
            }

            GUIContent label = new GUIContent("Анализатор", "Чем оценить журнал после прохождения. «Анализатор режима» — тот, что указан в режиме.");
            DrawObjectPopup(serializedObject.FindProperty(ANALYZER_FIELD), label, options, labels);
        }

        private List<ScenarioAnalyzer> GetProjectAnalyzers()
        {
            if (_isAnalyzerCacheValid)
            {
                return _projectAnalyzers;
            }

            _projectAnalyzers.Clear();

            foreach (string path in AssetPickerField.GetAssetPaths(typeof(ScenarioAnalyzer)))
            {
                ScenarioAnalyzer analyzer = AssetDatabase.LoadAssetAtPath<ScenarioAnalyzer>(path);

                if (analyzer != null)
                {
                    _projectAnalyzers.Add(analyzer);
                }
            }

            _isAnalyzerCacheValid = true;
            return _projectAnalyzers;
        }

        private void DrawAnalyzersHeader(Rect rect)
        {
            EditorGUI.LabelField(rect, new GUIContent("Анализаторы для переоценки", "Анализаторы, которыми можно заново оценить журнал."));
        }

        private void DrawAnalyzerElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            Rect field = new Rect(rect.x, rect.y + ROW_PADDING, rect.width, EditorGUIUtility.singleLineHeight);
            SerializedProperty element = _analyzersList.serializedProperty.GetArrayElementAtIndex(index);
            AssetPickerField.Draw<ScenarioAnalyzer>(field, element, GUIContent.none, ScenarioKitFolders.ANALYZERS);
        }

        private void DrawControl()
        {
            ScenarioKitStyles.Section("Управление прохождением");

            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Запуск режимов, симуляция событий и ход прохождения доступны в Play Mode.", MessageType.Info);
                return;
            }

            DrawModeButtons();
            DrawSessionState();
            DrawSimulation();
            DrawTape();
        }

        private void DrawModeButtons()
        {
            ScenarioDefinition scenario = Runner.Scenario;

            if (scenario == null)
            {
                EditorGUILayout.HelpBox("Назначьте сценарий, чтобы запускать режимы.", MessageType.Warning);
                return;
            }

            foreach (ScenarioMode mode in scenario.Modes)
            {
                if (mode == null)
                {
                    continue;
                }

                if (GUILayout.Button(new GUIContent("▶ " + ModeLabels.GetName(mode), "Начать прохождение этого режима заново")))
                {
                    ScenarioMode startMode = mode;
                    _pendingAction = () => Runner.StartMode(startMode);
                }
            }

            using (new EditorGUI.DisabledScope(_session == null))
            {
                if (GUILayout.Button(new GUIContent("■ Прервать", "Прервать текущее прохождение")))
                {
                    _pendingAction = AbortSession;
                }
            }
        }

        private void DrawSessionState()
        {
            if (_session == null)
            {
                EditorGUILayout.LabelField("Прохождение не идёт", GetGreyStyle());
                return;
            }

            EditorGUILayout.LabelField("Режим", ModeLabels.GetName(_session.Mode));
            EditorGUILayout.LabelField("Текущая группа", string.IsNullOrEmpty(_groupName) ? "—" : _groupName);
            EditorGUILayout.LabelField("Активные шаги", EditorStyles.boldLabel);

            if (_activeSteps.Count == 0)
            {
                EditorGUILayout.LabelField("Нет активных шагов", GetGreyStyle());
                return;
            }

            foreach (StepDefinition step in _activeSteps)
            {
                Rect rect = EditorGUILayout.GetControlRect(false, CHIP_HEIGHT);
                StepChip.Draw(rect, step, _session.GetStatus(step), false);
            }
        }

        private void DrawSimulation()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Симуляция события", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUIContent label = new GUIContent("Событие", "Событие, которое будет отправлено так, будто его вызвал объект сцены.");
                _simulatedKey = (ScenarioEventKey)EditorGUILayout.ObjectField(label, _simulatedKey, typeof(ScenarioEventKey), false);

                if (GUILayout.Button(new GUIContent("▾", "Выбрать событие из списка"), EditorStyles.miniButton, GUILayout.Width(MENU_BUTTON_WIDTH)))
                {
                    ShowEventMenu();
                }

                using (new EditorGUI.DisabledScope(_simulatedKey == null))
                {
                    if (GUILayout.Button(new GUIContent("Отправить", "Отправить событие в текущее прохождение"), GUILayout.Width(SEND_BUTTON_WIDTH)))
                    {
                        ScenarioEventKey key = _simulatedKey;
                        _pendingAction = () => Runner.SimulateEvent(key);
                    }
                }
            }
        }

        private void ShowEventMenu()
        {
            GenericMenu menu = new GenericMenu();
            HashSet<ScenarioEventKey> awaited = new HashSet<ScenarioEventKey>();

            foreach (StepDefinition step in _activeSteps)
            {
                if (step == null)
                {
                    continue;
                }

                foreach (ScenarioEventKey key in step.SuccessEvents)
                {
                    if (key != null && awaited.Add(key))
                    {
                        AddEventItem(menu, "Ждут активные шаги/" + key.name, key);
                    }
                }
            }

            foreach (string path in AssetPickerField.GetAssetPaths(typeof(ScenarioEventKey)))
            {
                ScenarioEventKey key = AssetDatabase.LoadAssetAtPath<ScenarioEventKey>(path);

                if (key != null)
                {
                    AddEventItem(menu, "Все события/" + key.name, key);
                }
            }

            if (menu.GetItemCount() == 0)
            {
                menu.AddDisabledItem(new GUIContent("В проекте нет событий"));
            }

            menu.ShowAsContext();
        }

        private void AddEventItem(GenericMenu menu, string path, ScenarioEventKey key)
        {
            menu.AddItem(new GUIContent(path), key == _simulatedKey, () =>
            {
                _simulatedKey = key;
                Repaint();
            });
        }

        private void DrawTape()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Журнал — последние " + TAPE_SIZE + " записей", EditorStyles.boldLabel);

            if (_tape.Count == 0)
            {
                EditorGUILayout.LabelField("Записей пока нет", GetGreyStyle());
                return;
            }

            foreach (string line in _tape)
            {
                EditorGUILayout.LabelField(line, GetGreyStyle());
            }
        }

        private void DrawResults()
        {
            ScenarioKitStyles.Section("Результаты");

            if (_lastResult != null)
            {
                EditorGUILayout.HelpBox(ReportFormatter.GetResultText(_lastResult), MessageType.Info);
            }

            DrawReanalysis();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Загрузить последний журнал", "Прочитать самый свежий файл журнала и оценить его выбранным анализатором")))
                {
                    _pendingAction = LoadLatestLog;
                }

                if (GUILayout.Button(new GUIContent("Открыть папку журналов", "Показать папку с файлами журналов")))
                {
                    _pendingAction = OpenLogFolder;
                }
            }

            foreach (string warning in _loadWarnings)
            {
                EditorGUILayout.HelpBox(warning, MessageType.Warning);
            }
        }

        private void DrawReanalysis()
        {
            ScenarioLog log = GetReviewLog();
            List<ScenarioAnalyzer> analyzers = GetReviewAnalyzers();
            string source = _loadedLog != null ? Path.GetFileName(_loadedPath) : "последнее прохождение";
            EditorGUILayout.LabelField("Журнал для оценки", log != null ? source : "нет — пройдите режим или загрузите журнал");

            if (analyzers.Count == 0)
            {
                EditorGUILayout.LabelField("Добавьте анализаторы в список «Анализаторы для переоценки».", GetGreyStyle());
            }
            else
            {
                string[] labels = new string[analyzers.Count];

                for (int i = 0; i < analyzers.Count; i++)
                {
                    labels[i] = GetAnalyzerLabel(analyzers[i]);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    _reviewAnalyzerIndex = Mathf.Clamp(_reviewAnalyzerIndex, 0, analyzers.Count - 1);
                    _reviewAnalyzerIndex = EditorGUILayout.Popup("Анализатор", _reviewAnalyzerIndex, labels);

                    using (new EditorGUI.DisabledScope(log == null))
                    {
                        if (GUILayout.Button(new GUIContent("Переоценить", "Оценить журнал выбранным анализатором"), GUILayout.Width(SEND_BUTTON_WIDTH)))
                        {
                            _pendingAction = Reanalyze;
                        }
                    }
                }
            }

            if (_reviewResult != null)
            {
                EditorGUILayout.HelpBox("Переоценка\n" + ReportFormatter.GetResultText(_reviewResult), MessageType.None);
            }
        }

        private ScenarioLog GetReviewLog()
        {
            if (_loadedLog != null)
            {
                return _loadedLog;
            }

            return EditorApplication.isPlaying ? Runner.LastLog : null;
        }

        private List<ScenarioAnalyzer> GetReviewAnalyzers()
        {
            List<ScenarioAnalyzer> analyzers = new List<ScenarioAnalyzer>();

            foreach (ScenarioAnalyzer analyzer in Runner.Analyzers)
            {
                if (analyzer != null && !analyzers.Contains(analyzer))
                {
                    analyzers.Add(analyzer);
                }
            }

            return analyzers;
        }

        private ScenarioAnalyzer GetReviewAnalyzer()
        {
            List<ScenarioAnalyzer> analyzers = GetReviewAnalyzers();

            if (analyzers.Count == 0)
            {
                return null;
            }

            return analyzers[Mathf.Clamp(_reviewAnalyzerIndex, 0, analyzers.Count - 1)];
        }

        private void Reanalyze()
        {
            ScenarioLog log = GetReviewLog();

            if (log != null)
            {
                _reviewResult = Runner.Analyze(log, GetReviewAnalyzer());
            }
        }

        private void LoadLatestLog()
        {
            _loadWarnings.Clear();
            string folder = ScenarioLogStorage.GetFolderPath(Runner.Logger);
            string path = ScenarioLogStorage.GetLatestPath(folder);

            if (string.IsNullOrEmpty(path))
            {
                _loadWarnings.Add("В папке журналов пока нет файлов: " + folder);
                return;
            }

            ScenarioLog log = ScenarioLogStorage.Load(path, _loadWarnings);

            if (log == null)
            {
                return;
            }

            _loadedLog = log;
            _loadedPath = path;
            _reviewResult = null;

            if (GetReviewAnalyzer() != null)
            {
                Reanalyze();
            }
        }

        private void OpenLogFolder()
        {
            string folder = ScenarioLogStorage.GetFolderPath(Runner.Logger);
            Directory.CreateDirectory(folder);
            EditorUtility.RevealInFinder(folder);
        }

        private void AbortSession()
        {
            Runner.AbortSession();
            SetSession(null);
            _loadedLog = null;
            _reviewResult = null;
        }

        private void RunPendingAction()
        {
            if (_pendingAction == null)
            {
                return;
            }

            Action action = _pendingAction;
            _pendingAction = null;
            action();
            Repaint();
        }

        private void SetSubscribed(bool isSubscribed)
        {
            if (_isSubscribed == isSubscribed)
            {
                return;
            }

            ScenarioRunner runner = target as ScenarioRunner;

            if (isSubscribed && runner == null)
            {
                return;
            }

            _isSubscribed = isSubscribed;

            if (ReferenceEquals(runner, null))
            {
                return;
            }

            if (isSubscribed)
            {
                runner.SessionStartedEvent += SessionStartedEventHandler;
                runner.SessionFinishedEvent += SessionFinishedEventHandler;
            }
            else
            {
                runner.SessionStartedEvent -= SessionStartedEventHandler;
                runner.SessionFinishedEvent -= SessionFinishedEventHandler;
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

        private void AnalyzerAddedHandler(ReorderableList list)
        {
            SerializedProperty property = list.serializedProperty;
            int index = property.arraySize;
            property.InsertArrayElementAtIndex(index);
            property.GetArrayElementAtIndex(index).objectReferenceValue = null;
            list.index = index;
        }

        private void PlayModeStateChangedHandler(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                SetSubscribed(false);
                SetSession(null);
                _loadedLog = null;
                _reviewResult = null;
            }
            else if (state == PlayModeStateChange.EnteredPlayMode)
            {
                SyncRuntimeLink();
            }

            Repaint();
        }

        private void ProjectChangedHandler()
        {
            _isAnalyzerCacheValid = false;
        }

        private void SessionStartedEventHandler(ScenarioSession session)
        {
            SetSession(session);
            _loadedLog = null;
            _reviewResult = null;
            Repaint();
        }

        private void SessionFinishedEventHandler(ScenarioLog log, AnalysisResult result)
        {
            SetSession(null);
            Repaint();
        }

        private void ChangedEventHandler()
        {
            Repaint();
        }
    }
}
