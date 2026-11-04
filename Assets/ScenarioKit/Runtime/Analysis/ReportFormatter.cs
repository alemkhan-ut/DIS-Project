using System.Text;
using ScenarioKit.Logging;

namespace ScenarioKit.Analysis
{
    public static class ReportFormatter
    {
        private const string COMPLETED_TEXT = "выполнен";
        private const string NOT_COMPLETED_TEXT = "не выполнен";
        private const string FAILED_TEXT = "провален";
        private const string SKIPPED_TEXT = "пропущен";

        public static string GetResultText(AnalysisResult result)
        {
            if (result == null)
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder();
            builder.Append($"Итог: {result.Score} / {result.MaxScore} ({result.AnalyzerName})");

            foreach (StepScore step in result.Steps)
            {
                builder.Append('\n');
                builder.Append($"{step.StepId}: {GetStatusText(step.Status)}, попыток {step.Attempts}, баллы {step.Points}");
            }

            foreach (string warning in result.Warnings)
            {
                builder.Append('\n');
                builder.Append($"⚠ {warning}");
            }

            return builder.ToString();
        }

        public static string GetRecordText(LogRecord record)
        {
            if (record == null)
            {
                return string.Empty;
            }

            return $"[{record.Time:0.0} c] {record.Type} {record.StepId} {record.Result} {record.Message}";
        }

        public static string GetLogText(ScenarioLog log)
        {
            if (log == null || log.Records == null)
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder();

            foreach (LogRecord record in log.Records)
            {
                if (record == null)
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append('\n');
                }

                builder.Append(GetRecordText(record));
            }

            return builder.ToString();
        }

        private static string GetStatusText(string status)
        {
            switch (status)
            {
                case StepScoreStatuses.COMPLETED:
                    return COMPLETED_TEXT;
                case StepScoreStatuses.NOT_COMPLETED:
                    return NOT_COMPLETED_TEXT;
                case StepScoreStatuses.FAILED:
                    return FAILED_TEXT;
                case StepScoreStatuses.SKIPPED:
                    return SKIPPED_TEXT;
                default:
                    return status;
            }
        }
    }
}
