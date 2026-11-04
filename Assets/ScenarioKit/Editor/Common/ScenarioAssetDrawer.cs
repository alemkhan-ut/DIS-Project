using UnityEditor;
using UnityEngine;

namespace ScenarioKit.EditorTools
{
    public abstract class ScenarioAssetDrawer<T> : PropertyDrawer where T : ScriptableObject
    {
        protected abstract string CreateFolder { get; }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.ObjectReference)
            {
                EditorGUI.LabelField(position, label, new GUIContent("Поддерживаются только ссылки на ассеты"));
                return;
            }

            AssetPickerField.Draw<T>(position, property, label, CreateFolder);
        }
    }
}
