using System.Collections.Generic;

/// <summary>Validates a row-major map using only orthogonally adjacent land cells.</summary>
public static class LevelDesignRouteValidator
{
    public static bool TryValidate(
        int width, int height, IReadOnlyList<int> cells,
        int startType, int endType, int landType, out string message)
    {
        return TryGetRouteLength(width, height, cells, startType, endType, landType, out _, out message);
    }

    /// <summary>Shortest orthogonal route, measured in moves and including entry/exit connections.</summary>
    public static bool TryGetRouteLength(
        int width, int height, IReadOnlyList<int> cells,
        int startType, int endType, int landType, out int routeStepCount, out string message)
    {
        routeStepCount = 0;
        if (width < 1 || height < 1 || cells == null || (long)width * height != cells.Count)
        {
            message = "路线检查失败：矩阵数据不完整，请重新生成网格。";
            return false;
        }
        if (startType < 0 || endType < 0 || landType < 0
            || startType == endType || startType == landType || endType == landType)
        {
            message = "路线检查失败：请配置不同的起点、终点和陆地笔刷。";
            return false;
        }

        var start = -1;
        var startCount = 0;
        var endCount = 0;
        var landCount = 0;
        for (var index = 0; index < cells.Count; index++)
        {
            if (cells[index] == startType)
            {
                start = index;
                startCount++;
            }
            else if (cells[index] == endType)
                endCount++;
            else if (cells[index] == landType)
                landCount++;
        }

        if (startCount != 1 || endCount != 1)
        {
            message = $"路线问题：必须各有一个起点和终点（当前起点 {startCount} 个，终点 {endCount} 个）。";
            return false;
        }
        if (landCount == 0)
        {
            message = "路线问题：请绘制陆地道路，连接起点和终点。";
            return false;
        }

        var visited = new bool[cells.Count];
        var distances = new int[cells.Count];
        var queue = new Queue<int>();
        visited[start] = true;
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var row = current / width;
            var column = current % width;
            for (var direction = 0; direction < 4; direction++)
            {
                int neighbor;
                switch (direction)
                {
                    case 0:
                        if (column == 0) continue;
                        neighbor = current - 1;
                        break;
                    case 1:
                        if (column == width - 1) continue;
                        neighbor = current + 1;
                        break;
                    case 2:
                        if (row == 0) continue;
                        neighbor = current - width;
                        break;
                    default:
                        if (row == height - 1) continue;
                        neighbor = current + width;
                        break;
                }

                // Reaching the end must involve at least one land tile.
                if (cells[neighbor] == endType && current != start)
                {
                    routeStepCount = distances[current] + 1;
                    message = string.Empty;
                    return true;
                }
                if (!visited[neighbor] && cells[neighbor] == landType)
                {
                    visited[neighbor] = true;
                    distances[neighbor] = distances[current] + 1;
                    queue.Enqueue(neighbor);
                }
            }
        }

        message = "路线问题：起点无法沿陆地走到终点，请检查道路断点；只支持上下左右连接，斜角不算相连。";
        return false;
    }
}
