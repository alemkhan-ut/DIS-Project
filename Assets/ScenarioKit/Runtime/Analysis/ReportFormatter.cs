using System.Text;
using ScenarioKit.Logging;
using ScenarioKit.Steps;

namespace ScenarioKit.Analysis
{
    public static class ReportFormatter
    {
        private const string DEFAULT_GREEN = "#59C773";
        private const string DEFAULT_RED = "#FF6161";

        private const string COMPLETED_TEXT = "выполнен";
        private const string NOT_COMPLETED_TEXT = "не выполнен";
        private const string FAILED_TEXT = "провален";
        private const string SKIPPED_TEXT = "пропущен";

        public static string GetResultText(AnalysisResult result)
        {
            return GetResultText(result, true, DEFAULT_GREEN, DEFAULT_RED);
        }

        public static string GetResultText(AnalysisResult result, bool richText, string greenHex = DEFAULT_GREEN, string redHex = DEFAULT_RED)
        {
            if (result == null)
            {
                return string.Empty;
            }

            string green = string.IsNullOrEmpty(greenHex) ? DEFAULT_GREEN : greenHex;
            string red = string.IsNullOrEmpty(redHex) ? DEFAULT_RED : redHex;

            StringBuilder builder = new StringBuilder();
            builder.Append($"Итог: {result.Score} / {result.MaxScore} ({result.AnalyzerName})");

            foreach (StepScore step in result.Steps)
            {
                builder.Append('\n');
                string displayId = StepDefinition.ResolveLogIdentifier(step.StepId);
                string statusText = GetStatusText(step.Status);

                if (richText)
                {
                    bool isCompleted = step.Status == StepScoreStatuses.COMPLETED;
                    string statusColor = isCompleted ? green : red;
                    string coloredStatus = $"<color={statusColor}>{statusText}</color>";

                    bool hasAttempts = step.Attempts > 0;
                    string attemptsColor = hasAttempts ? green : red;
                    string coloredAttempts = $"<color={attemptsColor}>попыток {step.Attempts}</color>";

                    bool pointsOk = step.Points > 0 || (isCompleted && step.Points >= 0);
                    string pointsColor = pointsOk ? green : red;
                    string coloredPoints = $"<color={pointsColor}>баллы {step.Points}</color>";

                    builder.Append($"{displayId}: {coloredStatus}, {coloredAttempts}, {coloredPoints}");
                }
                else
                {
                    builder.Append($"{displayId}: {statusText}, попыток {step.Attempts}, баллы {step.Points}");
                }
            }

            foreach (string warning in result.Warnings)
            {
                builder.Append('\n');
                if (richText)
                {
                    builder.Append($"<color={red}>⚠ {warning}</color>");
                }
                else
                {
                    builder.Append($"⚠ {warning}");
                }
            }

            return builder.ToString();
        }

        public static string GetRecordText(LogRecord record)
        {
            if (record == null)
            {
                return string.Empty;
            }

            string displayId = StepDefinition.ResolveLogIdentifier(record.StepId);
            string message = record.Message;

            if (string.IsNullOrEmpty(message))
            {
                switch (record.Type)
                {
                    case LogRecordTypes.SCENARIO_STARTED:
                        message = "Старт сценария";
                        break;
                    case LogRecordTypes.SCENARIO_FINISHED:
                        message = "Сценарий завершён";
                        break;
                    case LogRecordTypes.SCENARIO_ABORTED:
                        message = "Сценарий прерван";
                        break;
                    case LogRecordTypes.GROUP_FINISHED:
                        message = record.Result == "threshold"
                            ? "Превышен порог нарушений"
                            : "Завершение группы";
                        break;
                }
            }

            StringBuilder details = new StringBuilder();

            if (!string.IsNullOrEmpty(displayId))
            {
                details.Append(displayId);
            }

            if (!string.IsNullOrEmpty(message))
            {
                if (details.Length > 0)
                {
                    details.Append($" - {message}");
                }
                else
                {
                    details.Append(message);
                }
            }
            else if (!string.IsNullOrEmpty(record.Result))
            {
                if (details.Length > 0)
                {
                    details.Append($" - {record.Result}");
                }
                else
                {
                    details.Append(record.Result);
                }
            }

            if (details.Length > 0)
            {
                return $"[{record.Time:0.0} c] {record.Type} - {details}";
            }

            return $"[{record.Time:0.0} c] {record.Type}";
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
