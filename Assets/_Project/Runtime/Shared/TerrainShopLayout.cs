using System.Collections.Generic;
using UnityEngine;

namespace MRStrategyRTS.Shared
{
    /// <summary>Keeps the merchandise floor one grid cell beyond the map's front edge.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class TerrainShopLayout : MonoBehaviour
    {
        [SerializeField] private Transform mapTiles;
        [SerializeField] private Transform shopRoot;
        [SerializeField, Min(0)] private float gapInTiles = 1f;
        [SerializeField, Min(0.1f)] private float refreshInterval = 0.5f;
        private readonly List<Transform> tiles = new();
        private readonly List<MeshFilter> meshes = new();
        private readonly List<float> rows = new();
        private double nextRefresh;

        private void OnEnable() => RefreshLayout();
        private void LateUpdate()
        {
            if (Time.realtimeSinceStartupAsDouble < nextRefresh) return;
            nextRefresh = Time.realtimeSinceStartupAsDouble + refreshInterval;
            RefreshLayout();
        }

        public bool RefreshLayout()
        {
#if UNITY_EDITOR
            if (UnityEditor.EditorUtility.IsPersistent(gameObject)) return false;
#endif
            if (mapTiles == null) mapTiles = transform.Find("_GeneratedTerrainTiles");
            if (shopRoot == null) shopRoot = transform.Find("Minecraft Tower Defense");
            if (mapTiles == null || shopRoot == null || shopRoot.parent != transform) return false;
            var shopTiles = shopRoot.Find("_GeneratedTerrainTiles");
            if (!TryGetFloorBounds(mapTiles, out var mapBounds, out var cellDepth)
                || !TryGetFloorBounds(shopTiles, out var shopBounds, out _)) return false;
            var offset = new Vector3(mapBounds.center.x - shopBounds.center.x, 0,
                mapBounds.min.z - gapInTiles * cellDepth - shopBounds.max.z);
            if (offset.sqrMagnitude > 0.00000001f) shopRoot.localPosition += offset;
            return true;
        }

        public bool TryGetGap(out float gap, out float cellDepth)
        {
            gap = cellDepth = 0;
            if (shopRoot == null || !TryGetFloorBounds(mapTiles, out var map, out cellDepth)
                || !TryGetFloorBounds(shopRoot.Find("_GeneratedTerrainTiles"), out var shop, out _)) return false;
            gap = map.min.z - shop.max.z;
            return true;
        }

        private bool TryGetFloorBounds(Transform grid, out Bounds bounds, out float cellDepth)
        {
            bounds = default;
            cellDepth = 0;
            if (grid == null) return false;
            bool found = false;
            rows.Clear();
            grid.GetComponentsInChildren(true, tiles);
            foreach (var tile in tiles)
            {
                if (!tile.name.StartsWith("Tile_", System.StringComparison.Ordinal)
                    || !tile.gameObject.activeInHierarchy) continue;
                rows.Add(transform.InverseTransformPoint(tile.position).z);
                tile.GetComponentsInChildren(true, meshes);
                foreach (var mesh in meshes)
                {
                    if (mesh.sharedMesh == null || mesh.GetComponentInParent<MRStrategyRTS.RTS.DefenseTower>() != null) continue;
                    var meshBounds = mesh.sharedMesh.bounds;
                    for (int i = 0; i < 8; i++)
                    {
                        var corner = meshBounds.center + Vector3.Scale(meshBounds.extents,
                            new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                        var point = transform.InverseTransformPoint(mesh.transform.TransformPoint(corner));
                        if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; }
                        else bounds.Encapsulate(point);
                    }
                }
            }
            rows.Sort();
            float spacing = float.PositiveInfinity;
            for (int i = 1; i < rows.Count; i++)
            {
                float delta = rows[i] - rows[i - 1];
                if (delta > 0.0001f) spacing = Mathf.Min(spacing, delta);
            }
            cellDepth = float.IsInfinity(spacing) ? bounds.size.z : spacing;
            return found && cellDepth > 0.0001f;
        }
    }
}
