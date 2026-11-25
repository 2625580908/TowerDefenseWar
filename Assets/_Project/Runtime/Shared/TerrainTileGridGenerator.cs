using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Serialization;

namespace MRStrategyRTS.Shared
{
    /// <summary>
    /// Creates a centered grid of floor-tile prefab instances below this terrain root.
    /// </summary>
    [ExecuteAlways]
    public sealed class TerrainTileGridGenerator : MonoBehaviour
    {
        private const string GeneratedRootName = "_GeneratedTerrainTiles";
        private const string OriginMarkerTag = "Origin";
        private const string DestinationMarkerTag = "Destination";

        // #region debug-point B:terrain-rebuild
        private const string DebugServerUrl = "http://127.0.0.1:7777/event";
        private const string DebugSessionId = "pico-terrain-freeze";

        [Serializable]
        private struct DebugEventPayload
        {
            public string sessionId;
            public string runId;
            public string hypothesisId;
            public long ts;
            public string location;
            public string msg;
            public string data;
        }
        // #endregion

        [Serializable]
        private sealed class TerrainBlock
        {
            [Serializable]
            private sealed class TileVariant
            {
                [SerializeField] private GameObject tileObject;
                [SerializeField] private Texture2D editorPreview;
                [Tooltip("Tag assigned to every generated tile instance of this type. The tag must exist in Unity Tag Manager.")]
                [SerializeField, HideInInspector] private string generatedTag = "Untagged";
                [Tooltip("When enabled, generated tiles keep their prefab Collider or receive a BoxCollider if none exists.")]
                [SerializeField, HideInInspector] private bool generateCollider;

                public GameObject TileObject => tileObject;
                public Texture2D EditorPreview => editorPreview;
                public string GeneratedTag => generatedTag;
                public bool GenerateCollider => generateCollider;
            }

            private const int CurrentTileLayoutVersion = 1;

            [SerializeField] private string blockName = "Terrain Block";
            [Tooltip("Each entry pairs a tile object with an optional image used in the custom Inspector.")]
            [SerializeField] private List<TileVariant> tileVariants = new();
            [SerializeField, HideInInspector] private List<int> tileTypeIndices = new();
            [SerializeField, HideInInspector] private int tileLayoutVersion;
            [SerializeField, Min(1)] private int rows = 8;
            [SerializeField, Min(1)] private int columns = 8;
            [SerializeField, Min(0.001f)] private float rowSpacing = 0.1f;
            [SerializeField, Min(0.001f)] private float columnSpacing = 0.1f;
            [SerializeField] private Vector3 localPosition;
            [Tooltip("Rotation applied to each generated tile, not the terrain block root.")]
            [FormerlySerializedAs("localEulerAngles")]
            [SerializeField] private Vector3 tileEulerAngles;
            [SerializeField] private Vector3 tileScale = new(0.1f, 0.02f, 0.1f);

            public string BlockName => string.IsNullOrWhiteSpace(blockName) ? "Terrain Block" : blockName;
            public bool HasTileVariants
            {
                get
                {
                    if (tileVariants == null)
                    {
                        return false;
                    }

                    for (var index = 0; index < tileVariants.Count; index++)
                    {
                        if (tileVariants[index]?.TileObject != null)
                        {
                            return true;
                        }
                    }

                    return false;
                }
            }
            public int Rows => rows;
            public int Columns => columns;
            public float RowSpacing => rowSpacing;
            public float ColumnSpacing => columnSpacing;
            public Vector3 LocalPosition => localPosition;
            public float SurfaceLocalY => localPosition.y;
            public Vector3 TileEulerAngles => tileEulerAngles;
            public Vector3 TileScale => tileScale;

            public int FindTileType(Sprite brushSprite)
            {
                if (brushSprite == null || tileVariants == null)
                    return -1;

                for (var index = 0; index < tileVariants.Count; index++)
                {
                    var variant = tileVariants[index];
                    if (variant?.TileObject != null
                        && variant.EditorPreview == brushSprite.texture)
                        return index;
                }

                return -1;
            }

            public bool HasTileType(int index)
            {
                return tileVariants != null && index >= 0 && index < tileVariants.Count
                    && tileVariants[index]?.TileObject != null;
            }

