using ScenarioKit.Events;

namespace ScenarioKit.Steps
{
    public abstract class StepTracker
    {
        public abstract StepVerdict Handle(ScenarioEventKey key, IWorldState world);

        public abstract void Reset();
    }
}
