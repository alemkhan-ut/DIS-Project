using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ScenarioKit.Analysis;
using ScenarioKit.Execution;
using ScenarioKit.Logging;
using ScenarioKit.Modes;
using ScenarioKit.Steps;
using UnityEngine;
using UnityEngine.UI;

namespace ScenarioKit.Demo
{
    public class ScenarioHud : MonoBehaviour
    {
        private const float MESSAGE_DURATION = 3f;
        private const string TASK_PREFIX = "• ";

        [Header("Раннер")]
        [SerializeField]
        [Tooltip("Раннер сценария, которым управляет интерфейс.")]
        private ScenarioRunner _runner;

        [Header("Стартовая панель")]
        [SerializeField]
        [Tooltip("Панель выбора режима.")]
        private GameObject _startPanel;

        [SerializeField]
        [Tooltip("Префаб кнопки режима.")]
        private ModeButton _modeButtonPrefab;

        [SerializeField]
        [Tooltip("Куда добавляются кнопки режимов.")]
        private Transform _modeButtonsRoot;

        [SerializeField]
        [Tooltip("Кнопка «Загрузить последний журнал» на стартовой панели.")]
        private Button _startLoadLatestButton;

        [Header("Панель прохождения")]
        [SerializeField]
        [Tooltip("Панель с режимом, группой и списком заданий.")]
        private GameObject _statusPanel;

        [SerializeField]
        [Tooltip("Строка «Режим / группа».")]
        private Text _statusText;

        [SerializeField]
        [Tooltip("Список активных заданий.")]
        private Text _tasksText;

        [SerializeField]
        [Tooltip("Кнопка «Пройти заново» во время прохождения.")]
        private Button _statusRestartButton;

        [SerializeField]
        [Tooltip("Кнопка «Прервать» во время прохождения.")]
        private Button _statusAbortButton;

        [Header("Сообщение о нарушении")]
        [SerializeField]
        [Tooltip("Панель сообщения о нарушении.")]
        private GameObject _messagePanel;

        [SerializeField]
        [Tooltip("Текст сообщения о нарушении.")]
        private Text _messageText;

        [Header("Панель результатов")]
        [SerializeField]
        [Tooltip("Панель с итогом прохождения.")]
        private GameObject _resultsPanel;

        [SerializeField]
        [Tooltip("Текст итога.")]
        private Text _resultText;

        [SerializeField]
        [Tooltip("Выбор анализатора для переоценки.")]
        private Dropdown _analyzerDropdown;

        [SerializeField]
        [Tooltip("Кнопка «Переоценить».")]
        private Button _reanalyzeButton;

        [SerializeField]
        [Tooltip("Кнопка «Загрузить последний журнал».")]
        private Button _loadLatestButton;

        [SerializeField]
        [Tooltip("Кнопка «Показать журнал».")]
        private Button _showLogButton;

        [SerializeField]
        [Tooltip("Кнопка «Пройти заново».")]
        private Button _restartButton;

        [SerializeField]
        [Tooltip("Кнопка «Выбрать режим».")]
        private Button _chooseModeButton;

        [Header("Панель журнала")]
        [SerializeField]
        [Tooltip("Панель просмотра журнала.")]
        private GameObject _logPanel;

        [SerializeField]
        [Tooltip("Прокрутка журнала.")]
        private ScrollRect _logScrollRect;

        [SerializeField]
        [Tooltip("Текст журнала.")]
        private Text _logText;

        [SerializeField]
        [Tooltip("Кнопка закрытия журнала.")]
        private Button _closeLogButton;

        private List<ModeButton> _modeButtons = new List<ModeButton>();
        private ScenarioSession _session;
        private ScenarioMode _lastMode;
        private ScenarioLog _shownLog;
        private Coroutine _messageCoroutine;

        public ScenarioLog ShownLog => _shownLog;

