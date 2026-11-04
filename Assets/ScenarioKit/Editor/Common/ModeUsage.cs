using ScenarioKit.Modes;

namespace ScenarioKit.EditorTools
{
    public readonly struct ModeUsage
    {
        public readonly ScenarioMode Mode;
        public readonly string GroupName;
        public readonly int StageIndex;

        public ModeUsage(ScenarioMode mode, string groupName, int stageIndex)
        {
            Mode = mode;
            GroupName = groupName;
            StageIndex = stageIndex;
        }
    }
}
