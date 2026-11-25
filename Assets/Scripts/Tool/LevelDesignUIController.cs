using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using MRStrategyRTS.Config;
using MRStrategyRTS.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using MatrixSaveData = LevelDesignSaveData;

[DisallowMultipleComponent]
public sealed class LevelDesignUIController : MonoBehaviour
{
    private sealed class RuntimeBrush
    {
        public string displayName;
        public Sprite sprite;
        public Button button;
        public Image selectionBorder;
        public UnityAction selectAction;
    }

    [Header("自动绑定的 UI")]
    [SerializeField] private UIImageMatrixGenerator matrixGenerator;
    [SerializeField] private TMP_Text selectedBrushNameText;
    [SerializeField] private Transform brushContainer;
    [SerializeField] private Button increaseWidthButton;
    [SerializeField] private Button decreaseWidthButton;
    [SerializeField] private TMP_Text widthValueText;
    [SerializeField] private Button increaseHeightButton;
    [SerializeField] private Button decreaseHeightButton;
    [SerializeField] private TMP_Text heightValueText;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button openButton;

    [Header("打开设计地形")]
    [Tooltip("实例化此模板，并将存档矩阵传给其中的 TerrainTileGridGenerator。")]
    public GameObject terrainPrefab;
    [Tooltip("可选的生成父节点；未指定时放在主摄像机前方。")]
    public Transform terrainSpawnParent;
    [Min(0)] public int terrainBlockIndex;
    [Min(0.1f)] public float terrainPreviewDistance = 0.75f;
    public float terrainPreviewHeightOffset = -0.45f;

    [Header("设计地图随机关卡")]
    [Tooltip("每次打开从有效模板中随机选择；未配置时自动使用 LevelProgressManager 的关卡列表。")]
    public List<LevelDefinition> randomLevelTemplates = new();
    [Tooltip("有效地面格子的怪物贡献，空白格子不计入。")]
    [Min(0.01f)] public float enemiesPerTile = 0.5f;
    [Tooltip("每个可建炮台底座额外贡献的怪物数量。")]
    [Min(0f)] public float enemiesPerTowerBase = 3f;
    [Tooltip("起终点之间每一步有效路线额外贡献的怪物数量。")]
    [Min(0f)] public float enemiesPerRouteStep = 0.75f;
    [Tooltip("匹配数量的随机浮动比例；0.1 表示基准数量上下浮动约 10%。")]
    [Range(0f, 0.3f)] public float enemyCountRandomVariation = 0.1f;
    [Min(1)] public int minimumDesignEnemyCount = 10;
    public DesignedMapMetrics LastDesignedMapMetrics { get; private set; }
    public DesignedLevelEnemyBudget LastDesignedEnemyBudget { get; private set; }
    [SerializeField] private EnemyWaveLevelController designLevelController;
    [SerializeField] private GameUIControlller gameUiController;
    private LevelProgressManager levelProgressManager;

    [Header("笔刷名称")]
    [Tooltip("按 FloorTile 子物体顺序对应 01、02、03、04、05、Start、End。")]
    public List<string> brushNames = new()
    {
        "草地",
        "地块 02",
        "地块 03",
        "地块 04",
        "地块 05",
        "起点",
        "终点"
    };

    [Header("笔刷选中样式")]
    public Color selectedBrushBorderColor = new(1f, 0.82f, 0.25f, 1f);
    public Color normalBrushBorderColor = new(1f, 1f, 1f, 0.35f);

    [Header("矩阵范围")]
    [Range(7, 10)] public int minimumWidth = 7;
    [Range(7, 10)] public int maximumWidth = 10;
    [Range(7, 10)] public int minimumHeight = 7;
    [Range(7, 10)] public int maximumHeight = 10;

    [Header("默认矩阵尺寸")]
    [Range(7, 10)] public int defaultWidth = 10;
    [Range(7, 10)] public int defaultHeight = 10;

    [Header("矩阵存档")]
    [Tooltip("启动时自动读取上次保存的矩阵。")]
    public bool loadSavedMatrixOnStart = true;
    [Tooltip("保存在 Application.persistentDataPath 下的文件名。")]
    public string saveFileName = "level-design-grid.json";
    [SerializeField] private LevelDesignSavedLevelsUI savedLevelsUi;
    public string LastStatusMessage { get; private set; }
    public LevelDesignSaveStore SaveStore => new(SaveFilePath);

    [Header("保存路线校验与提示")]
    public Sprite startBrushSprite;
    public Sprite endBrushSprite;
    public Sprite landBrushSprite;
    [Tooltip("显示保存结果的 hint 文本，提示显示 5 秒后自动清空。")]
    public TMP_Text saveStatusText;

    [Header("矩阵滚动")]
    [SerializeField] private ScrollRect matrixScrollRect;
    [SerializeField] private Scrollbar verticalScrollbar;
    [Min(6f)] public float scrollbarThickness = 16f;
    public Color scrollbarBackgroundColor = new(0f, 0f, 0f, 0.45f);
    public Color scrollbarHandleColor = new(0.85f, 0.85f, 0.85f, 0.9f);

    private readonly List<RuntimeBrush> runtimeBrushes = new();
    private int selectedBrushIndex = -1;
    private bool initialized;
    private MatrixSaveData loadedSaveData;
    private string editingSaveId;
    private bool creatingNewDesign;
    private Coroutine clearSaveStatusCoroutine;
    private readonly System.Random levelRandom = new();
    public GameObject OpenedTerrain { get; private set; }

    public string SaveFilePath => Path.Combine(
        Application.persistentDataPath,
        string.IsNullOrWhiteSpace(saveFileName)
            ? "level-design-grid.json"
            : saveFileName);

    private void Awake()
    {
        AutoBind();
        ClearSaveStatus();
        Initialize();
        savedLevelsUi ??= GetComponent<LevelDesignSavedLevelsUI>();
        if (savedLevelsUi == null)
            savedLevelsUi = gameObject.AddComponent<LevelDesignSavedLevelsUI>();
        savedLevelsUi.Initialize(this, selectedBrushNameText, GetComponent<Image>());
    }

