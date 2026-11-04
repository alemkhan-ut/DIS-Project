using ScenarioKit.Analysis;
using UnityEditor;

namespace ScenarioKit.EditorTools
{
    [CustomPropertyDrawer(typeof(ScenarioAnalyzer), true)]
    public class AnalyzerDrawer : ScenarioAssetDrawer<ScenarioAnalyzer>
    {
        protected override string CreateFolder => ScenarioKitFolders.ANALYZERS;
    }
}
