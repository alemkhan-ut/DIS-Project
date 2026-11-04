using ScenarioKit.Events;
using UnityEngine;

namespace ScenarioKit.Sources
{
    public class UnityEventSource : ScenarioEventSource
    {
        [Header("Событие")]
        [SerializeField]
        [Tooltip("Событие, которое произойдёт при вызове Raise из любого UnityEvent.")]
        private ScenarioEventKey _key;

        public ScenarioEventKey Key => _key;

        public void Raise()
        {
            Raise(_key);
        }

        public void RaiseIfTrue(bool value)
        {
            if (value)
            {
                Raise(_key);
            }
        }

        public void RaiseIfFalse(bool value)
        {
            if (!value)
            {
                Raise(_key);
            }
        }
    }
}
