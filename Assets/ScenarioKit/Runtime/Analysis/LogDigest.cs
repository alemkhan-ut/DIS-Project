using System.Collections.Generic;
using ScenarioKit.Logging;

namespace ScenarioKit.Analysis
{
    public class LogDigest
    {
        private const string EMPTY_LOG_WARNING = "Журнал пуст";
        private const string NO_STEP_IDS_WARNING = "В журнале нет списка шагов — используются шаги из записей";
        private const string NOT_FINISHED_WARNING = "Прохождение не завершено";
        private const string UNKNOWN_STEP_WARNING_FORMAT = "В журнале есть запись о неизвестном шаге «{0}»";

        private readonly List<string> _stepIds = new List<string>();
        private readonly List<string> _warnings = new List<string>();
        private readonly Dictionary<string, StepFacts> _facts = new Dictionary<string, StepFacts>();

        private LogDigest()
        {
        }

        public IReadOnlyList<string> StepIds => _stepIds;
        public IReadOnlyList<string> Warnings => _warnings;
        public bool IsFinished { get; private set; }

        public static LogDigest Create(ScenarioLog log)
        {
            LogDigest digest = new LogDigest();

            if (log == null || log.Records == null || log.Records.Count == 0)
            {
                digest._warnings.Add(EMPTY_LOG_WARNING);
                return digest;
            }

            digest.Build(log);
            return digest;
        }

        public StepFacts GetFacts(string stepId)
        {
            StepFacts facts;

            if (!string.IsNullOrEmpty(stepId) && _facts.TryGetValue(stepId, out facts))
            {
                return facts;
            }

            return new StepFacts();
        }

        private void Build(ScenarioLog log)
        {
            if (log.StepIds != null)
            {
                foreach (string id in log.StepIds)
                {
                    if (!string.IsNullOrEmpty(id) && !_facts.ContainsKey(id))
                    {
                        _stepIds.Add(id);
                        _facts[id] = new StepFacts();
                    }
                }
            }

            bool hasDeclaredIds = _stepIds.Count > 0;

            if (!hasDeclaredIds)
            {
                _warnings.Add(NO_STEP_IDS_WARNING);
            }

            HashSet<string> unknownIds = new HashSet<string>();
            HashSet<string> completedIds = new HashSet<string>();

            foreach (LogRecord record in log.Records)
            {
                if (record == null)
                {
                    continue;
                }

                if (record.Type == LogRecordTypes.SCENARIO_FINISHED)
                {
                    IsFinished = true;
                    continue;
                }

                if (!IsStepRecord(record.Type) || string.IsNullOrEmpty(record.StepId))
                {
                    continue;
                }

                StepFacts facts = GetOrRegister(record.StepId, hasDeclaredIds, unknownIds);
                Apply(record.Type, facts, completedIds.Contains(record.StepId));

                if (record.Type == LogRecordTypes.STEP_COMPLETED)
                {
                    completedIds.Add(record.StepId);
                }
            }

            if (!IsFinished)
            {
                _warnings.Add(NOT_FINISHED_WARNING);
            }
        }

        private StepFacts GetOrRegister(string stepId, bool hasDeclaredIds, HashSet<string> unknownIds)
        {
            StepFacts facts;

            if (_facts.TryGetValue(stepId, out facts))
            {
                return facts;
            }

            facts = new StepFacts();
            _facts[stepId] = facts;

            if (!hasDeclaredIds)
            {
                _stepIds.Add(stepId);
            }
            else if (unknownIds.Add(stepId))
            {
                _warnings.Add(string.Format(UNKNOWN_STEP_WARNING_FORMAT, stepId));
            }

            return facts;
        }

        private static bool IsStepRecord(string type)
        {
            return type == LogRecordTypes.STEP_ACTIVATED
                || type == LogRecordTypes.STEP_COMPLETED
                || type == LogRecordTypes.VIOLATION
                || type == LogRecordTypes.STEP_FAILED
                || type == LogRecordTypes.STEP_SKIPPED;
        }

        private static void Apply(string type, StepFacts facts, bool alreadyCompleted)
        {
            switch (type)
            {
                case LogRecordTypes.VIOLATION:
                    facts.Violations++;

                    if (!alreadyCompleted)
                    {
                        facts.ViolationsBeforeCompletion++;
                    }

                    break;
                case LogRecordTypes.STEP_COMPLETED:
                    facts.Completed = true;
                    break;
                case LogRecordTypes.STEP_FAILED:
                    facts.Failed = true;
                    break;
                case LogRecordTypes.STEP_SKIPPED:
                    facts.Skipped = true;
                    break;
            }
        }
    }
}
