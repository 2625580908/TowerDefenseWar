using System;

/// <summary>Counts belong to the playable design block, not decorative meshes or shop towers.</summary>
public readonly struct DesignedMapMetrics
{
    public int GroundTileCount { get; }
    public int TowerBaseCount { get; }
    public int RouteStepCount { get; }

    public DesignedMapMetrics(int groundTileCount, int towerBaseCount, int routeStepCount)
    {
        if (groundTileCount < 3 || towerBaseCount < 0 || towerBaseCount > groundTileCount
            || routeStepCount < 2 || routeStepCount >= groundTileCount)
            throw new ArgumentOutOfRangeException(nameof(groundTileCount), "地图统计数据无效。");
        GroundTileCount = groundTileCount;
        TowerBaseCount = towerBaseCount;
        RouteStepCount = routeStepCount;
    }
}

/// <summary>Area, available defenses and time along the route jointly determine the enemy budget.</summary>
public readonly struct DesignedLevelEnemyBudget
{
    public int BaseCount { get; }
    public int MinimumCount { get; }
    public int MaximumCount { get; }
    public int TotalCount { get; }

    private DesignedLevelEnemyBudget(int baseCount, int minimumCount, int maximumCount, int totalCount)
    {
        BaseCount = baseCount;
        MinimumCount = minimumCount;
        MaximumCount = maximumCount;
        TotalCount = totalCount;
    }

    public static DesignedLevelEnemyBudget Calculate(
        DesignedMapMetrics metrics, float enemiesPerTile, float enemiesPerTowerBase,
        float enemiesPerRouteStep, float randomVariation, int minimumEnemies, Random random)
    {
        if (random == null)
            throw new ArgumentNullException(nameof(random));
        if (metrics.GroundTileCount < 3 || metrics.RouteStepCount < 2
            || !IsNonNegativeFinite(enemiesPerTile) || enemiesPerTile <= 0f
            || !IsNonNegativeFinite(enemiesPerTowerBase) || !IsNonNegativeFinite(enemiesPerRouteStep)
            || !IsNonNegativeFinite(randomVariation) || randomVariation > 0.3f || minimumEnemies < 1)
            throw new ArgumentOutOfRangeException(nameof(enemiesPerTile), "怪物数量匹配参数无效。");

        var weighted = metrics.GroundTileCount * (double)enemiesPerTile
            + metrics.TowerBaseCount * (double)enemiesPerTowerBase
            + metrics.RouteStepCount * (double)enemiesPerRouteStep;
        var baseline = Math.Max(minimumEnemies, checked((int)Math.Ceiling(weighted)));
        var spread = checked((int)Math.Round(baseline * (double)randomVariation, MidpointRounding.AwayFromZero));
        var minimum = Math.Max(minimumEnemies, baseline - spread);
        var maximum = checked(baseline + spread);
        // Sample once. UI and wave generation must use this same total.
        var total = minimum + (int)Math.Floor(random.NextDouble() * ((long)maximum - minimum + 1));
        return new DesignedLevelEnemyBudget(baseline, minimum, maximum, total);
    }

    private static bool IsNonNegativeFinite(float value)
    {
        return value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
