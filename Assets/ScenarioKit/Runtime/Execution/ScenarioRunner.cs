using System;
using System.Collections.Generic;
using ScenarioKit.Analysis;
using ScenarioKit.Events;
using ScenarioKit.Logging;
using ScenarioKit.Modes;
using ScenarioKit.Steps;
using ScenarioKit.Validation;
using UnityEngine;
using UnityEngine.Events;

namespace ScenarioKit.Execution
{
    public class ScenarioRunner : MonoBehaviour
    {
        public event Action<ScenarioSession> SessionStartedEvent;
        public event Action<ScenarioLog, AnalysisResult> SessionFinishedEvent;

        [Header("Сценарий")]
        [SerializeField]
        [Tooltip("Сценарий, который проходит пользователь.")]
        private ScenarioDefinition _scenario;

        [SerializeField]
        [Tooltip("Куда записывать журнал. Если пусто, журнал хранится только в памяти.")]
        private ScenarioLoggerAsset _logger;

        [Header("Анализ")]
        [SerializeField]
        [Tooltip("Анализатор вместо стандартного анализатора режима. Пусто — берётся анализатор режима.")]
        private ScenarioAnalyzer _analyzerOverride;

        [SerializeField]
        [Tooltip("Анализаторы, которыми можно заново оценить журнал.")]
        private List<ScenarioAnalyzer> _analyzers = new List<ScenarioAnalyzer>();

        [Header("Запуск")]
        [SerializeField]
        [Tooltip("Режим, который запускается автоматически при старте сцены. Пусто — запуск вручную.")]
        private ScenarioMode _autoStartMode;

        [SerializeField]
        [Tooltip("Вызывается перед стартом каждой сессии, после сброса состояния мира.")]
        private UnityEvent _onBeforeSessionStart = new UnityEvent();

        private ScenarioSession _session;
        private IScenarioLogger _sessionLogger;
        private ScenarioLog _sessionHeader;
        private MemoryLoggerAsset _fallbackLogger;
        private double _startTime;

        public ScenarioDefinition Scenario => _scenario;
        public ScenarioLoggerAsset Logger => _logger;
        public ScenarioAnalyzer AnalyzerOverride => _analyzerOverride;
        public IReadOnlyList<ScenarioAnalyzer> Analyzers => _analyzers;
        public ScenarioMode AutoStartMode => _autoStartMode;
        public ScenarioSession Session => _session;
        // Встроенные логгеры дописывают записи в переданный заголовок, поэтому это живой журнал идущей сессии.
        public ScenarioLog CurrentLog => _session != null ? _sessionHeader : null;
        public ScenarioLog LastLog { get; private set; }
        public AnalysisResult LastResult { get; private set; }

        private void Start()
        {
            if (_autoStartMode != null)
            {
                StartMode(_autoStartMode);
            }
        }

        private void OnEnable()
        {
            ScenarioEventBus.RaisedEvent += RaisedEventHandler;
        }

        private void OnDisable()
        {
            ScenarioEventBus.RaisedEvent -= RaisedEventHandler;
        }

        private void OnDestroy()
        {
            if (_fallbackLogger != null)
            {
                Destroy(_fallbackLogger);
            }
        }

        public void StartMode(ScenarioMode mode)
        {
            if (_scenario == null)
            {
                Debug.LogError("Раннер сценария: не назначен сценарий.", this);
                return;
            }

            if (mode == null)
            {
                Debug.LogError("Раннер сценария: не выбран режим для запуска.", this);
                return;
            }

            AbortSession();
            LogValidationMessages();

            _onBeforeSessionStart?.Invoke();
            ScenarioEventBus.ResetState();

            ScenarioLog header = CreateHeader(mode);
            _sessionLogger = GetLoggerAsset().Create(header);
            _sessionHeader = header;
            _startTime = Time.timeAsDouble;

            _session = new ScenarioSession(mode, _sessionLogger, ScenarioEventBus.State, GetSessionTime);
            _session.FinishedEvent += FinishedEventHandler;

            LastResult = null;

            ScenarioSession session = _session;
            SessionStartedEvent?.Invoke(session);
            session.Start();
        }

        public void AbortSession()
        {
            if (_session == null)
            {
                return;
            }

            ScenarioSession session = _session;
            IScenarioLogger sessionLogger = _sessionLogger;

            _session = null;
            _sessionLogger = null;
            session.FinishedEvent -= FinishedEventHandler;

            if (session.IsFinished)
            {
                return;
            }

            session.Abort();
            LastLog = sessionLogger.Complete();
            LastResult = null;
        }

        public void SimulateEvent(ScenarioEventKey key)
        {
            ScenarioEventBus.Raise(key);
        }

        public AnalysisResult Analyze(ScenarioLog log, ScenarioAnalyzer analyzer)
        {
            if (analyzer != null)
            {
                return analyzer.Analyze(log);
            }

            AnalysisResult result = new AnalysisResult();
            result.AnalyzerName = string.Empty;
            result.Warnings.Add("Анализатор не выбран");
            return result;
        }

        private double GetSessionTime()
        {
            return Time.timeAsDouble - _startTime;
        }

        private ScenarioLoggerAsset GetLoggerAsset()
        {
            if (_logger != null)
            {
                return _logger;
            }

            if (_fallbackLogger == null)
            {
                _fallbackLogger = ScriptableObject.CreateInstance<MemoryLoggerAsset>();
                Debug.LogWarning("Раннер сценария: логгер не назначен, журнал хранится только в памяти.", this);
            }

            return _fallbackLogger;
        }

        private ScenarioLog CreateHeader(ScenarioMode mode)
        {
            ScenarioLog header = new ScenarioLog();
            header.ScenarioId = _scenario.ScenarioId;
            header.ModeName = mode.DisplayName;
            header.StartedUtc = DateTime.UtcNow.ToString("o");

            foreach (StepDefinition step in mode.GetSteps())
            {
                header.StepIds.Add(step.Id);
            }

            return header;
        }

        private void LogValidationMessages()
        {
            foreach (ValidationMessage message in ScenarioValidator.GetMessages(_scenario))
            {
                Debug.LogWarning($"Проверка сценария: {message.Text}", message.Context);
            }
        }

        private void RaisedEventHandler(ScenarioEventKey key)
        {
            _session?.HandleEvent(key);
        }

        private void FinishedEventHandler()
        {
            ScenarioSession session = _session;
            IScenarioLogger sessionLogger = _sessionLogger;

            _session = null;
            _sessionLogger = null;
            session.FinishedEvent -= FinishedEventHandler;

            ScenarioAnalyzer analyzer = _analyzerOverride != null ? _analyzerOverride : session.Mode.DefaultAnalyzer;

            LastLog = sessionLogger.Complete();
            LastResult = Analyze(LastLog, analyzer);

            Debug.Log($"Сценарий завершён: {LastResult.Score}/{LastResult.MaxScore}", this);
            SessionFinishedEvent?.Invoke(LastLog, LastResult);
        }
    }
}
