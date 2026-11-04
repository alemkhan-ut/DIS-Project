using ScenarioKit.Steps;

namespace ScenarioKit.EditorTools
{
    public class StepDragData
    {
        public readonly StepDefinition Step;
        public readonly object SourceList;
        public readonly int SourceIndex;

        public StepDragData(StepDefinition step, object sourceList, int sourceIndex)
        {
            Step = step;
            SourceList = sourceList;
            SourceIndex = sourceIndex;
        }
    }
}
