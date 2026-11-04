using ScenarioKit.Logging;
using UnityEngine;

namespace ScenarioKit.Analysis
{
    [CreateAssetMenu(menuName = "ScenarioKit/Анализатор: экзамен")]
    public class ExamAnalyzer : ScenarioAnalyzer
    {
        private const string UNDETERMINED_WARNING_FORMAT = "Не удалось определить результат шага «{0}» — считается пропущенным";

        [SerializeField]
        [Min(0)]
        [Tooltip("Начальный балл экзамена.")]
        private int _baseScore = 100;

        [SerializeField]
        [Min(0)]
        [Tooltip("Штраф за проваленный шаг.")]
        private int _failedPenalty = 10;

        [SerializeField]
        [Min(0)]
        [Tooltip("Штраф за пропущенный шаг.")]
        private int _skippedPenalty = 5;

        public int BaseScore => _baseScore;
        public int FailedPenalty => _failedPenalty;
        public int SkippedPenalty => _skippedPenalty;

        public override AnalysisResult Analyze(ScenarioLog log)
        {
            LogDigest digest = LogDigest.Create(log);
            AnalysisResult result = new AnalysisResult
            {
                AnalyzerName = DisplayName,
                MaxScore = _baseScore
            };

            result.Warnings.AddRange(digest.Warnings);
            int total = _baseScore;

            foreach (string id in digest.StepIds)
            {
                StepFacts facts = digest.GetFacts(id);
                StepScore score = new StepScore
                {
                    StepId = id
                };

                if (facts.Failed || facts.Violations > 0)
                {
                    score.Status = StepScoreStatuses.FAILED;
                    score.Attempts = facts.Violations;
                    score.Points = -_failedPenalty;
                }
                else if (facts.Skipped)
                {
                    score.Status = StepScoreStatuses.SKIPPED;
                    score.Points = -_skippedPenalty;
                }
                else if (facts.Completed)
                {
                    score.Status = StepScoreStatuses.COMPLETED;
                    score.Attempts = 1;
                }
                else
                {
                    score.Status = StepScoreStatuses.SKIPPED;
                    score.Points = -_skippedPenalty;
                    result.Warnings.Add(string.Format(UNDETERMINED_WARNING_FORMAT, id));
                }

                total += score.Points;
                result.Steps.Add(score);
            }

            result.Score = digest.StepIds.Count == 0 ? 0 : Mathf.Max(0, total);
            return result;
        }
    }
}
