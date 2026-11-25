using MRStrategyRTS.Battlefield;
using MRStrategyRTS.Input;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace MRStrategyRTS.Shared
{
    /// <summary>
    /// Creates an editor-playable MVP arena. Replace its primitives with authored prefabs later.
    /// </summary>
    public sealed class SkirmishSceneBootstrap : MonoBehaviour
    {
        [SerializeField] private bool buildEnvironmentOnStart = true;
        [SerializeField] private bool buildNavMeshOnStart = true;
        [SerializeField] private float tabletopWidth = 12f;
        [SerializeField] private float tabletopDepth = 9f;

        private void Awake()
        {
            if (!buildEnvironmentOnStart)
            {
                return;
            }

            EnsureCamera();
            EnsureCoreControllers();
            BuildEnvironment();
        }

        private void EnsureCamera()
        {
            if (Camera.main != null)
            {
                return;
            }

            var cameraObject = new GameObject("Editor RTS Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetPositionAndRotation(
                new Vector3(0f, 10.5f, -10.5f),
                Quaternion.Euler(42f, 0f, 0f));
            cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
        }

        private void EnsureCoreControllers()
        {
            if (GetComponent<GameBootstrap>() == null)
            {
                gameObject.AddComponent<GameBootstrap>();
            }

            if (GetComponent<RTSSelectionController>() == null)
            {
                gameObject.AddComponent<RTSSelectionController>();
            }

            GetComponent<RTSSelectionController>().SetInteractionCamera(Camera.main);
        }

        private void BuildEnvironment()
        {
            if (transform.Find("Battlefield") != null)
            {
                return;
            }

            var battlefield = new GameObject("Battlefield");
            battlefield.transform.SetParent(transform);
            battlefield.AddComponent<BattlefieldPlacementController>();

            CreatePrimitive(PrimitiveType.Cube, "Floating Diorama",
                battlefield.transform, new Vector3(0f, 0f, 0f),
                new Vector3(tabletopWidth, 0.5f, tabletopDepth), new Color(0.13f, 0.15f, 0.17f));
            CreatePrimitive(PrimitiveType.Cube, "Playable Terrain",
                battlefield.transform, new Vector3(0f, 0.34f, 0f),
                new Vector3(tabletopWidth - 0.35f, 0.2f, tabletopDepth - 0.35f), new Color(0.13f, 0.31f, 0.10f));
            var river = CreatePrimitive(PrimitiveType.Cube, "River",
                battlefield.transform, new Vector3(0f, 0.48f, 0f),
                new Vector3(1.5f, 0.05f, tabletopDepth - 0.9f), new Color(0.02f, 0.35f, 0.75f));
            var riverModifier = river.AddComponent<NavMeshModifier>();
            riverModifier.overrideArea = true;
            riverModifier.area = NavMesh.GetAreaFromName("Not Walkable");

            CreateBridge(battlefield.transform, new Vector3(0f, 0.66f, -2.5f));
            CreateBridge(battlefield.transform, new Vector3(0f, 0.66f, 0f));
            CreateBridge(battlefield.transform, new Vector3(0f, 0.66f, 2.5f));
            CreateRoad(battlefield.transform, new Vector3(-3.1f, 0.58f, -2.7f), new Vector3(3.9f, 0.06f, 0.34f), -13f);
            CreateRoad(battlefield.transform, new Vector3(3.1f, 0.58f, 2.7f), new Vector3(3.9f, 0.06f, 0.34f), -13f);
            CreateRoad(battlefield.transform, new Vector3(-3.1f, 0.58f, 2.6f), new Vector3(3.6f, 0.06f, 0.34f), 12f);
            CreateRoad(battlefield.transform, new Vector3(3.1f, 0.58f, -2.6f), new Vector3(3.6f, 0.06f, 0.34f), 12f);

            CreateBase(battlefield.transform, "Alliance Base", new Vector3(-4.15f, 0.6f, -2.9f), new Color(0.03f, 0.18f, 0.85f));
            CreateBase(battlefield.transform, "Legion Base", new Vector3(4.15f, 0.6f, 2.9f), new Color(0.85f, 0.05f, 0.03f));
            CreateResourceField(battlefield.transform, new Vector3(-3.85f, 0.6f, 3.0f));
            CreateResourceField(battlefield.transform, new Vector3(3.85f, 0.6f, -3.0f));
            CreateResourceField(battlefield.transform, new Vector3(-2.3f, 0.6f, 0.9f));
            CreateResourceField(battlefield.transform, new Vector3(2.3f, 0.6f, -0.9f));
            CreateForest(battlefield.transform);
            CreateRockRidge(battlefield.transform);
            CreateDefenseTurret(battlefield.transform, new Vector3(-3.8f, 0.7f, -0.5f), new Color(0.03f, 0.18f, 0.85f));
            CreateDefenseTurret(battlefield.transform, new Vector3(-4.8f, 0.7f, 1.2f), new Color(0.03f, 0.18f, 0.85f));
            CreateDefenseTurret(battlefield.transform, new Vector3(3.8f, 0.7f, 0.5f), new Color(0.85f, 0.05f, 0.03f));
            CreateDefenseTurret(battlefield.transform, new Vector3(4.8f, 0.7f, -1.2f), new Color(0.85f, 0.05f, 0.03f));
            CreateLight(battlefield.transform);
            ConfigureNavMesh(battlefield);
        }

        private static void CreateBase(Transform parent, string baseName, Vector3 position, Color color)
        {
            var foundation = CreatePrimitive(PrimitiveType.Cylinder, baseName, parent, position,
                new Vector3(1.1f, 0.15f, 1.1f), Color.gray);
            foundation.transform.position += Vector3.up * 0.1f;
            CreatePrimitive(PrimitiveType.Cylinder, $"{baseName} Command Core", parent,
                position + Vector3.up * 0.62f, new Vector3(0.48f, 0.55f, 0.48f), color);
            CreatePrimitive(PrimitiveType.Cylinder, $"{baseName} Beacon", parent,
                position + Vector3.up * 1.5f, new Vector3(0.08f, 0.42f, 0.08f), Color.white);

            for (var i = 0; i < 4; i++)
            {
                var angle = i * 90f * Mathf.Deg2Rad;
                var offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.86f;
                CreatePrimitive(PrimitiveType.Cube, $"{baseName} Module {i + 1}", parent,
                    position + offset + Vector3.up * 0.27f, new Vector3(0.36f, 0.28f, 0.36f), color);
            }
        }

        private static void CreateResourceField(Transform parent, Vector3 position)
        {
            var resource = CreatePrimitive(PrimitiveType.Cylinder, "Resource Node", parent,
                position, new Vector3(0.62f, 0.1f, 0.62f), Color.gray);
            resource.transform.position += Vector3.up * 0.1f;

            for (var i = 0; i < 5; i++)
            {
                var angle = i * 72f * Mathf.Deg2Rad;
                var offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.28f;
                CreatePrimitive(PrimitiveType.Capsule, $"Ore Crystal {i + 1}", parent,
                    position + offset + Vector3.up * 0.56f, new Vector3(0.15f, 0.45f, 0.15f), new Color(1f, 0.52f, 0.02f));
            }
        }

        private static void CreateBridge(Transform parent, Vector3 position)
        {
            CreatePrimitive(PrimitiveType.Cube, "Bridge Deck", parent, position,
                new Vector3(2.05f, 0.16f, 0.7f), new Color(0.25f, 0.27f, 0.29f));
            CreatePrimitive(PrimitiveType.Cube, "Bridge Guard Left", parent,
                position + new Vector3(0f, 0.2f, -0.62f), new Vector3(2.05f, 0.08f, 0.05f), new Color(0.04f, 0.42f, 0.9f));
            CreatePrimitive(PrimitiveType.Cube, "Bridge Guard Right", parent,
                position + new Vector3(0f, 0.2f, 0.62f), new Vector3(2.05f, 0.08f, 0.05f), new Color(0.04f, 0.42f, 0.9f));
        }

        private static void CreateRoad(Transform parent, Vector3 position, Vector3 scale, float rotationY)
        {
            var road = CreatePrimitive(PrimitiveType.Cube, "Tactical Road", parent, position, scale, new Color(0.16f, 0.15f, 0.12f));
            road.transform.localRotation = Quaternion.Euler(0f, rotationY, 0f);
        }

        private static void CreateForest(Transform parent)
        {
            var treePositions = new[]
            {
                new Vector3(-5f, .6f, 3.5f), new Vector3(-4.3f, .6f, 3.8f), new Vector3(-5.2f, .6f, 2.5f),
                new Vector3(-2.8f, .6f, 3.9f), new Vector3(-2.4f, .6f, -3.8f), new Vector3(-1.7f, .6f, -3.6f),
                new Vector3(5f, .6f, -3.5f), new Vector3(4.3f, .6f, -3.8f), new Vector3(5.2f, .6f, -2.5f),
                new Vector3(2.8f, .6f, -3.9f), new Vector3(2.5f, .6f, 3.8f), new Vector3(1.8f, .6f, 3.6f)
            };

            for (var i = 0; i < treePositions.Length; i++)
            {
                var position = treePositions[i];
                CreatePrimitive(PrimitiveType.Cylinder, "Pine Trunk", parent,
                    position + Vector3.up * 0.24f, new Vector3(0.05f, 0.25f, 0.05f), new Color(0.22f, 0.1f, 0.03f));
                CreatePrimitive(PrimitiveType.Capsule, "Pine Canopy", parent,
                    position + Vector3.up * 0.65f, new Vector3(0.3f, 0.52f, 0.3f), new Color(0.02f, 0.22f, 0.06f));
            }
        }

        private static void CreateRockRidge(Transform parent)
        {
            var ridgePositions = new[]
            {
                new Vector3(-5.2f, .6f, -3.8f), new Vector3(-4.8f, .6f, -3.9f), new Vector3(-4.2f, .6f, -4f),
                new Vector3(-3.4f, .6f, 3.8f), new Vector3(-2.7f, .6f, 4f), new Vector3(5.2f, .6f, 3.8f),
                new Vector3(4.6f, .6f, 4f), new Vector3(3.7f, .6f, -3.9f), new Vector3(3f, .6f, -4f)
            };

            foreach (var position in ridgePositions)
            {
                CreatePrimitive(PrimitiveType.Sphere, "Rock Outcrop", parent,
                    position + Vector3.up * 0.32f, new Vector3(0.52f, 0.64f, 0.4f), new Color(0.36f, 0.29f, 0.21f));
            }
        }

        private static void CreateDefenseTurret(Transform parent, Vector3 position, Color factionColor)
        {
            CreatePrimitive(PrimitiveType.Cylinder, "Defense Pad", parent,
                position, new Vector3(0.38f, 0.12f, 0.38f), Color.gray);
            CreatePrimitive(PrimitiveType.Cylinder, "Defense Turret", parent,
                position + Vector3.up * 0.32f, new Vector3(0.16f, 0.25f, 0.16f), factionColor);
            CreatePrimitive(PrimitiveType.Cube, "Defense Cannon", parent,
                position + new Vector3(0.2f, 0.53f, 0f), new Vector3(0.28f, 0.07f, 0.08f), factionColor);
        }

        private void CreateLight(Transform parent)
        {
            var lightObject = new GameObject("Battlefield Key Light");
            lightObject.transform.SetParent(parent);
            lightObject.transform.SetPositionAndRotation(new Vector3(-3f, 8f, -4f), Quaternion.Euler(55f, -25f, 0f));
            var sceneLight = lightObject.AddComponent<Light>();
            sceneLight.type = LightType.Directional;
            sceneLight.intensity = 1.4f;
            sceneLight.color = new Color(1f, 0.88f, 0.72f);
        }

        private void ConfigureNavMesh(GameObject battlefield)
        {
            var surface = battlefield.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~0;
            surface.agentTypeID = 0;

            if (buildNavMeshOnStart)
            {
                surface.BuildNavMesh();
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
            var renderer = primitive.GetComponent<Renderer>();
            renderer.material.color = color;
            return primitive;
        }
    }
}
