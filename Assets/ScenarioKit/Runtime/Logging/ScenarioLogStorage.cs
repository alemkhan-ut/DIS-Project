using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ScenarioKit.Logging
{
    public static class ScenarioLogStorage
    {
        private const string DEFAULT_FOLDER_NAME = "ScenarioLogs";
        private const string FILE_PATTERN = "*.json";

        public static string DefaultFolderPath => Path.Combine(Application.persistentDataPath, DEFAULT_FOLDER_NAME);

        public static string GetJson(ScenarioLog log)
        {
            return JsonUtility.ToJson(log, true);
        }

        public static void Save(ScenarioLog log, string path)
        {
            string directory = Path.GetDirectoryName(path);

            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, GetJson(log));
        }

        public static ScenarioLog Load(string path, List<string> warnings)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                warnings?.Add($"Файл журнала не найден: {path}");
                return null;
            }

            try
            {
                ScenarioLog log = JsonUtility.FromJson<ScenarioLog>(File.ReadAllText(path));

                if (log == null)
                {
                    warnings?.Add($"Не удалось прочитать журнал: {path}");
                }

                return log;
            }
            catch (Exception exception)
            {
                warnings?.Add($"Не удалось прочитать журнал: {path} ({exception.Message})");
                return null;
            }
        }

        public static string GetLatestPath(string folder)
        {
            IReadOnlyList<string> paths = GetLogPaths(folder);

            return paths.Count > 0 ? paths[0] : null;
        }

        public static IReadOnlyList<string> GetLogPaths(string folder)
        {
            List<string> paths = new List<string>();

            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                return paths;
            }

            paths.AddRange(Directory.GetFiles(folder, FILE_PATTERN));
            paths.Sort((first, second) => File.GetLastWriteTimeUtc(second).CompareTo(File.GetLastWriteTimeUtc(first)));

            return paths;
        }
    }
}
