using UnityEngine;

namespace MRStrategyRTS.Config
{
    /// <summary>
    /// Defines the gameplay values for one monster type.
    /// </summary>
    [CreateAssetMenu(fileName = "MonsterDefinition", menuName = "MR Strategy RTS/Config/Monster Definition")]
    public sealed class MonsterDefinition : ScriptableObject
    {
        [Header("怪物信息")]
        [Tooltip("用于存档、统计和波次引用的稳定 ID。")]
        public string monsterId = "monster_basic";
        public string displayName = "普通怪物";
        public GameObject prefab;

        [Header("生命与奖励")]
        [Min(1)] public int maxHealth = 100;
        [Tooltip("抵达 Destination 时对基地造成的伤害。")]
        [Min(1)] public int baseDamage = 10;
        [Tooltip("被防御塔击杀时给予玩家的金币。")]
        [Min(0)] public int killGoldReward = 10;

        [Header("移动")]
        [Min(0.01f)] public float moveSpeed = 0.35f;
        [Tooltip("走路动画以 1 倍速度播放时，对应的世界移动速度。用于让动画步频匹配 Move Speed。")]
        [Min(0.001f)] public float animationWalkSpeed = 0.05f;
        [Tooltip("怪物生成及行走时增加的世界 Y 高度，用于修正模型脚部陷入地面。")]
        public float groundHeightOffset;
        [Tooltip("可走地砖的 Inspector 类型编号。02 地砖填写 2。")]
        [Min(1)] public int walkableTileType = 2;
    }
}
