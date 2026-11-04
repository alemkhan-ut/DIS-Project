using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ScenarioKit.Steps;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ScenarioKit.EditorTools
{
    public static class AssetPickerField
    {
        private const float BUTTON_WIDTH = 22f;
        private const float SPACING = 2f;
        private const string NONE_LABEL = "(не выбрано)";
        private const string CREATE_LABEL = "＋ Создать…";
        private const string UNDO_CREATE = "Создание ассета ScenarioKit";
        private const string STEP_ID_FIELD = "_id"; // приватное поле StepDefinition, имя фиксировано сериализацией

        private static PendingCreate _pending;

        public static void Draw<T>(Rect rect, SerializedProperty property, GUIContent label, string createFolder, Type fieldType = null) where T : ScriptableObject
        {
            Type assetType = fieldType ?? typeof(T);
            label = EditorGUI.BeginProperty(rect, label, property);
            Rect fieldRect = rect;

            if (label != null && !string.IsNullOrEmpty(label.text))
            {
                fieldRect = EditorGUI.PrefixLabel(rect, label);
            }

            ShowPendingPopup(rect, property);

            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            Rect pingRect = new Rect(fieldRect.xMax - BUTTON_WIDTH, fieldRect.y, BUTTON_WIDTH, fieldRect.height);
            Rect menuRect = new Rect(pingRect.x - BUTTON_WIDTH - SPACING, fieldRect.y, BUTTON_WIDTH, fieldRect.height);
            Rect objectRect = new Rect(fieldRect.x, fieldRect.y, menuRect.x - fieldRect.x - SPACING, fieldRect.height);

            EditorGUI.ObjectField(objectRect, property, assetType, GUIContent.none);

            if (GUI.Button(menuRect, new GUIContent("▾", "Выбрать из списка или создать новый"), EditorStyles.miniButton))
            {
                ShowMenu(property, assetType, createFolder);
            }

            using (new EditorGUI.DisabledScope(property.objectReferenceValue == null))
            {
                GUIContent pingContent = new GUIContent(EditorGUIUtility.IconContent("Search Icon"));
                pingContent.tooltip = "Показать в окне Project";

                if (GUI.Button(pingRect, pingContent, EditorStyles.miniButton))
                {
                    EditorGUIUtility.PingObject(property.objectReferenceValue);
                }
            }

            EditorGUI.indentLevel = indent;
            EditorGUI.EndProperty();
        }

        public static void DrawLayout<T>(SerializedProperty property, GUIContent label, string createFolder, Type fieldType = null) where T : ScriptableObject
        {
            Rect rect = EditorGUILayout.GetControlRect();
            Draw<T>(rect, property, label, createFolder, fieldType);
        }

        private static void ShowMenu(SerializedProperty property, Type assetType, string createFolder)
        {
            Object target = property.serializedObject.targetObject;
            string propertyPath = property.propertyPath;
            Object current = property.objectReferenceValue;

            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent(NONE_LABEL), current == null, () => SetReference(target, propertyPath, assetType, null));
            menu.AddSeparator(string.Empty);

            List<string> paths = GetAssetPaths(assetType);

            foreach (string path in paths)
            {
                ScriptableObject asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);

                if (asset == null || !assetType.IsInstanceOfType(asset))
                {
                    continue;
                }

                string folderName = Path.GetFileName(Path.GetDirectoryName(path));
                GUIContent content = new GUIContent(asset.name + "    (" + folderName + ")");
                menu.AddItem(content, asset == current, () => SetReference(target, propertyPath, assetType, asset));
            }

            menu.AddSeparator(string.Empty);
            AddCreateItems(menu, assetType, target, propertyPath, createFolder);
            menu.ShowAsContext();
        }

        private static List<string> GetAssetPaths(assetType) where T : ScriptableObject
        {
            string[] guids = AssetDatabase.FindAssets("t:" + assetType.Name);
            List<string> paths = new List<string>(guids.Length);

            foreach (string guid in guids)
            {
                paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            }

            paths.Sort(ComparePathsByName);
            return paths;
        }

        private static int ComparePathsByName(string a, string b)
        {
            int result = string.Compare(Path.GetFileNameWithoutExtension(a), Path.GetFileNameWithoutExtension(b), StringComparison.OrdinalIgnoreCase);
            return result != 0 ? result : string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
        }

        private static void AddCreateItems(GenericMenu menu, Type assetType, Object target, string propertyPath, string createFolder)
        {
            if (!assetType.IsAbstract)
            {
                menu.AddItem(new GUIContent(CREATE_LABEL), false, () => RequestCreate(target, propertyPath, assetType, assetType, createFolder));
                return;
            }

            bool hasTypes = false;

            foreach (Type type in TypeCache.GetTypesDerivedFrom(assetType))
            {
                if (type.IsAbstract || type.IsGenericType)
                {
                    continue;
                }

                hasTypes = true;
                Type createType = type;
                GUIContent content = new GUIContent(CREATE_LABEL + "/" + GetTypeLabel(createType));
                menu.AddItem(content, false, () => RequestCreate(target, propertyPath, assetType, createType, createFolder));
            }

            if (!hasTypes)
            {
                menu.AddDisabledItem(new GUIContent(CREATE_LABEL));
            }
        }

        private static string GetTypeLabel(Type type)
        {
            CreateAssetMenuAttribute attribute = type.GetCustomAttribute<CreateAssetMenuAttribute>();

            if (attribute == null || string.IsNullOrEmpty(attribute.menuName))
            {
                return type.Name;
            }

            string[] segments = attribute.menuName.Split('/');
            return segments[segments.Length - 1];
        }

        private static void RequestCreate(Object target, string propertyPath, Type assetType, Type type, string createFolder)
        {
            _pending = new PendingCreate(target, propertyPath, assetType, type, createFolder);
            InternalEditorUtility.RepaintAllViews();
        }

        // Окно ввода имени открываем из OnGUI поля: из колбэка GenericMenu контекст GUI уже потерян.
        private static void ShowPendingPopup(Rect rect, SerializedProperty property)
        {
            if (_pending == null || Event.current.type != EventType.Repaint)
            {
                return;
            }

            if (_pending.Target != property.serializedObject.targetObject || _pending.PropertyPath != property.propertyPath)
            {
                return;
            }

            PendingCreate pending = _pending;
            _pending = null;
            string defaultName = ScenarioKitFolders.GetDefaultName(pending.Folder);
            PopupWindow.Show(rect, new NamePromptPopup(defaultName, name => CreateAndAssign(pending, name)));
        }

        private static void CreateAndAssign(PendingCreate pending, string assetName)
        {
            ScriptableObject asset = CreateAsset(pending.Type, pending.Folder, assetName);

            if (asset == null)
            {
                return;
            }

            SetReference(pending.Target, pending.PropertyPath, pending.AssetType, asset);
            EditorGUIUtility.PingObject(asset);
        }

        private static ScriptableObject CreateAsset(Type type, string folder, string assetName)
        {
            if (!EnsureFolder(folder))
            {
                return null;
            }

            string fileName = GetSafeFileName(assetName);
            string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + fileName + ".asset");
            ScriptableObject asset = ScriptableObject.CreateInstance(type);

            if (asset is StepDefinition)
            {
                SerializedObject serialized = new SerializedObject(asset);
                serialized.FindProperty(STEP_ID_FIELD).stringValue = assetName;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            AssetDatabase.CreateAsset(asset, path);
            Undo.RegisterCreatedObjectUndo(asset, UNDO_CREATE);
            return asset;
        }

        private static bool EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || !folder.StartsWith("Assets"))
            {
                return false;
            }

            string[] parts = folder.Split('/');
            string current = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];

                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }

            return true;
        }

        private static string GetSafeFileName(string assetName)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            string result = assetName;

            foreach (char symbol in invalid)
            {
                result = result.Replace(symbol, '_');
            }

            return string.IsNullOrWhiteSpace(result) ? "Новый ассет" : result.Trim();
        }

        // Колбэк меню может прийти, когда SerializedProperty уже недействителен, поэтому открываем объект заново.
        private static void SetReference(Object target, string propertyPath, Type assetType, Object value)
        {
            if (target == null)
            {
                return;
            }

            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyPath);

            if (property == null)
            {
                return;
            }

            if (value != null && !assetType.IsInstanceOfType(value))
            {
                return;
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedProperties();
        }

        private class PendingCreate
        {
            public readonly Object Target;
            public readonly string PropertyPath;
            public readonly Type AssetType;
            public readonly Type Type;
            public readonly string Folder;

            public PendingCreate(Object target, string propertyPath, Type assetType, Type type, string folder)
            {
                Target = target;
                PropertyPath = propertyPath;
                AssetType = assetType;
                Type = type;
                Folder = folder;
            }
        }
    }
}
