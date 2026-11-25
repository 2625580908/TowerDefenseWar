using MRStrategyRTS.RTS;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;

namespace MRStrategyRTS.Shared
{
    /// <summary>
    /// Returns a grabbed object to its original parent unless it is released inside a snap zone.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable))]
    public sealed class XRGrabSnapPlacement : MonoBehaviour
    {
        [Header("Snap Target")]
        [SerializeField] private string snapZoneTag = "SnapZone";
        [SerializeField, Min(0f)] private float snapDistance = 0.02f;

        [Header("Preview")]
        [Tooltip("Prefab instantiated while the grabbed object is inside a valid snap zone.")]
        [SerializeField] private GameObject snapPreviewModel;

        [Header("Placement")]
        [Tooltip("Local position offset from the tagged Collider used by both the preview and the snapped object.")]
        public Vector3 snapLocalOffset;

        [Header("商店展示")]
        [Tooltip("启用后，Pinch 会保留台子上的展示炮塔，并生成一个待放置的副本。")]
        [SerializeField] private bool isShopDisplay;
        [Header("Editor Test")]
        [Tooltip("Only active in Unity Editor Play Mode. Allows mouse click-and-drag testing.")]
        [SerializeField] private bool enableEditorMouseTest = true;
        [Tooltip("Distance changed by one mouse-wheel step while dragging.")]
        [SerializeField, Min(0.01f)] private float mouseWheelDepthSpeed = 0.1f;
        [SerializeField, Min(0.01f)] private float minimumMouseDragDepth = 0.1f;
        [SerializeField, Min(0.1f)] private float maximumMouseDragDepth = 10f;

        [Header("Audio")]
        [Tooltip("Played once when the object is successfully snapped into a valid zone.")]
        public AudioClip snapSound;
        [Range(0f, 1f)] public float snapSoundVolume = 1f;

        [Header("Events")]
        [SerializeField] private UnityEvent onSnapped;
        [SerializeField] private UnityEvent onReturnedToOrigin;

        private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabInteractable;
        private Transform originalParent;
        private Vector3 originalLocalPosition;
        private Quaternion originalLocalRotation;
        private Vector3 originalLocalScale;
        private int originalSiblingIndex;
        private Rigidbody cachedRigidbody;
        private DefenseTower defenseTower;
        private AudioSource audioSource;
        private XRSelectFilterDelegate purchaseFilter;
        private bool isGrabbed;
        private bool isPurchasedInstance;
        private bool hasBeenPlaced;
        private Quaternion placementWorldRotation;
        private Collider previewZone;
        private GameObject previewInstance;

#if UNITY_EDITOR
        private static XRGrabSnapPlacement mouseGrabbedPlacement;
        private static Plane mouseDragPlane;
        private static float mouseDragDepth;
#endif

        public bool IsShopDisplay => isShopDisplay;
        public bool IsPlacedOnMap => hasBeenPlaced;

        private void Awake()
        {
            grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            cachedRigidbody = GetComponent<Rigidbody>();
            defenseTower = GetComponentInChildren<DefenseTower>(true);
            audioSource = GetComponent<AudioSource>();
            CaptureOriginalPose();
            placementWorldRotation = transform.rotation;
            grabInteractable.trackRotation = false;
            SetDefenseTowerActive(false);

            purchaseFilter = new XRSelectFilterDelegate((_, _) => CanStartPurchase());
            grabInteractable.selectFilters.Add(purchaseFilter);
        }

        private void OnEnable()
        {
            if (grabInteractable == null)
            {
                grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            }

            grabInteractable.selectEntered.AddListener(OnGrabbed);
            grabInteractable.selectExited.AddListener(OnReleased);
        }

        private void OnDisable()
        {
            if (grabInteractable == null)
            {
                return;
            }

            grabInteractable.selectEntered.RemoveListener(OnGrabbed);
            grabInteractable.selectExited.RemoveListener(OnReleased);
            isGrabbed = false;
            SetDefenseTowerActive(false);
            DestroyPreview();
        }

        private void OnDestroy()
        {
            if (grabInteractable != null && purchaseFilter != null)
            {
                grabInteractable.selectFilters.Remove(purchaseFilter);
            }
        }

        private void OnGrabbed(SelectEnterEventArgs args)
        {
            BeginGrab();
        }

        private void BeginGrab()
        {
            placementWorldRotation = transform.rotation;
            if (isShopDisplay)
            {
                CreatePurchaseInstance();
            }

            isGrabbed = true;
            SetDefenseTowerActive(false);
            UpdatePreview();
        }

        private void Update()
        {
            if (MRStrategyRTS.GameSessionState.IsBattlePaused) return;
            if (isGrabbed)
            {
                UpdatePreview();
            }

#if UNITY_EDITOR
            UpdateEditorMouseTest();
#endif
        }

        private void OnReleased(SelectExitEventArgs args)
        {
            ReleaseGrab();
        }

