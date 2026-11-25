using System.Collections.Generic;
using MRStrategyRTS.AI;
using UnityEngine;

namespace MRStrategyRTS.RTS
{
    /// <summary>Timed tower effects: strongest slow wins; poison never adds across towers.</summary>
    [DisallowMultipleComponent]
    public sealed class UnitTowerStatus : MonoBehaviour
    {
        private struct Slow
        {
            public int source;
            public float fraction;
            public float until;
        }
        private readonly List<Slow> slows = new();
        private UnitCombat combat;
        private MonsterPathWalker[] walkers;
        private float poisonUntil = -1;
        private float nextPoisonTick;
        private float poisonInterval;
        private int poisonDamage;
        private GameObject poisonOwner;
        public float MovementMultiplier { get; private set; } = 1f;
        public bool IsPoisoned => combat != null && combat.IsAlive && Time.time <= poisonUntil;

        private void Awake()
        {
            combat = GetComponent<UnitCombat>();
            walkers = GetComponentsInChildren<MonsterPathWalker>(true);
        }

        public void ApplySlow(float fraction, float duration, GameObject source)
        {
            if (combat == null || !combat.IsAlive) return;
            var effect = new Slow { source = source != null ? source.GetInstanceID() : 0,
                fraction = Mathf.Clamp(fraction,0,.75f), until = Time.time + Mathf.Max(.1f,duration) };
            for (var i=0;i<slows.Count;i++)
            {
                if (slows[i].source != effect.source) continue;
                slows[i] = effect;
                UpdateSlow();
                return;
            }
            slows.Add(effect);
            UpdateSlow();
        }

        public void ApplyPoison(int damage, float interval, float duration, GameObject source)
        {
            if (combat == null || !combat.IsAlive) return;
            interval = Mathf.Max(.1f,interval);
            damage = Mathf.Max(1,damage);
            var active = IsPoisoned;
            // Ignore weaker sources without extending the stronger source's poison.
            if (active && damage / interval < poisonDamage / poisonInterval) return;
            poisonDamage = damage;
            poisonInterval = interval;
            poisonUntil = Time.time + Mathf.Max(interval,duration);
            poisonOwner = source;
            // Refreshing duration must not repeatedly postpone the next damage tick.
            if (!active) nextPoisonTick = Time.time + interval;
        }

        private void Update()
        {
            if (MRStrategyRTS.GameSessionState.IsBattlePaused) return;
            if (combat == null || !combat.IsAlive) return;
            UpdateSlow();
            while (poisonUntil >= 0 && nextPoisonTick <= poisonUntil + .0001f
                   && Time.time + .0001f >= nextPoisonTick && combat.IsAlive)
            {
                nextPoisonTick += poisonInterval;
                combat.TakeDamage(poisonDamage,poisonOwner);
            }
            if (Time.time > poisonUntil) poisonUntil = -1;
        }

        private void UpdateSlow()
        {
            float strongest = 0;
            for (var i=slows.Count-1;i>=0;i--)
            {
                if (Time.time >= slows[i].until) { slows.RemoveAt(i); continue; }
                strongest = Mathf.Max(strongest,slows[i].fraction);
            }
            MovementMultiplier = 1 - strongest;
            foreach (var walker in walkers)
                if (walker != null) walker.SetTowerMovementMultiplier(MovementMultiplier);
        }

        private void OnDisable()
        {
            slows.Clear();
            poisonUntil = -1;
            MovementMultiplier = 1;
            if (walkers != null)
                foreach(var walker in walkers)
                    if(walker != null) walker.SetTowerMovementMultiplier(1);
        }
    }
}
