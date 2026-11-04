using System.Collections.Generic;
using ScenarioKit.Events;
using ScenarioKit.Modes;
using ScenarioKit.Steps;
using UnityEngine;

namespace ScenarioKit.Validation
{
    public static class ScenarioValidator
    {
        public static List<ValidationMessage> GetMessages(ScenarioDefinition scenario)
        {
            List<ValidationMessage> messages = new List<ValidationMessage>();

            if (scenario == null)
            {
                return messages;
            }

            AddStepMessages(scenario.Steps, scenario, messages);

            if (scenario.Modes.Count == 0)
            {
                Add(messages, ValidationSeverity.Error, "В сценарии нет режимов — добавьте тренировку или экзамен", scenario);
            }

            foreach (ScenarioMode mode in scenario.Modes)
            {
                if (mode == null)
                {
                    Add(messages, ValidationSeverity.Warning, "В списке режимов есть пустая ячейка", scenario);
                    continue;
                }

                messages.AddRange(GetModeMessages(mode, scenario.Steps));
            }

            return messages;
        }

        public static List<ValidationMessage> GetModeMessages(ScenarioMode mode, IReadOnlyList<StepDefinition> library)
        {
            List<ValidationMessage> messages = new List<ValidationMessage>();

            if (mode == null)
            {
                return messages;
            }

            string modeName = GetModeName(mode);
            HashSet<StepDefinition> used = new HashSet<StepDefinition>();
            HashSet<StepDefinition> reportedDuplicates = new HashSet<StepDefinition>();
            HashSet<StepDefinition> reportedMissing = new HashSet<StepDefinition>();
            HashSet<StepDefinition> libraryLookup = library != null ? new HashSet<StepDefinition>(library) : null;

            foreach (StepGroup group in mode.Groups)
            {
                AddGroupMessages(group, mode, modeName, libraryLookup, used, reportedDuplicates, reportedMissing, messages);
            }

            if (library != null)
            {
                foreach (StepDefinition step in library)
                {
                    if (step != null && !used.Contains(step))
                    {
                        Add(messages, ValidationSeverity.Warning, $"Шаг «{step.Id}» не входит в режим «{modeName}»", mode);
                    }
                }
            }
            else
            {
                AddStepMessages(new List<StepDefinition>(used), mode, messages);
            }

            return messages;
        }

        private static void AddGroupMessages(
            StepGroup group,
            ScenarioMode mode,
            string modeName,
            HashSet<StepDefinition> libraryLookup,
            HashSet<StepDefinition> used,
            HashSet<StepDefinition> reportedDuplicates,
            HashSet<StepDefinition> reportedMissing,
            List<ValidationMessage> messages)
        {
            if (group == null)
            {
                Add(messages, ValidationSeverity.Warning, $"В режиме «{modeName}» есть пустая ячейка вместо группы", mode);
                return;
            }

            string groupName = string.IsNullOrEmpty(group.Name) ? "без названия" : group.Name;

            if (group.Stages.Count == 0)
            {
                Add(messages, ValidationSeverity.Warning, $"В режиме «{modeName}» группа «{groupName}» не содержит стадий", mode);
            }

            foreach (ActivationStage stage in group.Stages)
            {
                if (stage == null)
                {
                    Add(messages, ValidationSeverity.Warning, $"В группе «{groupName}» режима «{modeName}» есть пустая ячейка вместо стадии", mode);
                    continue;
                }

                if (stage.Steps.Count == 0)
                {
                    Add(messages, ValidationSeverity.Warning, $"В группе «{groupName}» режима «{modeName}» есть пустая стадия", mode);
                    continue;
                }

                foreach (StepDefinition step in stage.Steps)
                {
                    AddModeStepMessages(step, mode, modeName, groupName, libraryLookup, used, reportedDuplicates, reportedMissing, messages);
                }
            }
        }

