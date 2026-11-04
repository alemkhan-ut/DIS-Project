using ScenarioKit.Logging;
using UnityEngine;

namespace ScenarioKit.Analysis
{
    public abstract class ScenarioAnalyzer : ScriptableObject
    {
        [SerializeField]
        [Tooltip("Название анализатора в отчёте. Если пусто, берётся имя ассета.")]
        private string _displayName;

        public string DisplayName => string.IsNullOrEmpty(_displayName) ? name : _displayName;

        public abstract AnalysisResult Analyze(ScenarioLog log);
    }
}
