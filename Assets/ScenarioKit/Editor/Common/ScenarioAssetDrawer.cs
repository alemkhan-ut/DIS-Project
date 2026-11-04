using System;
using System.Collections.Generic;
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

            AssetPickerField.Draw<T>(position, property, label, CreateFolder, GetFieldType());
        }

        private Type GetFieldType()
        {
            Type type = fieldInfo?.FieldType;

            if (type == null)
            {
                return null;
            }

            if (type.IsArray)
            {
                return type.GetElementType();
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                return type.GetGenericArguments()[0];
            }

            return type;
        }
    }
}
