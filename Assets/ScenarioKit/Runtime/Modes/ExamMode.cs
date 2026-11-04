using System.Collections.Generic;
using UnityEngine;

namespace ScenarioKit.Modes
{
    [CreateAssetMenu(menuName = "ScenarioKit/Режим: экзамен")]
    public class ExamMode : ScenarioMode
    {
        [SerializeField]
        [Tooltip("Группы шагов экзамена. Идут по очереди.")]
        private List<StepGroup> _groups = new List<StepGroup>();

        [SerializeField]
        [Min(1)]
        [Tooltip("Сколько нарушений в группе допустимо. При достижении порога остаток группы пропускается.")]
        private int _violationThreshold = 2;

        public int ViolationThreshold => _violationThreshold;

        public override IReadOnlyList<StepGroup> Groups => _groups;
        public override bool RetryAfterViolation => false;

        public override bool ShouldSkipRestOfGroup(int violationsInGroup)
        {
            return violationsInGroup >= _violationThreshold;
        }
    }
}
