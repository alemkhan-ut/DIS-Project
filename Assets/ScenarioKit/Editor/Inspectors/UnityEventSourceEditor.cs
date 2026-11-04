using ScenarioKit.Sources;
using UnityEditor;

namespace ScenarioKit.EditorTools
{
    [CustomEditor(typeof(UnityEventSource))]
    public class UnityEventSourceEditor : ScenarioEventSourceEditor
    {
        private const string KEY_FIELD = "_key";

        private const string HINT = "Как подключить: в любом UnityEvent (например, Button.onClick) нажмите «+», "
            + "перетащите сюда этот объект и выберите метод UnityEventSource.Raise.\n"
            + "Для Toggle.onValueChanged выберите динамический bool: RaiseIfTrue (событие при включении) или RaiseIfFalse (при выключении).";

        protected override void DrawFields()
        {
            EditorGUILayout.HelpBox(HINT, MessageType.Info);
            DrawEventKey(KEY_FIELD, "Событие", "Событие, которое произойдёт при вызове Raise из любого UnityEvent.");
            DrawOtherFields(KEY_FIELD);
        }
    }
}
