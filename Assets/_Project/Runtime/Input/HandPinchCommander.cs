using System.Collections.Generic;
using MRStrategyRTS.RTS;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace MRStrategyRTS.Input
{
    /// <summary>
    /// Right index-finger ray plus thumb-index pinch: select a squad, then issue a move command.
    /// </summary>
    public sealed class HandPinchCommander : MonoBehaviour
    {
        [SerializeField] private RTSSelectionController selection;
        [SerializeField, Min(0.005f)] private float pinchThreshold = 0.025f;
        [SerializeField, Min(0.1f)] private float maxRayDistance = 8f;

        private readonly List<XRHandSubsystem> subsystems = new();
        private bool wasPinching;

        private void Awake()
        {
            if (selection == null)
            {
                selection = GetComponent<RTSSelectionController>();
            }
        }

        private void Update()
        {
            if (TerrainTwoHandPinchManipulator.IsManipulating)
            {
                wasPinching = false;
                return;
            }

            if (selection == null || !TryGetPinchRay(out var ray, out var pinching))
            {
                wasPinching = false;
                return;
            }

            if (pinching && !wasPinching)
            {
                ExecutePinch(ray);
            }

            wasPinching = pinching;
        }

        private bool TryGetPinchRay(out Ray ray, out bool pinching)
        {
            ray = default;
            pinching = false;
            subsystems.Clear();
            SubsystemManager.GetSubsystems(subsystems);

            foreach (var subsystem in subsystems)
            {
                if (!subsystem.running || !subsystem.rightHand.isTracked)
                {
                    continue;
                }

                var hand = subsystem.rightHand;
                if (!hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out var indexPose) ||
                    !hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out var thumbPose))
                {
                    continue;
                }

                ray = new Ray(indexPose.position, indexPose.rotation * Vector3.forward);
                pinching = Vector3.Distance(indexPose.position, thumbPose.position) <= pinchThreshold;
                return true;
            }

            return false;
        }

        private void ExecutePinch(Ray ray)
        {
            if (!Physics.Raycast(ray, out var hit, maxRayDistance))
            {
                return;
            }

            var unit = hit.collider.GetComponentInParent<RTSUnit>();
            if (unit != null)
            {
                selection.TrySelectFromRay(ray, false);
                return;
            }

            selection.TryCommandFromRay(ray);
        }
    }
}
