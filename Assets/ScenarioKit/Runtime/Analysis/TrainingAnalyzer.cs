using ScenarioKit.Logging;
using UnityEngine;

namespace ScenarioKit.Analysis
{
    [CreateAssetMenu(menuName = "ScenarioKit/Анализатор: тренировка")]
    public class TrainingAnalyzer : ScenarioAnalyzer
    {
        [SerializeField]
        [Tooltip("Баллы за шаг по номеру попытки: первый элемент — с первой попытки, второй — со второй и так далее. Дальше баллов нет.")]
        private int[] _pointsByAttempt = { 10, 5 };

        public int[] PointsByAttempt => _pointsByAttempt;

        public override AnalysisResult Analyze(ScenarioLog log)
        {
            LogDigest digest = LogDigest.Create(log);
            AnalysisResult result = new AnalysisResult
            {
                AnalyzerName = DisplayName
            };

            result.Warnings.AddRange(digest.Warnings);
            int maxPerStep = _pointsByAttempt != null && _pointsByAttempt.Length > 0 ? _pointsByAttempt[0] : 0;

            foreach (string id in digest.StepIds)
            {
                StepFacts facts = digest.GetFacts(id);
                StepScore score = new StepScore
                {
                    StepId = id
                };

                if (facts.Completed)
                {
                    score.Status = StepScoreStatuses.COMPLETED;
                    score.Attempts = facts.ViolationsBeforeCompletion + 1;
                    score.Points = GetPoints(score.Attempts);
                }
                else
                {
                    score.Status = StepScoreStatuses.NOT_COMPLETED;
                    score.Attempts = facts.Violations;
                }

                result.Steps.Add(score);
                result.Score += score.Points;
                result.MaxScore += maxPerStep;
            }

            return result;
        }

        private int GetPoints(int attempt)
        {
            int index = attempt - 1;

            if (_pointsByAttempt == null || index < 0 || index >= _pointsByAttempt.Length)
            {
                return 0;
            }

            return _pointsByAttempt[index];
        }
    }
}