            public void SetLayout(int width, int height, List<int> indices)
            {
                columns = width;
                rows = height;
                tileTypeIndices = new List<int>(indices);
                tileLayoutVersion = CurrentTileLayoutVersion;
            }

            public int CountTilesWithTag(string tag)
            {
                var count = 0;
                for (var row = 0; row < rows; row++)
                for (var column = 0; column < columns; column++)
                {
                    var variant = GetTileVariant(row, column);
                    if (variant?.TileObject != null && variant.GeneratedTag == tag)
                        count++;
                }
                return count;
            }

            public void EnsureTileLayout()
            {
                MigrateLegacyTileIndices();

                var tileCount = rows * columns;
                if (tileTypeIndices == null)
                {
                    tileTypeIndices = new List<int>(tileCount);
                }

                while (tileTypeIndices.Count < tileCount)
                {
                    tileTypeIndices.Add(-1);
                }

                if (tileTypeIndices.Count > tileCount)
                {
                    tileTypeIndices.RemoveRange(tileCount, tileTypeIndices.Count - tileCount);
                }
            }

            private void MigrateLegacyTileIndices()
            {
                if (tileLayoutVersion >= CurrentTileLayoutVersion || tileTypeIndices == null)
                {
                    return;
                }

                for (var index = 0; index < tileTypeIndices.Count; index++)
                {
                    if (tileTypeIndices[index] > 0)
                    {
                        tileTypeIndices[index]--;
                    }
                }

                tileLayoutVersion = CurrentTileLayoutVersion;
            }

            public GameObject GetTilePrefab(int row, int column)
            {
                return GetTileVariant(row, column)?.TileObject;
            }

            private TileVariant GetTileVariant(int row, int column)
            {
                var tileIndex = row * columns + column;
                var typeIndex = tileIndex < tileTypeIndices.Count ? tileTypeIndices[tileIndex] : -1;
                if (tileVariants == null || typeIndex < 0 || typeIndex >= tileVariants.Count)
                {
                    return null;
                }

                return tileVariants[typeIndex];
            }

            public bool IsTileType(int tileIndex, int tileTypeIndex)
            {
                return tileIndex >= 0
                    && tileIndex < tileTypeIndices.Count
                    && tileTypeIndices[tileIndex] == tileTypeIndex;
            }

            public Vector3 GetLocalTilePosition(int tileIndex)
            {
                var row = tileIndex / columns;
                var column = tileIndex % columns;
                var xOffset = (columns - 1) * columnSpacing * 0.5f;
                var zOffset = (rows - 1) * rowSpacing * 0.5f;
                return localPosition + new Vector3(
                    column * columnSpacing - xOffset,
                    0f,
                    row * rowSpacing - zOffset);
            }

            public bool ContainsLocalTilePosition(Vector3 localPosition)
            {
                EnsureTileLayout();
                var xOffset = (columns - 1) * columnSpacing * 0.5f;
                var zOffset = (rows - 1) * rowSpacing * 0.5f;
                var column = Mathf.RoundToInt(
                    (localPosition.x - this.localPosition.x + xOffset) / columnSpacing);
                var row = Mathf.RoundToInt(
                    (localPosition.z - this.localPosition.z + zOffset) / rowSpacing);
                if (row < 0 || row >= rows || column < 0 || column >= columns)
                {
                    return false;
                }

                var tileIndex = row * columns + column;
                var tilePosition = GetLocalTilePosition(tileIndex);
                return Mathf.Abs(localPosition.x - tilePosition.x) <= columnSpacing * 0.5f
                    && Mathf.Abs(localPosition.z - tilePosition.z) <= rowSpacing * 0.5f
                    && GetTileVariant(row, column)?.TileObject != null;
            }

            public void ApplyGeneratedTileSettings(GameObject tile, int row, int column)
            {
                var variant = GetTileVariant(row, column);
                if (variant == null)
                {
                    return;
                }

                SetTileTag(tile, variant.GeneratedTag);
                ConfigureTileColliders(tile, variant.GenerateCollider);
            }
        }

