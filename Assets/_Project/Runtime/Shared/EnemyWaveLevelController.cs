using System.Collections;
using System.Collections.Generic;
using MRStrategyRTS.AI;
using MRStrategyRTS.Config;
using MRStrategyRTS.RTS;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace MRStrategyRTS.Shared
{
    /// <summary>
    /// Spawns one enemy wave and reduces base health when an enemy reaches Destination.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyWaveLevelController : MonoBehaviour
    {
        [Header("关卡配置")]
        [Tooltip("默认关卡配置。关卡选择器切换关卡时会自动替换此配置。")]
        [SerializeField] private LevelDefinition levelDefinition;

        [Header("战斗背景音乐")]
        [Tooltip("未指定时自动使用 Resources/Audio/TowerDefenseBattleMusic。")]
        [SerializeField] private AudioClip battleMusic;
        [SerializeField, Range(0f, 1f)] private float battleMusicVolume = 0.22f;

        [Header("事件")]
        public UnityEvent onGameStarted = new();
        public UnityEvent onWaveCompleted = new();
        public UnityEvent onBaseDefeated = new();
        public UnityEvent<int> onGoldChanged = new();

        public int BaseHealth { get; private set; }
        public int Gold { get; private set; }
        public int SpawnedEnemyCount { get; private set; }
        public int PlannedEnemyCount => ExpectedEnemyCount;
        public int ReachedDestinationCount { get; private set; }
        public bool WaveStarted { get; private set; }
        public bool IsPaused { get; private set; }
        public bool IsLevelFinished => baseDefeated || levelWon;
        public LevelDefinition CurrentLevelDefinition => levelDefinition;
        public GameObject CurrentTerrainInstance => runtimeTerrainInstance;
        public bool IsDesignedLevel { get; private set; }
        public event UnityAction LevelWon;
        public event UnityAction LevelFailed;

        public bool CanAfford(int goldCost)
        {
            return !IsPaused && goldCost >= 0 && Gold >= goldCost;
        }

        public bool TrySpendGold(int goldCost)
        {
            if (!CanAfford(goldCost))
            {
                return false;
            }

            Gold -= goldCost;
            UpdateUi();
            onGoldChanged.Invoke(Gold);
            return true;
        }

        public void AddGold(int goldAmount)
        {
            if (goldAmount <= 0)
            {
                return;
            }

            Gold += goldAmount;
            UpdateUi();
            onGoldChanged.Invoke(Gold);
        }

        private TerrainTileGridGenerator terrain;
        private Transform spawnParent;
        private GameObject runtimeTerrainInstance;
        private LevelTerrainBindings terrainBindings;
        private TextMeshProUGUI enemyCountText;
        private TextMeshProUGUI goldText;
        private Image baseHealthFillImage;
        private GameObject pokeStartButton;
        private XRSimpleInteractable pokeStartInteractable;
        private string enemyCountFormat = "怪物: {0}/{1}";
        private string goldFormat = "{0}";
        private readonly HashSet<UnitCombat> activeEnemies = new();
        private readonly Dictionary<UnitCombat, int> enemyBaseDamages = new();
        private Coroutine waveCoroutine;
        private bool baseDefeated;
        private bool levelWon;
        private LevelDefinition previousLevelDefinition;
        private LevelFeedbackPlayer feedback;
        private float timeScaleBeforePause = 1f;
        private bool audioPauseBeforePause;
        private AudioSource battleMusicSource;

        public void HandleBattleControl()
        {
            if (IsPaused) ResumeGame();
            else if (WaveStarted) PauseGame();
            else StartGame();
        }

        public void PauseGame()
        {
            if (IsPaused || !WaveStarted || IsLevelFinished || Time.timeScale <= 0f) return;
            timeScaleBeforePause = Time.timeScale;
            audioPauseBeforePause = AudioListener.pause;
            IsPaused = true;
            MRStrategyRTS.GameSessionState.SetBattlePaused(true);
            Time.timeScale = 0f;
            AudioListener.pause = true;
            terrainBindings?.RefreshBattleUi();
            // Poke queries must see the just-enabled menu even while physics is frozen.
            Physics.SyncTransforms();
        }

        public void ResumeGame()
        {
            if (!IsPaused) return;
            IsPaused = false;
            Time.timeScale = timeScaleBeforePause;
            AudioListener.pause = audioPauseBeforePause;
            MRStrategyRTS.GameSessionState.SetBattlePaused(false);
            terrainBindings?.RefreshBattleUi();
        }

        private void OnDisable()
        {
            ResumeGame();
            StopBattleMusic();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) PauseGame();
        }

        private void Awake()
        {
            onGoldChanged.AddListener(MRStrategyRTS.GameSessionState.NotifyGoldChanged);
            feedback = GetComponent<LevelFeedbackPlayer>();
            if (feedback == null)
                feedback = gameObject.AddComponent<LevelFeedbackPlayer>();
            MRStrategyRTS.GameSessionState.RegisterEconomy(CanAfford, TrySpendGold, AddGold);
            MRStrategyRTS.GameSessionState.SetLevelRunning(false);
            PrepareTerrainForLevel();
            ResetLevelState();
            SetPokeStartButtonActive(true);

            UpdateUi();
        }

        /// <summary>
        /// Changes all definition-driven state. The terrain prefab is instantiated
        /// later when the game UI is opened.
        /// </summary>
        public void LoadLevel(LevelDefinition definition)
        {
            if (definition == null)
            {
                Debug.LogWarning("Cannot load a null Level Definition.", this);
                return;
            }

            ClearLevelEntities();
            PrepareTerrainForLevel();
            levelDefinition = definition;
            ResetLevelState();
            UpdateUi();
            onGoldChanged.Invoke(Gold);
            SetPokeStartButtonActive(true);
        }

        /// <summary>Adopts the already-generated map and owns its temporary level/wave definitions.</summary>
        public bool LoadDesignedLevel(LevelDefinition definition, GameObject terrainInstance)
        {
            var generator = terrainInstance != null
                ? terrainInstance.GetComponentInChildren<TerrainTileGridGenerator>(true)
                : null;
            var bindings = terrainInstance != null
                ? terrainInstance.GetComponentInChildren<LevelTerrainBindings>(true)
                : null;
            if (definition == null || generator == null || bindings == null)
            {
                Debug.LogError("设计关卡需要关卡配置、地形生成器和 LevelTerrainBindings。", this);
                return false;
            }

            ClearLevelEntities();
            PrepareTerrainForLevel();
            previousLevelDefinition = levelDefinition;
            levelDefinition = definition;
            IsDesignedLevel = true;
            runtimeTerrainInstance = terrainInstance;
            terrain = generator;
            spawnParent = generator.transform;
            ResetLevelState();
            ApplyTerrainBindings(terrainInstance);
            onGoldChanged.Invoke(Gold);
            return true;
        }

        /// <summary>
        /// Instantiates the selected level's terrain when gameplay is entered.
        /// </summary>
        public GameObject CreateTerrainForCurrentLevel()
        {
            if (runtimeTerrainInstance != null)
            {
                return runtimeTerrainInstance;
            }

            var terrainPrefab = levelDefinition != null ? levelDefinition.terrainPrefab : null;
            if (terrainPrefab == null)
            {
                Debug.LogWarning(
                    "Select a level with a Terrain Prefab before opening the game.",
                    this);
                return null;
            }

            runtimeTerrainInstance = Instantiate(terrainPrefab);
            runtimeTerrainInstance.name = $"{terrainPrefab.name} (Level Terrain)";
            runtimeTerrainInstance.SetActive(true);
            terrain = runtimeTerrainInstance.GetComponentInChildren<TerrainTileGridGenerator>(true);
            ApplyTerrainBindings(runtimeTerrainInstance);
            if (terrain == null)
            {
                Debug.LogError(
                    $"Level terrain prefab '{terrainPrefab.name}' requires a TerrainTileGridGenerator.",
                    runtimeTerrainInstance);
            }
            else
            {
                spawnParent = terrain.transform;
            }

            Debug.Log(
                $"Created level terrain '{runtimeTerrainInstance.name}' for '{levelDefinition.displayName}'.",
                runtimeTerrainInstance);
            return runtimeTerrainInstance;
        }

        private void ApplyTerrainBindings(GameObject terrainInstance)
        {
            var bindings = terrainInstance.GetComponentInChildren<LevelTerrainBindings>(true);
            if (bindings == null)
            {
                Debug.LogWarning(
                    $"Level terrain prefab '{terrainInstance.name}' has no LevelTerrainBindings. "
                    + "Its terrain UI will not be connected to EnemyWaveLevelController.",
                    terrainInstance);
                return;
            }

            bindings.AutoBind();
            bindings.Initialize(this);
            terrainBindings = bindings;
            enemyCountText = bindings.enemyCountText;
            goldText = bindings.goldText;
            baseHealthFillImage = bindings.baseHealthFillImage;
            pokeStartButton = bindings.pokeStartButton;
            enemyCountFormat = bindings.enemyCountFormat;
            goldFormat = bindings.goldFormat;
            BindPokeStartButton();
            SetPokeStartButtonActive(!WaveStarted);
            UpdateUi();
        }

        private void OnDestroy()
        {
            ResumeGame();
            StopBattleMusic();
            onGoldChanged.RemoveListener(MRStrategyRTS.GameSessionState.NotifyGoldChanged);
            feedback?.Clear();
            UnbindPokeStartButton();
            ReleaseDesignedLevel();
            MRStrategyRTS.GameSessionState.SetLevelRunning(false);
            MRStrategyRTS.GameSessionState.ClearEconomy();
        }

        private void BindPokeStartButton()
        {
            UnbindPokeStartButton();
            if (pokeStartButton == null)
            {
                return;
            }

            pokeStartInteractable = pokeStartButton.GetComponentInChildren<XRSimpleInteractable>(true);
            if (pokeStartInteractable != null)
            {
                pokeStartInteractable.selectEntered.AddListener(OnPokeStartSelected);
            }
        }

        private void UnbindPokeStartButton()
        {
            if (pokeStartInteractable != null)
            {
                pokeStartInteractable.selectEntered.RemoveListener(OnPokeStartSelected);
                pokeStartInteractable = null;
            }
        }

        private void OnPokeStartSelected(SelectEnterEventArgs _)
        {
            UIClickAudioPlayer.PlayClick();
            HandleBattleControl();
        }

        [ContextMenu("Start Game")]
        public void StartGame()
        {
            if (WaveStarted || IsPaused)
            {
                return;
            }

            if (!TryValidateWaveSetup())
            {
                return;
            }

            WaveStarted = true;
            MRStrategyRTS.GameSessionState.SetLevelRunning(true);
            terrainBindings?.SetEditorStartButtonVisible(false);
            SetPokeStartButtonActive(false);
            terrainBindings?.RefreshBattleUi();
            PlayBattleMusic();
            onGameStarted.Invoke();
            waveCoroutine = StartCoroutine(SpawnWave());
        }

        private void PlayBattleMusic()
        {
            if (battleMusic == null)
                battleMusic = Resources.Load<AudioClip>("Audio/TowerDefenseBattleMusic");
            if (battleMusic == null) return;
            if (battleMusicSource == null)
            {
                // Keep music separate from the existing level-feedback and UI sound sources.
                battleMusicSource = gameObject.AddComponent<AudioSource>();
                battleMusicSource.playOnAwake = false;
                battleMusicSource.loop = true;
                battleMusicSource.spatialBlend = 0f;
                battleMusicSource.ignoreListenerPause = false;
                battleMusicSource.priority = 128;
            }
            battleMusicSource.clip = battleMusic;
            battleMusicSource.volume = battleMusicVolume;
            battleMusicSource.Play();
        }

        private void StopBattleMusic()
        {
            if (battleMusicSource != null) battleMusicSource.Stop();
        }

        [ContextMenu("Clear Level Entities")]
        public void ClearLevelEntities()
        {
            ResumeGame();
            StopBattleMusic();
            feedback?.Clear();
            if (waveCoroutine != null)
            {
                StopCoroutine(waveCoroutine);
                waveCoroutine = null;
            }

            var destroyedObjects = new HashSet<int>();
            foreach (var walker in FindObjectsByType<MonsterPathWalker>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                walker.SetMovementPaused(true);
                DestroyLevelObject(walker.gameObject, destroyedObjects);
            }

            foreach (var enemy in activeEnemies)
            {
                if (enemy != null)
                {
                    DestroyLevelObject(enemy.gameObject, destroyedObjects);
                }
            }

            foreach (var tower in FindObjectsByType<DefenseTower>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                var placement = tower.GetComponentInParent<XRGrabSnapPlacement>();
                if (placement == null || !placement.IsPlacedOnMap)
                {
                    continue;
                }

                tower.enabled = false;
                DestroyLevelObject(placement.gameObject, destroyedObjects);
            }

            foreach (var projectile in FindObjectsByType<UnitProjectile>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                projectile.enabled = false;
                DestroyLevelObject(projectile.gameObject, destroyedObjects);
            }

            activeEnemies.Clear();
            enemyBaseDamages.Clear();
        }

        [ContextMenu("Restart Level")]
        public void RestartLevel()
        {
            ClearLevelEntities();
            ResetLevelState();
            UpdateUi();
            onGoldChanged.Invoke(Gold);

            if (pokeStartButton != null)
            {
                SetPokeStartButtonActive(true);
            }

            terrainBindings?.SetEditorStartButtonVisible(true);
            terrainBindings?.RefreshBattleUi();
        }

        private void SetPokeStartButtonActive(bool isActive)
        {
            if (terrainBindings != null && terrainBindings.HasBattleUi)
            {
                terrainBindings.RefreshBattleUi();
                return;
            }
            if (pokeStartButton != null)
            {
                pokeStartButton.SetActive(isActive);
            }
        }

        /// <summary>
        /// Clears active gameplay and removes the dynamically created terrain instance.
        /// </summary>
        public void ReturnToLevelMenu()
        {
            ClearLevelEntities();
            PrepareTerrainForLevel();
            ResetLevelState();
        }

        private IEnumerator SpawnWave()
        {
            foreach (var wave in levelDefinition.waves)
            {
                if (baseDefeated)
                {
                    yield break;
                }

                if (wave.startDelay > 0f)
                {
                    yield return new WaitForSeconds(wave.startDelay);
                }

                foreach (var spawnEntry in wave.monsterSpawns)
                {
                    for (var index = 0; index < spawnEntry.count && !baseDefeated; index++)
                    {
                        SpawnEnemy(spawnEntry.monster);
                        if (index < spawnEntry.count - 1 && spawnEntry.spawnInterval > 0f)
                        {
                            yield return new WaitForSeconds(spawnEntry.spawnInterval);
                        }
                    }
                }
            }

            CheckForLevelWon();
        }

        private void SpawnEnemy(MonsterDefinition definition)
        {
            terrain.TryGetPathMarkers(out var origin, out _);
            var prefab = definition.prefab;
            var spawnPosition = origin.TransformPoint(levelDefinition.monsterSpawnLocalOffset)
                + levelDefinition.monsterSpawnWorldOffset;
            spawnPosition.y += definition.groundHeightOffset;
            var enemy = Instantiate(
                prefab,
                spawnPosition,
                Quaternion.identity,
                spawnParent);
            enemy.name = $"{prefab.name} ({SpawnedEnemyCount + 1:D2})";
            SpawnedEnemyCount++;

            var walker = enemy.GetComponentInChildren<MonsterPathWalker>();
            var combat = enemy.GetComponentInChildren<UnitCombat>();
            if (combat != null)
            {
                combat.ApplyDefinition(definition);
                activeEnemies.Add(combat);
                enemyBaseDamages[combat] = definition.baseDamage;
                combat.Died += OnEnemyDied;
                combat.KilledBy += OnEnemyKilled;
            }

            if (walker == null)
            {
                Debug.LogWarning("Enemy Wave Controller requires MonsterPathWalker on the enemy prefab.", enemy);
            }
            else
            {
                walker.ApplyDefinition(definition);
                walker.SetPath(terrain);
                walker.DestinationReached += OnEnemyReachedDestination;
            }

            UpdateUi();
        }

        private void OnEnemyReachedDestination(MonsterPathWalker walker)
        {
            walker.DestinationReached -= OnEnemyReachedDestination;
            if (baseDefeated || levelWon)
            {
                return;
            }
            var combat = walker.GetComponentInChildren<UnitCombat>();
            if (combat != null && !combat.IsAlive)
            {
                return;
            }

            if (combat == null || !enemyBaseDamages.TryGetValue(combat, out var baseDamage))
            {
                Debug.LogWarning(
                    "Reached Destination enemy has no Monster Definition base-damage data.",
                    walker);
                Destroy(walker.gameObject);
                return;
            }

            var previousHealth = BaseHealth;
            BaseHealth = Mathf.Max(0, BaseHealth - Mathf.Max(0, baseDamage));
            UpdateUi();
            if (BaseHealth < previousHealth)
                feedback?.PlayDamage();

            if (BaseHealth == 0 && !baseDefeated)
            {
                baseDefeated = true;
                feedback?.PlayResult(false);
                StopLevelGameplay();
                if (waveCoroutine != null)
                {
                    StopCoroutine(waveCoroutine);
                    waveCoroutine = null;
                }

                onBaseDefeated.Invoke();
                terrainBindings?.ShowFailurePage();
                LevelFailed?.Invoke();
            }
            else
            {
                ResolveEnemy(combat);
            }

            Destroy(walker.gameObject);
        }

        private void StopLevelGameplay()
        {
            ResumeGame();
            StopBattleMusic();
            terrainBindings?.RefreshBattleUi();
            foreach (var tower in FindObjectsByType<DefenseTower>(
                         FindObjectsInactive.Exclude,
                         FindObjectsSortMode.None))
            {
                tower.enabled = false;
            }

            foreach (var walker in FindObjectsByType<MonsterPathWalker>(
                         FindObjectsInactive.Exclude,
                         FindObjectsSortMode.None))
            {
                walker.SetMovementPaused(true);
            }

            foreach (var projectile in FindObjectsByType<UnitProjectile>(
                         FindObjectsInactive.Exclude,
                         FindObjectsSortMode.None))
            {
                Destroy(projectile.gameObject);
            }
        }

        private static void DestroyLevelObject(GameObject target, HashSet<int> destroyedObjects)
        {
            if (target == null || !destroyedObjects.Add(target.GetInstanceID()))
            {
                return;
            }

            Destroy(target);
        }

        private void OnEnemyDied(UnitCombat combat)
        {
            ResolveEnemy(combat);
            CheckForLevelWon();
        }

        private void OnEnemyKilled(UnitCombat combat, GameObject attacker)
        {
            if (attacker == null || attacker.GetComponentInParent<DefenseTower>() == null)
            {
                return;
            }

            AddGold(combat.KillGoldReward);
        }

        private void ResolveEnemy(UnitCombat combat)
        {
            if (combat == null || !activeEnemies.Remove(combat))
            {
                return;
            }

            combat.Died -= OnEnemyDied;
            combat.KilledBy -= OnEnemyKilled;
            enemyBaseDamages.Remove(combat);
            ReachedDestinationCount++;
            CheckForLevelWon();
        }

        private void CheckForLevelWon()
        {
            if (levelWon || baseDefeated || SpawnedEnemyCount < ExpectedEnemyCount || activeEnemies.Count > 0)
            {
                return;
            }

            levelWon = true;
            ResumeGame();
            StopBattleMusic();
            terrainBindings?.RefreshBattleUi();
            feedback?.PlayResult(true);
            onWaveCompleted.Invoke();
            terrainBindings?.ShowSuccessPage();
            LevelWon?.Invoke();
        }

        private bool TryValidateWaveSetup()
        {
            if (levelDefinition == null)
            {
                Debug.LogWarning(
                    "EnemyWaveLevelController requires a Level Definition.",
                    this);
                return false;
            }

            if (spawnParent == null || !ResolveTerrain()
                || !terrain.TryGetPathMarkers(out _, out _))
            {
                Debug.LogWarning(
                    "EnemyWaveLevelController requires generated terrain with Origin/Destination markers.",
                    this);
                return false;
            }

            foreach (var wave in levelDefinition.waves)
            {
                if (wave == null || wave.monsterSpawns == null)
                {
                    Debug.LogWarning("Level Definition contains an invalid wave.", this);
                    return false;
                }

                foreach (var spawnEntry in wave.monsterSpawns)
                {
                    if (spawnEntry == null || spawnEntry.monster == null
                        || spawnEntry.monster.prefab == null)
                    {
                        Debug.LogWarning(
                            "Every wave spawn entry needs a Monster Definition with a Prefab.",
                            this);
                        return false;
                    }
                }
            }

            return true;
        }

        private bool ResolveTerrain()
        {
            if (terrain == null && spawnParent != null)
            {
                terrain = spawnParent.GetComponentInParent<TerrainTileGridGenerator>();
            }

            if (terrain == null)
            {
                terrain = FindFirstObjectByType<TerrainTileGridGenerator>();
            }

            return terrain != null;
        }

        private void PrepareTerrainForLevel()
        {
            UnbindPokeStartButton();
            if (runtimeTerrainInstance != null)
            {
                runtimeTerrainInstance.SetActive(false);
                Destroy(runtimeTerrainInstance);
                runtimeTerrainInstance = null;
            }

            terrain = null;
            terrainBindings = null;
            spawnParent = null;
            pokeStartButton = null;
            enemyCountText = null;
            goldText = null;
            baseHealthFillImage = null;
            ReleaseDesignedLevel();
        }

        private void ReleaseDesignedLevel()
        {
            if (!IsDesignedLevel)
                return;
            DesignedLevelFactory.Release(levelDefinition);
            levelDefinition = previousLevelDefinition;
            previousLevelDefinition = null;
            IsDesignedLevel = false;
        }

        private void UpdateUi()
        {
            if (enemyCountText != null)
            {
                enemyCountText.text = string.Format(enemyCountFormat, SpawnedEnemyCount, ExpectedEnemyCount);
            }

            if (goldText != null)
            {
                goldText.text = string.Format(goldFormat, Gold);
            }

            if (baseHealthFillImage != null)
            {
                baseHealthFillImage.fillAmount = (float)BaseHealth / CurrentMaxBaseHealth;
            }
        }

        private int CurrentMaxBaseHealth => levelDefinition != null
            ? levelDefinition.baseHealth
            : 1;

        private int ExpectedEnemyCount
        {
            get
            {
                var count = 0;
                if (levelDefinition == null)
                {
                    return count;
                }

                foreach (var wave in levelDefinition.waves)
                {
                    if (wave == null || wave.monsterSpawns == null)
                    {
                        continue;
                    }

                    foreach (var spawnEntry in wave.monsterSpawns)
                    {
                        if (spawnEntry != null)
                        {
                            count += spawnEntry.count;
                        }
                    }
                }

                return count;
            }
        }

        private void ResetLevelState()
        {
            ResumeGame();
            StopBattleMusic();
            feedback?.Clear();
            BaseHealth = CurrentMaxBaseHealth;
            Gold = levelDefinition != null ? levelDefinition.startingGold : 0;
            SpawnedEnemyCount = 0;
            ReachedDestinationCount = 0;
            WaveStarted = false;
            MRStrategyRTS.GameSessionState.SetLevelRunning(false);
            baseDefeated = false;
            levelWon = false;
            terrainBindings?.RefreshBattleUi();
        }
    }
}
