using System.Collections.Generic;
using ScenarioKit.Events;
using UnityEngine;

namespace ScenarioKit.Steps
{
    [CreateAssetMenu(menuName = "ScenarioKit/Шаг (по событиям)")]
    public class EventStep : StepDefinition
    {
        [Header("Что нужно сделать")]
        [SerializeField]
        [Tooltip("События, которые нужно выполнить для завершения шага.")]
        private List<ScenarioEventKey> _sequence = new List<ScenarioEventKey>();

        [SerializeField]
        [Tooltip("Если включено, события нужно выполнять строго по порядку списка.")]
        private bool _strictOrder = true;

        [SerializeField]
        [Tooltip("Сообщение при нарушении порядка.")]
        private string _wrongOrderMessage = "Нарушен порядок действий";

        [Header("Условия")]
        [SerializeField]
        [Tooltip("Состояния мира, которые должны совпасть в момент события.")]
        private List<StateRequirement> _requiredStates = new List<StateRequirement>();

        [SerializeField]
        [Tooltip("Сообщение, если условие не выполнено.")]
        private string _stateMessage = "Не выполнено условие";

        [Header("Нарушения")]
        [SerializeField]
        [Tooltip("События, которые считаются нарушением на этом шаге.")]
        private List<ViolationRule> _violations = new List<ViolationRule>();

        public IReadOnlyList<ScenarioEventKey> Sequence => _sequence;
        public bool StrictOrder => _strictOrder;
        public string WrongOrderMessage => _wrongOrderMessage;
        public IReadOnlyList<StateRequirement> RequiredStates => _requiredStates;
        public string StateMessage => _stateMessage;
        public IReadOnlyList<ViolationRule> Violations => _violations;

        public override IEnumerable<ScenarioEventKey> SuccessEvents
        {
            get
            {
                foreach (ScenarioEventKey key in _sequence)
                {
                    if (key != null)
                    {
                        yield return key;
                    }
                }
            }
        }

        public override StepTracker CreateTracker()
        {
            return new EventStepTracker(this);
        }

        public override bool IsTriggeredBy(ScenarioEventKey key)
        {
            return key != null && _sequence.Contains(key);
        }

        private class EventStepTracker : StepTracker
        {
            private readonly EventStep _step;
            private int _progress;

            public EventStepTracker(EventStep step)
            {
                _step = step;
            }

            public override StepVerdict Handle(ScenarioEventKey key, IWorldState world)
            {
                if (key == null)
                {
                    return StepVerdict.None;
                }

                foreach (ViolationRule rule in _step._violations)
                {
                    if (rule != null && rule.Key == key)
                    {
                        return StepVerdict.GetViolation(StepVerdict.CODE_RULE, rule.Message);
                    }
                }

                if (_progress < _step._sequence.Count && _step._sequence[_progress] == key)
                {
                    if (!AreStatesMatched(world))
                    {
                        return StepVerdict.GetViolation(StepVerdict.CODE_STATE, _step._stateMessage);
                    }

                    _progress++;
                    return _progress >= _step._sequence.Count ? StepVerdict.Completed : StepVerdict.Progress;
                }

                if (_step._strictOrder && _step._sequence.IndexOf(key) > _progress)
                {
                    return StepVerdict.GetViolation(StepVerdict.CODE_WRONG_ORDER, _step._wrongOrderMessage);
                }

                return StepVerdict.None;
            }

            public override void Reset()
            {
                _progress = 0;
            }

            private bool AreStatesMatched(IWorldState world)
            {
                foreach (StateRequirement requirement in _step._requiredStates)
                {
                    if (requirement != null && world != null && world.Get(requirement.Key) != requirement.Expected)
                    {
                        return false;
                    }
                }

                return true;
            }
        }
    }
}
