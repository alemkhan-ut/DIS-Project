using ScenarioKit.Events;
using UnityEngine;

namespace ScenarioKit.Sources
{
    public abstract class ScenarioEventSource : MonoBehaviour
    {
        protected void Raise(ScenarioEventKey key)
        {
            ScenarioEventBus.Raise(key);
        }

        protected bool IsTagAllowed(GameObject other, string requiredTag)
        {
            return string.IsNullOrEmpty(requiredTag) || other.CompareTag(requiredTag);
        }
    }
}
