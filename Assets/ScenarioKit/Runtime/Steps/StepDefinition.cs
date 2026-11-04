using System.Collections.Generic;
using ScenarioKit.Events;
using UnityEngine;

namespace ScenarioKit.Steps
{
    public abstract class StepDefinition : ScriptableObject
    {
        [SerializeField]
        [Tooltip("Уникальный идентификатор шага. Если пусто, берётся имя ассета.")]
        private string _id;

        [SerializeField]
        [TextArea]
        [Tooltip("Текст задания, который увидит обучаемый.")]
        private string _instruction;

        public string Id => _id;
        public string Instruction => _instruction;

        public abstract IEnumerable<ScenarioEventKey> SuccessEvents { get; }

        public abstract StepTracker CreateTracker();

        public abstract bool IsTriggeredBy(ScenarioEventKey key);

        protected virtual void OnValidate()
        {
            if (string.IsNullOrEmpty(_id))
            {
                _id = name;
            }
        }
    }
}