        private void Start()
        {
            EnsureDropdownInitialized();
            CreateModeButtons();
            FillAnalyzerDropdown();
            _messagePanel.SetActive(false);
            _logPanel.SetActive(false);

            if (_runner.Session == null)
            {
                ShowStartPanel();
            }
        }

        private void OnEnable()
        {
            EnsureDropdownInitialized();
            BindModeButtons();
            _runner.SessionStartedEvent += SessionStartedEventHandler;
            _runner.SessionFinishedEvent += SessionFinishedEventHandler;
            _startLoadLatestButton.onClick.AddListener(StartLoadLatestButtonClickedHandler);
            _statusRestartButton.onClick.AddListener(StatusRestartButtonClickedHandler);
            _statusAbortButton.onClick.AddListener(StatusAbortButtonClickedHandler);
            _reanalyzeButton.onClick.AddListener(ReanalyzeButtonClickedHandler);
            _loadLatestButton.onClick.AddListener(LoadLatestButtonClickedHandler);
            _showLogButton.onClick.AddListener(ShowLogButtonClickedHandler);
            _restartButton.onClick.AddListener(RestartButtonClickedHandler);
            _chooseModeButton.onClick.AddListener(ChooseModeButtonClickedHandler);
            _closeLogButton.onClick.AddListener(CloseLogButtonClickedHandler);
            _analyzerDropdown.onValueChanged.AddListener(AnalyzerDropdownValueChangedHandler);
        }

        private void OnDisable()
        {
            _runner.SessionStartedEvent -= SessionStartedEventHandler;
            _runner.SessionFinishedEvent -= SessionFinishedEventHandler;
            _startLoadLatestButton.onClick.RemoveListener(StartLoadLatestButtonClickedHandler);
            _statusRestartButton.onClick.RemoveListener(StatusRestartButtonClickedHandler);
            _statusAbortButton.onClick.RemoveListener(StatusAbortButtonClickedHandler);
            _reanalyzeButton.onClick.RemoveListener(ReanalyzeButtonClickedHandler);
            _loadLatestButton.onClick.RemoveListener(LoadLatestButtonClickedHandler);
            _showLogButton.onClick.RemoveListener(ShowLogButtonClickedHandler);
            _restartButton.onClick.RemoveListener(RestartButtonClickedHandler);
            _chooseModeButton.onClick.RemoveListener(ChooseModeButtonClickedHandler);
            _closeLogButton.onClick.RemoveListener(CloseLogButtonClickedHandler);
            _analyzerDropdown.onValueChanged.RemoveListener(AnalyzerDropdownValueChangedHandler);
            SetSession(null);
            HideMessage();
        }

        private void CreateModeButtons()
        {
            if (_runner.Scenario == null)
            {
                return;
            }

            foreach (ScenarioMode mode in _runner.Scenario.Modes)
            {
                if (mode == null)
                {
                    continue;
                }

                ModeButton button = Instantiate(_modeButtonPrefab, _modeButtonsRoot);
                button.name = $"{mode.DisplayName} Mode Button";
                button.Setup(mode, StartMode);
                _modeButtons.Add(button);
            }
        }

        private void FillAnalyzerDropdown()
        {
            List<string> options = new List<string>();

            foreach (ScenarioAnalyzer analyzer in _runner.Analyzers)
            {
                options.Add(analyzer != null ? analyzer.DisplayName : "—");
            }

            _analyzerDropdown.ClearOptions();
            _analyzerDropdown.AddOptions(options);
        }

        // Колбэк кнопки не переживает перезагрузку скриптов в Play Mode, поэтому кнопки привязываются заново при включении.
        private void BindModeButtons()
        {
            foreach (ModeButton button in _modeButtons)
            {
                if (button != null)
                {
                    button.Setup(button.Mode, StartMode);
                }
            }
        }

        private void StartMode(ScenarioMode mode)
        {
            _runner.StartMode(mode);
        }

