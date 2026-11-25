using UnityEngine;

namespace MRStrategyRTS.Battlefield
{
    public sealed class BattlefieldPlacementController : MonoBehaviour
    {
        [SerializeField] private Transform battlefieldRoot;
        [SerializeField] private Transform interactionOrigin;
        [SerializeField] private LayerMask placementMask = ~0;
        [SerializeField] private float maxDistance = 8f;
        [SerializeField] private float minimumScale = 0.35f;
        [SerializeField] private float maximumScale = 1.5f;

        private Vector3 initialScale;
        public bool IsPlaced { get; private set; }

        private void Awake()
        {
            if (battlefieldRoot == null)
            {
                battlefieldRoot = transform;
            }
            initialScale = battlefieldRoot.localScale;
        }

        public bool TryPlace()
        {
            if (interactionOrigin == null)
            {
                return false;
            }

            var ray = new Ray(interactionOrigin.position, interactionOrigin.forward);
            if (!Physics.Raycast(ray, out var hit, maxDistance, placementMask))
            {
                return false;
            }

            battlefieldRoot.position = hit.point;
            battlefieldRoot.up = hit.normal;
            IsPlaced = true;
            return true;
        }

        public void Rotate(float degrees) =>
            battlefieldRoot.Rotate(Vector3.up, degrees, Space.World);

        public void SetNormalizedScale(float normalizedScale)
        {
            var scale = Mathf.Lerp(minimumScale, maximumScale, Mathf.Clamp01(normalizedScale));
            battlefieldRoot.localScale = initialScale * scale;
        }
    }
}
