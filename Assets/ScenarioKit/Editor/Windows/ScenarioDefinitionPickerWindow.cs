using System;
using System.Collections.Generic;
using System.IO;
using ScenarioKit.Execution;
using ScenarioKit.Modes;
using ScenarioKit.Steps;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ScenarioKit.EditorTools
{
    /// <summary>
    /// Модальное окно выбора и управления сценариями (ScenarioDefinition) с возможностью создания новых.
    /// </summary>
    public class ScenarioDefinitionPickerWindow : EditorWindow
    {
        private const float LIST_WIDTH = 300f;
        private const float ROW_HEIGHT = 56f;
        private const float MARGIN = 6f;

        private readonly List<ScenarioDefinition> _allScenarios = new List<ScenarioDefinition>();
        private readonly List<ScenarioDefinition> _filteredScenarios = new List<ScenarioDefinition>();
        private readonly Dictionary<ScenarioDefinition, string> _assetPaths = new Dictionary<ScenarioDefinition, string>();

        private ScenarioDefinition _selectedScenario;
        private Vector2 _listScroll;
        private Vector2 _detailsScroll;
        private string _searchFilter = string.Empty;

        // Панель создания нового сценария
        private bool _showCreatePanel;
        private string _newAssetName = "NewScenario";
        private string _newScenarioId = "new_scenario";
        private string _newAssetFolder = ScenarioKitFolders.ROOT;
        private string _createError = string.Empty;

        // Состояния сворачивания списков в деталях
        private bool _stepsFoldout = true;
        private bool _modesFoldout = true;

        [MenuItem("Tools/ScenarioKit/Сценарии (Scenario Definition)", priority = 10)]
        [MenuItem("Tools/ScenarioKit/Выбор сценария (Scenario Definition)", priority = 11)]
        public static void Open()
        {
            ScenarioDefinitionPickerWindow window = CreateInstance<ScenarioDefinitionPickerWindow>();
            window.titleContent = new GUIContent("Сценарии (Scenario Definition)");
            window.minSize = new Vector2(720f, 480f);
            window.maxSize = new Vector2(980f, 680f);
            window.ShowModalUtility();
        }

        public static ScenarioDefinitionPickerWindow OpenNonModal()
        {
            ScenarioDefinitionPickerWindow window = GetWindow<ScenarioDefinitionPickerWindow>();
            window.titleContent = new GUIContent("Сценарии (Scenario Definition)");
            window.minSize = new Vector2(720f, 480f);
            window.Show();
            return window;
        }

        private void OnEnable()
        {
            Refresh();
        }

        public void Refresh()
        {
            string[] stepGuids = AssetDatabase.FindAssets("t:StepDefinition");
            foreach (string guid in stepGuids)
            {
                string stepPath = AssetDatabase.GUIDToAssetPath(guid);
                StepDefinition step = AssetDatabase.LoadAssetAtPath<StepDefinition>(stepPath);
                if (step != null)
                {
                    StepDefinition.RegisterStep(step);
                }
            }

            _allScenarios.Clear();
            _assetPaths.Clear();

            string[] guids = AssetDatabase.FindAssets("t:ScenarioDefinition");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ScenarioDefinition scenario = AssetDatabase.LoadAssetAtPath<ScenarioDefinition>(path);
                if (scenario != null && !_allScenarios.Contains(scenario))
                {
                    _allScenarios.Add(scenario);
                    _assetPaths[scenario] = path;
                }
            }

            _allScenarios.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
            ApplyFilter();

            if (_selectedScenario == null && _filteredScenarios.Count > 0)
            {
                _selectedScenario = _filteredScenarios[0];
            }
        }

        private void ApplyFilter()
        {
            _filteredScenarios.Clear();
            string filter = _searchFilter?.Trim().ToLowerInvariant() ?? string.Empty;

            foreach (ScenarioDefinition scenario in _allScenarios)
            {
                if (scenario == null) continue;

                if (string.IsNullOrEmpty(filter))
                {
                    _filteredScenarios.Add(scenario);
                    continue;
                }

                string name = scenario.name.ToLowerInvariant();
                string id = (scenario.ScenarioId ?? string.Empty).ToLowerInvariant();
                string path = _assetPaths.TryGetValue(scenario, out string p) ? p.ToLowerInvariant() : string.Empty;

                if (name.Contains(filter) || id.Contains(filter) || path.Contains(filter))
                {
                    _filteredScenarios.Add(scenario);
                }
            }
        }

        private void OnGUI()
        {
            HandleKeyboard();

            DrawToolbar();

            if (_showCreatePanel)
            {
                DrawCreatePanel();
            }

            EditorGUILayout.BeginHorizontal();
            DrawList();
            DrawDetails();
            EditorGUILayout.EndHorizontal();

            DrawFooter();
        }

        private void HandleKeyboard()
        {
            Event current = Event.current;
            if (current == null || current.type != EventType.KeyDown) return;

            if (current.keyCode == KeyCode.Escape)
            {
                current.Use();
                Close();
            }
            else if (current.keyCode == KeyCode.Return || current.keyCode == KeyCode.KeypadEnter)
            {
                if (!_showCreatePanel && _selectedScenario != null)
                {
                    current.Use();
                    ConfirmSelection();
                }
            }
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("⟳ Обновить", EditorStyles.toolbarButton, GUILayout.Width(80f)))
            {
                Refresh();
            }

            GUILayout.Space(6f);
            EditorGUI.BeginChangeCheck();
            GUI.SetNextControlName("ScenarioSearchField");
            _searchFilter = EditorGUILayout.TextField(_searchFilter, EditorStyles.toolbarSearchField, GUILayout.Width(240f));
            if (EditorGUI.EndChangeCheck())
            {
                ApplyFilter();
            }

            if (!string.IsNullOrEmpty(_searchFilter) && GUILayout.Button("✕", EditorStyles.toolbarButton, GUILayout.Width(22f)))
            {
                _searchFilter = string.Empty;
                ApplyFilter();
                GUI.FocusControl(null);
            }

            GUILayout.Space(8f);
            GUILayout.Label($"Найдено: {_filteredScenarios.Count} из {_allScenarios.Count}", EditorStyles.miniLabel);

            GUILayout.FlexibleSpace();

            Color prevColor = GUI.backgroundColor;
            GUI.backgroundColor = _showCreatePanel ? ScenarioKitStyles.GetSelectionColor() : new Color(0.4f, 0.85f, 0.4f);
            if (GUILayout.Button(_showCreatePanel ? "Скрыть форму" : "＋ Создать сценарий", EditorStyles.toolbarButton, GUILayout.Width(140f)))
            {
                _showCreatePanel = !_showCreatePanel;
                _createError = string.Empty;
            }
            GUI.backgroundColor = prevColor;

            EditorGUILayout.EndHorizontal();
        }

        private void DrawCreatePanel()
        {
            Color prevColor = GUI.backgroundColor;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Создание нового сценария (Scenario Definition)", EditorStyles.boldLabel);

            EditorGUILayout.Space(2f);

            EditorGUI.BeginChangeCheck();
            _newAssetName = EditorGUILayout.TextField(new GUIContent("Имя ассета", "Название файла ассета без расширения."), _newAssetName);
            if (EditorGUI.EndChangeCheck())
            {
                // Автоматически предлагаем ID в формате snake_case, если пользователь не менял его вручную
                if (string.IsNullOrEmpty(_newScenarioId) || _newScenarioId.StartsWith("new_"))
                {
                    _newScenarioId = ToSnakeCase(_newAssetName);
                }
            }

            _newScenarioId = EditorGUILayout.TextField(new GUIContent("Идентификатор (ID)", "Уникальный строковый ID сценария для журналов и аналитики."), _newScenarioId);

            EditorGUILayout.BeginHorizontal();
            _newAssetFolder = EditorGUILayout.TextField(new GUIContent("Папка сохранения", "Путь к папке проекта, куда будет сохранён ассет."), _newAssetFolder);
            if (GUILayout.Button("Обзор...", EditorStyles.miniButton, GUILayout.Width(65f)))
            {
                string selectedFolder = EditorUtility.OpenFolderPanel("Выберите папку для сценария", _newAssetFolder, "");
                if (!string.IsNullOrEmpty(selectedFolder))
                {
                    if (selectedFolder.StartsWith(Application.dataPath))
                    {
                        _newAssetFolder = "Assets" + selectedFolder.Substring(Application.dataPath.Length);
                    }
                    else
                    {
                        _createError = "Папка должна находиться внутри проекта (Assets/...).";
                    }
                }
            }
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_createError))
            {
                EditorGUILayout.HelpBox(_createError, MessageType.Error);
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Создать через проводник...", EditorStyles.miniButton, GUILayout.Width(190f)))
            {
                CreateViaSaveDialog();
            }

            GUI.backgroundColor = new Color(0.35f, 0.80f, 0.45f);
            if (GUILayout.Button("Создать ассет", EditorStyles.miniButton, GUILayout.Width(130f)))
            {
                ExecuteCreateNewScenario();
            }
            GUI.backgroundColor = prevColor;

            if (GUILayout.Button("Отмена", EditorStyles.miniButton, GUILayout.Width(80f)))
            {
                _showCreatePanel = false;
                _createError = string.Empty;
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void ExecuteCreateNewScenario()
        {
            _createError = string.Empty;
            string assetName = _newAssetName?.Trim();
            if (string.IsNullOrEmpty(assetName))
            {
                _createError = "Укажите название сценария.";
                return;
            }

            string folder = string.IsNullOrWhiteSpace(_newAssetFolder) ? ScenarioKitFolders.ROOT : _newAssetFolder.Trim();
            if (!folder.StartsWith("Assets"))
            {
                _createError = "Путь к папке должен начинаться с 'Assets'.";
                return;
            }

            try
            {
                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                    AssetDatabase.Refresh();
                }

                string safeName = GetSafeFileName(assetName);
                string assetPath = AssetDatabase.GenerateUniqueAssetPath(folder.TrimEnd('/') + "/" + safeName + ".asset");

                ScenarioDefinition scenario = ScriptableObject.CreateInstance<ScenarioDefinition>();

                SerializedObject so = new SerializedObject(scenario);
                SerializedProperty idProp = so.FindProperty("_scenarioId");
                if (idProp != null)
                {
                    idProp.stringValue = string.IsNullOrWhiteSpace(_newScenarioId) ? safeName.ToLowerInvariant() : _newScenarioId.Trim();
                }
                so.ApplyModifiedPropertiesWithoutUndo();

                AssetDatabase.CreateAsset(scenario, assetPath);
                AssetDatabase.SaveAssets();

                Undo.RegisterCreatedObjectUndo(scenario, "Создание ScenarioDefinition");

                _showCreatePanel = false;
                Refresh();
                _selectedScenario = scenario;

                EditorGUIUtility.PingObject(scenario);
            }
            catch (Exception ex)
            {
                _createError = $"Ошибка при создании: {ex.Message}";
            }
        }

        private void CreateViaSaveDialog()
        {
            string defaultFolder = string.IsNullOrWhiteSpace(_newAssetFolder) ? ScenarioKitFolders.ROOT : _newAssetFolder;
            string defaultName = string.IsNullOrWhiteSpace(_newAssetName) ? "NewScenario" : _newAssetName;

            string path = EditorUtility.SaveFilePanelInProject("Новый сценарий", defaultName, "asset", "Выберите место сохранения сценария", defaultFolder);
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                string fileName = Path.GetFileNameWithoutExtension(path);
                ScenarioDefinition scenario = ScriptableObject.CreateInstance<ScenarioDefinition>();

                SerializedObject so = new SerializedObject(scenario);
                SerializedProperty idProp = so.FindProperty("_scenarioId");
                if (idProp != null)
                {
                    idProp.stringValue = string.IsNullOrWhiteSpace(_newScenarioId) ? fileName.ToLowerInvariant() : _newScenarioId.Trim();
                }
                so.ApplyModifiedPropertiesWithoutUndo();

                AssetDatabase.CreateAsset(scenario, path);
                AssetDatabase.SaveAssets();

                Undo.RegisterCreatedObjectUndo(scenario, "Создание ScenarioDefinition");

                _showCreatePanel = false;
                Refresh();
                _selectedScenario = scenario;

                EditorGUIUtility.PingObject(scenario);
            }
            catch (Exception ex)
            {
                _createError = $"Ошибка при сохранении: {ex.Message}";
            }
        }

        private void DrawList()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(LIST_WIDTH));
            EditorGUILayout.LabelField("Список сценариев", EditorStyles.boldLabel);

            _listScroll = EditorGUILayout.BeginScrollView(_listScroll);

            if (_filteredScenarios.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    _allScenarios.Count == 0
                        ? "В проекте пока нет сценариев. Нажмите «＋ Создать сценарий» выше, чтобы создать свой."
                        : "По вашему запросу сценарии не найдены.",
                    MessageType.Info);
            }

            foreach (ScenarioDefinition scenario in _filteredScenarios)
            {
                if (scenario == null) continue;

                bool isSelected = scenario == _selectedScenario;
                Color prevBg = GUI.backgroundColor;

                if (isSelected)
                {
                    GUI.backgroundColor = ScenarioKitStyles.GetSelectionColor();
                }

                EditorGUILayout.BeginVertical(EditorStyles.miniButton, GUILayout.Height(ROW_HEIGHT));

                // Клик и двойной клик
                Rect itemRect = GUILayoutUtility.GetLastRect();
                Event evt = Event.current;
                if (evt.type == EventType.MouseDown && itemRect.Contains(evt.mousePosition))
                {
                    _selectedScenario = scenario;
                    if (evt.clickCount == 2)
                    {
                        ConfirmSelection();
                    }
                    evt.Use();
                    Repaint();
                }

                // Заголовок строки
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(EditorGUIUtility.IconContent("ScriptableObject Icon"), GUILayout.Width(20f), GUILayout.Height(18f));
                GUILayout.Label(scenario.name, EditorStyles.boldLabel);
                EditorGUILayout.EndHorizontal();

                // Идентификатор и количество шагов/режимов
                string id = string.IsNullOrEmpty(scenario.ScenarioId) ? "—" : scenario.ScenarioId;
                int stepCount = scenario.Steps != null ? scenario.Steps.Count : 0;
                int modeCount = scenario.Modes != null ? scenario.Modes.Count : 0;

                EditorGUILayout.LabelField($"ID: {id}  •  {stepCount} шагов  •  {modeCount} реж.", EditorStyles.miniLabel);

                EditorGUILayout.EndVertical();

                GUI.backgroundColor = prevBg;
                EditorGUILayout.Space(2f);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawDetails()
        {
            Color prevColor = GUI.backgroundColor;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            _detailsScroll = EditorGUILayout.BeginScrollView(_detailsScroll);

            if (_selectedScenario == null)
            {
                EditorGUILayout.HelpBox("Выберите сценарий из списка слева или создайте новый через панель выше.", MessageType.Info);
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();
                return;
            }

            // Заголовок выбранного сценария
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(EditorGUIUtility.IconContent("ScriptableObject Icon"), GUILayout.Width(28f), GUILayout.Height(28f));
            EditorGUILayout.BeginVertical();
            GUILayout.Label(_selectedScenario.name, EditorStyles.largeLabel);
            EditorGUILayout.LabelField($"Идентификатор сценария (ID): {(!string.IsNullOrEmpty(_selectedScenario.ScenarioId) ? _selectedScenario.ScenarioId : "—")}", EditorStyles.boldLabel);
            EditorGUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Показать в Project", EditorStyles.miniButton, GUILayout.Width(140f)))
            {
                EditorGUIUtility.PingObject(_selectedScenario);
            }

            EditorGUILayout.EndHorizontal();

            ScenarioKitStyles.Section("Информация об ассете");

            if (_assetPaths.TryGetValue(_selectedScenario, out string path))
            {
                EditorGUILayout.TextField("Путь к ассету:", path);
            }

            int stepsCount = _selectedScenario.Steps != null ? _selectedScenario.Steps.Count : 0;
            int modesCount = _selectedScenario.Modes != null ? _selectedScenario.Modes.Count : 0;

            EditorGUILayout.LabelField($"Количество шагов: {stepsCount}");
            EditorGUILayout.LabelField($"Количество режимов: {modesCount}");

            // Секция Шагов
            EditorGUILayout.Space(6f);
            _stepsFoldout = EditorGUILayout.Foldout(_stepsFoldout, $"Шаги сценария ({stepsCount})", true, EditorStyles.foldoutHeader);
            if (_stepsFoldout)
            {
                EditorGUI.indentLevel++;
                if (stepsCount == 0)
                {
                    EditorGUILayout.LabelField("Шаги не добавлены.", EditorStyles.miniLabel);
                }
                else
                {
                    for (int i = 0; i < stepsCount; i++)
                    {
                        StepDefinition step = _selectedScenario.Steps[i];
                        EditorGUILayout.BeginHorizontal();
                        if (step != null)
                        {
                            string typeName = step.GetType().Name;
                            EditorGUILayout.LabelField($"{i + 1}. {step.DisplayName} ({typeName})");
                            if (GUILayout.Button("Показать", EditorStyles.miniButton, GUILayout.Width(65f)))
                            {
                                EditorGUIUtility.PingObject(step);
                            }
                        }
                        else
                        {
                            EditorGUILayout.LabelField($"{i + 1}. [Пустая ссылка на шаг]", EditorStyles.miniLabel);
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                }
                EditorGUI.indentLevel--;
            }

            // Секция Режимов
            EditorGUILayout.Space(6f);
            _modesFoldout = EditorGUILayout.Foldout(_modesFoldout, $"Режимы прохождения ({modesCount})", true, EditorStyles.foldoutHeader);
            if (_modesFoldout)
            {
                EditorGUI.indentLevel++;
                if (modesCount == 0)
                {
                    EditorGUILayout.LabelField("Режимы не добавлены.", EditorStyles.miniLabel);
                }
                else
                {
                    for (int i = 0; i < modesCount; i++)
                    {
                        ScenarioMode mode = _selectedScenario.Modes[i];
                        EditorGUILayout.BeginHorizontal();
                        if (mode != null)
                        {
                            string modeName = !string.IsNullOrEmpty(mode.DisplayName) ? mode.DisplayName : mode.name;
                            string typeName = mode.GetType().Name;
                            int groupCount = mode.Groups != null ? mode.Groups.Count : 0;
                            EditorGUILayout.LabelField($"{i + 1}. {modeName} ({typeName}) — групп: {groupCount}");
                            if (GUILayout.Button("Показать", EditorStyles.miniButton, GUILayout.Width(65f)))
                            {
                                EditorGUIUtility.PingObject(mode);
                            }
                        }
                        else
                        {
                            EditorGUILayout.LabelField($"{i + 1}. [Пустая ссылка на режим]", EditorStyles.miniLabel);
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                }
                EditorGUI.indentLevel--;
            }

            // Секция интеграции со сценой (ScenarioRunner)
            ScenarioKitStyles.Section("Связь со сценой");
            ScenarioRunner runner = Object.FindAnyObjectByType<ScenarioRunner>();
            if (runner != null)
            {
                bool isCurrentlyAssigned = runner.Scenario == _selectedScenario;
                if (isCurrentlyAssigned)
                {
                    EditorGUILayout.HelpBox($"✓ Этот сценарий назначен в ScenarioRunner ('{runner.name}') текущей сцены.", MessageType.Info);
                }
                else
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"Текущий сценарий в сцене: {(runner.Scenario != null ? runner.Scenario.name : "(не назначен)")}");
                    GUI.backgroundColor = new Color(0.4f, 0.75f, 1f);
                    if (GUILayout.Button("Назначить в ScenarioRunner", EditorStyles.miniButton, GUILayout.Width(190f)))
                    {
                        AssignToSceneRunner(runner, _selectedScenario);
                    }
                    GUI.backgroundColor = prevColor;
                    EditorGUILayout.EndHorizontal();
                }
            }
            else
            {
                EditorGUILayout.LabelField("ScenarioRunner в активной сцене не найден.", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawFooter()
        {
            Color prevColor = GUI.backgroundColor;
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            string selectedText = _selectedScenario != null ? $"Выбран: {_selectedScenario.name}" : "Сценарий не выбран";
            GUILayout.Label(selectedText, EditorStyles.boldLabel);

            GUILayout.FlexibleSpace();

            GUI.enabled = _selectedScenario != null;
            GUI.backgroundColor = new Color(0.35f, 0.75f, 1.0f);
            if (GUILayout.Button("Выбрать и открыть", EditorStyles.toolbarButton, GUILayout.Width(150f)))
            {
                ConfirmSelection();
            }
            GUI.backgroundColor = prevColor;
            GUI.enabled = true;

            if (GUILayout.Button("Закрыть", EditorStyles.toolbarButton, GUILayout.Width(80f)))
            {
                Close();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void ConfirmSelection()
        {
            if (_selectedScenario != null)
            {
                Selection.activeObject = _selectedScenario;
                EditorGUIUtility.PingObject(_selectedScenario);
            }
            Close();
        }

        private static void AssignToSceneRunner(ScenarioRunner runner, ScenarioDefinition scenario)
        {
            if (runner == null || scenario == null) return;

            SerializedObject runnerSo = new SerializedObject(runner);
            SerializedProperty prop = runnerSo.FindProperty("_scenario");
            if (prop != null)
            {
                prop.objectReferenceValue = scenario;
                runnerSo.ApplyModifiedProperties();
                EditorUtility.SetDirty(runner);
                EditorSceneManager.MarkSceneDirty(runner.gameObject.scene);
            }
        }

        private static string GetSafeFileName(string name)
        {
            char[] invalidChars = Path.GetInvalidFileNameChars();
            foreach (char c in invalidChars)
            {
                name = name.Replace(c, '_');
            }
            return name;
        }

        private static string ToSnakeCase(string str)
        {
            if (string.IsNullOrEmpty(str)) return string.Empty;

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < str.Length; i++)
            {
                char c = str[i];
                if (char.IsUpper(c))
                {
                    if (i > 0 && str[i - 1] != '_')
                    {
                        sb.Append('_');
                    }
                    sb.Append(char.ToLowerInvariant(c));
                }
                else if (char.IsWhiteSpace(c) || c == '-')
                {
                    sb.Append('_');
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }
    }
}
