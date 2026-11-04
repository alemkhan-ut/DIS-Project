using System.Collections.Generic;
using UnityEngine;

namespace ScenarioKit.Modes
{
    [CreateAssetMenu(menuName = "ScenarioKit/Режим: тренировка")]
    public class TrainingMode : ScenarioMode
    {
        [SerializeField]
        [Tooltip("Группа шагов тренировки.")]
        private StepGroup _group = new StepGroup();

        public override IReadOnlyList<StepGroup> Groups => new[] { _group };
        public override bool RetryAfterViolation => true;
    }
}
