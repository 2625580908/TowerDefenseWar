using System.Collections.Generic;
using MRStrategyRTS.Shared;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Hands;

namespace MRStrategyRTS.Input
{
    /// <summary>
    /// Moves, rotates, and scales a terrain root while both hands are pinching.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TerrainTileGridGenerator))]
    public sealed class TerrainTwoHandPinchManipulator : MonoBehaviour
    {
        private const float MinimumAllowedScaleMultiplier = 0.07f;

        [Header("操作对象")]
        [Tooltip("留空时操作当前物体。应指定 TerrainTileGridGenerator 所在的地形根对象。")]
        [SerializeField] private Transform terrainRoot;

        [Header("双手 Pinch")]
        [SerializeField, Min(0.005f)] private float pinchThreshold = 0.025f;
        [SerializeField, Min(0.01f)] private float positionSensitivity = 1f;
        [SerializeField] private bool allowVerticalMovement;

        [Header("地形起手检测")]
        [Tooltip("双手首次 Pinch 必须分别位于当前地形的格子上，空中 Pinch 不会开始操作。")]
        [SerializeField] private bool requireTerrainPinchStart = true;
        [SerializeField, Min(0.001f)] private float terrainPinchHeight = 0.04f;
        [SerializeField, Min(0.001f)] private float terrainPinchDistance = 0.12f;
        [SerializeField] private LayerMask terrainPinchMask = ~0;

        [Header("缩放限制")]
        [Tooltip("相对开始时地图大小的最小倍率。")]
        [SerializeField, Min(MinimumAllowedScaleMultiplier)] private float minimumScaleMultiplier = 0.35f;
        [Tooltip("相对开始时地图大小的最大倍率。")]
        [SerializeField, Min(0.01f)] private float maximumScaleMultiplier = 2f;

        [Header("世界 UI")]
        [Tooltip("保持地图下 World Space Canvas 的文字和血条大小，不让它们随地图缩小而看不清。")]
        [SerializeField] private bool keepWorldUiReadable = true;
        [SerializeField, Min(0.05f)] private float worldUiRefreshInterval = 0.5f;

        [Header("事件")]
        [SerializeField] private UnityEvent onManipulationStarted = new();
        [SerializeField] private UnityEvent onManipulationEnded = new();

        public static bool IsManipulating { get; private set; }
        public float CurrentScaleMultiplier => GetScaleMultiplier(terrainRoot.localScale);

        private readonly List<XRHandSubsystem> subsystems = new();
        private Vector3 initialLeftPinchPosition;
        private Vector3 initialRightPinchPosition;
        private Vector3 initialMidpoint;
        private Vector3 initialPosition;
        private Vector3 initialScale;
        private Vector3 originalScale;
        private Quaternion initialRotation;
        private float initialHandDistance;
        private TerrainTileGridGenerator terrainGenerator;
        private Transform generatedTilesRoot;
        private float nextWorldUiRefreshAt;
        private bool waitingForInitialPinchRelease = true;

        private void Awake()
        {
            if (terrainRoot == null)
            {
                terrainRoot = transform;
            }

            terrainGenerator = GetComponent<TerrainTileGridGenerator>();
            originalScale = terrainRoot.localScale;
        }

        private void OnDisable()
        {
            EndManipulation();
            waitingForInitialPinchRelease = true;
        }

        private void Update()
        {
            RefreshWorldUiScaleCompensators();

            if (MRStrategyRTS.GameSessionState.IsLevelRunning)
            {
                EndManipulation();
                return;
            }

            if (!TryGetTwoHandPinchPoints(out var leftPinchPosition, out var rightPinchPosition))
            {
                EndManipulation();
                waitingForInitialPinchRelease = false;
                return;
            }

            // On device startup, hands can already report a pinch while the terrain is
            // positioned in front of the user. Wait for one full release so that pose
            // tracking does not immediately move the newly spawned terrain.
            if (waitingForInitialPinchRelease)
            {
                return;
            }

            if (!IsManipulating)
            {
                if (!CanStartManipulation(leftPinchPosition, rightPinchPosition))
                {
                    return;
                }

                BeginManipulation(leftPinchPosition, rightPinchPosition);
            }

            ApplyManipulation(leftPinchPosition, rightPinchPosition);
        }

        private void BeginManipulation(Vector3 leftPinchPosition, Vector3 rightPinchPosition)
        {
            if (terrainRoot == null)
            {
                return;
            }

            IsManipulating = true;
            initialLeftPinchPosition = leftPinchPosition;
            initialRightPinchPosition = rightPinchPosition;
            initialMidpoint = (leftPinchPosition + rightPinchPosition) * 0.5f;
            initialPosition = terrainRoot.position;
            initialRotation = terrainRoot.rotation;
            initialScale = terrainRoot.localScale;
            initialHandDistance = Mathf.Max(
                Vector3.Distance(leftPinchPosition, rightPinchPosition),
                0.001f);
            onManipulationStarted.Invoke();
        }

