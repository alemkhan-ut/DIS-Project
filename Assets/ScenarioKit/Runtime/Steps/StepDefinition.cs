using System.Collections.Generic;
using ScenarioKit.Events;
using UnityEngine;

namespace ScenarioKit.Steps
{
    public abstract class StepDefinition : ScriptableObject
    {
        private static readonly Dictionary<string, string> _instructionsById = new Dictionary<string, string>();

        [SerializeField]
        [Tooltip("Уникальный идентификатор шага. Если пусто, берётся имя ассета.")]
        private string _id;

        [SerializeField]
        [TextArea]
        [Tooltip("Текст задания, который увидит обучаемый.")]
        private string _instruction;

        public string Id => string.IsNullOrEmpty(_id) ? name : _id;
        public string Instruction => _instruction;

        /// <summary>
        /// Отображаемое имя шага и идентификатор для журнала в формате "текст задания [идентификатор]".
        /// </summary>
        public string DisplayName => FormatLogIdentifier(_instruction, Id);

        /// <summary>
        /// Идентификатор шага для журнала прохождений.
        /// </summary>
        public string LogIdentifier => DisplayName;

        public abstract IEnumerable<ScenarioEventKey> SuccessEvents { get; }

        public abstract StepTracker CreateTracker();

        public abstract bool IsTriggeredBy(ScenarioEventKey key);

        public static string FormatLogIdentifier(string instruction, string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return string.Empty;
            }

            if (!string.IsNullOrEmpty(instruction))
            {
                string cleanInstruction = instruction.Replace('\r', ' ').Replace('\n', ' ').Trim();
                return $"{cleanInstruction} [{id}]";
            }

            return id;
        }

        public static string ResolveLogIdentifier(string stepId)
        {
            if (string.IsNullOrEmpty(stepId))
            {
                return stepId;
            }

            if (stepId.Contains("[") && stepId.EndsWith("]"))
            {
                return stepId;
            }

            if (_instructionsById.TryGetValue(stepId, out string instruction) && !string.IsNullOrEmpty(instruction))
            {
                return FormatLogIdentifier(instruction, stepId);
            }

            return stepId;
        }

        public static void RegisterStep(StepDefinition step)
        {
            if (step != null && !string.IsNullOrEmpty(step.Id))
            {
                _instructionsById[step.Id] = step.Instruction;
            }
        }

        protected virtual void OnEnable()
        {
            RegisterStep(this);
        }

        protected virtual void OnValidate()
        {
            if (string.IsNullOrEmpty(_id))
            {
                _id = name;
            }

            RegisterStep(this);
        }
    }
}
