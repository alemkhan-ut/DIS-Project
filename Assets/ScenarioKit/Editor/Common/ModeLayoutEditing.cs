using System;
using System.Collections.Generic;
using ScenarioKit.Steps;
using UnityEditor;

namespace ScenarioKit.EditorTools
{
    /// <summary>
    /// Структурные правки режима (группы → стадии → шаги) через SerializedObject.
    /// Каждая публичная операция — один шаг Undo. Шаг перед вставкой удаляется из всех стадий режима,
    /// поэтому дубль шага в режиме невозможен.
    /// </summary>
    public static class ModeLayoutEditing
    {
        public const string GROUPS_FIELD = "_groups";
        public const string GROUP_FIELD = "_group";
        public const string NAME_FIELD = "_name";
        public const string STAGES_FIELD = "_stages";
        public const string STEPS_FIELD = "_steps";

        private const string GROUP_NAME_PREFIX = "Группа ";

        public static bool HasGroupList(SerializedObject mode)
        {
            SerializedProperty groups = mode.FindProperty(GROUPS_FIELD);
            return groups != null && groups.isArray;
        }

        public static bool HasSupportedLayout(SerializedObject mode)
        {
            return HasGroupList(mode) || mode.FindProperty(GROUP_FIELD) != null;
        }

        public static int GetGroupCount(SerializedObject mode)
        {
            if (HasGroupList(mode))
            {
                return mode.FindProperty(GROUPS_FIELD).arraySize;
            }

            return mode.FindProperty(GROUP_FIELD) != null ? 1 : 0;
        }

        public static SerializedProperty GetGroup(SerializedObject mode, int groupIndex)
        {
            if (HasGroupList(mode))
            {
                return mode.FindProperty(GROUPS_FIELD).GetArrayElementAtIndex(groupIndex);
            }

            return mode.FindProperty(GROUP_FIELD);
        }

        public static SerializedProperty GetStages(SerializedObject mode, int groupIndex)
        {
            return GetGroup(mode, groupIndex).FindPropertyRelative(STAGES_FIELD);
        }

        public static SerializedProperty GetStageSteps(SerializedProperty stages, int stageIndex)
        {
            return stages.GetArrayElementAtIndex(stageIndex).FindPropertyRelative(STEPS_FIELD);
        }

        public static List<StepDefinition> GetSteps(SerializedProperty steps)
        {
            List<StepDefinition> result = new List<StepDefinition>(steps.arraySize);

            for (int i = 0; i < steps.arraySize; i++)
            {
                result.Add(steps.GetArrayElementAtIndex(i).objectReferenceValue as StepDefinition);
            }

            return result;
        }

        public static void MoveStep(SerializedObject mode, StepDefinition step, int groupIndex, int stageIndex, int index)
        {
            MoveSteps(mode, new[] { step }, groupIndex, stageIndex, index);
        }

        public static void MoveSteps(SerializedObject mode, IReadOnlyList<StepDefinition> steps, int groupIndex, int stageIndex, int index)
        {
            Perform(mode, "Перенос шага", () =>
            {
                int target = stageIndex;
                int position = index;

                foreach (StepDefinition step in steps)
                {
                    InsertIntoStage(mode, step, groupIndex, target, position, out target, out position);
                    position++;
                }
            });
        }

        public static void MoveStepsToNewStage(SerializedObject mode, IReadOnlyList<StepDefinition> steps, int groupIndex, int stageIndex)
        {
            Perform(mode, "Новая стадия", () =>
            {
                int target = stageIndex;

                foreach (StepDefinition step in steps)
                {
                    DetachStep(mode, step, groupIndex, ref target, -1, out _);
                }

                SerializedProperty stages = GetStages(mode, groupIndex);
                target = Math.Max(0, Math.Min(target, stages.arraySize));
                SerializedProperty stageSteps = InsertEmptyStage(stages, target);

                foreach (StepDefinition step in steps)
                {
                    if (step != null)
                    {
                        AddStep(stageSteps, step);
                    }
                }
            });
        }

