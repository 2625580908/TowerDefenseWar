using System.Collections.Generic;
using MRStrategyRTS.RTS;
using UnityEngine;

namespace MRStrategyRTS.Input
{
    public sealed class RTSSelectionController : MonoBehaviour
    {
        [SerializeField] private Camera interactionCamera;
        [SerializeField] private LayerMask unitMask = ~0;
        [SerializeField] private LayerMask commandSurfaceMask = ~0;
        [SerializeField] private float maxRayDistance = 30f;

        private readonly List<RTSUnit> selectedUnits = new();
        public IReadOnlyList<RTSUnit> SelectedUnits => selectedUnits;

        public void SetInteractionCamera(Camera camera)
        {
            interactionCamera = camera;
        }

        private void Update()
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            if (UnityEngine.Input.GetMouseButtonDown(0))
            {
                TrySelectFromRay(interactionCamera.ScreenPointToRay(UnityEngine.Input.mousePosition), false);
            }

            if (UnityEngine.Input.GetMouseButtonDown(1))
            {
                TryCommandFromRay(interactionCamera.ScreenPointToRay(UnityEngine.Input.mousePosition));
            }
#endif
        }

        public void TrySelectFromRay(Ray ray, bool additive)
        {
            if (!Physics.Raycast(ray, out var hit, maxRayDistance, unitMask))
            {
                if (!additive)
                {
                    ClearSelection();
                }
                return;
            }

            var unit = hit.collider.GetComponentInParent<RTSUnit>();
            if (unit == null || !unit.IsAlive)
            {
                return;
            }

            var squad = unit.GetComponentInParent<RTSSquad>();
            if (squad != null)
            {
                SelectSquad(squad);
                return;
            }

            if (!additive)
            {
                ClearSelection();
            }

            if (!selectedUnits.Contains(unit))
            {
                selectedUnits.Add(unit);
            }
        }

        public void TryCommandFromRay(Ray ray)
        {
            if (selectedUnits.Count == 0 ||
                !Physics.Raycast(ray, out var hit, maxRayDistance, commandSurfaceMask))
            {
                return;
            }

            var targetUnit = hit.collider.GetComponentInParent<RTSUnit>();
            foreach (var unit in selectedUnits)
            {
                if (unit == null || !unit.IsAlive)
                {
                    continue;
                }

                if (targetUnit != null && targetUnit.Faction != unit.Faction)
                {
                    unit.Attack(targetUnit);
                }
                else
                {
                    unit.MoveTo(hit.point);
                }
            }
        }

        public void ClearSelection() => selectedUnits.Clear();

        public void SelectUnit(RTSUnit unit)
        {
            if (unit == null || !unit.IsAlive)
            {
                return;
            }

            var squad = unit.GetComponentInParent<RTSSquad>();
            if (squad != null)
            {
                SelectSquad(squad);
                return;
            }

            ClearSelection();
            selectedUnits.Add(unit);
        }

        public void CommandMove(Vector3 worldPosition)
        {
            foreach (var unit in selectedUnits)
            {
                if (unit != null && unit.IsAlive)
                {
                    unit.MoveTo(worldPosition);
                }
            }
        }

        private void SelectSquad(RTSSquad squad)
        {
            ClearSelection();
            foreach (var squadUnit in squad.Units)
            {
                if (squadUnit != null && squadUnit.IsAlive)
                {
                    selectedUnits.Add(squadUnit);
                }
            }
        }
    }
}
