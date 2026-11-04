using System;
using System.Collections.Generic;

namespace ScenarioKit.Analysis
{
    [Serializable]
    public class AnalysisResult
    {
        public string AnalyzerName;
        public int Score;
        public int MaxScore;
        public List<StepScore> Steps = new List<StepScore>();
        public List<string> Warnings = new List<string>();
    }
}
