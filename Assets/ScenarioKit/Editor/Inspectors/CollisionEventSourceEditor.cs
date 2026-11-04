using ScenarioKit.Sources;
using UnityEditor;

namespace ScenarioKit.EditorTools
{
    [CustomEditor(typeof(CollisionEventSource))]
    public class CollisionEventSourceEditor : ScenarioEventSourceEditor
    {
        private const string ON_COLLISION_FIELD = "_onCollision";
        private const string TAG_FIELD = "_requiredTag";

        protected override void DrawFields()
        {
            EditorGUILayout.HelpBox("Для столкновений нужен Rigidbody хотя бы на одном из двух объектов.", MessageType.Info);
            DrawEventKey(ON_COLLISION_FIELD, "Событие при столкновении", "Событие при столкновении с объектом.");
            DrawTag(TAG_FIELD, "Тег объекта", "Тег объекта, который запускает событие.");
            DrawOtherFields(ON_COLLISION_FIELD, TAG_FIELD);
        }
    }
}
