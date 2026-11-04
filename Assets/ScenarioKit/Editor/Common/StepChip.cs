using ScenarioKit.Execution;
using ScenarioKit.Steps;
using UnityEditor;
using UnityEngine;

namespace ScenarioKit.EditorTools
{
    public static class StepChip
    {
        private const string EMPTY_LABEL = "(пусто)";
        private const string SEPARATOR = " — ";
        private const int DOUBLE_CLICK_COUNT = 2;
        private const float BORDER = 1f;
        private const float SELECTED_BORDER = 2f;

        public static void Draw(Rect rect, StepDefinition step, StepStatus? status, bool selected)
        {
            Event current = Event.current;

            if (current.type == EventType.Repaint)
            {
                StepStatus? shown = step == null ? StepStatus.Failed : status;
                EditorGUI.DrawRect(rect, ScenarioKitStyles.GetChipBackground(shown));
                Color borderColor = selected ? ScenarioKitStyles.GetSelectionColor() : ScenarioKitStyles.GetSeparatorColor();
                ScenarioKitStyles.DrawBorder(rect, borderColor, selected ? SELECTED_BORDER : BORDER);
            }

            string text = GetChipText(step);
            GUIStyle style = step == null ? ScenarioKitStyles.ChipErrorStyle : ScenarioKitStyles.ChipStyle;
            GUI.Label(rect, new GUIContent(text, text), style);

            bool isDoubleClick = current.type == EventType.MouseDown && current.clickCount == DOUBLE_CLICK_COUNT && rect.Contains(current.mousePosition);

            if (isDoubleClick && step != null)
            {
                Selection.activeObject = step;
                EditorGUIUtility.PingObject(step);
                current.Use();
            }
        }

        public static string GetChipText(StepDefinition step)
        {
            if (step == null)
            {
                return EMPTY_LABEL;
            }

            string instruction = string.IsNullOrEmpty(step.Instruction) ? string.Empty : step.Instruction.Replace('\n', ' ').Replace('\r', ' ');
            return string.IsNullOrEmpty(instruction) ? step.Id : step.Id + SEPARATOR + instruction;
        }
    }
}
