using System;

namespace ScenarioKit.Analysis
{
    [Serializable]
    public class StepScore
    {
        public string StepId;
        public string Status;
        public int Attempts;
        public int Points;
    }
}
