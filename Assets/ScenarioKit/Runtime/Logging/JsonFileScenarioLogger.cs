namespace ScenarioKit.Logging
{
    public class JsonFileScenarioLogger : MemoryScenarioLogger
    {
        private readonly string _path;

        public JsonFileScenarioLogger(ScenarioLog header, string path) : base(header)
        {
            _path = path;
        }

        public override void Write(LogRecord record)
        {
            base.Write(record);
            Flush();
        }

        public override ScenarioLog Complete()
        {
            Flush();
            return base.Complete();
        }

        private void Flush()
        {
            ScenarioLogStorage.Save(base.Complete(), _path);
        }
    }
}
