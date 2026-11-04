namespace ScenarioKit.Logging
{
    public class MemoryScenarioLogger : IScenarioLogger
    {
        private readonly ScenarioLog _log;

        public MemoryScenarioLogger(ScenarioLog header)
        {
            _log = header;
        }

        public virtual void Write(LogRecord record)
        {
            if (record == null)
            {
                return;
            }

            _log.Records.Add(record);
        }

        public virtual ScenarioLog Complete()
        {
            return _log;
        }
    }
}
