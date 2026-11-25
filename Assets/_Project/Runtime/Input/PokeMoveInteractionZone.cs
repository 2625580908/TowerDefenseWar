using System.Collections.Generic;
using MRStrategyRTS.RTS;
using MRStrategyRTS.Shared;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.XR.Hands;

namespace MRStrategyRTS.Input
{
    /// <summary>
    /// Direct-touch MR command zone. Poke a unit to select, then poke terrain to move it.
    /// </summary>
    [RequireComponent(typeof(RTSSelectionController))]
    public sealed class PokeMoveInteractionZone : MonoBehaviour
    {
        [SerializeField] private RTSSelectionController selection;
        [SerializeField, Min(0.01f)] private float pokeRadius = 0.06f;
        [SerializeField, Min(0.05f)] private float navMeshSampleRadius = 0.3f;
        [SerializeField, Min(0.01f)] private float directMoveSpeed = 0.35f;
        [SerializeField, Min(0.02f)] private float formationSpacing = 0.08f;
        [SerializeField] private LayerMask interactionMask = ~0;
        [SerializeField] private bool showDiagnostics = true;

        private readonly List<XRHandSubsystem> subsystems = new();
        private readonly List<NavMeshAgent> selectedAgents = new();
        private readonly List<Transform> selectedDirectUnits = new();
        private readonly Dictionary<Transform, Vector3> directMoveTargets = new();
        private MainScenePokeNavigationBootstrap navigation;
        private bool touching;
        private GameObject selectionMarker;
        private GameObject targetMarker;
        private string diagnosticStatus = "Waiting for hand tracking";
        private Vector3 lastTipPosition;

        private void Awake()
        {
            if (selection == null)
            {
                selection = GetComponent<RTSSelectionController>();
            }

            navigation = GetComponent<MainScenePokeNavigationBootstrap>();
            if (Camera.main != null)
            {
                selection.SetInteractionCamera(Camera.main);
            }

            CreateMarkers();
        }

        private void Update()
        {
            UpdateDirectMovement();

            if (TerrainTwoHandPinchManipulator.IsManipulating)
            {
                touching = false;
                return;
            }

            if (selection == null || !TryGetIndexTip(out var tipPosition))
            {
                touching = false;
                diagnosticStatus = "Waiting for hand tracking";
                return;
            }

            lastTipPosition = tipPosition;
            var touchedCollider = FindNearestCollider(tipPosition);
            if (touchedCollider == null)
            {
                touching = false;
                diagnosticStatus = "Hand tracked - move closer to a collider";
                return;
            }

            if (!touching)
            {
                HandlePoke(touchedCollider, tipPosition);
            }

            touching = true;
        }

        public void SimulatePoke(Vector3 worldPosition)
        {
            var touchedCollider = FindNearestCollider(worldPosition);
            if (touchedCollider != null)
            {
                HandlePoke(touchedCollider, worldPosition);
                return;
            }

            diagnosticStatus = "Simulated poke missed all colliders";
        }

