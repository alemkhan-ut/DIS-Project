using System;
using System.Collections.Generic;

namespace ScenarioKit.Logging
{
    [Serializable]
    public class LogRecord
    {
        public double Time;
        public string Type;
        public string StepId;
        public string Result;
        public string Message;
        public List<LogField> Data = new List<LogField>();

        public string GetValue(string key)
        {
            if (Data == null)
            {
                return null;
            }

            foreach (LogField field in Data)
            {
                if (field != null && field.Key == key)
                {
                    return field.Value;
                }
            }

            return null;
        }
    }
}
