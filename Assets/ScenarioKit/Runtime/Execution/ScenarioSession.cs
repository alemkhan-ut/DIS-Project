using System;
using System.Collections.Generic;
using ScenarioKit.Events;
using ScenarioKit.Logging;
using ScenarioKit.Modes;
using ScenarioKit.Steps;

namespace ScenarioKit.Execution
{
    public sealed class ScenarioSession
    {
        public event Action ChangedEvent;
        public event Action<StepDefinition, string> ViolationRaisedEvent;
        public event Action FinishedEvent;

        private const string OUT_OF_ORDER_MESSAGE = "Этот шаг ещё не доступен";
        private const string THRESHOLD_RESULT = "threshold";

        private readonly ScenarioMode _mode;
        private readonly IScenarioLogger _logger;
        private readonly IWorldState _world;
        private readonly Func<double> _clock;
        private readonly IReadOnlyList<StepDefinition> _steps;
        private readonly List<string> _groupNames = new List<string>();
        private readonly List<List<List<StepDefinition>>> _groupStages = new List<List<List<StepDefinition>>>();
        private readonly Dictionary<StepDefinition, int> _stepGroups = new Dictionary<StepDefinition, int>();
        private readonly Dictionary<StepDefinition, StepStatus> _statuses = new Dictionary<StepDefinition, StepStatus>();
        private readonly Dictionary<StepDefinition, StepTracker> _trackers = new Dictionary<StepDefinition, StepTracker>();
        private readonly List<StepDefinition> _activeSteps = new List<StepDefinition>();
        private readonly Queue<ScenarioEventKey> _pendingEvents = new Queue<ScenarioEventKey>();
        private readonly int[] _violations;

        private int _groupIndex = -1;
        private int _stageIndex = -1;
        private bool _isStarted;
        private bool _isFinished;
        private bool _isHandling;

        public ScenarioMode Mode => _mode;
        public bool IsFinished => _isFinished;
        public string CurrentGroupName => IsGroupIndexValid(_groupIndex) ? _groupNames[_groupIndex] : null;
        public IReadOnlyList<StepDefinition> ActiveSteps => _activeSteps;

        public ScenarioSession(ScenarioMode mode, IScenarioLogger logger, IWorldState world, Func<double> clock)
        {
            if (mode == null)
            {
                throw new ArgumentNullException(nameof(mode));
            }

            _mode = mode;
            _logger = logger;
            _world = world;
            _clock = clock;
            _steps = mode.GetSteps();

            BuildLayout();
            _violations = new int[_groupStages.Count];
        }

        public void Start()
        {
            if (_isStarted)
            {
                return;
            }

            _isStarted = true;
            Write(LogRecordTypes.SCENARIO_STARTED, null, null, null);
            StartGroup(0);
            ChangedEvent?.Invoke();
        }

        public void HandleEvent(ScenarioEventKey key)
        {
            if (!_isStarted || _isFinished || key == null)
            {
                return;
            }

            _pendingEvents.Enqueue(key);

            if (_isHandling)
            {
                return;
            }

            _isHandling = true;

            try
            {
                while (_pendingEvents.Count > 0 && !_isFinished)
                {
                    ProcessEvent(_pendingEvents.Dequeue());
                }
            }
            finally
            {
                _pendingEvents.Clear();
                _isHandling = false;
            }
        }

        public void Abort()
        {
            if (!_isStarted || _isFinished)
            {
                return;
            }

            Write(LogRecordTypes.SCENARIO_ABORTED, null, null, null);
            _isFinished = true;
            ChangedEvent?.Invoke();
        }

        public StepStatus GetStatus(StepDefinition step)
        {
            StepStatus status;

            if (step != null && _statuses.TryGetValue(step, out status))
            {
                return status;
            }

            return StepStatus.Pending;
        }

        private static bool IsTerminal(StepStatus status)
        {
            return status == StepStatus.Completed || status == StepStatus.Failed || status == StepStatus.Skipped;
        }

        private void BuildLayout()
        {
            HashSet<StepDefinition> seen = new HashSet<StepDefinition>();
            IReadOnlyList<StepGroup> groups = _mode.Groups;

            for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                StepGroup group = groups[groupIndex];
                List<List<StepDefinition>> stages = new List<List<StepDefinition>>();

                _groupNames.Add(group != null ? group.Name : null);
                _groupStages.Add(stages);

                if (group == null)
                {
                    continue;
                }

                foreach (ActivationStage stage in group.Stages)
                {
                    List<StepDefinition> stageSteps = new List<StepDefinition>();
                    stages.Add(stageSteps);

                    if (stage == null)
                    {
                        continue;
                    }

                    foreach (StepDefinition step in stage.Steps)
                    {
                        if (step == null || !seen.Add(step))
                        {
                            continue;
                        }

                        stageSteps.Add(step);
                        _stepGroups[step] = groupIndex;
                        _statuses[step] = StepStatus.Pending;
                        _trackers[step] = step.CreateTracker();
                    }
                }
            }
        }

        private void ProcessEvent(ScenarioEventKey key)
        {
            List<KeyValuePair<StepDefinition, StepStatus>> snapshot = GetSnapshot();
            bool isAccepted = false;

            foreach (KeyValuePair<StepDefinition, StepStatus> pair in snapshot)
            {
                if (_isFinished)
                {
                    break;
                }

                StepDefinition step = pair.Key;

                if (GetStatus(step) != pair.Value)
                {
                    continue;
                }

                if (pair.Value == StepStatus.Active)
                {
                    isAccepted |= HandleActiveStep(step, key);
                }
                else if (!isAccepted && _mode.OutOfOrder == OutOfOrderPolicy.RecordViolation && step.IsTriggeredBy(key))
                {
                    OnViolation(step, StepVerdict.CODE_OUT_OF_ORDER, OUT_OF_ORDER_MESSAGE);
                }
            }

            if (!_isFinished && IsCurrentStageTerminal())
            {
                AdvanceStage();
            }

            ChangedEvent?.Invoke();
        }

