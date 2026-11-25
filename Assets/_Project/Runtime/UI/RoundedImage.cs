using UnityEngine;
using UnityEngine.UI;

namespace MRStrategyRTS.UI
{
    /// <summary>
    /// A Simple UGUI Image with rounded corners.
    /// Replace a standard Image component with this component to use it.
    /// </summary>
    [AddComponentMenu("UI/Rounded Image")]
    [ExecuteAlways]
    public sealed class RoundedImage : Image
    {
        [SerializeField, Min(0f)]
        [Tooltip("Corner radius in local UI units.")]
        private float cornerRadius = 16f;

        [SerializeField, Range(1, 16)]
        [Tooltip("Vertices used for each quarter-circle. Higher values produce smoother corners.")]
        private int cornerSegments = 6;

        public float CornerRadius
        {
            get => cornerRadius;
            set
            {
                cornerRadius = Mathf.Max(0f, value);
                SetVerticesDirty();
            }
        }

        public int CornerSegments
        {
            get => cornerSegments;
            set
            {
                cornerSegments = Mathf.Clamp(value, 1, 16);
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            base.OnPopulateMesh(vertexHelper);

            // Sliced, tiled and filled Images generate their own non-rectangular mesh.
            if (type != Type.Simple || cornerRadius <= 0f || vertexHelper.currentVertCount != 4)
            {
                return;
            }

            var vertices = new UIVertex[4];
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);

            for (var index = 0; index < 4; index++)
            {
                vertexHelper.PopulateUIVertex(ref vertices[index], index);
                min = Vector2.Min(min, vertices[index].position);
                max = Vector2.Max(max, vertices[index].position);
            }

            var radius = Mathf.Min(cornerRadius, (max.x - min.x) * 0.5f, (max.y - min.y) * 0.5f);
            if (radius <= 0f)
            {
                return;
            }

            vertexHelper.Clear();
            AddVertex(vertexHelper, vertices[0], (min + max) * 0.5f, min, max, vertices);

            AddCorner(vertexHelper, vertices[0], new Vector2(min.x + radius, min.y + radius), radius, 270f, 180f, min, max, vertices);
            AddCorner(vertexHelper, vertices[0], new Vector2(min.x + radius, max.y - radius), radius, 180f, 90f, min, max, vertices);
            AddCorner(vertexHelper, vertices[0], new Vector2(max.x - radius, max.y - radius), radius, 90f, 0f, min, max, vertices);
            AddCorner(vertexHelper, vertices[0], new Vector2(max.x - radius, min.y + radius), radius, 0f, -90f, min, max, vertices);

            var perimeterVertexCount = vertexHelper.currentVertCount - 1;
            for (var index = 0; index < perimeterVertexCount; index++)
            {
                vertexHelper.AddTriangle(0, index + 1, index == perimeterVertexCount - 1 ? 1 : index + 2);
            }
        }

        private void AddCorner(
            VertexHelper vertexHelper,
            UIVertex template,
            Vector2 center,
            float radius,
            float fromDegrees,
            float toDegrees,
            Vector2 min,
            Vector2 max,
            UIVertex[] sourceVertices)
        {
            for (var index = 0; index <= cornerSegments; index++)
            {
                var degrees = Mathf.Lerp(fromDegrees, toDegrees, index / (float)cornerSegments);
                var radians = degrees * Mathf.Deg2Rad;
                var position = center + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * radius;
                AddVertex(vertexHelper, template, position, min, max, sourceVertices);
            }
        }

        private static void AddVertex(
            VertexHelper vertexHelper,
            UIVertex template,
            Vector2 position,
            Vector2 min,
            Vector2 max,
            UIVertex[] sourceVertices)
        {
            var horizontal = Mathf.InverseLerp(min.x, max.x, position.x);
            var vertical = Mathf.InverseLerp(min.y, max.y, position.y);
            template.position = position;
            template.uv0 = Vector2.Lerp(
                Vector2.Lerp(sourceVertices[0].uv0, sourceVertices[3].uv0, horizontal),
                Vector2.Lerp(sourceVertices[1].uv0, sourceVertices[2].uv0, horizontal),
                vertical);
            vertexHelper.AddVert(template);
        }

        private void OnValidate()
        {
            cornerRadius = Mathf.Max(0f, cornerRadius);
            cornerSegments = Mathf.Clamp(cornerSegments, 1, 16);
            SetVerticesDirty();
        }
    }
}
