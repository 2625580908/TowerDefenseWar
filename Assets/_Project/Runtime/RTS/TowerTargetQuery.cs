using System.Collections.Generic;
using UnityEngine;

namespace MRStrategyRTS.RTS
{
    /// <summary>Deduplicates multi-collider enemies and grows before a crowded query truncates.</summary>
    public sealed class TowerTargetQuery
    {
        private Collider[] colliders = new Collider[32];
        private readonly HashSet<UnitCombat> unique = new();

        public void Collect(Vector3 center, float radius, string enemyTag, List<UnitCombat> result)
        {
            result.Clear();
            unique.Clear();
            int count;
            while (true)
            {
                count = Physics.OverlapSphereNonAlloc(center,Mathf.Max(0,radius),colliders,
                    ~0,QueryTriggerInteraction.Ignore);
                if (count < colliders.Length) break;
                System.Array.Resize(ref colliders,colliders.Length * 2);
            }
            for (var i=0;i<count;i++)
            {
                var unit = colliders[i].GetComponentInParent<UnitCombat>();
                if (unit == null || !unit.IsAlive || !unit.CompareTag(enemyTag)
                    || (unit.transform.position-center).sqrMagnitude > radius*radius || !unique.Add(unit))
                    continue;
                result.Add(unit);
            }
        }
    }
}