        [Header("Terrain Blocks")]
        [SerializeField] private List<TerrainBlock> terrainBlocks = new();
        [SerializeField] private bool autoRebuildInEditor = true;
        [Header("Mobile Performance")]
        [SerializeField] private bool simplifyTileMeshesForMobile;

        private static readonly Dictionary<int, Mesh> simplifiedTileMeshes = new();
        private bool layoutPreparedBeforeAwake;

        private void Awake()
        {
            if (Application.isPlaying && !layoutPreparedBeforeAwake)
            {
                Rebuild();
            }
        }

        /// <summary>
        /// Resolves a UI brush through the configured preview texture, preserving tile tags and colliders.
        /// </summary>
        public int FindTileTypeForBrush(Sprite sprite, int blockIndex = 0)
        {
            return terrainBlocks != null && blockIndex >= 0 && blockIndex < terrainBlocks.Count
                ? terrainBlocks[blockIndex]?.FindTileType(sprite) ?? -1
                : -1;
        }

        public int CountTilesWithTag(string tag, int blockIndex = 0)
        {
            return terrainBlocks != null && blockIndex >= 0 && blockIndex < terrainBlocks.Count
                ? terrainBlocks[blockIndex]?.CountTilesWithTag(tag) ?? 0 : 0;
        }

        /// <summary>
        /// Copies a zero-based, row-major layout into a terrain block and generates its prefab tiles.
        /// Empty cells use -1. Invalid input leaves the existing layout unchanged.
        /// </summary>
        public bool ApplyLayout(int width, int height, IReadOnlyList<int> indices, int blockIndex = 0)
        {
            if (width < 1 || height < 1 || indices == null
                || (long)width * height != indices.Count
                || terrainBlocks == null || blockIndex < 0 || blockIndex >= terrainBlocks.Count
                || terrainBlocks[blockIndex] == null || !gameObject.scene.IsValid())
            {
                Debug.LogError("Terrain layout requires valid dimensions, a configured block and one index per cell.", this);
                return false;
            }

            var block = terrainBlocks[blockIndex];
            var copiedIndices = new List<int>(indices.Count);
            foreach (var index in indices)
            {
                if (index != -1 && !block.HasTileType(index))
                {
                    Debug.LogError($"Terrain layout contains an unconfigured tile type: {index}.", this);
                    return false;
                }
                copiedIndices.Add(index);
            }

            block.SetLayout(width, height, copiedIndices);
            Rebuild();
            layoutPreparedBeforeAwake = true;
            return true;
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying && autoRebuildInEditor)
            {
                UnityEditor.EditorApplication.delayCall += RebuildAfterValidation;
            }
#endif
        }

        [ContextMenu("Rebuild Tile Grid")]
        public void Rebuild()
        {
            if (!gameObject.scene.IsValid())
            {
                return;
            }

            // #region debug-point B:terrain-rebuild
            var rebuildStartedAt = Time.realtimeSinceStartup;
            var generatedTileCount = 0;
            // #endregion
            var generatedRoot = GetOrCreateGeneratedRoot();
            ClearChildren(generatedRoot);

            for (var blockIndex = 0; blockIndex < terrainBlocks.Count; blockIndex++)
            {
                var block = terrainBlocks[blockIndex];
                if (!block.HasTileVariants)
                {
                    continue;
                }

                block.EnsureTileLayout();
                var blockRoot = new GameObject($"{blockIndex:D2}_{block.BlockName}").transform;
                blockRoot.SetParent(generatedRoot, false);
                blockRoot.localPosition = block.LocalPosition;
                blockRoot.localRotation = Quaternion.identity;
                generatedTileCount += CreateBlockTiles(blockRoot, block, simplifyTileMeshesForMobile);
            }

            // #region debug-point B:terrain-rebuild
            ReportDebugEvent(
                "B",
                "TerrainTileGridGenerator.Rebuild",
                "[DEBUG] Terrain rebuild completed",
                $"durationMs={(Time.realtimeSinceStartup - rebuildStartedAt) * 1000f:F1};tiles={generatedTileCount};renderers={generatedRoot.GetComponentsInChildren<Renderer>(true).Length};colliders={generatedRoot.GetComponentsInChildren<Collider>(true).Length}");
            // #endregion
        }

