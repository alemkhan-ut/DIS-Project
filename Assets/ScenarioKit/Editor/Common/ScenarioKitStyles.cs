using ScenarioKit.Execution;
using UnityEditor;
using UnityEngine;

namespace ScenarioKit.EditorTools
{
    public static class ScenarioKitStyles
    {
        private const float SECTION_SPACE = 6f;
        private const float CHIP_ALPHA = 0.35f;

        private static GUIStyle _chipStyle;
        private static GUIStyle _chipErrorStyle;

        public static GUIStyle ChipStyle
        {
            get
            {
                if (_chipStyle == null)
                {
                    _chipStyle = CreateChipStyle();
                }

                return _chipStyle;
            }
        }

        public static GUIStyle ChipErrorStyle
        {
            get
            {
                if (_chipErrorStyle == null)
                {
                    _chipErrorStyle = CreateChipStyle();
                    _chipErrorStyle.normal.textColor = GetErrorColor();
                }

                return _chipErrorStyle;
            }
        }

        public static Color GetStatusColor(StepStatus status)
        {
            bool dark = EditorGUIUtility.isProSkin;

            switch (status)
            {
                case StepStatus.Active:
                    return dark ? new Color(0.30f, 0.60f, 1.00f) : new Color(0.10f, 0.40f, 0.85f);
                case StepStatus.Completed:
                    return dark ? new Color(0.35f, 0.78f, 0.45f) : new Color(0.15f, 0.60f, 0.25f);
                case StepStatus.Failed:
                    return GetErrorColor();
                case StepStatus.Skipped:
                    return dark ? new Color(0.95f, 0.80f, 0.25f) : new Color(0.80f, 0.60f, 0.05f);
                default:
                    return dark ? new Color(0.60f, 0.60f, 0.60f) : new Color(0.50f, 0.50f, 0.50f);
            }
        }

        public static Color GetErrorColor()
        {
            return EditorGUIUtility.isProSkin ? new Color(1.00f, 0.38f, 0.38f) : new Color(0.80f, 0.15f, 0.15f);
        }

        public static Color GetChipBackground(StepStatus? status)
        {
            Color color = GetStatusColor(status ?? StepStatus.Pending);
            color.a = CHIP_ALPHA;
            return color;
        }

        public static Color GetSelectionColor()
        {
            return EditorGUIUtility.isProSkin ? new Color(0.45f, 0.70f, 1.00f) : new Color(0.10f, 0.35f, 0.80f);
        }

        public static Color GetSeparatorColor()
        {
            return EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.12f) : new Color(0f, 0f, 0f, 0.20f);
        }

        public static void Section(string title)
        {
            EditorGUILayout.Space(SECTION_SPACE);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            Rect line = GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(line, GetSeparatorColor());
        }

        public static void DrawBorder(Rect rect, Color color, float thickness)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        private static GUIStyle CreateChipStyle()
        {
            GUIStyle style = new GUIStyle(EditorStyles.label);
            style.clipping = TextClipping.Clip;
            style.alignment = TextAnchor.MiddleLeft;
            style.wordWrap = false;
            style.padding = new RectOffset(6, 6, 2, 2);
            return style;
        }
    }
}