        private static void AddModeStepMessages(
            StepDefinition step,
            ScenarioMode mode,
            string modeName,
            string groupName,
            HashSet<StepDefinition> libraryLookup,
            HashSet<StepDefinition> used,
            HashSet<StepDefinition> reportedDuplicates,
            HashSet<StepDefinition> reportedMissing,
            List<ValidationMessage> messages)
        {
            if (step == null)
            {
                Add(messages, ValidationSeverity.Warning, $"В группе «{groupName}» режима «{modeName}» есть пустая ячейка вместо шага", mode);
                return;
            }

            if (!used.Add(step) && reportedDuplicates.Add(step))
            {
                Add(messages, ValidationSeverity.Error, $"Шаг «{step.Id}» встречается в режиме «{modeName}» больше одного раза", step);
            }

            if (libraryLookup != null && !libraryLookup.Contains(step) && reportedMissing.Add(step))
            {
                Add(messages, ValidationSeverity.Error, $"Шаг «{step.Id}» из режима «{modeName}» не входит в библиотеку шагов сценария", step);
            }
        }

        private static void AddStepMessages(IReadOnlyList<StepDefinition> steps, Object owner, List<ValidationMessage> messages)
        {
            Dictionary<string, StepDefinition> byId = new Dictionary<string, StepDefinition>();
            HashSet<string> reportedIds = new HashSet<string>();
            Dictionary<ScenarioEventKey, StepDefinition> byEvent = new Dictionary<ScenarioEventKey, StepDefinition>();

            foreach (StepDefinition step in steps)
            {
                if (step == null)
                {
                    Add(messages, ValidationSeverity.Warning, "В списке шагов есть пустая ячейка", owner);
                    continue;
                }

                AddIdMessages(step, byId, reportedIds, messages);

                EventStep eventStep = step as EventStep;

                if (eventStep != null && eventStep.Sequence.Count == 0)
                {
                    Add(messages, ValidationSeverity.Error, $"У шага «{step.Id}» не задана последовательность событий", step);
                }

                AddSharedEventMessages(step, byEvent, messages);
            }
        }

        private static void AddIdMessages(
            StepDefinition step,
            Dictionary<string, StepDefinition> byId,
            HashSet<string> reportedIds,
            List<ValidationMessage> messages)
        {
            if (string.IsNullOrEmpty(step.Id))
            {
                Add(messages, ValidationSeverity.Error, "У шага не задан идентификатор", step);
                return;
            }

            StepDefinition other;

            if (!byId.TryGetValue(step.Id, out other))
            {
                byId[step.Id] = step;
            }
            else if (other != step && reportedIds.Add(step.Id))
            {
                Add(messages, ValidationSeverity.Error, $"Идентификатор шага «{step.Id}» повторяется — у каждого шага он должен быть уникальным", step);
            }
        }

        private static void AddSharedEventMessages(
            StepDefinition step,
            Dictionary<ScenarioEventKey, StepDefinition> byEvent,
            List<ValidationMessage> messages)
        {
            HashSet<ScenarioEventKey> ownKeys = new HashSet<ScenarioEventKey>();

            foreach (ScenarioEventKey key in step.SuccessEvents)
            {
                if (key == null || !ownKeys.Add(key))
                {
                    continue;
                }

                StepDefinition other;

                if (byEvent.TryGetValue(key, out other) && other != step)
                {
                    Add(messages, ValidationSeverity.Warning, $"Шаги «{other.Id}» и «{step.Id}» ждут одно и то же событие «{key.name}»", step);
                }
                else
                {
                    byEvent[key] = step;
                }
            }
        }

        private static string GetModeName(ScenarioMode mode)
        {
            return string.IsNullOrEmpty(mode.DisplayName) ? mode.name : mode.DisplayName;
        }

        private static void Add(List<ValidationMessage> messages, ValidationSeverity severity, string text, Object context)
        {
            messages.Add(new ValidationMessage(severity, text, context));
        }
    }
}
