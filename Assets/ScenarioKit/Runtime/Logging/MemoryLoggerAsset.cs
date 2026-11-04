using UnityEngine;

namespace ScenarioKit.Logging
{
    [CreateAssetMenu(menuName = "ScenarioKit/Логгер: в памяти")]
    public class MemoryLoggerAsset : ScenarioLoggerAsset
    {
        public override IScenarioLogger Create(ScenarioLog header)
        {
            return new MemoryScenarioLogger(header);
        }
    }
}
