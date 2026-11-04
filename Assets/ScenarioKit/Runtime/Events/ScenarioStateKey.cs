using UnityEngine;

namespace ScenarioKit.Events
{
    [CreateAssetMenu(menuName = "ScenarioKit/Состояние")]
    public class ScenarioStateKey : ScriptableObject
    {
        [SerializeField]
        [TextArea]
        [Tooltip("Что означает это состояние. Только для вас, на работу не влияет.")]
        private string _description;

        public string Description => _description;
    }
}
