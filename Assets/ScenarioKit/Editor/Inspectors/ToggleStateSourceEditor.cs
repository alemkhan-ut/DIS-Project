using ScenarioKit.Sources;
using UnityEditor;

namespace ScenarioKit.EditorTools
{
    [CustomEditor(typeof(ToggleStateSource))]
    public class ToggleStateSourceEditor : ScenarioEventSourceEditor
    {
        private const string STATE_FIELD = "_state";

        protected override void DrawFields()
        {
            DrawStateKey(STATE_FIELD, "Состояние", "Состояние мира, в которое пишется значение переключателя.");
            DrawOtherFields(STATE_FIELD);
        }
    }
}