        public static void RemoveStepAt(SerializedObject mode, int groupIndex, int stageIndex, int index)
        {
            Perform(mode, "Удаление шага из стадии", () =>
            {
                SerializedProperty steps = GetStageSteps(GetStages(mode, groupIndex), stageIndex);
                steps.DeleteArrayElementAtIndex(index);
            });
        }

        public static void MoveStage(SerializedObject mode, int groupIndex, int from, int to)
        {
            Perform(mode, "Перестановка стадии", () => GetStages(mode, groupIndex).MoveArrayElement(from, to));
        }

        public static void RemoveStage(SerializedObject mode, int groupIndex, int stageIndex)
        {
            Perform(mode, "Удаление стадии", () => GetStages(mode, groupIndex).DeleteArrayElementAtIndex(stageIndex));
        }

        public static void SplitStage(SerializedObject mode, int groupIndex, int stageIndex)
        {
            Perform(mode, "Разбить стадию", () =>
            {
                SerializedProperty stages = GetStages(mode, groupIndex);
                List<StepDefinition> steps = GetSteps(GetStageSteps(stages, stageIndex));

                if (steps.Count < 2)
                {
                    return;
                }

                SetSteps(GetStageSteps(stages, stageIndex), steps.GetRange(0, 1));

                for (int i = 1; i < steps.Count; i++)
                {
                    SerializedProperty stageSteps = InsertEmptyStage(stages, stageIndex + i);
                    AddStep(stageSteps, steps[i]);
                }
            });
        }

        public static void MergeWithNext(SerializedObject mode, int groupIndex, int stageIndex)
        {
            Perform(mode, "Объединить стадии", () =>
            {
                SerializedProperty stages = GetStages(mode, groupIndex);

                if (stageIndex + 1 >= stages.arraySize)
                {
                    return;
                }

                SerializedProperty stageSteps = GetStageSteps(stages, stageIndex);

                foreach (StepDefinition step in GetSteps(GetStageSteps(stages, stageIndex + 1)))
                {
                    AddStep(stageSteps, step);
                }

                stages.DeleteArrayElementAtIndex(stageIndex + 1);
            });
        }

        public static void SetAllParallel(SerializedObject mode, int groupIndex)
        {
            Perform(mode, "Все шаги одновременно", () =>
            {
                SerializedProperty stages = GetStages(mode, groupIndex);
                List<StepDefinition> steps = GetGroupSteps(stages);

                if (steps.Count == 0)
                {
                    return;
                }

                stages.arraySize = 1;
                SetSteps(GetStageSteps(stages, 0), steps);
            });
        }

        public static void SetAllSequential(SerializedObject mode, int groupIndex)
        {
            Perform(mode, "Все шаги по очереди", () =>
            {
                SerializedProperty stages = GetStages(mode, groupIndex);
                List<StepDefinition> steps = GetGroupSteps(stages);
                stages.arraySize = steps.Count;

                for (int i = 0; i < steps.Count; i++)
                {
                    SetSteps(GetStageSteps(stages, i), steps.GetRange(i, 1));
                }
            });
        }

        public static void RemoveEmptyStages(SerializedObject mode, int groupIndex)
        {
            Perform(mode, "Удалить пустые стадии", () =>
            {
                SerializedProperty stages = GetStages(mode, groupIndex);

                for (int i = stages.arraySize - 1; i >= 0; i--)
                {
                    if (GetStageSteps(stages, i).arraySize == 0)
                    {
                        stages.DeleteArrayElementAtIndex(i);
                    }
                }
            });
        }

        public static void AddGroup(SerializedObject mode)
        {
            Perform(mode, "Новая группа", () =>
            {
                SerializedProperty groups = mode.FindProperty(GROUPS_FIELD);
                int index = groups.arraySize;
                groups.arraySize++;
                SerializedProperty group = groups.GetArrayElementAtIndex(index);
                group.FindPropertyRelative(NAME_FIELD).stringValue = GROUP_NAME_PREFIX + (index + 1);
                group.FindPropertyRelative(STAGES_FIELD).arraySize = 0;
            });
        }

        public static void RemoveGroup(SerializedObject mode, int groupIndex)
        {
            Perform(mode, "Удаление группы", () => mode.FindProperty(GROUPS_FIELD).DeleteArrayElementAtIndex(groupIndex));
        }

