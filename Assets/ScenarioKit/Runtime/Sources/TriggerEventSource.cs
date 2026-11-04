using ScenarioKit.Events;
using UnityEngine;

namespace ScenarioKit.Sources
{
    [RequireComponent(typeof(Collider))]
    public class TriggerEventSource : ScenarioEventSource
    {
        [Header("События")]
        [SerializeField]
        [Tooltip("Событие при входе в зону.")]
        private ScenarioEventKey _onEnter;

        [SerializeField]
        [Tooltip("Событие при выходе из зоны.")]
        private ScenarioEventKey _onExit;

        [Header("Фильтр")]
        [SerializeField]
        [Tooltip("Тег объекта, который запускает событие. Пусто — любой объект.")]
        private string _requiredTag = "Player";

        public ScenarioEventKey OnEnter => _onEnter;
        public ScenarioEventKey OnExit => _onExit;
        public string RequiredTag => _requiredTag;

        private void OnTriggerEnter(Collider other)
        {
            if (IsTagAllowed(other.gameObject, _requiredTag))
            {
                Raise(_onEnter);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (IsTagAllowed(other.gameObject, _requiredTag))
            {
                Raise(_onExit);
            }
        }
    }
}
