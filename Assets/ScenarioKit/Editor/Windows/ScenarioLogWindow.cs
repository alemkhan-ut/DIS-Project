using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ScenarioKit.Analysis;
using ScenarioKit.Logging;
using UnityEditor;
using UnityEngine;

namespace ScenarioKit.EditorTools
{
    public class ScenarioLogWindow : EditorWindow
    {
        private const string ALL_TYPES_LABEL = "Все типы";
        private const string NO_ANALYZER_LABEL = "— не выбран —";
        private const float LIST_WIDTH = 280f;

        private readonly Dictionary<string, LogEntry> _entries = new Dictionary<string, LogEntry>();
        private readonly List<string> _paths = new List<string>();
        private readonly List<ScenarioAnalyzer> _analyzers = new List<ScenarioAnalyzer>();
        private readonly List<string> _types = new List<string>();

        private string _selectedPath;
        private string _pendingPath;
        private bool _hasPendingSelection;
        private bool _pendingRefresh;
        private bool _pendingEvaluate;
        private int _analyzerIndex = -1;
        private int _typeIndex;
        private string _resultText = string.Empty;
        private string _logText = string.Empty;
        private Vector2 _listScroll;
        private Vector2 _logScroll;
        private Vector2 _resultScroll;

        [MenuItem("Tools/ScenarioKit/Журналы прохождений")]
        public static void Open()
        {
            ScenarioLogWindow window = GetWindow<ScenarioLogWindow>();
            window.titleContent = new GUIContent("Журналы прохождений");
            window.minSize = new Vector2(700f, 400f);
            window.Show();
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void OnGUI()
        {
            if (Event.current.type == EventType.Layout)
            {
                ApplyPendingChanges();
            }

            DrawToolbar();

            EditorGUILayout.BeginHorizontal();
            DrawList();
            DrawDetails();
            EditorGUILayout.EndHorizontal();
        }

        private void ApplyPendingChanges()
        {
            if (_pendingRefresh)
            {
                _pendingRefresh = false;
                Refresh();
            }

            if (_hasPendingSelection)
            {
                _hasPendingSelection = false;
                Select(_pendingPath);
            }

            if (_pendingEvaluate)
            {
                _pendingEvaluate = false;
                Evaluate();
            }
        }

        private void Refresh()
        {
            _entries.Clear();
            _paths.Clear();
            _paths.AddRange(ScenarioLogStorage.GetLogPaths(ScenarioLogStorage.DefaultFolderPath));

            _analyzers.Clear();
            string[] guids = AssetDatabase.FindAssets($"t:{nameof(ScenarioAnalyzer)}");

            foreach (string guid in guids)
            {
                ScenarioAnalyzer analyzer = AssetDatabase.LoadAssetAtPath<ScenarioAnalyzer>(AssetDatabase.GUIDToAssetPath(guid));

                if (analyzer != null)
                {
                    _analyzers.Add(analyzer);
                }
            }

            if (_analyzerIndex >= _analyzers.Count)
            {
                _analyzerIndex = -1;
            }

            if (_analyzerIndex < 0 && _analyzers.Count > 0)
            {
                _analyzerIndex = 0;
            }

            if (!_paths.Contains(_selectedPath))
            {
                _selectedPath = null;
            }

            Evaluate();
        }

        private LogEntry GetEntry(string path)
        {
            LogEntry entry;

            if (_entries.TryGetValue(path, out entry))
            {
                return entry;
            }

            entry = new LogEntry();
            ScenarioLog log = ScenarioLogStorage.Load(path, entry.Warnings);
            entry.Log = log;
            DateTime time = File.GetLastWriteTime(path);

            if (log != null && !string.IsNullOrEmpty(log.StartedUtc))
            {
                DateTime parsed;

                if (DateTime.TryParse(log.StartedUtc, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out parsed))
                {
                    time = parsed.ToLocalTime();
                }
            }

            string title = log == null
                ? "⚠ не удалось прочитать"
                : $"{log.ModeName} · {log.ScenarioId}";
            entry.Label = $"{time:dd.MM.yyyy HH:mm:ss}\n{title}\n{Path.GetFileName(path)}";
            _entries[path] = entry;

            return entry;
        }

        private void Select(string path)
        {
            _selectedPath = path;
            _typeIndex = 0;
            Evaluate();
        }

        private void Evaluate()
        {
            _resultText = string.Empty;
            _logText = string.Empty;
            _types.Clear();
            _types.Add(ALL_TYPES_LABEL);

            if (string.IsNullOrEmpty(_selectedPath))
            {
                return;
            }

            LogEntry entry = GetEntry(_selectedPath);

            if (entry.Log == null)
            {
                _resultText = string.Join("\n", entry.Warnings);
                return;
            }

            foreach (LogRecord record in entry.Log.Records)
            {
                if (record != null && !string.IsNullOrEmpty(record.Type) && !_types.Contains(record.Type))
                {
                    _types.Add(record.Type);
                }
            }

            if (_typeIndex >= _types.Count)
            {
                _typeIndex = 0;
            }

            if (_analyzerIndex >= 0 && _analyzerIndex < _analyzers.Count && _analyzers[_analyzerIndex] != null)
            {
                try
                {
                    _resultText = ReportFormatter.GetResultText(_analyzers[_analyzerIndex].Analyze(entry.Log));
                }
                catch (Exception exception)
                {
                    _resultText = $"⚠ Ошибка оценки: {exception.Message}";
                }
            }
            else
            {
                _resultText = "Выберите анализатор, чтобы оценить журнал.";
            }

            UpdateLogText(entry.Log);
        }

        private void UpdateLogText(ScenarioLog log)
        {
            string filter = _typeIndex > 0 ? _types[_typeIndex] : null;
            List<string> lines = new List<string>();

            foreach (LogRecord record in log.Records)
            {
                if (record != null && (filter == null || record.Type == filter))
                {
                    lines.Add(ReportFormatter.GetRecordText(record));
                }
            }

            _logText = string.Join("\n", lines);
        }

        private void OpenFolder()
        {
            string folder = ScenarioLogStorage.DefaultFolderPath;
            Directory.CreateDirectory(folder);
            EditorUtility.RevealInFinder(folder);
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("Обновить", EditorStyles.toolbarButton, GUILayout.Width(80f)))
            {
                _pendingRefresh = true;
                Repaint();
            }

            if (GUILayout.Button("Открыть папку", EditorStyles.toolbarButton, GUILayout.Width(100f)))
            {
                OpenFolder();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(LIST_WIDTH));
            _listScroll = EditorGUILayout.BeginScrollView(_listScroll);

            if (_paths.Count == 0)
            {
                EditorGUILayout.HelpBox("Журналов пока нет. Пройдите сценарий в Play Mode.", MessageType.Info);
            }

            foreach (string path in _paths)
            {
                LogEntry entry = GetEntry(path);
                bool selected = path == _selectedPath;
                Color previous = GUI.backgroundColor;

                if (selected)
                {
                    GUI.backgroundColor = ScenarioKitStyles.GetSelectionColor();
                }
                else if (entry.Log == null)
                {
                    GUI.backgroundColor = ScenarioKitStyles.GetErrorColor();
                }

                if (GUILayout.Button(entry.Label, GUILayout.Height(52f)) && !selected)
                {
                    _pendingPath = path;
                    _hasPendingSelection = true;
                    Repaint();
                }

                GUI.backgroundColor = previous;
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawDetails()
        {
            EditorGUILayout.BeginVertical();

            if (string.IsNullOrEmpty(_selectedPath))
            {
                EditorGUILayout.HelpBox("Выберите журнал слева.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            LogEntry entry = GetEntry(_selectedPath);

            if (entry.Log == null)
            {
                EditorGUILayout.HelpBox(string.Join("\n", entry.Warnings), MessageType.Warning);
                EditorGUILayout.EndVertical();
                return;
            }

            DrawAnalyzerRow();

            float width = position.width - LIST_WIDTH - 30f;

            _resultScroll = EditorGUILayout.BeginScrollView(_resultScroll, GUILayout.MaxHeight(160f));
            EditorGUILayout.SelectableLabel(_resultText, EditorStyles.textArea, GUILayout.MinHeight(EditorStyles.textArea.CalcHeight(new GUIContent(_resultText), width)));
            EditorGUILayout.EndScrollView();

            DrawLogFilter(entry.Log);

            _logScroll = EditorGUILayout.BeginScrollView(_logScroll);
            EditorGUILayout.SelectableLabel(_logText, EditorStyles.textArea, GUILayout.MinHeight(EditorStyles.textArea.CalcHeight(new GUIContent(_logText), width)));
            EditorGUILayout.EndScrollView();

            EditorGUILayout.EndVertical();
        }

        private void DrawAnalyzerRow()
        {
            string[] names = new string[_analyzers.Count + 1];
            names[0] = NO_ANALYZER_LABEL;

            for (int i = 0; i < _analyzers.Count; i++)
            {
                names[i + 1] = _analyzers[i] == null ? "—" : _analyzers[i].DisplayName;
            }

            EditorGUILayout.BeginHorizontal();
            int selected = EditorGUILayout.Popup("Анализатор", _analyzerIndex + 1, names) - 1;

            if (selected != _analyzerIndex)
            {
                _analyzerIndex = selected;
                _pendingEvaluate = true;
                Repaint();
            }

            if (GUILayout.Button("Оценить", GUILayout.Width(80f)))
            {
                _pendingEvaluate = true;
                Repaint();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawLogFilter(ScenarioLog log)
        {
            int selected = EditorGUILayout.Popup("Тип записи", _typeIndex, _types.ToArray());

            if (selected != _typeIndex)
            {
                _typeIndex = selected;
                UpdateLogText(log);
            }
        }
    }
}
