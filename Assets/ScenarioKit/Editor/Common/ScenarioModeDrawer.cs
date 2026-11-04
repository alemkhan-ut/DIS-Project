using ScenarioKit.Modes;
using UnityEditor;

namespace ScenarioKit.EditorTools
{
    [CustomPropertyDrawer(typeof(ScenarioMode), true)]
    public class ScenarioModeDrawer : ScenarioAssetDrawer<ScenarioMode>
    {
        protected override string CreateFolder => ScenarioKitFolders.MODES;
    }
}