        private void ReleaseGrab()
        {
            isGrabbed = false;
            if (TryFindSnapZone(out var zone))
            {
                if (isPurchasedInstance && !hasBeenPlaced && !TryPayBuildCost())
                {
                    DestroyPreview();
                    Destroy(gameObject);
                    return;
                }

                DestroyPreview();
                SnapToZone(zone.transform);
                return;
            }

            DestroyPreview();
            if (isPurchasedInstance)
            {
                if (hasBeenPlaced)
                {
                    RefundBuildCost();
                }

                Destroy(gameObject);
                return;
            }

            ReturnToOriginalPose();
        }

#if UNITY_EDITOR
        private void UpdateEditorMouseTest()
        {
            if (!Application.isPlaying || !enableEditorMouseTest)
            {
                return;
            }

            if (mouseGrabbedPlacement == null)
            {
                if (UnityEngine.Input.GetMouseButtonDown(0))
                {
                    TryBeginEditorMouseGrab();
                }

                return;
            }

            if (mouseGrabbedPlacement != this)
            {
                return;
            }

            if (UnityEngine.Input.GetMouseButton(0))
            {
                var camera = Camera.main;
                if (camera != null)
                {
                    var wheelDelta = UnityEngine.Input.mouseScrollDelta.y;
                    if (Mathf.Abs(wheelDelta) > 0.001f)
                    {
                        mouseDragDepth = Mathf.Clamp(
                            mouseDragDepth + wheelDelta * mouseWheelDepthSpeed,
                            minimumMouseDragDepth,
                            Mathf.Max(minimumMouseDragDepth, maximumMouseDragDepth));
                    }

                    mouseDragPlane = new Plane(
                        camera.transform.forward,
                        camera.transform.position + camera.transform.forward * mouseDragDepth);
                    var ray = camera.ScreenPointToRay(UnityEngine.Input.mousePosition);
                    if (mouseDragPlane.Raycast(ray, out var distance))
                    {
                        transform.position = ray.GetPoint(distance);
                    }
                }
            }

            if (UnityEngine.Input.GetMouseButtonUp(0))
            {
                mouseGrabbedPlacement = null;
                ReleaseGrab();
            }
        }

        private void TryBeginEditorMouseGrab()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            var ray = camera.ScreenPointToRay(UnityEngine.Input.mousePosition);
            if (!Physics.Raycast(ray, out var hit, float.PositiveInfinity, ~0, QueryTriggerInteraction.Collide))
            {
                return;
            }

            var placement = hit.collider.GetComponentInParent<XRGrabSnapPlacement>();
            if (placement != this || !CanStartPurchase())
            {
                return;
            }

            mouseGrabbedPlacement = this;
            mouseDragDepth = Mathf.Clamp(
                Vector3.Dot(hit.point - camera.transform.position, camera.transform.forward),
                minimumMouseDragDepth,
                Mathf.Max(minimumMouseDragDepth, maximumMouseDragDepth));
            mouseDragPlane = new Plane(
                camera.transform.forward,
                camera.transform.position + camera.transform.forward * mouseDragDepth);
            BeginGrab();
        }
#endif

        private void UpdatePreview()
        {
            if (!TryFindSnapZone(out var zone))
            {
                DestroyPreview();
                return;
            }

            if (snapPreviewModel == null)
            {
                return;
            }

            if (previewInstance == null || previewZone != zone)
            {
                DestroyPreview();
                previewInstance = Instantiate(snapPreviewModel, zone.transform);
                previewInstance.name = $"{snapPreviewModel.name} (Snap Preview)";
                previewInstance.SetActive(true);
                previewZone = zone;
            }
            // Match SnapToZone rather than inheriting the imported slot's 90-degree rotation.
            previewInstance.transform.localPosition = snapLocalOffset;
            previewInstance.transform.rotation = placementWorldRotation;
            SetWorldScale(previewInstance.transform, transform.lossyScale);
        }

        private bool TryFindSnapZone(out Collider snapZone)
        {
            snapZone = null;
            if (string.IsNullOrWhiteSpace(snapZoneTag))
            {
                return false;
            }

            var colliders = Physics.OverlapSphere(
                transform.position,
                snapDistance,
                ~0,
                QueryTriggerInteraction.Collide);
            var closestDistanceSquared = float.MaxValue;
            foreach (var collider in colliders)
            {
                if (collider.transform.IsChildOf(transform) || !collider.CompareTag(snapZoneTag))
                {
                    continue;
                }

                var closestPoint = collider.ClosestPoint(transform.position);
                var distanceSquared = Vector3.SqrMagnitude(closestPoint - transform.position);
                if (distanceSquared < closestDistanceSquared)
                {
                    snapZone = collider;
                    closestDistanceSquared = distanceSquared;
                }
            }

            return snapZone != null;
        }

