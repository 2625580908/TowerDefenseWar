using MRStrategyRTS.RTS;
using UnityEditor;
using UnityEngine;

namespace MRStrategyRTS.Editor
{
    [CustomEditor(typeof(DefenseTower))]
    public sealed class DefenseTowerEditor : UnityEditor.Editor
    {
        private SerializedProperty showAttackRangeProperty;
        private SerializedProperty rangeCircleSegmentsProperty;
        private SerializedProperty rangeLineWidthProperty;
        private SerializedProperty rangeCircleHeightOffsetProperty;
        private SerializedProperty rangeCircleColorProperty;

        private void OnEnable()
        {
            showAttackRangeProperty = serializedObject.FindProperty("showAttackRange");
            rangeCircleSegmentsProperty = serializedObject.FindProperty("rangeCircleSegments");
            rangeLineWidthProperty = serializedObject.FindProperty("rangeLineWidth");
            rangeCircleHeightOffsetProperty = serializedObject.FindProperty("rangeCircleHeightOffset");
            rangeCircleColorProperty = serializedObject.FindProperty("rangeCircleColor");
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            if (GUI.changed)
            {
                SceneView.RepaintAll();
            }
        }

        private void OnSceneGUI()
        {
            serializedObject.Update();
            if (!showAttackRangeProperty.boolValue)
            {
                return;
            }

            var tower = (DefenseTower)target;
            var segments = Mathf.Max(12, rangeCircleSegmentsProperty.intValue);
            var points = new Vector3[segments + 1];
            var center = tower.transform.position +
                Vector3.up * rangeCircleHeightOffsetProperty.floatValue;
            for (var index = 0; index <= segments; index++)
            {
                var angle = index * Mathf.PI * 2f / segments;
                points[index] = center + new Vector3(
                    Mathf.Cos(angle),
                    0f,
                    Mathf.Sin(angle)) * tower.AttackRange;
            }

            Handles.color = rangeCircleColorProperty.colorValue;
            Handles.DrawAAPolyLine(rangeLineWidthProperty.floatValue, points);
        }
    }
}
