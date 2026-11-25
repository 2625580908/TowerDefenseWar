using MRStrategyRTS.Shared;
using UnityEngine;

namespace MRStrategyRTS.RTS
{
    [CreateAssetMenu(menuName = "MR Strategy/Unit Definition")]
    public sealed class UnitDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string displayName;
        public FactionId faction;
        public GameObject prefab;

        [Header("Cost")]
        [Min(0)] public int credits = 500;
        [Min(0)] public int supply = 1;
        [Min(0f)] public float productionSeconds = 6f;

        [Header("Combat")]
        [Min(1)] public int maxHealth = 100;
        [Min(0f)] public float moveSpeed = 3f;
        [Min(0f)] public float attackRange = 5f;
        [Min(0)] public int attackDamage = 10;
        [Min(0.1f)] public float attackCooldown = 1f;
    }
}
