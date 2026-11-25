using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace MRStrategyRTS.Editor
{
    internal static class SkirmishNavMeshBaker
    {
        [MenuItem("MR Strategy/Bake Skirmish NavMesh")]
        private static void Bake()
        {
            var battlefield = GameObject.Find("Battlefield");
            if (battlefield == null)
            {
                Debug.LogError("No Battlefield root found. Enter Play Mode once to validate the generated MVP, or add a Battlefield root containing your authored terrain.");
                return;
            }

            var surface = battlefield.GetComponent<NavMeshSurface>();
            if (surface == null)
            {
                surface = Undo.AddComponent<NavMeshSurface>(battlefield);
            }

            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~0;
            surface.agentTypeID = 0;
            surface.BuildNavMesh();

            EditorSceneManager.MarkSceneDirty(battlefield.scene);
            Debug.Log("Skirmish NavMesh baked from Battlefield child colliders.");
        }

        [MenuItem("MR Strategy/Bake Skirmish NavMesh", true)]
        private static bool ValidateBake() => !Application.isPlaying;
    }
}
