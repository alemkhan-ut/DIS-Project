using System;
using ScenarioKit.Events;
using UnityEngine;

namespace ScenarioKit.Steps
{
    [Serializable]
    public class StateRequirement
    {
        [SerializeField]
        [Tooltip("Состояние, которое проверяется.")]
        private ScenarioStateKey _key;

        [SerializeField]
        [Tooltip("Каким должно быть состояние: включено (галочка) или выключено.")]
        private bool _expected = true;

        public ScenarioStateKey Key => _key;
        public bool Expected => _expected;
    }
}
