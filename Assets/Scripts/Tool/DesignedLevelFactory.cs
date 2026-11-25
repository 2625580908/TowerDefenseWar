using System;
using System.Collections.Generic;
using MRStrategyRTS.Config;
using UnityEngine;

/// <summary>Creates temporary definition assets and distributes an exact enemy budget into waves.</summary>
public static class DesignedLevelFactory
{
    public static bool HasValidSpawns(LevelDefinition template, int walkableTileType)
    {
        if (template == null || template.waves == null)
            return false;
        foreach (var wave in template.waves)
        {
            if (wave == null || wave.monsterSpawns == null)
                continue;
            foreach (var entry in wave.monsterSpawns)
                if (IsValidEntry(entry, walkableTileType))
                    return true;
        }
        return false;
    }

    public static int CalculateEnemyCount(int tileCount, float enemiesPerTile)
    {
        if (tileCount < 1 || float.IsNaN(enemiesPerTile) || float.IsInfinity(enemiesPerTile)
            || enemiesPerTile <= 0f)
            throw new ArgumentOutOfRangeException("Map area and enemy density must be positive.");
        return Math.Max(1, checked((int)Math.Ceiling(tileCount * (double)enemiesPerTile)));
    }

    public static LevelDefinition Create(
        LevelDefinition template, GameObject terrainPrefab, int tileCount,
        float enemiesPerTile, int walkableTileType, System.Random random)
    {
        return CreateWithEnemyCount(template, terrainPrefab,
            CalculateEnemyCount(tileCount, enemiesPerTile), walkableTileType, random);
    }

    public static LevelDefinition CreateWithEnemyCount(
        LevelDefinition template, GameObject terrainPrefab, int total,
        int walkableTileType, System.Random random)
    {
        if (!HasValidSpawns(template, walkableTileType))
            throw new ArgumentException("关卡模板没有能够沿当前陆地行走的有效怪物波次。");
        if (random == null)
            throw new ArgumentNullException(nameof(random));
        if (total < 1)
            throw new ArgumentOutOfRangeException(nameof(total));

        var definition = ScriptableObject.CreateInstance<LevelDefinition>();
        definition.hideFlags = HideFlags.DontSave;
        definition.name = "Designed Level Runtime";
        definition.levelId = "design_" + Guid.NewGuid().ToString("N");
        definition.displayName = $"设计关卡（{total} 个怪物）";
        definition.startingGold = template.startingGold;
        definition.baseHealth = template.baseHealth;
        definition.terrainPrefab = terrainPrefab;
        definition.monsterSpawnLocalOffset = template.monsterSpawnLocalOffset;
        definition.monsterSpawnWorldOffset = template.monsterSpawnWorldOffset;
        definition.waves = new List<WaveDefinition>();

        var entries = new List<MonsterSpawnEntry>();
        var weights = new List<double>();
        var totalWeight = 0d;
        foreach (var sourceWave in template.waves)
        {
            if (sourceWave == null || sourceWave.monsterSpawns == null)
                continue;
            WaveDefinition wave = null;
            foreach (var source in sourceWave.monsterSpawns)
            {
                if (!IsValidEntry(source, walkableTileType))
                    continue;
                if (wave == null)
                {
                    wave = ScriptableObject.CreateInstance<WaveDefinition>();
                    wave.hideFlags = HideFlags.DontSave;
                    wave.name = "Designed Wave Runtime";
                    wave.displayName = sourceWave.displayName;
                    wave.startDelay = Math.Max(0f, sourceWave.startDelay);
                    wave.monsterSpawns = new List<MonsterSpawnEntry>();
                    definition.waves.Add(wave);
                }
                var entry = new MonsterSpawnEntry
                {
                    monster = source.monster,
                    count = 0,
                    spawnInterval = Math.Max(0f, source.spawnInterval)
                };
                wave.monsterSpawns.Add(entry);
                entries.Add(entry);
                var weight = Math.Max(1, source.count);
                weights.Add(weight);
                totalWeight += weight;
            }
        }

        // Seed each type when the budget allows, then randomly distribute the remaining budget.
        var order = new List<int>();
        for (var index = 0; index < entries.Count; index++)
            order.Add(index);
        for (var index = order.Count - 1; index > 0; index--)
        {
            var swap = random.Next(index + 1);
            var value = order[index];
            order[index] = order[swap];
            order[swap] = value;
        }
        var seeded = Math.Min(total, entries.Count);
        for (var index = 0; index < seeded; index++)
            entries[order[index]].count = 1;
        for (var index = seeded; index < total; index++)
        {
            var roll = random.NextDouble() * totalWeight;
            for (var entryIndex = 0; entryIndex < entries.Count; entryIndex++)
            {
                roll -= weights[entryIndex];
                if (roll < 0d || entryIndex == entries.Count - 1)
                {
                    entries[entryIndex].count++;
                    break;
                }
            }
        }
        for (var index = definition.waves.Count - 1; index >= 0; index--)
        {
            var wave = definition.waves[index];
            wave.monsterSpawns.RemoveAll(entry => entry.count == 0);
            if (wave.monsterSpawns.Count == 0)
            {
                definition.waves.RemoveAt(index);
                UnityEngine.Object.Destroy(wave);
            }
        }
        return definition;
    }

    public static void Release(LevelDefinition definition)
    {
        if (definition == null)
            return;
        foreach (var wave in definition.waves)
            if (wave != null)
                UnityEngine.Object.Destroy(wave);
        UnityEngine.Object.Destroy(definition);
    }

    private static bool IsValidEntry(MonsterSpawnEntry entry, int walkableTileType)
    {
        return entry != null && entry.monster != null && entry.monster.prefab != null
            && entry.count > 0 && entry.monster.walkableTileType == walkableTileType;
    }
}