        private void SnapToZone(Transform zoneTransform)
        {
            if (zoneTransform == null)
            {
                ReturnToOriginalPose();
                return;
            }

            StopPhysicsMotion();
            var worldScale = transform.lossyScale;
            transform.SetParent(zoneTransform, false);
            transform.localPosition = snapLocalOffset;
            transform.rotation = placementWorldRotation;
            SetWorldScale(worldScale);
            hasBeenPlaced = true;
            SetDefenseTowerActive(true);
            PlaySnapSound();
            onSnapped.Invoke();
        }

        private void ReturnToOriginalPose()
        {
            StopPhysicsMotion();
            SetDefenseTowerActive(false);
            transform.SetParent(originalParent, false);
            if (originalParent != null)
            {
                transform.SetSiblingIndex(originalSiblingIndex);
            }

            transform.localPosition = originalLocalPosition;
            transform.localRotation = originalLocalRotation;
            transform.localScale = originalLocalScale;
            onReturnedToOrigin.Invoke();
        }

        private void CaptureOriginalPose()
        {
            originalParent = transform.parent;
            originalLocalPosition = transform.localPosition;
            originalLocalRotation = transform.localRotation;
            originalLocalScale = transform.localScale;
            originalSiblingIndex = transform.GetSiblingIndex();
        }

        private bool CanStartPurchase()
        {
            if (MRStrategyRTS.GameSessionState.IsBattlePaused) return false;
            if (!isShopDisplay)
            {
                return true;
            }

            return defenseTower != null
                && (defenseTower.BuildCost == 0
                    || MRStrategyRTS.GameSessionState.CanAffordGold(defenseTower.BuildCost));
        }

        private bool TryPayBuildCost()
        {
            if (defenseTower == null || defenseTower.BuildCost == 0)
            {
                return true;
            }

            return MRStrategyRTS.GameSessionState.TrySpendGold(defenseTower.BuildCost);
        }

        private void RefundBuildCost()
        {
            if (defenseTower == null || defenseTower.BuildCost <= 0)
            {
                return;
            }

            MRStrategyRTS.GameSessionState.AddGold(defenseTower.BuildCost);
        }

        private void CreatePurchaseInstance()
        {
            var displayParent = originalParent;
            var displayPosition = originalLocalPosition;
            var displayRotation = originalLocalRotation;
            var displayScale = originalLocalScale;

            isShopDisplay = false;
            isPurchasedInstance = true;
            hasBeenPlaced = false;
            GetComponent<DefenseTowerPurchaseVisual>()?.Refresh();
            var shopPrefab = defenseTower != null
                ? defenseTower.TowerDefinition?.prefab
                : null;
            var replacement = Instantiate(
                shopPrefab != null ? shopPrefab : gameObject,
                displayParent);
            replacement.name = $"{name} Display";
            replacement.transform.localPosition = displayPosition;
            replacement.transform.localRotation = displayRotation;
            replacement.transform.localScale = displayScale;

            var replacementPlacement = replacement.GetComponent<XRGrabSnapPlacement>();
            replacementPlacement.ConfigureAsShopDisplay();
        }

        public void ConfigureAsShopDisplay()
        {
            isShopDisplay = true;
            isPurchasedInstance = false;
            hasBeenPlaced = false;
            CaptureOriginalPose();
            SetDefenseTowerActive(false);
            var visual = GetComponent<DefenseTowerPurchaseVisual>();
            if (visual == null) visual = gameObject.AddComponent<DefenseTowerPurchaseVisual>();
            visual.Refresh();
        }

        private void StopPhysicsMotion()
        {
            if (cachedRigidbody == null)
            {
                return;
            }

            cachedRigidbody.linearVelocity = Vector3.zero;
            cachedRigidbody.angularVelocity = Vector3.zero;
        }

        private void SetWorldScale(Vector3 worldScale)
        {
            SetWorldScale(transform, worldScale);
        }

        private static void SetWorldScale(Transform target, Vector3 worldScale)
        {
            var parentScale = target.parent != null
                ? target.parent.lossyScale
                : Vector3.one;
            target.localScale = new Vector3(
                DivideScale(worldScale.x, parentScale.x),
                DivideScale(worldScale.y, parentScale.y),
                DivideScale(worldScale.z, parentScale.z));
        }

        private static float DivideScale(float value, float divisor)
        {
            return Mathf.Abs(divisor) > 0.0001f ? value / divisor : value;
        }

        private void DestroyPreview()
        {
            if (previewInstance == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(previewInstance);
            }
            else
            {
                DestroyImmediate(previewInstance);
            }

            previewInstance = null;
            previewZone = null;
        }

        private void SetDefenseTowerActive(bool isActive)
        {
            if (defenseTower != null)
            {
                defenseTower.enabled = isActive;
            }
        }

        private void PlaySnapSound()
        {
            if (snapSound == null)
            {
                return;
            }

            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 1f;
            }

            audioSource.PlayOneShot(snapSound, snapSoundVolume);
        }
    }
}
