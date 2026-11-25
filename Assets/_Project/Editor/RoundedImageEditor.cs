using MRStrategyRTS.UI;
using UnityEditor;
using UnityEditor.UI;
using UnityEngine;

namespace MRStrategyRTS.Editor
{
    [CustomEditor(typeof(RoundedImage))]
    [CanEditMultipleObjects]
    public sealed class RoundedImageEditor : ImageEditor
    {
        private SerializedProperty cornerRadius;
        private SerializedProperty cornerSegments;

        protected override void OnEnable()
        {
            base.OnEnable();
            cornerRadius = serializedObject.FindProperty("cornerRadius");
            cornerSegments = serializedObject.FindProperty("cornerSegments");
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            serializedObject.Update();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Rounded Corners", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(cornerRadius, new GUIContent("Corner Radius"));
            EditorGUILayout.PropertyField(cornerSegments, new GUIContent("Corner Segments"));

            serializedObject.ApplyModifiedProperties();
        }
    }
}
