using UnityEngine;

namespace MRStrategyRTS.Input
{
    /// <summary>
    /// Keeps a world-space UI readable when an ancestor terrain is scaled.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    public sealed class TerrainWorldUiScaleCompensator : MonoBehaviour
    {
        private TerrainTwoHandPinchManipulator terrainManipulator;
        private Vector3 originalLocalScale;

        private void Awake()
        {
            originalLocalScale = transform.localScale;
            if (terrainManipulator == null)
            {
                terrainManipulator = GetComponentInParent<TerrainTwoHandPinchManipulator>();
            }
        }

        public void Initialize(TerrainTwoHandPinchManipulator manipulator)
        {
            terrainManipulator = manipulator;
        }

        private void LateUpdate()
        {
            if (terrainManipulator == null)
            {
                return;
            }

            var scaleMultiplier = Mathf.Max(terrainManipulator.CurrentScaleMultiplier, 0.001f);
            transform.localScale = originalLocalScale / scaleMultiplier;
        }
    }
}