        [ContextMenu("Clear Tile Grid")]
        public void Clear()
        {
            var generatedRoot = transform.Find(GeneratedRootName);
            if (generatedRoot != null)
            {
                ClearChildren(generatedRoot);
            }
        }

        public bool TryGetPathMarkers(out Transform start, out Transform destination)
        {
            start = FindMarkerWithTag(OriginMarkerTag);
            destination = FindMarkerWithTag(DestinationMarkerTag);
            return start != null && destination != null;
        }

        /// <summary>
        /// Checks whether a world point is directly above a configured grid tile.
        /// This works even when generated tiles intentionally have no Collider.
        /// </summary>
        public bool IsWorldPointOverGeneratedTile(Vector3 worldPosition, float maximumSurfaceDistance)
        {
            var localPosition = transform.InverseTransformPoint(worldPosition);
            foreach (var block in terrainBlocks)
            {
                if (block == null || !block.ContainsLocalTilePosition(localPosition))
                {
                    continue;
                }

                var surfacePoint = transform.TransformPoint(new Vector3(
                    localPosition.x,
                    block.SurfaceLocalY,
                    localPosition.z));
                var verticalDistance = Mathf.Abs(Vector3.Dot(
                    worldPosition - surfacePoint,
                    transform.up));
                if (verticalDistance <= maximumSurfaceDistance)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Finds a four-direction path that visits the supplied one-based Inspector type.
        /// The first and final steps may connect to adjacent non-walkable entry or destination tiles.
        /// </summary>
        public bool TryFindTilePath(
            Vector3 startWorldPosition,
            Vector3 destinationWorldPosition,
            int walkableTileType,
            List<Vector3> path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            path.Clear();
            var tileTypeIndex = walkableTileType - 1;
            if (tileTypeIndex < 0)
            {
                return false;
            }

            List<int> bestTilePath = null;
            TerrainBlock bestBlock = null;
            var appendDestinationToBestPath = false;
            var bestDistance = float.PositiveInfinity;

            for (var blockIndex = 0; blockIndex < terrainBlocks.Count; blockIndex++)
            {
                var block = terrainBlocks[blockIndex];
                block.EnsureTileLayout();

                var startTile = FindNearestTileOfType(block, startWorldPosition, tileTypeIndex);
                var destinationTile = FindNearestTileOfType(block, destinationWorldPosition, tileTypeIndex);
                if (startTile < 0 || destinationTile < 0)
                {
                    continue;
                }

                var startIsOnWalkableTile =
                    IsPositionOnTile(block, startTile, startWorldPosition);
                if (!startIsOnWalkableTile &&
                    !IsPositionAdjacentToTile(block, startTile, startWorldPosition))
                {
                    continue;
                }

                var destinationIsOnWalkableTile =
                    IsPositionOnTile(block, destinationTile, destinationWorldPosition);
                if (!destinationIsOnWalkableTile &&
                    !IsPositionAdjacentToTile(block, destinationTile, destinationWorldPosition))
                {
                    continue;
                }

                var candidatePath = FindPathInBlock(block, startTile, destinationTile, tileTypeIndex);
                if (candidatePath == null)
                {
                    continue;
                }

                var distance = Vector3.SqrMagnitude(
                    GetWorldTilePosition(block, startTile) - startWorldPosition)
                    + Vector3.SqrMagnitude(
                        GetWorldTilePosition(block, destinationTile) - destinationWorldPosition);
                if (distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                bestTilePath = candidatePath;
                bestBlock = block;
                appendDestinationToBestPath = !destinationIsOnWalkableTile;
            }

            if (bestTilePath == null)
            {
                return false;
            }

            for (var index = 0; index < bestTilePath.Count; index++)
            {
                path.Add(GetWorldTilePosition(bestBlock, bestTilePath[index]));
            }

            if (appendDestinationToBestPath)
            {
                path.Add(destinationWorldPosition);
            }

            return true;
        }

#if UNITY_EDITOR
        private void RebuildAfterValidation()
        {
            if (this != null && !Application.isPlaying && autoRebuildInEditor)
            {
                Rebuild();
            }
        }
#endif

        private Transform GetOrCreateGeneratedRoot()
        {
            var generatedRoot = transform.Find(GeneratedRootName);
            if (generatedRoot != null)
            {
                return generatedRoot;
            }

            var rootObject = new GameObject(GeneratedRootName);
            generatedRoot = rootObject.transform;
            generatedRoot.SetParent(transform, false);
            return generatedRoot;
        }

        private static int CreateBlockTiles(
            Transform blockRoot,
            TerrainBlock block,
            bool simplifyTileMeshes)
        {
            var tileCount = 0;
            var xOffset = (block.Columns - 1) * block.ColumnSpacing * 0.5f;
            var zOffset = (block.Rows - 1) * block.RowSpacing * 0.5f;
            for (var row = 0; row < block.Rows; row++)
            {
                for (var column = 0; column < block.Columns; column++)
                {
                    var tilePrefab = block.GetTilePrefab(row, column);
                    if (tilePrefab == null)
                    {
                        continue;
                    }

                    var tile = Instantiate(tilePrefab, blockRoot);
                    tile.name = $"Tile_{row}_{column}";
                    var tileTransform = tile.transform;
                    tileTransform.localPosition = new Vector3(
                        column * block.ColumnSpacing - xOffset,
                        0f,
                        row * block.RowSpacing - zOffset);
                    tileTransform.localRotation = Quaternion.Euler(block.TileEulerAngles);
                    tileTransform.localScale = block.TileScale;
                    block.ApplyGeneratedTileSettings(tile, row, column);
                    if (simplifyTileMeshes)
                    {
                        SimplifyTileMeshes(tile);
                    }

                    tileCount++;
                }
            }

            return tileCount;
        }

        private static void SetTileTag(GameObject tile, string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                tile.tag = "Untagged";
                return;
            }

            try
            {
                tile.tag = tag;
            }
            catch (UnityException)
            {
                Debug.LogWarning(
                    $"Terrain tile tag '{tag}' is not defined. '{tile.name}' was set to Untagged.",
                    tile);
                tile.tag = "Untagged";
            }
        }

        private static void ConfigureTileColliders(GameObject tile, bool generateCollider)
        {
            var colliders = tile.GetComponentsInChildren<Collider>(true);
            if (!generateCollider)
            {
                foreach (var collider in colliders)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(collider);
                    }
                    else
                    {
                        DestroyImmediate(collider);
                    }
                }

                return;
            }

            if (colliders.Length == 0)
            {
                var collider = tile.AddComponent<BoxCollider>();
                SetBoxColliderToRenderBounds(tile.transform, collider);
            }
        }

