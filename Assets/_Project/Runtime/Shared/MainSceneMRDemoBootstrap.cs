using MRStrategyRTS.Input;
using MRStrategyRTS.RTS;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace MRStrategyRTS.Shared
{
    /// <summary>
    /// Builds a 50 by 50 metre logical tabletop with a controllable squad for MR interaction tests.
    /// </summary>
    public sealed class MainSceneMRDemoBootstrap : MonoBehaviour
    {
        [SerializeField, Min(10f)] private float mapSize = 50f;
        [SerializeField, Range(1, 24)] private int squadSize = 8;

        private void Awake()
        {
            if (transform.Find("MR Tactical Table") != null)
            {
                return;
            }

            var selection = GetComponent<RTSSelectionController>() ?? gameObject.AddComponent<RTSSelectionController>();
            selection.SetInteractionCamera(Camera.main);
            if (GetComponent<PokeMoveInteractionZone>() == null)
            {
                gameObject.AddComponent<PokeMoveInteractionZone>();
            }

            var pinchCommander = GetComponent<HandPinchCommander>();
            if (pinchCommander != null)
            {
                pinchCommander.enabled = false;
            }

            BuildTable();
        }

        private void BuildTable()
        {
            var root = new GameObject("MR Tactical Table").transform;
            root.SetParent(transform);

            var slab = CreatePrimitive(PrimitiveType.Cube, "Tabletop Base", root, Vector3.zero,
                new Vector3(mapSize + 2f, 0.7f, mapSize + 2f), new Color(0.07f, 0.09f, 0.12f));
            slab.transform.localPosition = Vector3.down * 0.35f;

            var ground = CreatePrimitive(PrimitiveType.Plane, "50x50 Command Grid", root, Vector3.zero,
                Vector3.one * (mapSize / 10f), new Color(0.16f, 0.33f, 0.16f));
            AddGrid(root);
            AssignTagRecursively(root, BaseData.Instance.TerrainTag);

            var surface = root.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();

            var squad = new GameObject("Blue Squad");
            squad.transform.SetParent(root);
            squad.AddComponent<RTSSquad>();
            for (var i = 0; i < squadSize; i++)
            {
                var column = i % 4;
                var row = i / 4;
                CreateSoldier(
                    squad.transform,
                    new Vector3(-12f + column * 1.2f, 0.75f, -7f + row * 1.2f),
                    i + 1,
                    BaseData.Instance.OneselfTag);
            }
            squad.GetComponent<RTSSquad>().RefreshUnits();

            CreateTargetMarker(root, new Vector3(12f, 0.08f, 7f));
            CreateBoundaryPillars(root);
        }

        private void AddGrid(Transform parent)
        {
            for (var i = -25; i <= 25; i += 5)
            {
                CreatePrimitive(PrimitiveType.Cube, "Grid X", parent, new Vector3(i, 0.015f, 0f),
                    new Vector3(0.035f, 0.02f, mapSize), new Color(0.05f, 0.48f, 0.42f));
                CreatePrimitive(PrimitiveType.Cube, "Grid Z", parent, new Vector3(0f, 0.018f, i),
                    new Vector3(mapSize, 0.02f, 0.035f), new Color(0.05f, 0.48f, 0.42f));
            }
        }

        private static void CreateSoldier(Transform parent, Vector3 position, int number, string unitTag)
        {
            var soldier = CreatePrimitive(PrimitiveType.Capsule, $"Blue Soldier {number}", parent, position,
                new Vector3(0.45f, 0.75f, 0.45f), new Color(0.03f, 0.25f, 0.92f));
            AssignTag(soldier, unitTag);
            var agent = soldier.AddComponent<NavMeshAgent>();
            agent.radius = 0.28f;
            agent.height = 1.3f;
            agent.speed = 4.5f;
            agent.acceleration = 14f;
            agent.angularSpeed = 540f;
            agent.stoppingDistance = 0.15f;
            soldier.AddComponent<RTSUnit>();
        }

        private static void CreateTargetMarker(Transform parent, Vector3 position)
        {
            CreatePrimitive(PrimitiveType.Cylinder, "Move Target", parent, position,
                new Vector3(1.2f, 0.05f, 1.2f), new Color(1f, 0.55f, 0.02f));
            CreatePrimitive(PrimitiveType.Cylinder, "Move Target Core", parent, position + Vector3.up * 0.08f,
                new Vector3(0.55f, 0.07f, 0.55f), Color.white);
        }

        private void CreateBoundaryPillars(Transform parent)
        {
            var extent = mapSize * 0.5f;
            foreach (var position in new[]
                     {
                         new Vector3(-extent, 0.5f, -extent), new Vector3(extent, 0.5f, -extent),
                         new Vector3(-extent, 0.5f, extent), new Vector3(extent, 0.5f, extent)
                     })
            {
                CreatePrimitive(PrimitiveType.Cylinder, "Table Anchor", parent, position,
                    new Vector3(0.35f, 0.5f, 0.35f), new Color(0.05f, 0.75f, 1f));
            }
        }

        private static GameObject CreatePrimitive(
            PrimitiveType type,
            string objectName,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Color color)
        {
            var primitive = GameObject.CreatePrimitive(type);
            primitive.name = objectName;
            primitive.transform.SetParent(parent);
            primitive.transform.localPosition = localPosition;
            primitive.transform.localScale = localScale;
            primitive.GetComponent<Renderer>().material.color = color;
            return primitive;
        }

        private static void AssignTagRecursively(Transform root, string tag)
        {
            AssignTag(root.gameObject, tag);
            foreach (Transform child in root)
            {
                AssignTagRecursively(child, tag);
            }
        }

        private static void AssignTag(GameObject target, string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                return;
            }

            try
            {
                target.tag = tag;
            }
            catch (UnityException)
            {
                Debug.LogWarning($"Tag '{tag}' is not defined. Add it in Project Settings > Tags and Layers.");
            }
        }
    }
}
