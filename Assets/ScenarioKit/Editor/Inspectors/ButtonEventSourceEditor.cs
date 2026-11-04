using ScenarioKit.Sources;
using UnityEditor;

namespace ScenarioKit.EditorTools
{
    [CustomEditor(typeof(ButtonEventSource))]
    public class ButtonEventSourceEditor : ScenarioEventSourceEditor
    {
        private const string ON_CLICK_FIELD = "_onClick";

        protected override void DrawFields()
        {
            DrawEventKey(ON_CLICK_FIELD, "Событие при нажатии", "Событие, которое произойдёт при нажатии на кнопку.");
            DrawOtherFields(ON_CLICK_FIELD);
        }
    }
}
