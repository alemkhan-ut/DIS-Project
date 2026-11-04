using System.Collections.Generic;
using ScenarioKit.Analysis;
using ScenarioKit.Steps;
using UnityEngine;

namespace ScenarioKit.Modes
{
    public abstract class ScenarioMode : ScriptableObject
    {
        [SerializeField]
        [Tooltip("Название режима, которое увидит пользователь.")]
        private string _displayName;

        [SerializeField]
        [Tooltip("Что делать, если событие относится к шагу, который ещё не активен.")]
        private OutOfOrderPolicy _outOfOrder = OutOfOrderPolicy.RecordViolation;

        [SerializeField]
        [Tooltip("Анализатор, который оценивает журнал в этом режиме по умолчанию.")]
        private ScenarioAnalyzer _defaultAnalyzer;

        public string DisplayName => _displayName;
        public OutOfOrderPolicy OutOfOrder => _outOfOrder;
        public ScenarioAnalyzer DefaultAnalyzer => _defaultAnalyzer;

        public abstract IReadOnlyList<StepGroup> Groups { get; }
        public abstract bool RetryAfterViolation { get; }

        public virtual bool ShouldSkipRestOfGroup(int violationsInGroup)
        {
            return false;
        }

        public IReadOnlyList<StepDefinition> GetSteps()
        {
            List<StepDefinition> steps = new List<StepDefinition>();
            HashSet<StepDefinition> seen = new HashSet<StepDefinition>();

            foreach (StepGroup group in Groups)
            {
                if (group == null)
                {
                    continue;
                }

                foreach (ActivationStage stage in group.Stages)
                {
                    if (stage == null)
                    {
                        continue;
                    }

                    foreach (StepDefinition step in stage.Steps)
                    {
                        if (step != null && seen.Add(step))
                        {
                            steps.Add(step);
                        }
                    }
                }
            }

            return steps;
        }
    }
}
