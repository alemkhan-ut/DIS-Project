namespace ScenarioKit.EditorTools
{
    public static class ScenarioKitFolders
    {
        public const string ROOT = "Assets/ScenarioKit/Demo/Data";
        public const string EVENTS = ROOT + "/Events";
        public const string STATES = ROOT + "/States";
        public const string STEPS = ROOT + "/Steps";
        public const string MODES = ROOT + "/Modes";
        public const string LOGGERS = ROOT + "/Loggers";
        public const string ANALYZERS = ROOT + "/Analyzers";

        private const string DEFAULT_NAME = "Новый ассет";

        public static string GetDefaultName(string folder)
        {
            switch (folder)
            {
                case EVENTS:
                    return "Новое событие";
                case STATES:
                    return "Новое состояние";
                case STEPS:
                    return "Новый шаг";
                case MODES:
                    return "Новый режим";
                case LOGGERS:
                    return "Новый логгер";
                case ANALYZERS:
                    return "Новый анализатор";
                default:
                    return DEFAULT_NAME;
            }
        }
    }
}
