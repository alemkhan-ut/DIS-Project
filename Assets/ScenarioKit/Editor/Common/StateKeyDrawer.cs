using ScenarioKit.Events;
using UnityEditor;

namespace ScenarioKit.EditorTools
{
    [CustomPropertyDrawer(typeof(ScenarioStateKey), true)]
    public class StateKeyDrawer : ScenarioAssetDrawer<ScenarioStateKey>
    {
        protected override string CreateFolder => ScenarioKitFolders.STATES;
    }
}
