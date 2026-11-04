using UnityEngine;

namespace ScenarioKit.Events
{
    [CreateAssetMenu(menuName = "ScenarioKit/Событие")]
    public class ScenarioEventKey : ScriptableObject
    {
        [SerializeField]
        [TextArea]
        [Tooltip("Что означает это событие. Только для вас, на работу не влияет.")]
        private string _description;

        public string Description => _description;
    }
}
