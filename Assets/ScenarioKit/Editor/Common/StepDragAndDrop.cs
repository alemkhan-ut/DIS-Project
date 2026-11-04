using System.Collections.Generic;
using ScenarioKit.Steps;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ScenarioKit.EditorTools
{
    public static class StepDragAndDrop
    {
        public const string DRAG_DATA_KEY = "ScenarioKit.StepDragData";

        private const float HIGHLIGHT_ALPHA = 0.25f;
        private const float HIGHLIGHT_BORDER = 2f;

        private static bool _hasHighlight;
        private static Rect _highlightZone;

        public static void BeginDrag(StepDefinition step, object sourceList, int index)
        {
            if (step == null)
            {
                return;
            }

            DragAndDrop.PrepareStartDrag();
            DragAndDrop.objectReferences = new Object[] { step };
            DragAndDrop.SetGenericData(DRAG_DATA_KEY, new StepDragData(step, sourceList, index));
            DragAndDrop.StartDrag(step.Id);
        }

        public static bool TryAcceptDrop(Rect zone, out List<StepDefinition> steps, out StepDragData source)
        {
            steps = null;
            source = null;
            Event current = Event.current;

            if (current.type == EventType.Repaint)
            {
                DrawHighlight(zone);
                return false;
            }

            if (current.type == EventType.DragExited)
            {
                _hasHighlight = false;
                return false;
            }

            if (current.type != EventType.DragUpdated && current.type != EventType.DragPerform)
            {
                return false;
            }

            if (!zone.Contains(current.mousePosition))
            {
                ClearHighlight(zone);
                return false;
            }

            StepDragData data = DragAndDrop.GetGenericData(DRAG_DATA_KEY) as StepDragData;
            List<StepDefinition> dropped = GetDroppedSteps(data);

            if (dropped.Count == 0)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                return false;
            }

            if (current.type == EventType.DragUpdated)
            {
                DragAndDrop.visualMode = data != null ? DragAndDropVisualMode.Move : DragAndDropVisualMode.Link;
                SetHighlight(zone);
                current.Use();
                return false;
            }

            DragAndDrop.AcceptDrag();
            _hasHighlight = false;
            current.Use();
            steps = dropped;
            source = data;
            return true;
        }

        private static List<StepDefinition> GetDroppedSteps(StepDragData data)
        {
            List<StepDefinition> result = new List<StepDefinition>();

            if (data != null && data.Step != null)
            {
                result.Add(data.Step);
                return result;
            }

            foreach (Object reference in DragAndDrop.objectReferences)
            {
                StepDefinition step = reference as StepDefinition;

                if (step != null)
                {
                    result.Add(step);
                }
            }

            return result;
        }

        private static void SetHighlight(Rect zone)
        {
            if (_hasHighlight && _highlightZone == zone)
            {
                return;
            }

            _hasHighlight = true;
            _highlightZone = zone;
            InternalEditorUtility.RepaintAllViews();
        }

        private static void ClearHighlight(Rect zone)
        {
            if (_hasHighlight && _highlightZone == zone)
            {
                _hasHighlight = false;
                InternalEditorUtility.RepaintAllViews();
            }
        }

        private static void DrawHighlight(Rect zone)
        {
            if (!_hasHighlight || _highlightZone != zone)
            {
                return;
            }

            Color color = ScenarioKitStyles.GetSelectionColor();
            ScenarioKitStyles.DrawBorder(zone, color, HIGHLIGHT_BORDER);
            color.a = HIGHLIGHT_ALPHA;
            EditorGUI.DrawRect(zone, color);
        }
    }
}
