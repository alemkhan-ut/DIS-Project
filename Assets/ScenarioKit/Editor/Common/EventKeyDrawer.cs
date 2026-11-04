using ScenarioKit.Events;
using UnityEditor;

namespace ScenarioKit.EditorTools
{
    [CustomPropertyDrawer(typeof(ScenarioEventKey), true)]
    public class EventKeyDrawer : ScenarioAssetDrawer<ScenarioEventKey>
    {
        protected override string CreateFolder => ScenarioKitFolders.EVENTS;
    }
}
