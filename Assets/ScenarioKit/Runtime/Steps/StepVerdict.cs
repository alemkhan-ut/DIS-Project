namespace ScenarioKit.Steps
{
    public readonly struct StepVerdict
    {
        public const string CODE_RULE = "rule";
        public const string CODE_WRONG_ORDER = "wrong_order";
        public const string CODE_STATE = "state";
        public const string CODE_OUT_OF_ORDER = "out_of_order";

        public static readonly StepVerdict None = new StepVerdict(StepVerdictType.None, null, null);
        public static readonly StepVerdict Progress = new StepVerdict(StepVerdictType.Progress, null, null);
        public static readonly StepVerdict Completed = new StepVerdict(StepVerdictType.Completed, null, null);

        private readonly StepVerdictType _type;
        private readonly string _code;
        private readonly string _message;

        public StepVerdictType Type => _type;
        public string Code => _code;
        public string Message => _message;

        public StepVerdict(StepVerdictType type, string code, string message)
        {
            _type = type;
            _code = code;
            _message = message;
        }

        public static StepVerdict GetViolation(string code, string message)
        {
            return new StepVerdict(StepVerdictType.Violation, code, message);
        }
    }
}
