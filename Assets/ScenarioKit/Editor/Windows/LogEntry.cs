using System.Collections.Generic;
using ScenarioKit.Logging;

namespace ScenarioKit.EditorTools
{
    internal class LogEntry
    {
        public readonly List<string> Warnings = new List<string>();
        public ScenarioLog Log;
        public string Label;
    }
}
