using ScenarioKit.Steps;
using UnityEditor;

namespace ScenarioKit.EditorTools
{
    [CustomPropertyDrawer(typeof(StepDefinition), true)]
    public class StepDefinitionDrawer : ScenarioAssetDrawer<StepDefinition>
    {
        protected override string CreateFolder => ScenarioKitFolders.STEPS;
    }
}
