using System.Collections.Generic;
using MRStrategyRTS.Config;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MRStrategyRTS.Shared
{
    /// <summary>
    /// Persists completed levels and builds a sequential six-level selection UI.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelProgressManager : MonoBehaviour
    {
        private const int LevelCount = 6;
        private const string CompletionKeyPrefix = "MRStrategyRTS.LevelProgress.Completed.";

        [Header("关卡")]
        [Tooltip("固定配置六个关卡。后一关需要前一关通关后才能解锁。")]
        [SerializeField] private LevelDefinition[] levels = new LevelDefinition[LevelCount];
        [SerializeField] private EnemyWaveLevelController levelController;
        [SerializeField] private GameUIControlller gameUiController;
        [Tooltip("GameUIControlller 中游戏页面对应的 UI 索引。")]
        [SerializeField, Min(0)] private int gameUiId = 2;

        [Header("关卡选择 UI")]
        [SerializeField] private Transform levelButtonParent;
        [Tooltip("已解锁关卡使用的按钮预制体。")]
        [SerializeField] private GameObject unlockedLevelButtonPrefab;
        [Tooltip("未解锁关卡使用的按钮预制体。")]
        [SerializeField] private GameObject lockedLevelButtonPrefab;

        [Header("事件")]
        [SerializeField] private UnityEvent<int> onLevelSelected = new();
        [SerializeField] private UnityEvent<int> onLevelCompleted = new();

        private bool subscribed;
        public IReadOnlyList<LevelDefinition> ConfiguredLevels => levels;

        private void Awake()
        {
            ResolveAndSubscribeToLevelController();

            if (gameUiController == null)
            {
                gameUiController = FindFirstObjectByType<GameUIControlller>(
                    FindObjectsInactive.Include);
            }
        }

        private void OnEnable()
        {
            ResolveAndSubscribeToLevelController();
        }

        private void Start()
        {
            EnsureInitialLevelLoaded();
            RefreshLevelButtons();
        }

        private void OnDisable()
        {
            UnsubscribeFromLevelController();
        }

        /// <summary>
        /// Rebuilds all six level buttons from local completion state.
        /// </summary>
        [ContextMenu("Refresh Level Buttons")]
        public void RefreshLevelButtons()
        {
            if (levelButtonParent == null)
            {
                Debug.LogWarning("LevelProgressManager requires a Level Button Parent.", this);
                return;
            }

            ClearGeneratedButtons();
            for (var levelIndex = 0; levelIndex < LevelCount; levelIndex++)
            {
                var unlocked = IsLevelUnlocked(levelIndex);
                var prefab = unlocked ? unlockedLevelButtonPrefab : lockedLevelButtonPrefab;
                if (prefab == null)
                {
                    Debug.LogWarning(
                        $"LevelProgressManager needs a {(unlocked ? "unlocked" : "locked")} button prefab.",
                        this);
                    continue;
                }

                var buttonObject = Instantiate(prefab, levelButtonParent);
                buttonObject.name = $"Level {levelIndex + 1} {(unlocked ? "Unlocked" : "Locked")}";
                ConfigureButton(buttonObject, levelIndex, unlocked);
            }
        }

        /// <summary>
        /// Selects an unlocked level using a zero-based index.
        /// </summary>
        public void SelectLevel(int levelIndex)
        {
            if (!IsValidLevelIndex(levelIndex) || !IsLevelUnlocked(levelIndex))
            {
                return;
            }

            var definition = levels[levelIndex];
            if (definition == null)
            {
                Debug.LogWarning($"Level {levelIndex + 1} has no Level Definition.", this);
                return;
            }

            if (levelController != null)
            {
                levelController.LoadLevel(definition);
            }

            gameUiController?.OpenGame(gameUiId);
            onLevelSelected.Invoke(levelIndex);
        }

        /// <summary>
        /// Selects the level immediately after the active level when it is unlocked.
        /// </summary>
        public void SelectNextLevel()
        {
            ResolveAndSubscribeToLevelController();
            if (levelController != null && levelController.IsDesignedLevel)
                return;

            var currentIndex = GetLevelIndex(levelController != null
                ? levelController.CurrentLevelDefinition
                : null);
            var nextIndex = currentIndex + 1;
            if (currentIndex < 0 || !IsValidLevelIndex(nextIndex))
            {
                Debug.Log("The current level is the last configured level.", this);
                return;
            }

            SelectLevel(nextIndex);
        }

        /// <summary>
        /// Persists the active level as completed. Safe to call more than once.
        /// </summary>
        public void CompleteCurrentLevel()
        {
            ResolveAndSubscribeToLevelController();
            if (levelController == null || levelController.IsDesignedLevel)
            {
                return;
            }

            var levelIndex = GetLevelIndex(levelController.CurrentLevelDefinition);
            if (levelIndex < 0)
            {
                Debug.LogWarning(
                    "The current Level Definition is not configured in LevelProgressManager.",
                    this);
                return;
            }

            var wasCompleted = IsLevelCompleted(levelIndex);
            PlayerPrefs.SetInt(GetCompletionKey(levelIndex), 1);
            PlayerPrefs.Save();
            if (!wasCompleted)
            {
                onLevelCompleted.Invoke(levelIndex);
            }

            RefreshLevelButtons();
        }

        [ContextMenu("Reset Local Level Progress")]
        public void ResetLocalLevelProgress()
        {
            for (var levelIndex = 0; levelIndex < LevelCount; levelIndex++)
            {
                PlayerPrefs.DeleteKey(GetCompletionKey(levelIndex));
            }

            PlayerPrefs.Save();
            RefreshLevelButtons();
        }

        public bool IsLevelUnlocked(int levelIndex)
        {
            if (!IsValidLevelIndex(levelIndex) || levels[levelIndex] == null)
            {
                return false;
            }

            return levelIndex == 0 || IsLevelCompleted(levelIndex - 1);
        }

        public bool IsLevelCompleted(int levelIndex)
        {
            return IsValidLevelIndex(levelIndex)
                && PlayerPrefs.GetInt(GetCompletionKey(levelIndex), 0) == 1;
        }

        public void EnsureInitialLevelLoaded()
        {
            ResolveAndSubscribeToLevelController();

            if (levelController == null || levelController.CurrentLevelDefinition != null)
            {
                return;
            }

            foreach (var definition in levels)
            {
                if (definition != null)
                {
                    levelController.LoadLevel(definition);
                    return;
                }
            }

            Debug.LogWarning("LevelProgressManager has no Level Definition to load.", this);
        }

        private void OnLevelWon()
        {
            CompleteCurrentLevel();
        }

        private void ConfigureButton(GameObject buttonObject, int levelIndex, bool unlocked)
        {
            var label = buttonObject.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = $"第{levelIndex + 1}关";
            }

            var button = buttonObject.GetComponentInChildren<Button>(true);
            if (button == null)
            {
                Debug.LogWarning("Level button prefab requires a Unity UI Button.", buttonObject);
                return;
            }

            button.interactable = unlocked;
            button.onClick.RemoveAllListeners();
            if (unlocked)
            {
                button.onClick.AddListener(() => SelectLevel(levelIndex));
            }
            UIClickAudioPlayer.Register(button);
        }

        private void ResolveAndSubscribeToLevelController()
        {
            if (levelController == null)
            {
                levelController = FindFirstObjectByType<EnemyWaveLevelController>(
                    FindObjectsInactive.Include);
            }

            if (subscribed || levelController == null)
            {
                return;
            }

            levelController.LevelWon += OnLevelWon;
            subscribed = true;
        }

        private void UnsubscribeFromLevelController()
        {
            if (!subscribed || levelController == null)
            {
                return;
            }

            levelController.LevelWon -= OnLevelWon;
            subscribed = false;
        }

        private void ClearGeneratedButtons()
        {
            for (var index = levelButtonParent.childCount - 1; index >= 0; index--)
            {
                var child = levelButtonParent.GetChild(index).gameObject;
                if (Application.isPlaying)
                {
                    Destroy(child);
                }
                else
                {
                    DestroyImmediate(child);
                }
            }
        }

        private int GetLevelIndex(LevelDefinition definition)
        {
            for (var index = 0; index < levels.Length; index++)
            {
                if (levels[index] == definition)
                {
                    return index;
                }
            }

            return -1;
        }

        private static bool IsValidLevelIndex(int levelIndex)
        {
            return levelIndex >= 0 && levelIndex < LevelCount;
        }

        private static string GetCompletionKey(int levelIndex)
        {
            return $"{CompletionKeyPrefix}{levelIndex}";
        }
    }
}