    private void OnDestroy()
    {
        CloseOpenedTerrain();
        if (matrixGenerator != null)
        {
            matrixGenerator.ImageClicked -= OnMatrixImageClicked;
        }

        RemoveButtonListeners();
        saveButton?.onClick.RemoveListener(SaveMatrix);
        openButton?.onClick.RemoveListener(OpenSavedTerrain);
        foreach (var brush in runtimeBrushes)
        {
            if (brush.button != null && brush.selectAction != null)
            {
                brush.button.onClick.RemoveListener(brush.selectAction);
            }
        }
    }

    [ContextMenu("Auto Bind LevelDesign UI")]
    public void AutoBind()
    {
        matrixGenerator ??= GetComponentInChildren<UIImageMatrixGenerator>(true);
        selectedBrushNameText ??= FindDirectChild("Name")?.GetComponent<TMP_Text>();
        brushContainer ??= FindDirectChild("FloorTile");

        BindDimensionControls(
            "width",
            ref increaseWidthButton,
            ref decreaseWidthButton,
            ref widthValueText);
        BindDimensionControls(
            "height",
            ref increaseHeightButton,
            ref decreaseHeightButton,
            ref heightValueText);
        saveButton ??= FindSaveButton();
        openButton ??= FindOpenButton();
        saveStatusText ??= FindDirectChild("hint")?.GetComponent<TMP_Text>();
        saveStatusText ??= FindDirectChild("SaveStatus")?.GetComponent<TMP_Text>();
        if (designLevelController == null)
            designLevelController = FindFirstObjectByType<EnemyWaveLevelController>(FindObjectsInactive.Include);
        if (gameUiController == null)
            gameUiController = FindFirstObjectByType<GameUIControlller>(FindObjectsInactive.Include);
        if (levelProgressManager == null)
            levelProgressManager = FindFirstObjectByType<LevelProgressManager>(FindObjectsInactive.Include);
    }

    public void OpenSavedTerrain()
    {
        TryOpenSavedTerrain();
    }

    public void ShowSavedLevels()
    {
        savedLevelsUi?.Show();
    }

    public void NewDesign()
    {
        AutoBind();
        Initialize();
        if (matrixGenerator == null || runtimeBrushes.Count == 0)
        {
            ShowSaveStatus("新建失败：矩阵或笔刷尚未初始化。");
            return;
        }
        var fresh = new MatrixSaveData
        {
            width = Mathf.Clamp(defaultWidth, minimumWidth, maximumWidth),
            height = Mathf.Clamp(defaultHeight, minimumHeight, maximumHeight)
        };
        var defaultBrush = FindBrushIndex(matrixGenerator.defaultSprite);
        for (var index = 0; index < fresh.width * fresh.height; index++)
            fresh.brushIndices.Add(defaultBrush);
        ApplySavedMatrix(fresh);
        CloseOpenedTerrain();
        loadedSaveData = null;
        editingSaveId = null;
        creatingNewDesign = true;
        SelectBrush(defaultBrush >= 0 ? defaultBrush : 0);
        savedLevelsUi?.Hide();
        ShowSaveStatus("已新建空白关卡，绘制路线后保存为新关卡");
    }

    public bool DeleteSavedLevel(string id)
    {
        if (!SaveStore.Delete(id))
            return false;
        if (loadedSaveData != null && loadedSaveData.id == id)
            loadedSaveData = null;
        if (editingSaveId == id)
            editingSaveId = null;
        return true;
    }

    public bool EditSavedLevel(string id)
    {
        try
        {
            AutoBind();
            Initialize();
            if (matrixGenerator == null || runtimeBrushes.Count == 0)
                throw new InvalidOperationException("矩阵或笔刷尚未初始化。");
            var data = SaveStore.Load(id);
            if (data == null)
                throw new InvalidOperationException("无法读取该关卡存档。");
            if (data.width < minimumWidth || data.width > maximumWidth
                || data.height < minimumHeight || data.height > maximumHeight)
                throw new InvalidOperationException("所选关卡尺寸超出当前设计范围。");
            foreach (var index in data.brushIndices)
                if (index >= 0 && GetSavedBrushSprite(data, index) == null)
                    throw new InvalidOperationException("存档引用的笔刷图片不存在，请恢复原笔刷后再编辑。");
            var editable = NormalizeSavedMatrix(data);
            ApplySavedMatrix(editable);
            loadedSaveData = editable;
            editingSaveId = data.id;
            creatingNewDesign = false;
            savedLevelsUi?.Hide();
            ShowSaveStatus("已加载所选关卡，可以继续编辑");
            return true;
        }
        catch (Exception exception)
        {
            ShowSaveStatus("编辑失败：" + exception.Message);
            return false;
        }
    }

    public bool PlaySavedLevel(string id)
    {
        try
        {
            var data = SaveStore.Load(id);
            if (data == null)
            {
                ShowSaveStatus("无法读取该关卡存档。");
                return false;
            }
            loadedSaveData = NormalizeSavedMatrix(data);
            if (!TryOpenSavedTerrain(true))
                return false;
            editingSaveId = null;
            creatingNewDesign = false;
            designLevelController.StartGame();
            if (!designLevelController.WaveStarted)
            {
                ShowSaveStatus("关卡无法开始，请检查地图起终点和怪物波次。");
                return false;
            }
            return true;
        }
        catch (Exception exception)
        {
            ShowSaveStatus($"试玩失败：{exception.Message}");
            return false;
        }
    }