        private void SetSession(ScenarioSession session)
        {
            if (_session != null)
            {
                _session.ChangedEvent -= ChangedEventHandler;
                _session.ViolationRaisedEvent -= ViolationRaisedEventHandler;
            }

            _session = session;

            if (_session != null)
            {
                _session.ChangedEvent += ChangedEventHandler;
                _session.ViolationRaisedEvent += ViolationRaisedEventHandler;
            }
        }

        private void ShowStartPanel()
        {
            _startPanel.SetActive(true);
            _statusPanel.SetActive(false);
            _resultsPanel.SetActive(false);
            _logPanel.SetActive(false);
        }

        private void ShowStatusPanel()
        {
            _startPanel.SetActive(false);
            _statusPanel.SetActive(true);
            _resultsPanel.SetActive(false);
            _logPanel.SetActive(false);
        }

        private void ShowResults(ScenarioLog log, string text)
        {
            EnsureDropdownInitialized();
            _shownLog = log;
            _resultText.text = text;
            _restartButton.interactable = _lastMode != null;
            _startPanel.SetActive(false);
            _statusPanel.SetActive(false);
            _resultsPanel.SetActive(true);
            _logPanel.SetActive(false);
        }

        private void RefreshStatus()
        {
            if (_session == null)
            {
                return;
            }

            string groupName = string.IsNullOrEmpty(_session.CurrentGroupName) ? "—" : _session.CurrentGroupName;
            _statusText.text = $"Режим: {_session.Mode.DisplayName}\nГруппа: {groupName}";
            _tasksText.text = GetTasksText(_session.ActiveSteps);
        }

        private string GetTasksText(IReadOnlyList<StepDefinition> steps)
        {
            StringBuilder builder = new StringBuilder("Задания:");

            foreach (StepDefinition step in steps)
            {
                if (step == null)
                {
                    continue;
                }

                builder.Append('\n');
                builder.Append(TASK_PREFIX);
                builder.Append(string.IsNullOrEmpty(step.Instruction) ? step.Id : step.Instruction);
            }

            return builder.ToString();
        }

        private ScenarioAnalyzer GetSelectedAnalyzer()
        {
            int index = _analyzerDropdown.value;

            return index >= 0 && index < _runner.Analyzers.Count ? _runner.Analyzers[index] : null;
        }

        private void SelectAnalyzer(ScenarioAnalyzer analyzer)
        {
            for (int i = 0; i < _runner.Analyzers.Count; i++)
            {
                if (_runner.Analyzers[i] == analyzer)
                {
                    _analyzerDropdown.SetValueWithoutNotify(i);
                    return;
                }
            }
        }

        private string GetAnalysisText(ScenarioLog log)
        {
            if (log == null)
            {
                return "Журнал не выбран.";
            }

            return ReportFormatter.GetResultText(_runner.Analyze(log, GetSelectedAnalyzer()));
        }

        private void LoadLatestLog()
        {
            List<string> warnings = new List<string>();
            string path = ScenarioLogStorage.GetLatestPath(ScenarioLogStorage.GetFolderPath(_runner.Logger));
            ScenarioLog log = null;

            if (string.IsNullOrEmpty(path))
            {
                warnings.Add("В папке журналов нет файлов.");
            }
            else
            {
                log = ScenarioLogStorage.Load(path, warnings);
            }

            StringBuilder builder = new StringBuilder();

            if (!string.IsNullOrEmpty(path))
            {
                builder.Append($"Журнал: {Path.GetFileName(path)}\n");
            }

            foreach (string warning in warnings)
            {
                builder.Append($"<color=#FF6161>⚠ {warning}</color>\n");
            }

            builder.Append(GetAnalysisText(log));
            ShowResults(log, builder.ToString());
        }

        private void ShowMessage(string text)
        {
            HideMessage();
            _messageText.text = text;
            _messagePanel.SetActive(true);
            _messageCoroutine = StartCoroutine(HideMessageCoroutine());
        }

