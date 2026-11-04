using ScenarioKit.Events;
using UnityEngine;

namespace ScenarioKit.Sources
{
    [RequireComponent(typeof(Collider))]
    public class CollisionEventSource : ScenarioEventSource
    {
        [Header("Событие")]
        [SerializeField]
        [Tooltip("Событие при столкновении с объектом.")]
        private ScenarioEventKey _onCollision;

        [Header("Фильтр")]
        [SerializeField]
        [Tooltip("Тег объекта, который запускает событие. Пусто — любой объект.")]
        private string _requiredTag = "Player";

        public ScenarioEventKey OnCollision => _onCollision;
        public string RequiredTag => _requiredTag;

        private void OnCollisionEnter(Collision collision)
        {
            if (IsTagAllowed(collision.gameObject, _requiredTag))
            {
                Raise(_onCollision);
            }
        }
    }
}
