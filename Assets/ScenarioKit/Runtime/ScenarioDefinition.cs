using System.Collections.Generic;
using ScenarioKit.Modes;
using ScenarioKit.Steps;
using UnityEngine;

namespace ScenarioKit
{
    [CreateAssetMenu(menuName = "ScenarioKit/Сценарий")]
    public class ScenarioDefinition : ScriptableObject
    {
        [Header("Сценарий")]
        [SerializeField]
        [Tooltip("Идентификатор сценария, попадает в журнал.")]
        private string _scenarioId;

        [SerializeField]
        [Tooltip("Библиотека шагов сценария. Режимы берут шаги отсюда.")]
        private List<StepDefinition> _steps = new List<StepDefinition>();

        [SerializeField]
        [Tooltip("Режимы прохождения сценария: тренировка, экзамен.")]
        private List<ScenarioMode> _modes = new List<ScenarioMode>();

        public string ScenarioId => _scenarioId;
        public IReadOnlyList<StepDefinition> Steps => _steps;
        public IReadOnlyList<ScenarioMode> Modes => _modes;
    }
}