        private void HideMessage()
        {
            if (_messageCoroutine != null)
            {
                StopCoroutine(_messageCoroutine);
                _messageCoroutine = null;
            }

            _messagePanel.SetActive(false);
        }

        private void ShowAbortedResults()
        {
            ShowResults(_runner.LastLog, GetAnalysisText(_runner.LastLog));
        }

        private IEnumerator HideMessageCoroutine()
        {
            yield return new WaitForSeconds(MESSAGE_DURATION);
            _messageCoroutine = null;
            _messagePanel.SetActive(false);
        }

        // Раннер дописывает журнал прерванной сессии после её ChangedEvent, поэтому итог показывается кадром позже.
        private IEnumerator ShowAbortedResultsCoroutine()
        {
            yield return null;

            if (_session == null)
            {
                ShowAbortedResults();
            }
        }

        private void SessionStartedEventHandler(ScenarioSession session)
        {
            _lastMode = session.Mode;
            SetSession(session);
            HideMessage();
            ShowStatusPanel();
            RefreshStatus();
        }

        private void SessionFinishedEventHandler(ScenarioLog log, AnalysisResult result)
        {
            SetSession(null);
            HideMessage();

            if (_lastMode != null)
            {
                SelectAnalyzer(_runner.AnalyzerOverride != null ? _runner.AnalyzerOverride : _lastMode.DefaultAnalyzer);
            }

            ShowResults(log, ReportFormatter.GetResultText(result));
        }

        private void ChangedEventHandler()
        {
            if (_session.IsFinished && _runner.Session != _session)
            {
                SetSession(null);
                HideMessage();
                StartCoroutine(ShowAbortedResultsCoroutine());
                return;
            }

            RefreshStatus();
        }

        private void ViolationRaisedEventHandler(StepDefinition step, string message)
        {
            ShowMessage(message);
        }

        private void StartLoadLatestButtonClickedHandler()
        {
            LoadLatestLog();
        }

        private void StatusRestartButtonClickedHandler()
        {
            StartMode(_lastMode);
        }

        private void StatusAbortButtonClickedHandler()
        {
            SetSession(null);
            HideMessage();
            _runner.AbortSession();
            ShowAbortedResults();
        }

        private void ReanalyzeButtonClickedHandler()
        {
            _resultText.text = GetAnalysisText(_shownLog);
        }

        private void AnalyzerDropdownValueChangedHandler(int index)
        {
            if (_shownLog != null)
            {
                _resultText.text = GetAnalysisText(_shownLog);
            }
        }

        private void LoadLatestButtonClickedHandler()
        {
            LoadLatestLog();
        }

        private void ShowLogButtonClickedHandler()
        {
            _logText.text = _shownLog != null ? ReportFormatter.GetLogText(_shownLog) : "Журнал не выбран.";
            _logPanel.SetActive(true);
            Canvas.ForceUpdateCanvases();
            _logScrollRect.verticalNormalizedPosition = 1f;
        }

        private void RestartButtonClickedHandler()
        {
            if (_lastMode != null)
            {
                StartMode(_lastMode);
            }
        }

        private void ChooseModeButtonClickedHandler()
        {
            ShowStartPanel();
        }

        private void CloseLogButtonClickedHandler()
        {
            _logPanel.SetActive(false);
        }

        private void EnsureDropdownInitialized()
        {
            if (_analyzerDropdown == null)
            {
                return;
            }

            System.Reflection.FieldInfo runnerField = typeof(Dropdown).GetField("m_AlphaTweenRunner", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (runnerField != null && runnerField.GetValue(_analyzerDropdown) == null)
            {
                object runner = System.Activator.CreateInstance(runnerField.FieldType);
                System.Reflection.MethodInfo initMethod = runnerField.FieldType.GetMethod("Init", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (initMethod != null)
                {
                    initMethod.Invoke(runner, new object[] { _analyzerDropdown });
                }
                runnerField.SetValue(_analyzerDropdown, runner);
            }
        }
    }
}
