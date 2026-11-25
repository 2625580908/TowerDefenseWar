using System.Collections.Generic;
using MRStrategyRTS.RTS;
using UnityEngine;

namespace MRStrategyRTS.AI
{
    public sealed class SkirmishAI : MonoBehaviour
    {
        [SerializeField] private UnitProductionBuilding productionBuilding;
        [SerializeField] private UnitDefinition combatUnit;
        [SerializeField] private Transform attackTarget;
        [SerializeField, Min(1f)] private float decisionInterval = 8f;
        [SerializeField, Min(1)] private int attackGroupSize = 4;

        private readonly List<RTSUnit> ownedUnits = new();
        private float nextDecisionAt;

        private void Update()
        {
            if (Time.time < nextDecisionAt)
            {
                return;
            }

            nextDecisionAt = Time.time + decisionInterval;
            productionBuilding.TryProduce(combatUnit);
            RefreshUnits();

            if (ownedUnits.Count < attackGroupSize || attackTarget == null)
            {
                return;
            }

            foreach (var unit in ownedUnits)
            {
                unit.MoveTo(attackTarget.position);
            }
        }

        private void RefreshUnits()
        {
            ownedUnits.Clear();
            foreach (var unit in FindObjectsByType<RTSUnit>(FindObjectsSortMode.None))
            {
                if (unit.IsAlive && unit.Definition != null && unit.Definition.faction == combatUnit.faction)
                {
                    ownedUnits.Add(unit);
                }
            }
        }
    }
}
