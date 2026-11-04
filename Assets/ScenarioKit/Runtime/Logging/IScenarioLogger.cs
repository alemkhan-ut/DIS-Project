namespace ScenarioKit.Logging
{
    public interface IScenarioLogger
    {
        void Write(LogRecord record);

        ScenarioLog Complete();
    }
}
