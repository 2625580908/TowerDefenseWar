using System;
using MRStrategyRTS.AI;
using UnityEngine;
using UnityEngine.Events;

namespace MRStrategyRTS.Shared
{
    /// <summary>
    /// Small test helper that instantiates exactly one enemy and emits a spawn event.
    /// </summary>
    public sealed class SpawnEnemyOnceTest : MonoBehaviour
    {
        [Serializable]
        public sealed class EnemySpawnedEvent : UnityEvent<GameObject>
        {
        }

        [Header("Spawn")]
        [SerializeField] private GameObject enemyPrefab;
        [SerializeField] private Transform spawnParent;
        [SerializeField] private bool spawnOnStart = true;
        [Tooltip("Local offset relative to the Origin tagged tile.")]
        public Vector3 spawnLocalOffset;

        [Header("Event")]
        [SerializeField] private EnemySpawnedEvent onEnemySpawned = new();

        public event Action<GameObject> EnemySpawned;
        public GameObject SpawnedEnemy { get; private set; }

        private void Start()
        {
            if (spawnOnStart)
            {
                SpawnOnce();
            }
        }

        [ContextMenu("Spawn Enemy Once")]
        public void SpawnOnce()
        {
            if (enemyPrefab == null)
            {
                Debug.LogWarning("SpawnEnemyOnceTest requires an Enemy Prefab.", this);
                return;
            }

            if (spawnParent == null)
            {
                Debug.LogWarning("SpawnEnemyOnceTest requires a Spawn Parent.", this);
                return;
            }

            var terrain = spawnParent.GetComponentInParent<TerrainTileGridGenerator>();
            if (terrain == null || !terrain.TryGetPathMarkers(out var start, out _))
            {
                Debug.LogWarning(
                    "SpawnEnemyOnceTest requires a Terrain Tile Grid Generator with Origin and Destination tagged objects.",
                    this);
                return;
            }

            SpawnedEnemy = Instantiate(
                enemyPrefab,
                start.TransformPoint(spawnLocalOffset),
                Quaternion.identity,
                spawnParent);
            SpawnedEnemy.name = $"{enemyPrefab.name} (Test Enemy)";
            var pathWalker = SpawnedEnemy.GetComponentInChildren<MonsterPathWalker>();
            if (pathWalker != null)
            {
                pathWalker.SetPath(terrain);
            }

            onEnemySpawned.Invoke(SpawnedEnemy);
            EnemySpawned?.Invoke(SpawnedEnemy);
        }
    }
}