        private bool TryGetIndexTip(out Vector3 tipPosition)
        {
            tipPosition = default;
            subsystems.Clear();
            SubsystemManager.GetSubsystems(subsystems);

            foreach (var subsystem in subsystems)
            {
                if (!subsystem.running)
                {
                    continue;
                }

                if (TryGetIndexTip(subsystem.rightHand, out tipPosition) ||
                    TryGetIndexTip(subsystem.leftHand, out tipPosition))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetIndexTip(XRHand hand, out Vector3 tipPosition)
        {
            tipPosition = default;
            if (!hand.isTracked ||
                !hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out var indexTipPose))
            {
                return false;
            }

            tipPosition = indexTipPose.position;
            return true;
        }

        private Collider FindNearestCollider(Vector3 position)
        {
            var hits = Physics.OverlapSphere(position, pokeRadius, interactionMask, QueryTriggerInteraction.Ignore);
            Collider nearest = null;
            var nearestDistance = float.MaxValue;

            foreach (var hit in hits)
            {
                var distance = Vector3.SqrMagnitude(hit.ClosestPoint(position) - position);
                if (distance < nearestDistance)
                {
                    nearest = hit;
                    nearestDistance = distance;
                }
            }

            return nearest;
        }

        private void HandlePoke(Collider touchedCollider, Vector3 contactPoint)
        {
            var taggedObject = FindTaggedObject(touchedCollider.transform);
            if (taggedObject == null)
            {
                SetDiagnostic("Poke hit untagged object: " + touchedCollider.name);
                return;
            }

            var baseData = BaseData.Instance;
            if (HasTag(taggedObject, baseData.OneselfTag))
            {
                SelectFriendlyUnit(taggedObject);
                return;
            }

            if (!HasTag(taggedObject, baseData.TerrainTag))
            {
                SetDiagnostic("Poke hit unsupported tag: " + taggedObject.tag);
                return;
            }

            ShowTarget(contactPoint);
            if (selectedAgents.Count == 0 && selectedDirectUnits.Count == 0)
            {
                SetDiagnostic("Terrain hit, but no unit is selected");
                return;
            }

            var navMeshFound = NavMesh.SamplePosition(
                contactPoint,
                out var navHit,
                navMeshSampleRadius,
                NavMesh.AllAreas);
            if (selectedAgents.Count > 0 && !navMeshFound)
            {
                SetDiagnostic("Terrain hit, but no NavMesh near target");
                return;
            }

            var movingAgents = 0;
            var formationCount = selectedAgents.Count + selectedDirectUnits.Count;
            var formationIndex = 0;
            foreach (var agent in selectedAgents)
            {
                if (agent != null && agent.isOnNavMesh && navMeshFound)
                {
                    var formationPoint = GetFormationPoint(
                        navHit.position,
                        formationIndex++,
                        formationCount);
                    if (NavMesh.SamplePosition(
                            formationPoint,
                            out var formationNavHit,
                            navMeshSampleRadius,
                            NavMesh.AllAreas))
                    {
                        agent.SetDestination(formationNavHit.position);
                    }
                    else
                    {
                        agent.SetDestination(navHit.position);
                    }

                    movingAgents++;
                }
            }

            foreach (var unit in selectedDirectUnits)
            {
                if (unit != null)
                {
                    var formationPoint = GetFormationPoint(
                        contactPoint,
                        formationIndex++,
                        formationCount);
                    directMoveTargets[unit] = new Vector3(
                        formationPoint.x,
                        unit.position.y,
                        formationPoint.z);
                    movingAgents++;
                }
            }

            ShowTarget(navMeshFound ? navHit.position : contactPoint);
            SetDiagnostic("Move command sent to " + movingAgents + " unit(s)");
        }

        private Vector3 GetFormationPoint(Vector3 center, int index, int count)
        {
            var columns = Mathf.CeilToInt(Mathf.Sqrt(count));
            var rows = Mathf.CeilToInt((float)count / columns);
            var column = index % columns;
            var row = index / columns;
            return center + new Vector3(
                (column - (columns - 1) * 0.5f) * formationSpacing,
                0f,
                (row - (rows - 1) * 0.5f) * formationSpacing);
        }

        private void SelectFriendlyUnit(GameObject friendlyObject)
        {
            var squad = friendlyObject.GetComponentInParent<RTSSquad>();
            if (squad != null)
            {
                foreach (var squadUnit in squad.Units)
                {
                    AddAgent(squadUnit);
                }
            }
            else
            {
                EnsureNavigationAgent(friendlyObject);
                var agent = friendlyObject.GetComponentInParent<NavMeshAgent>();
                if (agent != null)
                {
                    ToggleAgent(agent);
                }
                else
                {
                    ToggleDirectUnit(friendlyObject.transform);
                }
            }

            var rtsUnit = friendlyObject.GetComponentInParent<RTSUnit>();
            if (rtsUnit != null)
            {
                selection.SelectUnit(rtsUnit);
            }

            ShowSelection(friendlyObject.transform.position);
            SetDiagnostic(
                "Selection updated: " +
                (selectedAgents.Count + selectedDirectUnits.Count) +
                " unit(s)");
        }

        private void UpdateDirectMovement()
        {
            foreach (var pair in directMoveTargets)
            {
                if (pair.Key == null)
                {
                    continue;
                }

                pair.Key.position = Vector3.MoveTowards(
                    pair.Key.position,
                    pair.Value,
                    directMoveSpeed * Time.deltaTime);
            }
        }

        private void EnsureNavigationAgent(GameObject friendlyObject)
        {
            if (friendlyObject.GetComponent<NavMeshAgent>() != null)
            {
                return;
            }

            if (navigation == null)
            {
                navigation = GetComponent<MainScenePokeNavigationBootstrap>();
            }

            if (navigation != null && !navigation.TryConfigureAgent(friendlyObject))
            {
                SetDiagnostic("Selected unit could not be placed on NavMesh");
            }
        }

        private void AddAgent(RTSUnit unit)
        {
            if (unit == null)
            {
                return;
            }

            var agent = unit.GetComponent<NavMeshAgent>();
            if (agent != null && !selectedAgents.Contains(agent))
            {
                selectedAgents.Add(agent);
            }
        }

        private void ToggleAgent(NavMeshAgent agent)
        {
            if (!selectedAgents.Remove(agent))
            {
                selectedAgents.Add(agent);
            }
        }

        private void ToggleDirectUnit(Transform unit)
        {
            if (!selectedDirectUnits.Remove(unit))
            {
                selectedDirectUnits.Add(unit);
            }
        }

        private static GameObject FindTaggedObject(Transform transform)
        {
            while (transform != null)
            {
                if (transform.gameObject.tag != "Untagged")
                {
                    return transform.gameObject;
                }

                transform = transform.parent;
            }

            return null;
        }

        private static bool HasTag(GameObject target, string tag)
        {
            if (target == null || string.IsNullOrWhiteSpace(tag))
            {
                return false;
            }

            try
            {
                return target.CompareTag(tag);
            }
            catch (UnityException)
            {
                return false;
            }
        }

        private void CreateMarkers()
        {
            selectionMarker = CreateMarker("Poke Selection Marker", new Color(0.02f, 0.7f, 1f));
            targetMarker = CreateMarker("Poke Move Marker", new Color(1f, 0.55f, 0.02f));
            selectionMarker.SetActive(false);
            targetMarker.SetActive(false);
        }

        private static GameObject CreateMarker(string markerName, Color color)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = markerName;
            marker.transform.localScale = new Vector3(0.1f, 0.008f, 0.1f);
            marker.GetComponent<Collider>().enabled = false;
            marker.GetComponent<Renderer>().material.color = color;
            return marker;
        }

        private void ShowSelection(Vector3 position)
        {
            selectionMarker.transform.position = position + Vector3.up * 0.01f;
            selectionMarker.SetActive(true);
        }

        private void ShowTarget(Vector3 position)
        {
            targetMarker.transform.position = position + Vector3.up * 0.012f;
            targetMarker.SetActive(true);
        }

        private void SetDiagnostic(string status)
        {
            diagnosticStatus = status;
            Debug.Log("[Poke] " + status, this);
        }

        private void OnGUI()
        {
            if (!showDiagnostics)
            {
                return;
            }

            var style = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 24,
                wordWrap = true
            };
            var message = "Poke: " + diagnosticStatus +
                          "\nTip: " + lastTipPosition.ToString("F2") +
                          "\nSelected units: " + (selectedAgents.Count + selectedDirectUnits.Count);
            GUI.Box(new Rect(24, 24, 760, 130), message, style);
        }
    }
}
