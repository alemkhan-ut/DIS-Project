using System;
using System.Collections.Generic;

namespace ScenarioKit.Logging
{
    [Serializable]
    public class ScenarioLog
    {
        public int FormatVersion = 1;
        public string ScenarioId;
        public string ModeName;
        public string StartedUtc;
        public List<string> StepIds = new List<string>();
        public List<LogRecord> Records = new List<LogRecord>();
    }
}