        private List<KeyValuePair<StepDefinition, StepStatus>> GetSnapshot()
        {
            List<KeyValuePair<StepDefinition, StepStatus>> snapshot = new List<KeyValuePair<StepDefinition, StepStatus>>();

            foreach (StepDefinition step in _steps)
            {
                StepStatus status = GetStatus(step);

                if (!IsTerminal(status))
                {
                    snapshot.Add(new KeyValuePair<StepDefinition, StepStatus>(step, status));
                }
            }

            return snapshot;
        }

        private bool HandleActiveStep(StepDefinition step, ScenarioEventKey key)
        {
            StepVerdict verdict = _trackers[step].Handle(key, _world);

            switch (verdict.Type)
            {
                case StepVerdictType.Completed:
                    SetStatus(step, StepStatus.Completed);
                    Write(LogRecordTypes.STEP_COMPLETED, step, null, null);
                    return true;
                case StepVerdictType.Progress:
                    return true;
                case StepVerdictType.Violation:
                    OnViolation(step, verdict.Code, verdict.Message);
                    return false;
                default:
                    return false;
            }
        }

        private void StartGroup(int groupIndex)
        {
            _groupIndex = groupIndex;
            _stageIndex = -1;

            if (!IsGroupIndexValid(groupIndex))
            {
                Finish();
                return;
            }

            Write(LogRecordTypes.GROUP_STARTED, null, null, _groupNames[groupIndex]);

            if (_mode.ShouldSkipRestOfGroup(_violations[groupIndex]))
            {
                SkipRestOfGroup();
                return;
            }

            AdvanceStage();
        }

        private void AdvanceStage()
        {
            List<List<StepDefinition>> stages = _groupStages[_groupIndex];

            while (true)
            {
                _stageIndex++;

                if (_stageIndex >= stages.Count)
                {
                    Write(LogRecordTypes.GROUP_FINISHED, null, null, null);
                    StartGroup(_groupIndex + 1);
                    return;
                }

                bool isActivated = false;

                foreach (StepDefinition step in stages[_stageIndex])
                {
                    if (IsTerminal(GetStatus(step)))
                    {
                        continue;
                    }

                    SetStatus(step, StepStatus.Active);
                    _trackers[step].Reset();
                    Write(LogRecordTypes.STEP_ACTIVATED, step, null, null);
                    isActivated = true;
                }

                if (isActivated)
                {
                    return;
                }
            }
        }

        private void OnViolation(StepDefinition step, string code, string message)
        {
            Write(LogRecordTypes.VIOLATION, step, code, message);
            ViolationRaisedEvent?.Invoke(step, message);

            if (_isFinished)
            {
                return;
            }

            if (_mode.RetryAfterViolation)
            {
                if (GetStatus(step) == StepStatus.Active)
                {
                    _trackers[step].Reset();
                }

                return;
            }

            SetStatus(step, StepStatus.Failed);
            Write(LogRecordTypes.STEP_FAILED, step, null, null);

            int groupIndex = _stepGroups[step];
            _violations[groupIndex]++;

            if (groupIndex == _groupIndex && _mode.ShouldSkipRestOfGroup(_violations[groupIndex]))
            {
                SkipRestOfGroup();
            }
        }

        private void SkipRestOfGroup()
        {
            foreach (List<StepDefinition> stage in _groupStages[_groupIndex])
            {
                foreach (StepDefinition step in stage)
                {
                    if (IsTerminal(GetStatus(step)))
                    {
                        continue;
                    }

                    SetStatus(step, StepStatus.Skipped);
                    Write(LogRecordTypes.STEP_SKIPPED, step, null, null);
                }
            }

            Write(LogRecordTypes.GROUP_FINISHED, null, THRESHOLD_RESULT, null);
            StartGroup(_groupIndex + 1);
        }

        private void Finish()
        {
            if (_isFinished)
            {
                return;
            }

            Write(LogRecordTypes.SCENARIO_FINISHED, null, null, null);
            _isFinished = true;
            FinishedEvent?.Invoke();
        }

        private bool IsGroupIndexValid(int groupIndex)
        {
            return groupIndex >= 0 && groupIndex < _groupStages.Count;
        }

        private bool IsCurrentStageTerminal()
        {
            if (!IsGroupIndexValid(_groupIndex))
            {
                return false;
            }

            List<List<StepDefinition>> stages = _groupStages[_groupIndex];

            if (_stageIndex < 0 || _stageIndex >= stages.Count)
            {
                return false;
            }

            foreach (StepDefinition step in stages[_stageIndex])
            {
                if (!IsTerminal(GetStatus(step)))
                {
                    return false;
                }
            }

            return true;
        }

        private void SetStatus(StepDefinition step, StepStatus status)
        {
            _statuses[step] = status;

            if (status == StepStatus.Active)
            {
                if (!_activeSteps.Contains(step))
                {
                    _activeSteps.Add(step);
                }
            }
            else
            {
                _activeSteps.Remove(step);
            }
        }

        private void Write(string type, StepDefinition step, string result, string message)
        {
            LogRecord record = new LogRecord
            {
                Time = _clock != null ? _clock() : 0d,
                Type = type,
                StepId = step != null ? step.Id : null,
                Result = result,
                Message = message
            };

            _logger?.Write(record);
        }
    }
}
