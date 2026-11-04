using System.Collections.Generic;
using ScenarioKit.Events;
using ScenarioKit.Modes;
using ScenarioKit.Steps;
using UnityEditor;
using Object = UnityEngine.Object;

namespace ScenarioKit.EditorTools
{
    [InitializeOnLoad]
    public static class ScenarioUsageIndex
    {
        private static readonly Dictionary<ScenarioEventKey, List<StepDefinition>> _stepsByEvent = new Dictionary<ScenarioEventKey, List<StepDefinition>>();
        private static readonly Dictionary<ScenarioStateKey, List<StepDefinition>> _stepsByState = new Dictionary<ScenarioStateKey, List<StepDefinition>>();
        private static readonly Dictionary<StepDefinition, List<ModeUsage>> _modesByStep = new Dictionary<StepDefinition, List<ModeUsage>>();
        private static readonly Dictionary<ScenarioMode, List<ScenarioDefinition>> _scenariosByMode = new Dictionary<ScenarioMode, List<ScenarioDefinition>>();
        private static readonly StepDefinition[] _noSteps = new StepDefinition[0];
        private static readonly ModeUsage[] _noModes = new ModeUsage[0];
        private static readonly ScenarioDefinition[] _noScenarios = new ScenarioDefinition[0];

        private static bool _isBuilt;

        static ScenarioUsageIndex()
        {
            EditorApplication.projectChanged += ProjectChangedHandler;
            Undo.undoRedoPerformed += UndoRedoPerformedHandler;
        }

        public static void Invalidate()
        {
            _isBuilt = false;
        }

        public static IReadOnlyList<StepDefinition> GetStepsUsing(ScenarioEventKey key)
        {
            EnsureBuilt();
            return key != null && _stepsByEvent.TryGetValue(key, out List<StepDefinition> steps) ? steps : _noSteps;
        }

        public static IReadOnlyList<StepDefinition> GetStepsUsing(ScenarioStateKey key)
        {
            EnsureBuilt();
            return key != null && _stepsByState.TryGetValue(key, out List<StepDefinition> steps) ? steps : _noSteps;
        }

        public static IReadOnlyList<ModeUsage> GetModesUsing(StepDefinition step)
        {
            EnsureBuilt();
            return step != null && _modesByStep.TryGetValue(step, out List<ModeUsage> usages) ? usages : _noModes;
        }

        public static IReadOnlyList<ScenarioDefinition> GetScenariosUsing(ScenarioMode mode)
        {
            EnsureBuilt();
            return mode != null && _scenariosByMode.TryGetValue(mode, out List<ScenarioDefinition> scenarios) ? scenarios : _noScenarios;
        }

        private static void EnsureBuilt()
        {
            if (_isBuilt)
            {
                return;
            }

            _stepsByEvent.Clear();
            _stepsByState.Clear();
            _modesByStep.Clear();
            _scenariosByMode.Clear();

            foreach (StepDefinition step in LoadAll<StepDefinition>())
            {
                IndexStep(step);
            }

            foreach (ScenarioMode mode in LoadAll<ScenarioMode>())
            {
                IndexMode(mode);
            }

            foreach (ScenarioDefinition scenario in LoadAll<ScenarioDefinition>())
            {
                IndexScenario(scenario);
            }

            _isBuilt = true;
        }

        private static List<T> LoadAll<T>() where T : Object
        {
            string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
            List<T> result = new List<T>(guids.Length);

            foreach (string guid in guids)
            {
                T asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));

                if (asset != null)
                {
                    result.Add(asset);
                }
            }

            return result;
        }

        private static void IndexStep(StepDefinition step)
        {
            EventStep eventStep = step as EventStep;

            if (eventStep == null)
            {
                return;
            }

            foreach (ScenarioEventKey key in eventStep.Sequence)
            {
                AddUnique(_stepsByEvent, key, step);
            }

            foreach (ViolationRule rule in eventStep.Violations)
            {
                if (rule != null)
                {
                    AddUnique(_stepsByEvent, rule.Key, step);
                }
            }

            foreach (StateRequirement requirement in eventStep.RequiredStates)
            {
                if (requirement != null)
                {
                    AddUnique(_stepsByState, requirement.Key, step);
                }
            }
        }

        private static void IndexMode(ScenarioMode mode)
        {
            foreach (StepGroup group in mode.Groups)
            {
                if (group == null)
                {
                    continue;
                }

                for (int i = 0; i < group.Stages.Count; i++)
                {
                    ActivationStage stage = group.Stages[i];

                    if (stage == null)
                    {
                        continue;
                    }

                    foreach (StepDefinition step in stage.Steps)
                    {
                        AddItem(_modesByStep, step, new ModeUsage(mode, group.Name, i), false);
                    }
                }
            }
        }

        private static void IndexScenario(ScenarioDefinition scenario)
        {
            foreach (ScenarioMode mode in scenario.Modes)
            {
                AddUnique(_scenariosByMode, mode, scenario);
            }
        }

        private static void AddUnique<TKey, TValue>(Dictionary<TKey, List<TValue>> map, TKey key, TValue value) where TKey : Object
        {
            AddItem(map, key, value, true);
        }

        private static void AddItem<TKey, TValue>(Dictionary<TKey, List<TValue>> map, TKey key, TValue value, bool unique) where TKey : Object
        {
            if (key == null)
            {
                return;
            }

            if (!map.TryGetValue(key, out List<TValue> list))
            {
                list = new List<TValue>();
                map.Add(key, list);
            }

            if (!unique || !list.Contains(value))
            {
                list.Add(value);
            }
        }

        private static void ProjectChangedHandler()
        {
            Invalidate();
        }

        private static void UndoRedoPerformedHandler()
        {
            Invalidate();
        }
    }
}
