using System.Collections.Generic;
using UnityEngine;

namespace MRStrategyRTS.Config
{
    /// <summary>
    /// Defines one playable tower-defense level and its ordered waves.
    /// </summary>
    [CreateAssetMenu(fileName = "LevelDefinition", menuName = "MR Strategy RTS/Config/Level Definition")]
    public sealed class LevelDefinition : ScriptableObject
    {
        [Header("关卡信息")]
        [Tooltip("用于存档、解锁和关卡选择的稳定 ID。")]
        public string levelId = "level_01";
        public string displayName = "关卡 01";

        [Header("开局资源")]
        [Min(0)] public int startingGold;
        [Min(1)] public int baseHealth = 100;

        [Header("关卡地形")]
        [Tooltip("本关运行时生成的地形预制体。预制体必须包含 TerrainTileGridGenerator 和 Origin/Destination 地砖。")]
        public GameObject terrainPrefab;

        [Header("怪物生成")]
        [Tooltip("相对 Origin 标记的本地生成偏移。")]
        public Vector3 monsterSpawnLocalOffset = new(0f, 0f, -0.15f);
        [Tooltip("额外的世界坐标出生偏移，单位为米。Y 为正数时抬高身体，不受起点地砖旋转影响；与怪物自身 Ground Height Offset 叠加。")]
        public Vector3 monsterSpawnWorldOffset = new(0f, 0.015f, 0f);

        [Header("波次")]
        [Tooltip("按列表顺序执行。每个 WaveDefinition 可配置多个怪物类型。")]
        public List<WaveDefinition> waves = new();
    }
}
