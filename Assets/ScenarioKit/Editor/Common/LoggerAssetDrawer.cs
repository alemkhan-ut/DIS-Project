using ScenarioKit.Logging;
using UnityEditor;

namespace ScenarioKit.EditorTools
{
    [CustomPropertyDrawer(typeof(ScenarioLoggerAsset), true)]
    public class LoggerAssetDrawer : ScenarioAssetDrawer<ScenarioLoggerAsset>
    {
        protected override string CreateFolder => ScenarioKitFolders.LOGGERS;
    }
}
