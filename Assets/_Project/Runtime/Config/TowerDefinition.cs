using UnityEngine;

namespace MRStrategyRTS.Config
{
    public enum TowerDamageMode { Single, Splash, Slow, Chain, Poison }
    public enum TowerTargetPriority { Nearest, HighestHealth, Unpoisoned }
    /// <summary>
    /// Defines the gameplay values for one placeable defense tower type.
    /// </summary>
    [CreateAssetMenu(fileName = "TowerDefinition", menuName = "MR Strategy RTS/Config/Tower Definition")]
    public sealed class TowerDefinition : ScriptableObject
    {
        [Header("防御塔信息")]
        [Tooltip("用于存档、解锁和统计的稳定 ID。")]
        public string towerId = "tower_basic";
        public string displayName = "基础防御塔";
        public string roleName = "均衡防守";
        [TextArea] public string featureSummary = "单体攻击";
        public Color accentColor = new Color(.94f,.73f,.36f,1f);
        [Tooltip("用于放置的防御塔预制体。")]
        public GameObject prefab;
        [Min(0)] public int buildCost;

        [Header("攻击")]
        [Tooltip("每次攻击生成的子弹预制体。")]
        public GameObject bulletPrefab;
        [Min(1)] public int damage = 10;
        [Min(0.01f)] public float bulletSpeed = 0.6f;
        [Min(0.01f)] public float attackRange = 0.6f;
        [Min(0.05f)] public float attackInterval = 0.8f;
        public TowerDamageMode damageMode;
        public TowerTargetPriority targetPriority;

        [Header("范围重炮")]
        [Min(0.001f)] public float splashRadius = .055f;
        [Range(0f,1f)] public float splashDamageMultiplier = .5f;
        [Min(1)] public int maximumSplashTargets = 6;
        [Header("霜晶减速")]
        [Range(0f,.75f)] public float slowFraction = .35f;
        [Min(.1f)] public float slowDuration = 2f;
        [Header("雷链跳电")]
        [Min(1)] public int chainTargetCount = 3;
        [Min(.001f)] public float chainJumpRadius = .065f;
        [Range(.1f,1f)] public float chainDamageMultiplier = .65f;
        [Header("蚀毒持续伤害")]
        [Min(1)] public int poisonTickDamage = 3;
        [Min(.1f)] public float poisonTickInterval = .5f;
        [Min(.1f)] public float poisonDuration = 2f;

        [Header("发射音效")]
        [Tooltip("本塔专属发射音效；未配置时沿用防御塔预制体上的音效。")]
        public AudioClip shootSound;
        [Range(0f, 1f)] public float shootSoundVolume = 0.55f;
        [Header("命中音效")]
        [Tooltip("子弹实际命中时从命中位置播放；目标消失或子弹超时不播放。")]
        public AudioClip impactSound;
        [Range(0f, 1f)] public float impactSoundVolume = 0.4f;
    }
}