        private static void Perform(SerializedObject mode, string undoName, Action edit)
        {
            mode.ApplyModifiedProperties();
            mode.Update();
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            edit();
            mode.ApplyModifiedProperties();
            Undo.SetCurrentGroupName(undoName);
            Undo.CollapseUndoOperations(undoGroup);
            ScenarioUsageIndex.Invalidate();
        }

        private static void InsertIntoStage(
            SerializedObject mode,
            StepDefinition step,
            int groupIndex,
            int stageIndex,
            int index,
            out int finalStage,
            out int finalIndex)
        {
            finalStage = stageIndex;
            finalIndex = index;

            if (step == null)
            {
                return;
            }

            DetachStep(mode, step, groupIndex, ref finalStage, index, out finalIndex);
            SerializedProperty stages = GetStages(mode, groupIndex);

            if (finalStage < 0 || finalStage >= stages.arraySize)
            {
                return;
            }

            SerializedProperty steps = GetStageSteps(stages, finalStage);
            finalIndex = Math.Max(0, Math.Min(finalIndex, steps.arraySize));
            InsertStep(steps, finalIndex, step);
        }

        /// <summary>
        /// Удаляет шаг из всех стадий режима. Стадии, ставшие пустыми из-за переноса, удаляются
        /// (кроме целевой). Индексы целевой стадии и позиции корректируются.
        /// targetIndex &lt; 0 — целевая стадия ещё не существует (targetStage — позиция вставки новой стадии).
        /// </summary>
        private static void DetachStep(SerializedObject mode, StepDefinition step, int targetGroup, ref int targetStage, int targetIndex, out int adjustedIndex)
        {
            adjustedIndex = targetIndex;
            bool targetExists = targetIndex >= 0;
            int groupCount = GetGroupCount(mode);

            for (int g = 0; g < groupCount; g++)
            {
                SerializedProperty stages = GetStages(mode, g);

                for (int s = stages.arraySize - 1; s >= 0; s--)
                {
                    SerializedProperty steps = GetStageSteps(stages, s);
                    bool isTarget = targetExists && g == targetGroup && s == targetStage;
                    bool removed = false;

                    for (int i = steps.arraySize - 1; i >= 0; i--)
                    {
                        if (steps.GetArrayElementAtIndex(i).objectReferenceValue != step)
                        {
                            continue;
                        }

                        steps.DeleteArrayElementAtIndex(i);
                        removed = true;

                        if (isTarget && i < adjustedIndex)
                        {
                            adjustedIndex--;
                        }
                    }

                    if (removed && !isTarget && steps.arraySize == 0)
                    {
                        stages.DeleteArrayElementAtIndex(s);

                        if (g == targetGroup && s < targetStage)
                        {
                            targetStage--;
                        }
                    }
                }
            }
        }

        private static List<StepDefinition> GetGroupSteps(SerializedProperty stages)
        {
            List<StepDefinition> steps = new List<StepDefinition>();

            for (int i = 0; i < stages.arraySize; i++)
            {
                steps.AddRange(GetSteps(GetStageSteps(stages, i)));
            }

            return steps;
        }

        private static SerializedProperty InsertEmptyStage(SerializedProperty stages, int index)
        {
            stages.InsertArrayElementAtIndex(index);
            SerializedProperty steps = GetStageSteps(stages, index);
            steps.arraySize = 0;
            return steps;
        }

        private static void SetSteps(SerializedProperty steps, List<StepDefinition> values)
        {
            steps.arraySize = values.Count;

            for (int i = 0; i < values.Count; i++)
            {
                steps.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }

        private static void AddStep(SerializedProperty steps, StepDefinition step)
        {
            InsertStep(steps, steps.arraySize, step);
        }

        private static void InsertStep(SerializedProperty steps, int index, StepDefinition step)
        {
            if (index >= steps.arraySize)
            {
                steps.arraySize++;
                index = steps.arraySize - 1;
            }
            else
            {
                steps.InsertArrayElementAtIndex(index);
            }

            steps.GetArrayElementAtIndex(index).objectReferenceValue = step;
        }
    }
}
