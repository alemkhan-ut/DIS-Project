using UnityEngine;

namespace ScenarioKit.Validation
{
    public readonly struct ValidationMessage
    {
        public ValidationMessage(ValidationSeverity severity, string text, Object context)
        {
            Severity = severity;
            Text = text;
            Context = context;
        }

        public ValidationSeverity Severity { get; }
        public string Text { get; }
        public Object Context { get; }
    }
}