    public Sprite GetSavedBrushSprite(MatrixSaveData data, int brushIndex)
    {
        if (brushIndex < 0)
            return null;
        if (data.version >= 2 && data.brushSpriteNames != null)
        {
            if (brushIndex >= data.brushSpriteNames.Count)
                return null;
            var name = data.brushSpriteNames[brushIndex];
            foreach (var brush in runtimeBrushes)
                if (brush.sprite.name == name)
                    return brush.sprite;
            return null;
        }
        return brushIndex < runtimeBrushes.Count ? runtimeBrushes[brushIndex].sprite : null;
    }

    private bool TryOpenSavedTerrain(bool allowSelectedSave = false)
    {
        AutoBind();
        Initialize();
        if (creatingNewDesign && !allowSelectedSave)
        {
            ShowSaveStatus("请先保存新建关卡，再打开试玩。");
            return false;
        }
        // Startup preview and terrain generation use the same saved snapshot.
        loadedSaveData ??= LoadMatrixSaveData();
        var saveData = loadedSaveData;
        if (saveData == null)
        {
            ShowSaveStatus("没有可打开的矩阵存档，请先点击保存。");
            Debug.LogWarning("没有可打开的矩阵存档，请先点击保存。", this);
            return false;
        }

        if (matrixGenerator == null || terrainPrefab == null || designLevelController == null || runtimeBrushes.Count == 0
            || saveData.width < minimumWidth || saveData.height < minimumHeight
            || saveData.width > maximumWidth || saveData.height > maximumHeight)
        {
            ShowSaveStatus("打开失败：请检查地形模板、关卡控制器和地图尺寸。");
            Debug.LogError("请配置 Terrain Prefab、关卡控制器和笔刷，并确保存档尺寸在矩阵范围内。", this);
            return false;
        }

        if (!LevelDesignRouteValidator.TryGetRouteLength(
                saveData.width, saveData.height, saveData.brushIndices,
                ResolveRouteBrush(startBrushSprite, "起点", "Start"),
                ResolveRouteBrush(endBrushSprite, "终点", "End"),
                ResolveRouteBrush(landBrushSprite, "陆地", "02"), out var routeSteps, out var routeMessage))
        {
            ShowSaveStatus(routeMessage);
            return false;
        }

        ApplySavedMatrix(saveData);

        // An inactive staging parent prevents template Awake/Start from generating the old layout.
        var staging = new GameObject("LevelDesign Terrain Staging");
        staging.SetActive(false);
        GameObject instance = null;
        LevelDefinition generatedDefinition = null;
        var adopted = false;
        try
        {
            instance = Instantiate(terrainPrefab, staging.transform, false);
            instance.SetActive(false);
            var generator = instance.GetComponentInChildren<TerrainTileGridGenerator>(true);
            if (generator == null)
                throw new InvalidOperationException("Terrain Prefab 缺少 TerrainTileGridGenerator。");
            if (instance.GetComponentInChildren<LevelTerrainBindings>(true) == null)
                throw new InvalidOperationException("Terrain Prefab 缺少 LevelTerrainBindings，无法绑定开始按钮。");

            var tileIndices = new List<int>(saveData.brushIndices.Count);
            foreach (var brushIndex in saveData.brushIndices)
            {
                if (brushIndex == -1)
                {
                    tileIndices.Add(-1);
                    continue;
                }
                if (brushIndex < 0 || brushIndex >= runtimeBrushes.Count)
                    throw new InvalidOperationException($"存档笔刷编号 {brushIndex} 不存在。");

                var brush = runtimeBrushes[brushIndex];
                var tileType = generator.FindTileTypeForBrush(brush.sprite, terrainBlockIndex);
                if (tileType < 0)
                    throw new InvalidOperationException(
                        $"笔刷“{brush.displayName}”未匹配地块，请将其图片配置到 Tile Variants 的 Editor Preview。");
                tileIndices.Add(tileType);
            }

            if (!generator.ApplyLayout(saveData.width, saveData.height, tileIndices, terrainBlockIndex))
                throw new InvalidOperationException("地形矩阵生成失败。");

            var landBrushIndex = ResolveRouteBrush(landBrushSprite, "陆地", "02");
            var walkableTileType = generator.FindTileTypeForBrush(
                runtimeBrushes[landBrushIndex].sprite, terrainBlockIndex) + 1;
            var candidates = ResolveLevelTemplates(walkableTileType);
            if (candidates.Count == 0)
                throw new InvalidOperationException("请配置有效的随机关卡模板，模板需包含可沿陆地行走的怪物。");
            var chosenTemplate = candidates[levelRandom.Next(candidates.Count)];
            var tileCount = 0;
            foreach (var tileIndex in tileIndices)
                if (tileIndex >= 0)
                    tileCount++;
            var towerBaseCount = generator.CountTilesWithTag("TowerDefenseBase", terrainBlockIndex);
            var metrics = new DesignedMapMetrics(tileCount, towerBaseCount, routeSteps);
            var budget = DesignedLevelEnemyBudget.Calculate(metrics, enemiesPerTile,
                enemiesPerTowerBase, enemiesPerRouteStep, enemyCountRandomVariation,
                minimumDesignEnemyCount, levelRandom);
            generatedDefinition = DesignedLevelFactory.CreateWithEnemyCount(
                chosenTemplate, terrainPrefab, budget.TotalCount, walkableTileType, levelRandom);
            generatedDefinition.displayName =
                $"设计关卡（地面 {tileCount}，底座 {towerBaseCount}，路线 {routeSteps}，怪物 {budget.TotalCount}）";

            // Design preview must not trigger the independent enemy-spawn test component.
            foreach (var test in instance.GetComponentsInChildren<SpawnEnemyOnceTest>(true))
                test.enabled = false;

            instance.transform.SetParent(terrainSpawnParent, false);
            if (terrainSpawnParent == null && Camera.main != null)
            {
                var cameraTransform = Camera.main.transform;
                var forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up);
                if (forward.sqrMagnitude < 0.0001f)
                    forward = Vector3.forward;
                forward.Normalize();
                instance.transform.position = cameraTransform.position
                    + forward * terrainPreviewDistance + Vector3.up * terrainPreviewHeightOffset;
                instance.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            }

            CloseOpenedTerrain();
            instance.name = $"Designed Terrain ({saveData.width}x{saveData.height})";
            instance.SetActive(true);
            if (!designLevelController.LoadDesignedLevel(generatedDefinition, instance))
                throw new InvalidOperationException("无法接入设计关卡开始按钮。");
            adopted = true;
            OpenedTerrain = instance;
            designLevelController.onGameStarted.AddListener(OnDesignedGameStarted);

            LastDesignedMapMetrics = metrics;
            LastDesignedEnemyBudget = budget;
            var enemyCount = designLevelController.PlannedEnemyCount;
            ShowSaveStatus($"地面 {tileCount} 格 · 底座 {towerBaseCount} 个 · 路线 {routeSteps} 步\n怪物 {enemyCount} 个，点击开始挑战。");
            Debug.Log($"设计地图 {saveData.width}×{saveData.height}：地面 {tileCount}，底座 {towerBaseCount}，路线 {routeSteps} 步，"
                + $"基准怪物 {budget.BaseCount}，随机范围 {budget.MinimumCount}～{budget.MaximumCount}，实际波次总数 {enemyCount}，模板 {chosenTemplate.name}。", this);
            return true;
        }
        catch (Exception exception)
        {
            if (adopted)
                CloseOpenedTerrain();
            else
            {
                if (instance != null)
                    Destroy(instance);
                DesignedLevelFactory.Release(generatedDefinition);
            }
            ShowSaveStatus($"打开失败：{exception.Message}");
            Debug.LogError($"打开设计地形失败：{exception.Message}", this);
            return false;
        }
        finally
        {
            Destroy(staging);
        }
    }

    private List<LevelDefinition> ResolveLevelTemplates(int walkableTileType)
    {
        var candidates = new List<LevelDefinition>();
        AddValidLevelTemplates(randomLevelTemplates, walkableTileType, candidates);
        if (candidates.Count == 0 && levelProgressManager != null)
            AddValidLevelTemplates(levelProgressManager.ConfiguredLevels, walkableTileType, candidates);
        if (candidates.Count == 0 && designLevelController != null
            && !designLevelController.IsDesignedLevel)
            AddValidLevelTemplates(
                new[] { designLevelController.CurrentLevelDefinition }, walkableTileType, candidates);
        return candidates;
    }

    private static void AddValidLevelTemplates(
        IReadOnlyList<LevelDefinition> templates, int walkableTileType, List<LevelDefinition> candidates)
    {
        if (templates == null)
            return;
        foreach (var template in templates)
            if (DesignedLevelFactory.HasValidSpawns(template, walkableTileType)
                && !candidates.Contains(template))
                candidates.Add(template);
    }

    private void ApplySavedMatrix(MatrixSaveData saveData)
    {
        matrixGenerator.widthCount = saveData.width;
        matrixGenerator.heightCount = saveData.height;
        matrixGenerator.GenerateGrid();
        RestoreSavedMatrix(saveData);
        ConfigureMatrixScrolling(true);
        UpdateDimensionUi();
    }

    public void CloseOpenedTerrain()
    {
        if (designLevelController != null)
            designLevelController.onGameStarted.RemoveListener(OnDesignedGameStarted);
        if (OpenedTerrain == null)
            return;
        if (designLevelController != null
            && designLevelController.CurrentTerrainInstance == OpenedTerrain)
            designLevelController.ReturnToLevelMenu();
        else
        {
            OpenedTerrain.SetActive(false);
            Destroy(OpenedTerrain);
        }
        OpenedTerrain = null;
    }

    private void OnDesignedGameStarted()
    {
        if (OpenedTerrain == null || designLevelController == null
            || designLevelController.CurrentTerrainInstance != OpenedTerrain)
            return;

        // The level controller owns gameplay now; hiding this UI must not delete its map.
        designLevelController.onGameStarted.RemoveListener(OnDesignedGameStarted);
        OpenedTerrain = null;
        gameUiController?.OpenGame(0);
    }

    private void OnDisable()
    {
        savedLevelsUi?.Hide();
        ClearSaveStatus();
        CloseOpenedTerrain();
    }

    public void IncreaseWidth()
    {
        ResizeMatrix(matrixGenerator.widthCount + 1, matrixGenerator.heightCount);
    }

    public void DecreaseWidth()
    {
        ResizeMatrix(matrixGenerator.widthCount - 1, matrixGenerator.heightCount);
    }

    public void IncreaseHeight()
    {
        ResizeMatrix(matrixGenerator.widthCount, matrixGenerator.heightCount + 1);
    }

    public void DecreaseHeight()
    {
        ResizeMatrix(matrixGenerator.widthCount, matrixGenerator.heightCount - 1);
    }

    public void ResetToDefaultSize()
    {
        ResizeMatrix(defaultWidth, defaultHeight);
    }

    public void SaveMatrix()
    {
        if (matrixGenerator == null || runtimeBrushes.Count == 0)
        {
            ShowSaveStatus("保存失败：矩阵或笔刷尚未初始化。");
            Debug.LogWarning("LevelDesign cannot save before the matrix and brushes are initialized.", this);
            return;
        }

        var saveData = new MatrixSaveData
        {
            version = 2,
            width = matrixGenerator.widthCount,
            height = matrixGenerator.heightCount
        };
        foreach (var brush in runtimeBrushes)
            saveData.brushSpriteNames.Add(brush.sprite.name);

        foreach (var image in matrixGenerator.GeneratedImages)
        {
            saveData.brushIndices.Add(FindBrushIndex(image != null ? image.sprite : null));
        }

        var startType = ResolveRouteBrush(startBrushSprite, "起点", "Start");
        var endType = ResolveRouteBrush(endBrushSprite, "终点", "End");
        var landType = ResolveRouteBrush(landBrushSprite, "陆地", "02");
        if (!LevelDesignRouteValidator.TryValidate(
                saveData.width, saveData.height, saveData.brushIndices,
                startType, endType, landType, out var routeMessage))
        {
            ShowSaveStatus(routeMessage);
            Debug.LogWarning(routeMessage, this);
            return;
        }

        try
        {
            var isEditing = !string.IsNullOrEmpty(editingSaveId);
            var saved = isEditing
                ? SaveStore.Update(editingSaveId, saveData, saveData.brushSpriteNames)
                : SaveStore.SaveNew(saveData, saveData.brushSpriteNames);
            loadedSaveData = NormalizeSavedMatrix(saved);
            editingSaveId = saved.id;
            creatingNewDesign = false;
            ShowSaveStatus(isEditing ? "原关卡修改已保存" : "保存成功");
            savedLevelsUi?.RefreshIfVisible();
            Debug.Log($"Saved LevelDesign '{saved.displayName}' to '{SaveStore.DirectoryPath}'.", this);
        }
        catch (LevelDesignSaveStore.DuplicateTerrainException exception)
        {
            if (string.IsNullOrEmpty(editingSaveId) && !creatingNewDesign)
                loadedSaveData = NormalizeSavedMatrix(exception.ExistingSave);
            ShowSaveStatus(exception.Message);
        }
        catch (FileNotFoundException exception)
        {
            ShowSaveStatus(exception.Message);
        }
        catch (Exception exception)
        {
            ShowSaveStatus("保存失败：无法写入存档，请检查存储空间或文件权限。");
            Debug.LogError(
                $"Failed to save LevelDesign matrix to '{SaveFilePath}'.\n{exception}",
                this);
        }
    }

    private int ResolveRouteBrush(Sprite configuredSprite, string displayName, string spriteName)
    {
        if (configuredSprite != null)
            return FindBrushIndex(configuredSprite);
        for (var index = 0; index < runtimeBrushes.Count; index++)
        {
            var brush = runtimeBrushes[index];
            if (string.Equals(brush.displayName, displayName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(brush.sprite.name, spriteName, StringComparison.OrdinalIgnoreCase))
                return index;
        }
        return -1;
    }

    private void ShowSaveStatus(string message)
    {
        LastStatusMessage = message;
        ClearSaveStatus();
        if (saveStatusText == null)
            return;

        saveStatusText.text = message;
        if (isActiveAndEnabled)
            clearSaveStatusCoroutine = StartCoroutine(ClearSaveStatusAfterDelay());
    }

    private IEnumerator ClearSaveStatusAfterDelay()
    {
        yield return new WaitForSecondsRealtime(5f);
        clearSaveStatusCoroutine = null;
        if (saveStatusText != null)
            saveStatusText.text = string.Empty;
    }

    private void ClearSaveStatus()
    {
        if (clearSaveStatusCoroutine != null)
        {
            StopCoroutine(clearSaveStatusCoroutine);
            clearSaveStatusCoroutine = null;
        }
        if (saveStatusText != null)
            saveStatusText.text = string.Empty;
    }

    public void SelectBrush(int brushIndex)
    {
        if (brushIndex < 0 || brushIndex >= runtimeBrushes.Count)
        {
            return;
        }

        selectedBrushIndex = brushIndex;
        if (selectedBrushNameText != null)
        {
            selectedBrushNameText.text = runtimeBrushes[brushIndex].displayName;
        }

        RefreshBrushSelectionVisuals();
    }

    private void Initialize()
    {
        if (initialized || matrixGenerator == null)
        {
            return;
        }

        initialized = true;
        minimumWidth = Mathf.Clamp(minimumWidth, 7, 10);
        maximumWidth = Mathf.Clamp(maximumWidth, minimumWidth, 10);
        minimumHeight = Mathf.Clamp(minimumHeight, 7, 10);
        maximumHeight = Mathf.Clamp(maximumHeight, minimumHeight, 10);
        defaultWidth = Mathf.Clamp(defaultWidth, minimumWidth, maximumWidth);
        defaultHeight = Mathf.Clamp(defaultHeight, minimumHeight, maximumHeight);

        BuildBrushPalette();
        loadedSaveData = loadSavedMatrixOnStart ? LoadMatrixSaveData() : null;
        matrixGenerator.widthCount = loadedSaveData != null
            ? Mathf.Clamp(loadedSaveData.width, minimumWidth, maximumWidth)
            : defaultWidth;
        matrixGenerator.heightCount = loadedSaveData != null
            ? Mathf.Clamp(loadedSaveData.height, minimumHeight, maximumHeight)
            : defaultHeight;
        matrixGenerator.generateOnStart = false;
        matrixGenerator.useFixedSquareCells = true;
        matrixGenerator.fitCellSizeToParentWidth = true;
        matrixGenerator.fitWidthReservedSpace = scrollbarThickness;
        matrixGenerator.preserveAspect = true;
        matrixGenerator.raycastTarget = true;
        matrixGenerator.ImageClicked -= OnMatrixImageClicked;
        matrixGenerator.ImageClicked += OnMatrixImageClicked;

        ConfigureDimensionButtons();
        ConfigureSaveButton();
        ConfigureOpenButton();
        matrixGenerator.GenerateGrid();
        RestoreSavedMatrix(loadedSaveData);
        ConfigureMatrixScrolling(true);
        UpdateDimensionUi();

        if (runtimeBrushes.Count > 0)
        {
            SelectBrush(0);
        }
    }

    private void BuildBrushPalette()
    {
        runtimeBrushes.Clear();
        if (brushContainer == null)
        {
            return;
        }

        for (var index = 0; index < brushContainer.childCount; index++)
        {
            var brushTransform = brushContainer.GetChild(index);
            var brushImage = brushTransform.GetComponent<Image>();
            if (brushImage == null || brushImage.sprite == null)
            {
                continue;
            }

            var button = brushTransform.GetComponent<Button>();
            if (button == null)
            {
                button = brushTransform.gameObject.AddComponent<Button>();
            }

            button.targetGraphic = brushImage;
            brushImage.raycastTarget = true;

            var brushIndex = runtimeBrushes.Count;
            var brush = new RuntimeBrush
            {
                displayName = ResolveBrushName(brushIndex, brushTransform.name),
                sprite = brushImage.sprite,
                button = button,
                selectionBorder = brushTransform.Find("Border")?.GetComponent<Image>()
            };
            brush.selectAction = () => SelectBrush(brushIndex);
            button.onClick.AddListener(brush.selectAction);
            UIClickAudioPlayer.Register(button);
            runtimeBrushes.Add(brush);
        }
    }

    private string ResolveBrushName(int brushIndex, string fallbackName)
    {
        if (brushIndex < brushNames.Count
            && !string.IsNullOrWhiteSpace(brushNames[brushIndex]))
        {
            return brushNames[brushIndex];
        }

        return string.IsNullOrWhiteSpace(fallbackName)
            ? $"笔刷 {brushIndex + 1:D2}"
            : fallbackName;
    }

    private int FindBrushIndex(Sprite sprite)
    {
        if (sprite == null)
        {
            return -1;
        }

        for (var index = 0; index < runtimeBrushes.Count; index++)
        {
            if (runtimeBrushes[index].sprite == sprite)
            {
                return index;
            }
        }

        return -1;
    }

    private MatrixSaveData LoadMatrixSaveData()
    {
        try
        {
            var saveData = SaveStore.LoadLatest();
            if (saveData == null)
                return null;
            Debug.Log($"Loaded LevelDesign matrix from '{SaveFilePath}'.", this);
            return NormalizeSavedMatrix(saveData);
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"Failed to load LevelDesign matrix from '{SaveFilePath}'.\n{exception}",
                this);
            return null;
        }
    }

    private MatrixSaveData NormalizeSavedMatrix(MatrixSaveData saveData)
    {
        var width = Mathf.Clamp(saveData.width, minimumWidth, maximumWidth);
        var height = Mathf.Clamp(saveData.height, minimumHeight, maximumHeight);
        if (width == saveData.width && height == saveData.height && saveData.version == 1)
            return saveData;

        var normalized = new MatrixSaveData
        {
            id = saveData.id,
            displayName = saveData.displayName,
            savedUtcTicks = saveData.savedUtcTicks,
            width = width,
            height = height
        };
        var defaultBrush = FindBrushIndex(matrixGenerator.defaultSprite);
        for (var row = 0; row < height; row++)
            for (var column = 0; column < width; column++)
            {
                var index = defaultBrush;
                if (row < saveData.height && column < saveData.width)
                {
                    var savedIndex = saveData.brushIndices[row * saveData.width + column];
                    index = saveData.version == 1 ? savedIndex
                        : FindBrushIndex(GetSavedBrushSprite(saveData, savedIndex));
                    if (savedIndex >= 0 && index < 0)
                        throw new InvalidOperationException("存档引用的笔刷图片不存在，请恢复原笔刷后再打开。");
                }
                normalized.brushIndices.Add(index);
            }
        return normalized;
    }

    private void RestoreSavedMatrix(MatrixSaveData saveData)
    {
        if (saveData == null)
        {
            return;
        }

        var generatedImages = matrixGenerator.GeneratedImages;
        var copyWidth = Mathf.Min(saveData.width, matrixGenerator.widthCount);
        var copyHeight = Mathf.Min(saveData.height, matrixGenerator.heightCount);
        for (var row = 0; row < copyHeight; row++)
        {
            for (var column = 0; column < copyWidth; column++)
            {
                var savedIndex = row * saveData.width + column;
                var imageIndex = row * matrixGenerator.widthCount + column;
                var brushIndex = saveData.brushIndices[savedIndex];
                if (imageIndex < generatedImages.Count && brushIndex < runtimeBrushes.Count
                    && generatedImages[imageIndex] != null)
                {
                    generatedImages[imageIndex].sprite = brushIndex >= 0
                        ? runtimeBrushes[brushIndex].sprite : null;
                }
            }
        }
    }

    private void RefreshBrushSelectionVisuals()
    {
        for (var index = 0; index < runtimeBrushes.Count; index++)
        {
            var border = runtimeBrushes[index].selectionBorder;
            if (border != null)
            {
                border.color = index == selectedBrushIndex
                    ? selectedBrushBorderColor
                    : normalBrushBorderColor;
            }
        }
    }

    private void OnMatrixImageClicked(int imageIndex, Image image)
    {
        if (selectedBrushIndex < 0
            || selectedBrushIndex >= runtimeBrushes.Count
            || image == null)
        {
            return;
        }

        image.sprite = runtimeBrushes[selectedBrushIndex].sprite;
    }

    private void ResizeMatrix(int requestedWidth, int requestedHeight)
    {
        if (matrixGenerator == null)
        {
            return;
        }

        var oldWidth = matrixGenerator.widthCount;
        var oldHeight = matrixGenerator.heightCount;
        var oldSprites = CaptureMatrixSprites(oldWidth, oldHeight);

        matrixGenerator.widthCount = Mathf.Clamp(
            requestedWidth,
            minimumWidth,
            maximumWidth);
        matrixGenerator.heightCount = Mathf.Clamp(
            requestedHeight,
            minimumHeight,
            maximumHeight);

        if (oldWidth != matrixGenerator.widthCount
            || oldHeight != matrixGenerator.heightCount)
        {
            matrixGenerator.GenerateGrid();
            RestoreMatrixSprites(oldSprites, oldWidth, oldHeight);
            ConfigureMatrixScrolling(false);
        }

        UpdateDimensionUi();
    }

    private Sprite[] CaptureMatrixSprites(int width, int height)
    {
        var sprites = new Sprite[Mathf.Max(0, width * height)];
        var generatedImages = matrixGenerator.GeneratedImages;
        var copyCount = Mathf.Min(sprites.Length, generatedImages.Count);
        for (var index = 0; index < copyCount; index++)
        {
            sprites[index] = generatedImages[index] != null
                ? generatedImages[index].sprite
                : null;
        }

        return sprites;
    }

    private void RestoreMatrixSprites(Sprite[] oldSprites, int oldWidth, int oldHeight)
    {
        var copyWidth = Mathf.Min(oldWidth, matrixGenerator.widthCount);
        var copyHeight = Mathf.Min(oldHeight, matrixGenerator.heightCount);
        var generatedImages = matrixGenerator.GeneratedImages;
        for (var row = 0; row < copyHeight; row++)
        {
            for (var column = 0; column < copyWidth; column++)
            {
                var oldIndex = row * oldWidth + column;
                var newIndex = row * matrixGenerator.widthCount + column;
                if (oldIndex < oldSprites.Length
                    && newIndex < generatedImages.Count
                    && oldSprites[oldIndex] != null)
                {
                    generatedImages[newIndex].sprite = oldSprites[oldIndex];
                }
            }
        }
    }

    private void ConfigureDimensionButtons()
    {
        RemoveButtonListeners();
        increaseWidthButton?.onClick.AddListener(IncreaseWidth);
        decreaseWidthButton?.onClick.AddListener(DecreaseWidth);
        increaseHeightButton?.onClick.AddListener(IncreaseHeight);
        decreaseHeightButton?.onClick.AddListener(DecreaseHeight);
        UIClickAudioPlayer.Register(increaseWidthButton);
        UIClickAudioPlayer.Register(decreaseWidthButton);
        UIClickAudioPlayer.Register(increaseHeightButton);
        UIClickAudioPlayer.Register(decreaseHeightButton);
    }

    private void ConfigureSaveButton()
    {
        if (saveButton == null)
        {
            Debug.LogWarning("LevelDesign save Button was not found.", this);
            return;
        }

        // Replace the event to remove the copied OpenUI callback from the button.
        saveButton.onClick = new Button.ButtonClickedEvent();
        saveButton.onClick.AddListener(SaveMatrix);
        UIClickAudioPlayer.Register(saveButton);
    }

    private void ConfigureOpenButton()
    {
        if (openButton == null)
            return;
        openButton.onClick = new Button.ButtonClickedEvent();
        openButton.onClick.AddListener(OpenSavedTerrain);
        UIClickAudioPlayer.Register(openButton);
    }

    private void RemoveButtonListeners()
    {
        increaseWidthButton?.onClick.RemoveListener(IncreaseWidth);
        decreaseWidthButton?.onClick.RemoveListener(DecreaseWidth);
        increaseHeightButton?.onClick.RemoveListener(IncreaseHeight);
        decreaseHeightButton?.onClick.RemoveListener(DecreaseHeight);
    }

    private void UpdateDimensionUi()
    {
        if (widthValueText != null)
        {
            widthValueText.SetText("{0}", matrixGenerator.widthCount);
        }

        if (heightValueText != null)
        {
            heightValueText.SetText("{0}", matrixGenerator.heightCount);
        }

        if (increaseWidthButton != null)
        {
            increaseWidthButton.interactable = matrixGenerator.widthCount < maximumWidth;
        }

        if (decreaseWidthButton != null)
        {
            decreaseWidthButton.interactable = matrixGenerator.widthCount > minimumWidth;
        }

        if (increaseHeightButton != null)
        {
            increaseHeightButton.interactable = matrixGenerator.heightCount < maximumHeight;
        }

        if (decreaseHeightButton != null)
        {
            decreaseHeightButton.interactable = matrixGenerator.heightCount > minimumHeight;
        }
    }

    private void ConfigureMatrixScrolling(bool resetPosition)
    {
        var viewport = matrixGenerator != null
            ? matrixGenerator.transform as RectTransform
            : null;
        if (viewport == null || matrixGenerator.GridRoot == null)
        {
            return;
        }

        var viewportImage = viewport.GetComponent<Image>();
        if (viewportImage != null)
        {
            viewportImage.raycastTarget = true;
        }

        if (viewport.GetComponent<RectMask2D>() == null)
        {
            viewport.gameObject.AddComponent<RectMask2D>();
        }

        matrixScrollRect ??= viewport.GetComponent<ScrollRect>();
        if (matrixScrollRect == null)
        {
            matrixScrollRect = viewport.gameObject.AddComponent<ScrollRect>();
        }

        verticalScrollbar ??= FindScrollbar(viewport, "Vertical Scrollbar");
        verticalScrollbar ??= CreateScrollbar(viewport, false);

        var oldHorizontalScrollbar = viewport.Find("Horizontal Scrollbar");
        if (oldHorizontalScrollbar != null)
        {
            oldHorizontalScrollbar.gameObject.SetActive(false);
        }

        matrixScrollRect.content = matrixGenerator.GridRoot;
        matrixScrollRect.viewport = viewport;
        matrixScrollRect.horizontal = false;
        matrixScrollRect.vertical = true;
        matrixScrollRect.movementType = ScrollRect.MovementType.Clamped;
        matrixScrollRect.inertia = true;
        matrixScrollRect.decelerationRate = 0.135f;
        matrixScrollRect.scrollSensitivity = matrixGenerator.ResolvedCellSize * 0.5f;
        matrixScrollRect.horizontalScrollbar = null;
        matrixScrollRect.verticalScrollbar = verticalScrollbar;
        matrixScrollRect.verticalScrollbarVisibility =
            ScrollRect.ScrollbarVisibility.Permanent;

        matrixGenerator.GridRoot.SetAsFirstSibling();
        verticalScrollbar.transform.SetAsLastSibling();

        Canvas.ForceUpdateCanvases();
        matrixScrollRect.StopMovement();
        if (resetPosition)
        {
            matrixScrollRect.verticalNormalizedPosition = 1f;
        }
    }

    private Scrollbar FindScrollbar(RectTransform viewport, string scrollbarName)
    {
        var child = viewport.Find(scrollbarName);
        return child != null ? child.GetComponent<Scrollbar>() : null;
    }

    private Scrollbar CreateScrollbar(RectTransform viewport, bool horizontal)
    {
        var scrollbarObject = new GameObject(
            horizontal ? "Horizontal Scrollbar" : "Vertical Scrollbar",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Scrollbar));
        scrollbarObject.layer = viewport.gameObject.layer;
        var scrollbarRect = scrollbarObject.GetComponent<RectTransform>();
        scrollbarRect.SetParent(viewport, false);

        if (horizontal)
        {
            scrollbarRect.anchorMin = new Vector2(0f, 0f);
            scrollbarRect.anchorMax = new Vector2(1f, 0f);
            scrollbarRect.pivot = new Vector2(0.5f, 0f);
            scrollbarRect.offsetMin = Vector2.zero;
            scrollbarRect.offsetMax = new Vector2(
                -scrollbarThickness,
                scrollbarThickness);
        }
        else
        {
            scrollbarRect.anchorMin = new Vector2(1f, 0f);
            scrollbarRect.anchorMax = new Vector2(1f, 1f);
            scrollbarRect.pivot = new Vector2(1f, 0.5f);
            scrollbarRect.offsetMin = new Vector2(-scrollbarThickness, 0f);
            scrollbarRect.offsetMax = Vector2.zero;
        }

        var background = scrollbarObject.GetComponent<Image>();
        background.color = scrollbarBackgroundColor;

        var slidingAreaObject = new GameObject(
            "Sliding Area",
            typeof(RectTransform));
        slidingAreaObject.layer = viewport.gameObject.layer;
        var slidingAreaRect = slidingAreaObject.GetComponent<RectTransform>();
        slidingAreaRect.SetParent(scrollbarRect, false);
        slidingAreaRect.anchorMin = Vector2.zero;
        slidingAreaRect.anchorMax = Vector2.one;
        slidingAreaRect.offsetMin = new Vector2(2f, 2f);
        slidingAreaRect.offsetMax = new Vector2(-2f, -2f);

        var handleObject = new GameObject(
            "Handle",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        handleObject.layer = viewport.gameObject.layer;
        var handleRect = handleObject.GetComponent<RectTransform>();
        handleRect.SetParent(slidingAreaRect, false);
        handleRect.anchorMin = Vector2.zero;
        handleRect.anchorMax = Vector2.one;
        handleRect.offsetMin = Vector2.zero;
        handleRect.offsetMax = Vector2.zero;

        var handleImage = handleObject.GetComponent<Image>();
        handleImage.color = scrollbarHandleColor;

        var scrollbar = scrollbarObject.GetComponent<Scrollbar>();
        scrollbar.targetGraphic = handleImage;
        scrollbar.handleRect = handleRect;
        scrollbar.direction = horizontal
            ? Scrollbar.Direction.LeftToRight
            : Scrollbar.Direction.BottomToTop;
        scrollbar.navigation = new Navigation
        {
            mode = Navigation.Mode.None
        };
        return scrollbar;
    }

    private void BindDimensionControls(
        string groupName,
        ref Button increaseButton,
        ref Button decreaseButton,
        ref TMP_Text valueText)
    {
        var group = FindDirectChild(groupName);
        if (group == null)
        {
            return;
        }

        increaseButton ??= group.Find("+")?.GetComponent<Button>();
        decreaseButton ??= group.Find("-")?.GetComponent<Button>();
        valueText ??= group.Find("Value")?.GetComponent<TMP_Text>();
    }

    private Transform FindDirectChild(string childName)
    {
        for (var index = 0; index < transform.childCount; index++)
        {
            var child = transform.GetChild(index);
            if (string.Equals(
                    child.name,
                    childName,
                    System.StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }
        }

        return null;
    }

    private Button FindSaveButton()
    {
        for (var index = 0; index < transform.childCount; index++)
        {
            var child = transform.GetChild(index);
            var button = child.GetComponent<Button>();
            var image = child.GetComponent<Image>();
            var spriteName = image != null && image.sprite != null
                ? image.sprite.name
                : string.Empty;
            if (button != null
                && (child.name.IndexOf("Save", StringComparison.OrdinalIgnoreCase) >= 0
                    || child.name.IndexOf("保存", StringComparison.OrdinalIgnoreCase) >= 0
                    || spriteName.IndexOf("保存", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return button;
            }
        }

        return null;
    }

    private Button FindOpenButton()
    {
        foreach (var button in GetComponentsInChildren<Button>(true))
        {
            var image = button.GetComponent<Image>();
            var spriteName = image != null && image.sprite != null ? image.sprite.name : string.Empty;
            if (string.Equals(button.name, "Open", StringComparison.OrdinalIgnoreCase)
                || button.name.Contains("打开") || spriteName.Contains("打开"))
                return button;
        }
        return null;
    }

    private void OnValidate()
    {
        enemiesPerTile = float.IsNaN(enemiesPerTile) || float.IsInfinity(enemiesPerTile)
            ? 0.5f : Mathf.Max(0.01f, enemiesPerTile);
        minimumWidth = Mathf.Clamp(minimumWidth, 7, 10);
        maximumWidth = Mathf.Clamp(maximumWidth, minimumWidth, 10);
        minimumHeight = Mathf.Clamp(minimumHeight, 7, 10);
        maximumHeight = Mathf.Clamp(maximumHeight, minimumHeight, 10);
        defaultWidth = Mathf.Clamp(defaultWidth, minimumWidth, maximumWidth);
        defaultHeight = Mathf.Clamp(defaultHeight, minimumHeight, maximumHeight);
        scrollbarThickness = Mathf.Max(6f, scrollbarThickness);
        if (string.IsNullOrWhiteSpace(saveFileName))
        {
            saveFileName = "level-design-grid.json";
        }
    }
}
