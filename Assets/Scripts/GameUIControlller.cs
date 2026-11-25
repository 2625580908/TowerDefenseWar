using System.Collections.Generic;
using MRStrategyRTS.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class GameUIControlller : MonoBehaviour
{
    public List<GameObject> uiElements =
        new List<GameObject>();

    [Header("Performance")]
    [Tooltip("Optional TextMeshPro text used to display the current frame rate.")]
    public TMP_Text fpsDisplayText;
    [SerializeField, Min(0.1f)] private float fpsUpdateInterval = 0.5f;

    [Header("Hand Rays")]
    [Tooltip("Left-hand Near-Far Interactor. Only its far ray is disabled while playing a level.")]
    public NearFarInteractor leftHandRayInteractor;
    [Tooltip("Right-hand Near-Far Interactor. Only its far ray is disabled while playing a level.")]
    public NearFarInteractor rightHandRayInteractor;

    [Header("Game Level")]
    [Tooltip("The selected level terrain is created only when Open Game is called.")]
    [SerializeField] private EnemyWaveLevelController levelController;
    [Tooltip("地形中心相对头部的水平前方距离，单位为米。")]
    [Min(0.1f)] public float terrainSpawnDistance = 0.6f;
    [Tooltip("地形中心相对头部的高度偏移，单位为米；负数表示低于头部。")]
    public float terrainSpawnHeightOffset = -0.45f;
    [Tooltip("主菜单在 UI Elements 列表中的索引。")]
    [SerializeField, Min(0)] private int mainMenuUiId;
    [Tooltip("游戏页面中返回主菜单的 Canvas Button。")]
    public Button gameExitButton;
    private LevelProgressManager levelProgressManager;
    private XRSimpleInteractable runtimeGameExitPokeButton;
    private float fpsElapsedTime;
    private int fpsFrameCount;

    private void Start()
    {
        if (mainMenuUiId >= 0 && mainMenuUiId < uiElements.Count)
            MRStrategyRTS.UI.MenuButtonFeedback.Install(uiElements[mainMenuUiId]);
        ResolveLevelSystems();
        ConfigureGameExitButton();
        SetHandRaysActive(true);
    }

    private void Update()
    {
        if (fpsDisplayText == null)
        {
            return;
        }

        fpsElapsedTime += Time.unscaledDeltaTime;
        fpsFrameCount++;
        if (fpsElapsedTime < fpsUpdateInterval)
        {
            return;
        }

        var fps = Mathf.RoundToInt(fpsFrameCount / fpsElapsedTime);
        var renderedTriangles = CountVisibleTriangles();
        fpsDisplayText.SetText("FPS: {0}\nTris: {1:N0}", fps, renderedTriangles);
        fpsElapsedTime = 0f;
        fpsFrameCount = 0;
    }

    /// <summary>
    /// 将物体重置到摄像头正前方，并让物体 Z 轴与摄像头 Z 轴同向
    /// </summary>
    /// <param name="target">要移动的物体</param>
    /// <param name="distance">正前方距离（米）</param>
    /// <param name="heightOffset">相对摄像头高度的 Y 偏移（米），负数=更低</param>
    /// <param name="cam">目标摄像头，null 则用 Camera.main</param>
    public void ResetToCameraFront(Transform target, float distance, float heightOffset, Camera cam = null)
    {
        if (target == null) return;
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        Transform camT = cam.transform;

        // 水平前方（避免俯仰导致物体跑到地板/天花板）
        Vector3 forward = Vector3.ProjectOnPlane(camT.forward, Vector3.up);
        if (forward.sqrMagnitude < 1e-4f)
            forward = Vector3.ProjectOnPlane(camT.up, Vector3.up);
        if (forward.sqrMagnitude < 1e-4f)
            forward = Vector3.forward;
        forward.Normalize();

        // 位置：水平前方 distance 米，Y = 眼睛高度 + heightOffset
        Vector3 camPos = camT.position;
        Vector3 pos = camPos + forward * distance;
        pos.y = camPos.y + heightOffset;
        target.position = pos;

        // 朝向：物体 Z 轴与头的 Z 轴同向
        target.rotation = Quaternion.LookRotation(forward, Vector3.up);
    }
    
    public void OpenUI(int UiId)
    {
        CloseUI();
        uiElements[UiId].SetActive(true);
        SetHandRaysActive(true);
    }


    public void OpenGame(int UiId)
    {
        ResolveLevelSystems();
        if (levelController == null)
        {
            Debug.LogError("OpenGame requires an EnemyWaveLevelController in the scene.", this);
            return;
        }

        levelProgressManager?.EnsureInitialLevelLoaded();
        var terrainInstance = levelController.CreateTerrainForCurrentLevel();
        if (terrainInstance == null)
        {
            Debug.LogError(
                "OpenGame could not create terrain. Configure a Terrain Prefab on the selected Level Definition.",
                this);
            return;
        }

        BindRuntimeGameExitPokeButton(terrainInstance);
        CloseUI();
        ResetToCameraFront(terrainInstance.transform, terrainSpawnDistance, terrainSpawnHeightOffset);
        SetHandRaysActive(false);
    }

    public void ReturnToMainMenu()
    {
        UnbindRuntimeGameExitPokeButton();
        if (mainMenuUiId < 0 || mainMenuUiId >= uiElements.Count)
        {
            Debug.LogWarning("GameUIControlller has no valid Main Menu UI index.", this);
            return;
        }

        OpenUI(mainMenuUiId);
    }

    public void ExitCurrentLevelToMainMenu()
    {
        ResolveLevelSystems();
        UnbindRuntimeGameExitPokeButton();
        levelController?.ReturnToLevelMenu();
        ReturnToMainMenu();
    }


    public void QuitGame()
    {
        Application.Quit();
    }
    
    public void CloseUI()
    {
        foreach (var item in uiElements)
        {
            item.SetActive(false);
        }
    }

    private void SetHandRaysActive(bool isActive)
    {
        if (leftHandRayInteractor != null)
        {
            leftHandRayInteractor.enableFarCasting = isActive;
        }

        if (rightHandRayInteractor != null)
        {
            rightHandRayInteractor.enableFarCasting = isActive;
        }
    }

    private void ResolveLevelSystems()
    {
        levelController ??= FindFirstObjectByType<EnemyWaveLevelController>(FindObjectsInactive.Include);
        levelProgressManager ??= FindFirstObjectByType<LevelProgressManager>(FindObjectsInactive.Include);
    }

    private void ConfigureGameExitButton()
    {
        if (gameExitButton == null)
        {
            return;
        }

        gameExitButton.onClick.RemoveListener(ExitCurrentLevelToMainMenu);
        gameExitButton.onClick.AddListener(ExitCurrentLevelToMainMenu);
        UIClickAudioPlayer.Register(gameExitButton);
    }

    private void BindRuntimeGameExitPokeButton(GameObject terrainInstance)
    {
        UnbindRuntimeGameExitPokeButton();
        foreach (var interactable in terrainInstance.GetComponentsInChildren<XRSimpleInteractable>(true))
        {
            if (interactable.name.IndexOf("Exit", System.StringComparison.OrdinalIgnoreCase) < 0
                && interactable.name.IndexOf("退出", System.StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            runtimeGameExitPokeButton = interactable;
            runtimeGameExitPokeButton.selectEntered.AddListener(OnGameExitSelected);
            runtimeGameExitPokeButton.gameObject.SetActive(true);
            return;
        }

        Debug.LogWarning("GameUIControlller could not find an ExitButton in the level terrain.", this);
    }

    private void UnbindRuntimeGameExitPokeButton()
    {
        if (runtimeGameExitPokeButton != null)
        {
            runtimeGameExitPokeButton.selectEntered.RemoveListener(OnGameExitSelected);
        }

        runtimeGameExitPokeButton = null;
    }

    private void OnGameExitSelected(SelectEnterEventArgs _)
    {
        UIClickAudioPlayer.PlayClick();
        ExitCurrentLevelToMainMenu();
    }

    private static int CountVisibleTriangles()
    {
        var camera = Camera.main;
        if (camera == null)
        {
            return 0;
        }

        var frustumPlanes = GeometryUtility.CalculateFrustumPlanes(camera);
        var triangleCount = 0;
        foreach (var meshRenderer in FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude,
                     FindObjectsSortMode.None))
        {
            if (!meshRenderer.enabled
                || !GeometryUtility.TestPlanesAABB(frustumPlanes, meshRenderer.bounds))
            {
                continue;
            }

            var meshFilter = meshRenderer.GetComponent<MeshFilter>();
            if (meshFilter?.sharedMesh != null)
            {
                triangleCount += CountMeshTriangles(meshFilter.sharedMesh);
            }
        }

        foreach (var skinnedRenderer in FindObjectsByType<SkinnedMeshRenderer>(
                     FindObjectsInactive.Exclude,
                     FindObjectsSortMode.None))
        {
            if (skinnedRenderer.enabled
                && GeometryUtility.TestPlanesAABB(frustumPlanes, skinnedRenderer.bounds)
                && skinnedRenderer.sharedMesh != null)
            {
                triangleCount += CountMeshTriangles(skinnedRenderer.sharedMesh);
            }
        }

        return triangleCount;
    }

    private static int CountMeshTriangles(Mesh mesh)
    {
        var triangleCount = 0;
        for (var subMeshIndex = 0; subMeshIndex < mesh.subMeshCount; subMeshIndex++)
        {
            triangleCount += (int)(mesh.GetIndexCount(subMeshIndex) / 3);
        }

        return triangleCount;
    }
}
