using System;
using System.Collections.Generic;
using ScenarioKit.Steps;
using UnityEngine;

namespace ScenarioKit.Modes
{
    [Serializable]
    public class ActivationStage
    {
        [SerializeField]
        [Tooltip("Шаги стадии. Все они активны одновременно, выполнять их можно в любом порядке.")]
        private List<StepDefinition> _steps = new List<StepDefinition>();

        public IReadOnlyList<StepDefinition> Steps => _steps;
    }
}
