using System.Collections.Generic;
using MRStrategyRTS.Config;
using UnityEngine;

namespace MRStrategyRTS.RTS
{
    /// <summary>Resolves one tower hit with unique targets and retained kill attribution.</summary>
    public sealed class TowerDamageResolver
    {
        private readonly TowerTargetQuery query = new();
        private readonly List<UnitCombat> candidates = new();
        private readonly HashSet<UnitCombat> hit = new();
        private static Material chainMaterial;

        public void Resolve(UnitCombat primary, TowerDefinition definition, int damage,
            GameObject owner, string enemyTag)
        {
            if (primary == null || !primary.IsAlive) return;
            var position = primary.transform.position;
            var mode = definition != null ? definition.damageMode : TowerDamageMode.Single;
            // TakeDamage may kill and destroy the target; capture its position first.
            primary.TakeDamage(damage,owner);
            if (mode == TowerDamageMode.Splash)
            {
                query.Collect(position,definition.splashRadius,enemyTag,candidates);
                candidates.Sort((a,b) => (a.transform.position-position).sqrMagnitude
                    .CompareTo((b.transform.position-position).sqrMagnitude));
                int remaining = Mathf.Max(0,definition.maximumSplashTargets-1);
                foreach(var unit in candidates)
                {
                    if (unit == primary || !unit.IsAlive) continue;
                    if (remaining-- <= 0) break;
                    unit.TakeDamage(Mathf.Max(0,Mathf.RoundToInt(damage*definition.splashDamageMultiplier)),owner);
                }
            }
            else if (mode == TowerDamageMode.Chain)
            {
                hit.Clear(); hit.Add(primary);
                var previous = position;
                int chainDamage = damage;
                for(int i=1;i<Mathf.Max(1,definition.chainTargetCount);i++)
                {
                    query.Collect(previous,definition.chainJumpRadius,enemyTag,candidates);
                    UnitCombat next = null; float closest = float.PositiveInfinity;
                    foreach(var unit in candidates)
                    {
                        float distance = (unit.transform.position-previous).sqrMagnitude;
                        if(hit.Contains(unit) || distance>=closest) continue;
                        next=unit; closest=distance;
                    }
                    if(next == null) break;
                    chainDamage = Mathf.Max(1,Mathf.RoundToInt(chainDamage*definition.chainDamageMultiplier));
                    hit.Add(next);
                    var nextPosition=next.transform.position;
                    DrawChain(previous,nextPosition,definition.accentColor);
                    previous=nextPosition;
                    next.TakeDamage(chainDamage,owner);
                }
            }
            else if (primary != null && primary.IsAlive && (mode == TowerDamageMode.Slow || mode == TowerDamageMode.Poison))
            {
                var status = primary.GetComponent<UnitTowerStatus>();
                if(status == null) status=primary.gameObject.AddComponent<UnitTowerStatus>();
                if(mode == TowerDamageMode.Slow)
                    status.ApplySlow(definition.slowFraction,definition.slowDuration,owner);
                else
                    status.ApplyPoison(definition.poisonTickDamage,definition.poisonTickInterval,definition.poisonDuration,owner);
            }
        }

        private static void DrawChain(Vector3 from,Vector3 to,Color color)
        {
            var lineObject=new GameObject("Tower Chain Arc");
            var line=lineObject.AddComponent<LineRenderer>();
            if(chainMaterial == null) chainMaterial=Resources.Load<Material>("TowerChainLine");
            line.sharedMaterial=chainMaterial;
            line.positionCount=3;line.useWorldSpace=true;
            line.SetPosition(0,from+Vector3.up*.03f);
            line.SetPosition(1,Vector3.Lerp(from,to,.5f)+Vector3.up*.043f);
            line.SetPosition(2,to+Vector3.up*.03f);
            line.startWidth=.002f;line.endWidth=.0008f;
            line.startColor=color;line.endColor=color;
            line.numCapVertices=2;
            Object.Destroy(lineObject,.13f);
        }
    }
}
