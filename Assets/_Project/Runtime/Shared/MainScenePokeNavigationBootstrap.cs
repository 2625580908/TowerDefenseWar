using System.Collections.Generic;
using MRStrategyRTS.RTS;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace MRStrategyRTS.Shared
{
    /// <summary>
    /// Provides navigation for the small physical tabletop used by the Main MR scene.
    /// </summary>
    public sealed class MainScenePokeNavigationBootstrap : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float agentRadius = 0.04f;
        [SerializeField, Min(0.05f)] private float agentHeight = 0.2f;
        [SerializeField, Min(0.05f)] private float navMeshSampleRadius = 5f;
        // #region debug-point D:frame-stall-sample
        private float nextFrameSampleAt;
        private float maxFrameDuration;
        // #endregion

        private void Start()
        {
            var terrain = FindTerrain();
            if (terrain == null)
            {
                Debug.LogWarning("Poke navigation requires an active object tagged Terrain.", this);
                return;
            }

            BuildTerrainNavMesh(terrain);
            ConfigureFriendlyAgents();
            ConfigureCombatUnits();
        }

        // #region debug-point D:frame-stall-sample
        private void Update()
        {
            maxFrameDuration = Mathf.Max(maxFrameDuration, Time.unscaledDeltaTime);
            if (Time.unscaledTime < nextFrameSampleAt)
            {
                return;
            }

            TerrainTileGridGenerator.ReportDebugEvent(
                "D",
                "MainScenePokeNavigationBootstrap.Update",
                "[DEBUG] Frame sample",
                $"maxFrameMs={maxFrameDuration * 1000f:F1}");
            maxFrameDuration = 0f;
            nextFrameSampleAt = Time.unscaledTime + 2f;
        }
        // #endregion

        private GameObject FindTerrain()
        {
            try
            {
                return GameObject.FindGameObjectWithTag(BaseData.Instance.TerrainTag);
            }
            catch (UnityException)
            {
                return null;
            }
        }

        private static void BuildTerrainNavMesh(GameObject terrain)
        {
            // #region debug-point A/C:navmesh-build
            var navMeshBuildStartedAt = Time.realtimeSinceStartup;
            // #endregion
            var surface = terrain.GetComponent<NavMeshSurface>() ?? terrain.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;

            // CollectObjects.All guarantees the terrain root collider is included. Disable every
            // other collider while building so units and the XR rig cannot become walkable areas.
            var colliders = Object.FindObjectsByType<Collider>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            var colliderStates = new List<(Collider collider, bool enabled)>();
            foreach (var collider in colliders)
            {
                if (collider.transform.IsChildOf(terrain.transform))
                {
                    continue;
                }

                colliderStates.Add((collider, collider.enabled));
                collider.enabled = false;
            }

            try
            {
                surface.BuildNavMesh();
                var navMeshVertexCount = NavMesh.CalculateTriangulation().vertices.Length;
                Debug.Log("[PokeNavigation] NavMesh vertices: " + navMeshVertexCount, terrain);
                // #region debug-point A/C:navmesh-build
                TerrainTileGridGenerator.ReportDebugEvent(
                    "A",
                    "MainScenePokeNavigationBootstrap.BuildTerrainNavMesh",
                    "[DEBUG] NavMesh build completed",
                    $"durationMs={(Time.realtimeSinceStartup - navMeshBuildStartedAt) * 1000f:F1};sceneColliders={colliders.Length};disabledColliders={colliderStates.Count};vertices={navMeshVertexCount}");
                // #endregion
            }
            finally
            {
                foreach (var state in colliderStates)
                {
                    state.collider.enabled = state.enabled;
                }
            }
        }

        private void ConfigureFriendlyAgents()
        {
            GameObject[] friendlyObjects;
            try
            {
                friendlyObjects = GameObject.FindGameObjectsWithTag(BaseData.Instance.OneselfTag);
            }
            catch (UnityException)
            {
                return;
            }

            foreach (var friendlyObject in friendlyObjects)
            {
                DetachFromTerrainScale(friendlyObject);
                if (!TryConfigureAgent(friendlyObject))
                {
                    Debug.LogWarning("Poke navigation could not place unit on NavMesh: " + friendlyObject.name, this);
                }
            }
        }

        private static void DetachFromTerrainScale(GameObject friendlyObject)
        {
            var transform = friendlyObject.transform;
            if (transform.parent != null)
            {
                // The tabletop is deliberately flattened on Y. Keep the unit's world transform
                // but remove that non-uniform parent scale before it is moved independently.
                transform.SetParent(null, true);
            }
        }

        public bool TryConfigureAgent(GameObject friendlyObject)
        {
            if (friendlyObject == null ||
                !NavMesh.SamplePosition(
                    friendlyObject.transform.position,
                    out var navHit,
                    navMeshSampleRadius,
                    NavMesh.AllAreas))
            {
                return false;
            }

            var rigidbody = friendlyObject.GetComponent<Rigidbody>();
            if (rigidbody != null)
            {
                rigidbody.isKinematic = true;
                rigidbody.useGravity = false;
            }

            DetachFromTerrainScale(friendlyObject);
            friendlyObject.transform.position = navHit.position;
            var agent = friendlyObject.GetComponent<NavMeshAgent>() ?? friendlyObject.AddComponent<NavMeshAgent>();
            agent.radius = agentRadius;
            agent.height = agentHeight;
            agent.baseOffset = agentHeight * 0.5f;
            agent.speed = 0.35f;
            agent.acceleration = 2f;
            agent.angularSpeed = 720f;
            agent.stoppingDistance = agentRadius;
            Debug.Log("[PokeNavigation] Ready: " + friendlyObject.name + " | on NavMesh: " + agent.isOnNavMesh, this);
            return agent.isOnNavMesh;
        }

        private void ConfigureCombatUnits()
        {
            ConfigureCombatUnitsWithTag(BaseData.Instance.OneselfTag);
            ConfigureCombatUnitsWithTag(BaseData.Instance.EnemyTag);
        }

        private static void ConfigureCombatUnitsWithTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                return;
            }

            try
            {
                foreach (var unitObject in GameObject.FindGameObjectsWithTag(tag))
                {
                    if (unitObject.GetComponent<UnitCombat>() == null)
                    {
                        unitObject.AddComponent<UnitCombat>();
                    }
                }
            }
            catch (UnityException)
            {
                Debug.LogWarning("Combat setup skipped because tag does not exist: " + tag);
            }
        }
    }
}
