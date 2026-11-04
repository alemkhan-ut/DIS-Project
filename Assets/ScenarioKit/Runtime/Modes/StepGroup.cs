using System;
using System.Collections.Generic;
using UnityEngine;

namespace ScenarioKit.Modes
{
    [Serializable]
    public class StepGroup
    {
        [SerializeField]
        [Tooltip("Название группы шагов.")]
        private string _name;

        [SerializeField]
        [Tooltip("Стадии идут по очереди: следующая начинается, когда выполнена предыдущая.")]
        private List<ActivationStage> _stages = new List<ActivationStage>();

        public string Name => _name;
        public IReadOnlyList<ActivationStage> Stages => _stages;
    }
}