        private static void SetBoxColliderToRenderBounds(Transform tileTransform, BoxCollider collider)
        {
            var renderers = tileTransform.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                collider.center = Vector3.zero;
                collider.size = Vector3.one;
                return;
            }

            var bounds = new Bounds();
            var hasBounds = false;
            foreach (var renderer in renderers)
            {
                var rendererBounds = renderer.bounds;
                var minimum = rendererBounds.min;
                var maximum = rendererBounds.max;
                for (var x = 0; x <= 1; x++)
                {
                    for (var y = 0; y <= 1; y++)
                    {
                        for (var z = 0; z <= 1; z++)
                        {
                            var point = new Vector3(
                                x == 0 ? minimum.x : maximum.x,
                                y == 0 ? minimum.y : maximum.y,
                                z == 0 ? minimum.z : maximum.z);
                            point = tileTransform.InverseTransformPoint(point);
                            if (hasBounds)
                            {
                                bounds.Encapsulate(point);
                            }
                            else
                            {
                                bounds = new Bounds(point, Vector3.zero);
                                hasBounds = true;
                            }
                        }
                    }
                }
            }

            collider.center = bounds.center;
            collider.size = new Vector3(
                Mathf.Max(bounds.size.x, 0.001f),
                Mathf.Max(bounds.size.y, 0.001f),
                Mathf.Max(bounds.size.z, 0.001f));
        }

        private int FindNearestTileOfType(TerrainBlock block, Vector3 worldPosition, int tileTypeIndex)
        {
            var nearestTile = -1;
            var nearestDistance = float.PositiveInfinity;
            var tileCount = block.Rows * block.Columns;
            for (var tileIndex = 0; tileIndex < tileCount; tileIndex++)
            {
                if (!block.IsTileType(tileIndex, tileTypeIndex))
                {
                    continue;
                }

                var tilePosition = GetWorldTilePosition(block, tileIndex);
                var distance = (new Vector2(tilePosition.x, tilePosition.z)
                    - new Vector2(worldPosition.x, worldPosition.z)).sqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestTile = tileIndex;
                }
            }

            return nearestTile;
        }

        private bool IsPositionOnTile(TerrainBlock block, int tileIndex, Vector3 worldPosition)
        {
            var localPosition = transform.InverseTransformPoint(worldPosition);
            var tilePosition = block.GetLocalTilePosition(tileIndex);
            return Mathf.Abs(localPosition.x - tilePosition.x) <= block.ColumnSpacing * 0.5f
                && Mathf.Abs(localPosition.z - tilePosition.z) <= block.RowSpacing * 0.5f;
        }

        private bool IsPositionAdjacentToTile(TerrainBlock block, int tileIndex, Vector3 worldPosition)
        {
            var localPosition = transform.InverseTransformPoint(worldPosition);
            var tilePosition = block.GetLocalTilePosition(tileIndex);
            var horizontalNeighbor = Mathf.Abs(localPosition.x - tilePosition.x) <= block.ColumnSpacing * 1.5f
                && Mathf.Abs(localPosition.z - tilePosition.z) <= block.RowSpacing * 0.5f;
            var verticalNeighbor = Mathf.Abs(localPosition.x - tilePosition.x) <= block.ColumnSpacing * 0.5f
                && Mathf.Abs(localPosition.z - tilePosition.z) <= block.RowSpacing * 1.5f;
            return horizontalNeighbor || verticalNeighbor;
        }

        private List<int> FindPathInBlock(
            TerrainBlock block,
            int startTile,
            int destinationTile,
            int tileTypeIndex)
        {
            var tileCount = block.Rows * block.Columns;
            var costs = new float[tileCount];
            var previous = new int[tileCount];
            var visited = new bool[tileCount];
            var openTiles = new List<int> { startTile };
            for (var tileIndex = 0; tileIndex < tileCount; tileIndex++)
            {
                costs[tileIndex] = float.PositiveInfinity;
                previous[tileIndex] = -1;
            }

            costs[startTile] = 0f;
            while (openTiles.Count > 0)
            {
                var currentOpenIndex = 0;
                var currentTile = openTiles[0];
                var currentScore = costs[currentTile]
                    + GetGridDistance(block, currentTile, destinationTile);
                for (var openIndex = 1; openIndex < openTiles.Count; openIndex++)
                {
                    var candidateTile = openTiles[openIndex];
                    var candidateScore = costs[candidateTile]
                        + GetGridDistance(block, candidateTile, destinationTile);
                    if (candidateScore < currentScore)
                    {
                        currentOpenIndex = openIndex;
                        currentTile = candidateTile;
                        currentScore = candidateScore;
                    }
                }

                openTiles.RemoveAt(currentOpenIndex);
                if (currentTile == destinationTile)
                {
                    return BuildTilePath(previous, destinationTile);
                }

                visited[currentTile] = true;
                foreach (var neighborTile in GetOrthogonalNeighbors(block, currentTile))
                {
                    if (visited[neighborTile] || !block.IsTileType(neighborTile, tileTypeIndex))
                    {
                        continue;
                    }

                    var candidateCost = costs[currentTile] + 1f;
                    if (candidateCost >= costs[neighborTile])
                    {
                        continue;
                    }

                    costs[neighborTile] = candidateCost;
                    previous[neighborTile] = currentTile;
                    if (!openTiles.Contains(neighborTile))
                    {
                        openTiles.Add(neighborTile);
                    }
                }
            }

            return null;
        }

        private static List<int> BuildTilePath(int[] previous, int destinationTile)
        {
            var path = new List<int>();
            for (var tile = destinationTile; tile >= 0; tile = previous[tile])
            {
                path.Add(tile);
            }

            path.Reverse();
            return path;
        }

        private static IEnumerable<int> GetOrthogonalNeighbors(TerrainBlock block, int tileIndex)
        {
            var row = tileIndex / block.Columns;
            var column = tileIndex % block.Columns;
            if (column > 0)
            {
                yield return tileIndex - 1;
            }

            if (column < block.Columns - 1)
            {
                yield return tileIndex + 1;
            }

            if (row > 0)
            {
                yield return tileIndex - block.Columns;
            }

            if (row < block.Rows - 1)
            {
                yield return tileIndex + block.Columns;
            }
        }

        private static int GetGridDistance(TerrainBlock block, int firstTile, int secondTile)
        {
            var firstRow = firstTile / block.Columns;
            var firstColumn = firstTile % block.Columns;
            var secondRow = secondTile / block.Columns;
            var secondColumn = secondTile % block.Columns;
            return Mathf.Abs(firstRow - secondRow) + Mathf.Abs(firstColumn - secondColumn);
        }

        private Vector3 GetWorldTilePosition(TerrainBlock block, int tileIndex)
        {
            return transform.TransformPoint(block.GetLocalTilePosition(tileIndex));
        }

        private static void SimplifyTileMeshes(GameObject tile)
        {
            foreach (var meshFilter in tile.GetComponentsInChildren<MeshFilter>(true))
            {
                var sourceMesh = meshFilter.sharedMesh;
                if (sourceMesh == null)
                {
                    continue;
                }

                var sourceMeshId = sourceMesh.GetInstanceID();
                if (!simplifiedTileMeshes.TryGetValue(sourceMeshId, out var simplifiedMesh))
                {
                    var bounds = sourceMesh.bounds;
                    simplifiedMesh = new Mesh
                    {
                        name = $"{sourceMesh.name}_MobileTile"
                    };
                    simplifiedMesh.vertices = new[]
                    {
                        new Vector3(bounds.min.x, bounds.min.y, bounds.max.z),
                        new Vector3(bounds.max.x, bounds.min.y, bounds.max.z),
                        new Vector3(bounds.max.x, bounds.max.y, bounds.max.z),
                        new Vector3(bounds.min.x, bounds.max.y, bounds.max.z)
                    };
                    simplifiedMesh.uv = new[]
                    {
                        new Vector2(0f, 0f),
                        new Vector2(1f, 0f),
                        new Vector2(1f, 1f),
                        new Vector2(0f, 1f)
                    };
                    simplifiedMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                    simplifiedMesh.RecalculateNormals();
                    simplifiedMesh.RecalculateBounds();
                    simplifiedTileMeshes[sourceMeshId] = simplifiedMesh;
                }

                meshFilter.sharedMesh = simplifiedMesh;
            }
        }

        // #region debug-point A:remote-event
        public static void ReportDebugEvent(string hypothesisId, string location, string message, string data)
        {
            var payload = new DebugEventPayload
            {
                sessionId = DebugSessionId,
                runId = "post-fix",
                hypothesisId = hypothesisId,
                ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                location = location,
                msg = message,
                data = data
            };
            var request = new UnityWebRequest(DebugServerUrl, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload))),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = 1
            };
            request.SetRequestHeader("Content-Type", "application/json");
            request.SendWebRequest();
        }
        // #endregion

        private static void ClearChildren(Transform root)
        {
            for (var index = root.childCount - 1; index >= 0; index--)
            {
                if (Application.isPlaying)
                {
                    var child = root.GetChild(index).gameObject;
                    child.SetActive(false);
                    Destroy(child);
                }
                else
                {
                    DestroyImmediate(root.GetChild(index).gameObject);
                }
            }
        }

        private Transform FindMarkerWithTag(string markerTag)
        {
            var root = transform.Find(GeneratedRootName);
            if (root == null)
                return null;
            try
            {
                foreach (var marker in root.GetComponentsInChildren<Transform>(true))
                {
                    var enabledInGrid = true;
                    for (var ancestor = marker; ancestor != root; ancestor = ancestor.parent)
                    {
                        if (!ancestor.gameObject.activeSelf)
                        {
                            enabledInGrid = false;
                            break;
                        }
                    }
                    if (enabledInGrid && marker.CompareTag(markerTag))
                        return marker;
                }
                return null;
            }
            catch (UnityException)
            {
                return null;
            }
        }
    }
}
