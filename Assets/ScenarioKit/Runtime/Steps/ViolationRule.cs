using System;
using ScenarioKit.Events;
using UnityEngine;

namespace ScenarioKit.Steps
{
    [Serializable]
    public class ViolationRule
    {
        [SerializeField]
        [Tooltip("Событие, которое считается нарушением.")]
        private ScenarioEventKey _key;

        [SerializeField]
        [Tooltip("Сообщение, которое будет записано при нарушении.")]
        private string _message;

        public ScenarioEventKey Key => _key;
        public string Message => _message;
    }
}
