using ScenarioKit.Sources;
using UnityEditor;
using UnityEngine;

namespace ScenarioKit.EditorTools
{
    [CustomEditor(typeof(TriggerEventSource))]
    public class TriggerEventSourceEditor : ScenarioEventSourceEditor
    {
        private const string ON_ENTER_FIELD = "_onEnter";
        private const string ON_EXIT_FIELD = "_onExit";
        private const string TAG_FIELD = "_requiredTag";

        protected override void DrawFields()
        {
            DrawColliderWarning();
            DrawEventKey(ON_ENTER_FIELD, "Событие при входе", "Событие при входе в зону.");
            DrawEventKey(ON_EXIT_FIELD, "Событие при выходе", "Событие при выходе из зоны.");
            DrawTag(TAG_FIELD, "Тег объекта", "Тег объекта, который запускает событие.");
            DrawOtherFields(ON_ENTER_FIELD, ON_EXIT_FIELD, TAG_FIELD);
        }

        private void DrawColliderWarning()
        {
            Component component = (Component)target;
            Collider collider = component.GetComponent<Collider>();

            if (collider == null)
            {
                EditorGUILayout.HelpBox("На объекте нет коллайдера. Добавьте Collider с включённым Is Trigger.", MessageType.Warning);
            }
            else if (!collider.isTrigger)
            {
                EditorGUILayout.HelpBox("У коллайдера не включён Is Trigger — вход в зону не будет срабатывать.", MessageType.Warning);
            }
        }
    }
}