        private void ApplyManipulation(Vector3 leftPinchPosition, Vector3 rightPinchPosition)
        {
            if (terrainRoot == null)
            {
                return;
            }

            var currentMidpoint = (leftPinchPosition + rightPinchPosition) * 0.5f;
            var translation = currentMidpoint - initialMidpoint;
            if (!allowVerticalMovement)
            {
                translation.y = 0f;
            }

            terrainRoot.position = initialPosition + translation * positionSensitivity;

            var initialDirection = initialRightPinchPosition - initialLeftPinchPosition;
            var currentDirection = rightPinchPosition - leftPinchPosition;
            initialDirection.y = 0f;
            currentDirection.y = 0f;
            if (initialDirection.sqrMagnitude > 0.000001f && currentDirection.sqrMagnitude > 0.000001f)
            {
                var rotationDelta = Vector3.SignedAngle(initialDirection, currentDirection, Vector3.up);
                terrainRoot.rotation = Quaternion.AngleAxis(rotationDelta, Vector3.up) * initialRotation;
            }

            var distanceMultiplier = Vector3.Distance(leftPinchPosition, rightPinchPosition) / initialHandDistance;
            var startScaleMultiplier = GetScaleMultiplier(initialScale);
            var scaleMultiplier = Mathf.Clamp(
                startScaleMultiplier * distanceMultiplier,
                Mathf.Max(MinimumAllowedScaleMultiplier, minimumScaleMultiplier),
                Mathf.Max(
                    MinimumAllowedScaleMultiplier,
                    minimumScaleMultiplier,
                    maximumScaleMultiplier));
            terrainRoot.localScale = originalScale * scaleMultiplier;
        }

        private void EndManipulation()
        {
            if (!IsManipulating)
            {
                return;
            }

            IsManipulating = false;
            onManipulationEnded.Invoke();
        }

        private bool TryGetTwoHandPinchPoints(out Vector3 leftPoint, out Vector3 rightPoint)
        {
            leftPoint = default;
            rightPoint = default;
            subsystems.Clear();
            SubsystemManager.GetSubsystems(subsystems);

            foreach (var subsystem in subsystems)
            {
                if (!subsystem.running ||
                    !TryGetPinchPoint(subsystem.leftHand, out leftPoint) ||
                    !TryGetPinchPoint(subsystem.rightHand, out rightPoint))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private bool TryGetPinchPoint(XRHand hand, out Vector3 pinchPoint)
        {
            pinchPoint = default;
            if (!hand.isTracked ||
                !hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out var indexPose) ||
                !hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out var thumbPose) ||
                Vector3.Distance(indexPose.position, thumbPose.position) > pinchThreshold)
            {
                return false;
            }

            pinchPoint = (indexPose.position + thumbPose.position) * 0.5f;
            return true;
        }

        private bool CanStartManipulation(Vector3 leftPinchPosition, Vector3 rightPinchPosition)
        {
            return !requireTerrainPinchStart ||
                (IsPinchingGeneratedTerrain(leftPinchPosition) &&
                 IsPinchingGeneratedTerrain(rightPinchPosition));
        }

        private bool IsPinchingGeneratedTerrain(Vector3 pinchPosition)
        {
            var rayOrigin = pinchPosition + Vector3.up * terrainPinchHeight;
            if (Physics.Raycast(
                    rayOrigin,
                    Vector3.down,
                    out var hit,
                    terrainPinchHeight + terrainPinchDistance,
                    terrainPinchMask,
                    QueryTriggerInteraction.Ignore))
            {
                if (generatedTilesRoot == null)
                {
                    generatedTilesRoot = terrainRoot.Find("_GeneratedTerrainTiles");
                }

                if (generatedTilesRoot != null &&
                    hit.collider.transform.IsChildOf(generatedTilesRoot))
                {
                    return true;
                }

                if (hit.collider.GetComponentInParent<TerrainTileGridGenerator>() == terrainGenerator)
                {
                    return true;
                }
            }

            return terrainGenerator != null &&
                terrainGenerator.IsWorldPointOverGeneratedTile(
                    pinchPosition,
                    terrainPinchHeight + terrainPinchDistance);
        }

        private float GetScaleMultiplier(Vector3 scale)
        {
            if (originalScale.sqrMagnitude < 0.000001f)
            {
                return 1f;
            }

            return Vector3.Dot(scale, originalScale) / originalScale.sqrMagnitude;
        }

        private void RefreshWorldUiScaleCompensators()
        {
            if (!keepWorldUiReadable || Time.unscaledTime < nextWorldUiRefreshAt)
            {
                return;
            }

            nextWorldUiRefreshAt = Time.unscaledTime + worldUiRefreshInterval;
            foreach (var canvas in terrainRoot.GetComponentsInChildren<Canvas>(true))
            {
                if (canvas.renderMode != RenderMode.WorldSpace)
                {
                    continue;
                }

                var compensator = canvas.GetComponent<TerrainWorldUiScaleCompensator>();
                if (compensator == null)
                {
                    compensator = canvas.gameObject.AddComponent<TerrainWorldUiScaleCompensator>();
                }

                compensator.Initialize(this);
            }
        }
    }
}
