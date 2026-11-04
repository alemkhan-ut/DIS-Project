using System;
using System.IO;
using UnityEngine;

namespace ScenarioKit.Logging
{
    [CreateAssetMenu(menuName = "ScenarioKit/Логгер: JSON-файл")]
    public class JsonFileLoggerAsset : ScenarioLoggerAsset
    {
        private const string FILE_NAME_FORMAT = "{0}_{1}_{2}.json";
        private const string TIMESTAMP_FORMAT = "yyyyMMdd_HHmmss";
        private const char INVALID_CHAR_REPLACEMENT = '_';

        [SerializeField]
        [Tooltip("Папка внутри постоянного хранилища приложения, куда пишутся файлы журнала.")]
        private string _folderName = "ScenarioLogs";

        public string FolderName => _folderName;

        public override IScenarioLogger Create(ScenarioLog header)
        {
            string fileName = string.Format(
                FILE_NAME_FORMAT,
                GetSafeName(header.ScenarioId),
                GetSafeName(header.ModeName),
                DateTime.Now.ToString(TIMESTAMP_FORMAT));
            string path = Path.Combine(Application.persistentDataPath, _folderName, fileName);

            return new JsonFileScenarioLogger(header, path);
        }

        private static string GetSafeName(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            char[] invalid = Path.GetInvalidFileNameChars();
            char[] chars = value.ToCharArray();

            for (int i = 0; i < chars.Length; i++)
            {
                if (Array.IndexOf(invalid, chars[i]) >= 0)
                {
                    chars[i] = INVALID_CHAR_REPLACEMENT;
                }
            }

            return new string(chars);
        }
    }
}
