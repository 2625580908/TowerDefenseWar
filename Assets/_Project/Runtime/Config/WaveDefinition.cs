using System;
using System.Collections.Generic;
using UnityEngine;

namespace MRStrategyRTS.Config
{
    /// <summary>
    /// Defines an ordered group of monster spawn entries.
    /// </summary>
    [CreateAssetMenu(fileName = "WaveDefinition", menuName = "MR Strategy RTS/Config/Wave Definition")]
    public sealed class WaveDefinition : ScriptableObject
    {
        [Header("波次信息")]
        public string displayName = "波次";
        [Tooltip("本波开始前等待的秒数。")]
        [Min(0f)] public float startDelay;

        [Header("怪物生成")]
        [Tooltip("按列表顺序生成。每个条目可以使用不同怪物和生成间隔。")]
        public List<MonsterSpawnEntry> monsterSpawns = new();
    }

    [Serializable]
    public sealed class MonsterSpawnEntry
    {
        [Header("怪物类型")]
        public MonsterDefinition monster;

        [Header("生成参数")]
        [Min(1)] public int count = 1;
        [Tooltip("此条目内相邻怪物的生成间隔，单位为秒。")]
        [Min(0f)] public float spawnInterval = 1f;
    }
}
