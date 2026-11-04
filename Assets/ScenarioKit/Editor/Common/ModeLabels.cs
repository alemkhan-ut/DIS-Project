using ScenarioKit.Modes;

namespace ScenarioKit.EditorTools
{
    public static class ModeLabels
    {
        private const string EMPTY_LABEL = "(пусто)";
        private const string TRAINING_LABEL = "Тренировка";
        private const string EXAM_LABEL = "Экзамен";

        public static string GetName(ScenarioMode mode)
        {
            if (mode == null)
            {
                return EMPTY_LABEL;
            }

            return string.IsNullOrEmpty(mode.DisplayName) ? mode.name : mode.DisplayName;
        }

        public static string GetTypeName(ScenarioMode mode)
        {
            if (mode is TrainingMode)
            {
                return TRAINING_LABEL;
            }

            if (mode is ExamMode)
            {
                return EXAM_LABEL;
            }

            return mode == null ? EMPTY_LABEL : mode.GetType().Name;
        }
    }
}
