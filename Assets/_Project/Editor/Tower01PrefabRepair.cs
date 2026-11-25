using MRStrategyRTS.Config;
using MRStrategyRTS.RTS;
using MRStrategyRTS.Shared;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace MRStrategyRTS.Editor
{
    /// <summary>
    /// Restores the runtime components on the Tower01 prefab after model-prefab reimports.
    /// </summary>
    [InitializeOnLoad]
    internal static class Tower01PrefabRepair
    {
        private const string TowerPrefabPath = "Assets/Prefabs/Minecraft Tower Defense/Tower01.prefab";
        private const string TowerDefinitionPath = "Assets/_Project/Data/Towers/Tower01.asset";
        private const string HoverPrefabPath = "Assets/Prefabs/Minecraft Tower Defense/Tower01 Hover.prefab";

        static Tower01PrefabRepair()
        {
            EditorApplication.delayCall += RestoreIfNeeded;
        }

        [MenuItem("Tools/MR Strategy RTS/Repair Tower01 Prefab")]
        private static void RestoreFromMenu()
        {
            RestoreTowerPrefab(true);
            RestoreHoverPrefab(true);
        }

        private static void RestoreIfNeeded()
        {
            RestoreTowerPrefab(false);
            RestoreHoverPrefab(false);
        }

        private static void RestoreTowerPrefab(bool forceSave)
        {
            var root = PrefabUtility.LoadPrefabContents(TowerPrefabPath);
            if (root == null)
            {
                Debug.LogError($"Cannot load {TowerPrefabPath}.");
                return;
            }

            try
            {
                root = EnsureRuntimeRoot(root, "Tower01");
                var changed = RemoveDuplicateRuntimeComponents(root);
                var tower = root.GetComponent<DefenseTower>();
                if (tower == null)
                {
                    tower = root.AddComponent<DefenseTower>();
                    changed = true;
                }

                var rigidbody = root.GetComponent<Rigidbody>();
                if (rigidbody == null)
                {
                    rigidbody = root.AddComponent<Rigidbody>();
                    changed = true;
                }

                rigidbody.isKinematic = true;
                rigidbody.useGravity = false;

                var collider = root.GetComponent<BoxCollider>();
                if (collider == null)
                {
                    collider = root.AddComponent<BoxCollider>();
                    changed = true;
                    FitColliderToRenderers(root.transform, collider);
                }

                var grabInteractable = root.GetComponent<XRGrabInteractable>();
                if (grabInteractable == null)
                {
                    grabInteractable = root.AddComponent<XRGrabInteractable>();
                    changed = true;
                }

                var placement = root.GetComponent<XRGrabSnapPlacement>();
                if (placement == null)
                {
                    placement = root.AddComponent<XRGrabSnapPlacement>();
                    changed = true;
                }

                var towerDefinition = AssetDatabase.LoadAssetAtPath<TowerDefinition>(TowerDefinitionPath);
                var hoverPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HoverPrefabPath);
                changed |= AssignRuntimeReferences(tower, placement, grabInteractable, collider, towerDefinition, hoverPrefab);

                if (changed || forceSave)
                {
                    var savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, TowerPrefabPath);
                    if (towerDefinition != null && towerDefinition.prefab != savedPrefab)
                    {
                        towerDefinition.prefab = savedPrefab;
                        EditorUtility.SetDirty(towerDefinition);
                    }

                    AssetDatabase.SaveAssets();
                    Debug.Log($"Restored Tower01 runtime components on '{TowerPrefabPath}'.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static bool RemoveDuplicateRuntimeComponents(GameObject root)
        {
            var changed = false;
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child == root.transform)
                {
                    continue;
                }

                changed |= RemoveComponent<XRGrabSnapPlacement>(child.gameObject);
                changed |= RemoveComponent<XRGrabInteractable>(child.gameObject);
                changed |= RemoveComponent<DefenseTower>(child.gameObject);
                changed |= RemoveComponent<Rigidbody>(child.gameObject);

                foreach (var collider in child.GetComponents<Collider>())
                {
                    Object.DestroyImmediate(collider);
                    changed = true;
                }

                foreach (var behaviour in child.GetComponents<MonoBehaviour>())
                {
                    if (behaviour != null && behaviour.GetType().Name == "XRGeneralGrabTransformer")
                    {
                        Object.DestroyImmediate(behaviour);
                        changed = true;
                    }
                }
            }

            return changed;
        }

        private static bool RemoveComponent<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();
            if (component == null)
            {
                return false;
            }

            Object.DestroyImmediate(component);
            return true;
        }

        private static GameObject EnsureRuntimeRoot(GameObject root, string rootName)
        {
            if (root.GetComponent<MeshFilter>() == null)
            {
                return root;
            }

            var runtimeRoot = new GameObject(rootName);
            runtimeRoot.transform.SetPositionAndRotation(
                root.transform.position,
                root.transform.rotation);
            runtimeRoot.transform.localScale = root.transform.localScale;
            root.transform.SetParent(runtimeRoot.transform, true);
            root.name = $"{rootName} Model";
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            return runtimeRoot;
        }

        private static void RestoreHoverPrefab(bool forceSave)
        {
            var root = PrefabUtility.LoadPrefabContents(HoverPrefabPath);
            if (root == null)
            {
                Debug.LogError($"Cannot load {HoverPrefabPath}.");
                return;
            }

            try
            {
                root = EnsureRuntimeRoot(root, "Tower01 Hover");
                var changed = false;
                if (root.GetComponent<MobileWireframeMesh>() == null)
                {
                    root.AddComponent<MobileWireframeMesh>();
                    changed = true;
                }

                if (changed || forceSave)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, HoverPrefabPath);
                    AssetDatabase.SaveAssets();
                    Debug.Log($"Restored Tower01 Hover preview components on '{HoverPrefabPath}'.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static bool AssignRuntimeReferences(
            DefenseTower tower,
            XRGrabSnapPlacement placement,
            XRGrabInteractable grabInteractable,
            BoxCollider collider,
            TowerDefinition towerDefinition,
            GameObject hoverPrefab)
        {
            var changed = false;

            var towerData = new SerializedObject(tower);
            changed |= AssignObject(towerData, "towerDefinition", towerDefinition);
            changed |= AssignObject(towerData, "firePoint", FindChild(tower.transform, "firePoint"));
            towerData.ApplyModifiedPropertiesWithoutUndo();

            var placementData = new SerializedObject(placement);
            changed |= AssignString(placementData, "snapZoneTag", "TowerDefenseBase");
            changed |= AssignObject(placementData, "snapPreviewModel", hoverPrefab);
            placementData.ApplyModifiedPropertiesWithoutUndo();

            var grabData = new SerializedObject(grabInteractable);
            var trackRotation = grabData.FindProperty("m_TrackRotation");
            if (trackRotation != null && trackRotation.boolValue)
            {
                trackRotation.boolValue = false;
                changed = true;
            }

            var trackScale = grabData.FindProperty("m_TrackScale");
            if (trackScale != null && !trackScale.boolValue)
            {
                trackScale.boolValue = true;
                changed = true;
            }

            var colliders = grabData.FindProperty("m_Colliders");
            if (colliders != null
                && (colliders.arraySize != 1
                    || colliders.GetArrayElementAtIndex(0).objectReferenceValue != collider))
            {
                colliders.arraySize = 1;
                colliders.GetArrayElementAtIndex(0).objectReferenceValue = collider;
                changed = true;
            }

            grabData.ApplyModifiedPropertiesWithoutUndo();
            return changed;
        }

        private static bool AssignObject(SerializedObject serializedObject, string propertyName, Object value)
        {
            var property = serializedObject.FindProperty(propertyName);
            if (property == null || property.objectReferenceValue == value)
            {
                return false;
            }

            property.objectReferenceValue = value;
            return true;
        }

        private static bool AssignString(SerializedObject serializedObject, string propertyName, string value)
        {
            var property = serializedObject.FindProperty(propertyName);
            if (property == null || property.stringValue == value)
            {
                return false;
            }

            property.stringValue = value;
            return true;
        }

        private static Transform FindChild(Transform root, string childName)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == childName)
                {
                    return child;
                }
            }

            return null;
        }

        private static void FitColliderToRenderers(Transform root, BoxCollider collider)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return;
            }

            var bounds = new Bounds(root.InverseTransformPoint(renderers[0].bounds.center), Vector3.zero);
            foreach (var renderer in renderers)
            {
                var rendererBounds = renderer.bounds;
                bounds.Encapsulate(root.InverseTransformPoint(rendererBounds.min));
                bounds.Encapsulate(root.InverseTransformPoint(rendererBounds.max));
            }

            collider.center = bounds.center;
            collider.size = bounds.size;
        }
    }
}
