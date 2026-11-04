using UnityEngine;

namespace ScenarioKit.Logging
{
    public abstract class ScenarioLoggerAsset : ScriptableObject
    {
        [SerializeField]
        [Tooltip("Название логгера, которое увидит пользователь.")]
        private string _displayName;

        public string DisplayName => _displayName;

        public abstract IScenarioLogger Create(ScenarioLog header);
    }
}
