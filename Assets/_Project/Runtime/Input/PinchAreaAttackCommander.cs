using System.Collections.Generic;
using MRStrategyRTS.RTS;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace MRStrategyRTS.Input
{
    /// <summary>
    /// Pinch-drag on terrain to select friendly units.
    /// </summary>
    public sealed class PinchAreaAttackCommander : MonoBehaviour
    {
        [SerializeField, Min(0.005f)] private float pinchThreshold = 0.025f;
        [SerializeField, Min(0.01f)] private float minimumSelectionSize = 0.04f;
        [SerializeField, Min(0.001f)] private float selectionLineWidth = 0.008f;

        private readonly List<XRHandSubsystem> subsystems = new();
        private readonly List<UnitCombat> selectedUnits = new();
        private bool pinching;
        private bool drawingSelection;
        private Vector3 selectionStart;
        private Vector3 selectionEnd;
        private LineRenderer selectionLine;

        private void Awake()
        {
            selectionLine = gameObject.AddComponent<LineRenderer>();
            selectionLine.positionCount = 5;
            selectionLine.loop = false;
            selectionLine.widthMultiplier = selectionLineWidth;
            selectionLine.material = new Material(Shader.Find("Sprites/Default"));
            selectionLine.startColor = new Color(0.02f, 0.7f, 1f);
            selectionLine.endColor = new Color(0.02f, 0.7f, 1f);
            selectionLine.enabled = false;
        }

        private void Update()
        {
            if (TerrainTwoHandPinchManipulator.IsManipulating)
            {
                pinching = false;
                drawingSelection = false;
                selectionLine.enabled = false;
                return;
            }

            if (!TryGetPinchPoint(out var pinchPoint, out var isPinching))
            {
                pinching = false;
                drawingSelection = false;
                selectionLine.enabled = false;
                return;
            }

            if (isPinching && !pinching)
            {
                BeginPinch(pinchPoint);
            }
            else if (isPinching && drawingSelection)
            {
                selectionEnd = pinchPoint;
                UpdateSelectionLine();
            }
            else if (!isPinching && pinching)
            {
                EndPinch(pinchPoint);
            }

            pinching = isPinching;
        }

        private void BeginPinch(Vector3 point)
        {
            selectionStart = point;
            selectionEnd = point;
            drawingSelection = true;
            selectionLine.enabled = true;
            UpdateSelectionLine();
        }

        private void EndPinch(Vector3 point)
        {
            if (!drawingSelection)
            {
                return;
            }

            selectionEnd = point;
            drawingSelection = false;
            selectionLine.enabled = false;
            SelectUnitsInArea();
        }

        private bool TryGetPinchPoint(out Vector3 point, out bool isPinching)
        {
            point = default;
            isPinching = false;
            subsystems.Clear();
            SubsystemManager.GetSubsystems(subsystems);

            foreach (var subsystem in subsystems)
            {
                if (!subsystem.running)
                {
                    continue;
                }

                if (TryGetPinchPoint(subsystem.rightHand, out point, out isPinching) ||
                    TryGetPinchPoint(subsystem.leftHand, out point, out isPinching))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryGetPinchPoint(XRHand hand, out Vector3 point, out bool isPinching)
        {
            point = default;
            isPinching = false;
            if (!hand.isTracked ||
                !hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out var indexPose) ||
                !hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out var thumbPose))
            {
                return false;
            }

            isPinching = Vector3.Distance(indexPose.position, thumbPose.position) <= pinchThreshold;
            if (!Physics.Raycast(indexPose.position, Vector3.down, out var hit, 3f))
            {
                return false;
            }

            point = hit.point;
            return true;
        }

        private void SelectUnitsInArea()
        {
            if (Mathf.Abs(selectionEnd.x - selectionStart.x) < minimumSelectionSize ||
                Mathf.Abs(selectionEnd.z - selectionStart.z) < minimumSelectionSize)
            {
                return;
            }

            selectedUnits.Clear();
            var minX = Mathf.Min(selectionStart.x, selectionEnd.x);
            var maxX = Mathf.Max(selectionStart.x, selectionEnd.x);
            var minZ = Mathf.Min(selectionStart.z, selectionEnd.z);
            var maxZ = Mathf.Max(selectionStart.z, selectionEnd.z);

            foreach (var unitObject in FindUnitsWithTag(BaseData.Instance.OneselfTag))
            {
                var unit = unitObject.GetComponent<UnitCombat>();
                if (unit == null || !unit.IsAlive)
                {
                    continue;
                }

                var position = unit.transform.position;
                if (position.x >= minX && position.x <= maxX &&
                    position.z >= minZ && position.z <= maxZ)
                {
                    selectedUnits.Add(unit);
                }
            }

            Debug.Log("[PinchArea] Selected units: " + selectedUnits.Count, this);
        }

        private static GameObject[] FindUnitsWithTag(string tag)
        {
            try
            {
                return GameObject.FindGameObjectsWithTag(tag);
            }
            catch (UnityException)
            {
                return System.Array.Empty<GameObject>();
            }
        }

        private void UpdateSelectionLine()
        {
            var y = Mathf.Max(selectionStart.y, selectionEnd.y) + 0.01f;
            selectionLine.SetPositions(new[]
            {
                new Vector3(selectionStart.x, y, selectionStart.z),
                new Vector3(selectionEnd.x, y, selectionStart.z),
                new Vector3(selectionEnd.x, y, selectionEnd.z),
                new Vector3(selectionStart.x, y, selectionEnd.z),
                new Vector3(selectionStart.x, y, selectionStart.z)
            });
        }
    }
}
