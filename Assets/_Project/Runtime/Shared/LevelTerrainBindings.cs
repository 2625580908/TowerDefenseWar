using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MRStrategyRTS.Shared
{
    /// <summary>
    /// References UI and controls that belong to one terrain prefab instance.
    /// EnemyWaveLevelController applies these references when the terrain is created.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelTerrainBindings : MonoBehaviour
    {
        [Header("关卡 UI")]
        public TextMeshProUGUI enemyCountText;
        public TextMeshProUGUI goldText;
        public Image baseHealthFillImage;
        [TextArea] public string enemyCountFormat = "怪物: {0}/{1}";
        [TextArea] public string goldFormat = "{0}";

        [Header("开始按钮")]
        [Tooltip("仅在 Unity Editor Play Mode 显示和使用的 Canvas 开始按钮。")]
        public GameObject editorStartButton;
        [Tooltip("PICO 真机使用的 Poke 开始按钮或其父节点。")]
        public GameObject pokeStartButton;

        [Header("胜负页面")]
        [Tooltip("关卡胜利时显示的页面。")]
        public GameObject successPage;
        [Tooltip("关卡失败时显示的页面。")]
        public GameObject failurePage;

        [Header("胜利页面按钮")]
        [Tooltip("胜利后进入下一关。")]
        public Button nextLevelButton;
        [Tooltip("重新开始当前关卡。")]
        public Button restartLevelButton;
        [Tooltip("返回主菜单。")]
        public Button returnToMainMenuButton;

        [Header("失败页面按钮")]
        [Tooltip("失败后重新开始当前关卡。")]
        public Button failureRestartLevelButton;
        [Tooltip("失败后返回主菜单。")]
        public Button failureReturnToMainMenuButton;

        [Header("关卡结果事件")]
        [Tooltip("当前关卡胜利时触发。可绑定胜利 UI 的 SetActive(true)。")]
        public UnityEvent onLevelWon = new();
        [Tooltip("当前关卡失败时触发。可绑定失败 UI 的 SetActive(true)。")]
        public UnityEvent onLevelFailed = new();

        private EnemyWaveLevelController levelController;
        private LevelProgressManager levelProgressManager;
        private GameUIControlller gameUiController;
        private BattlePauseUI battleUi;
        public bool HasBattleUi => battleUi != null && battleUi.IsInitialized;
        public void RefreshBattleUi() => battleUi?.Refresh();

        private void Awake()
        {
            AutoBind();
        }

        /// <summary>
        /// Called by the runtime level controller after this terrain prefab is created.
        /// </summary>
        public void Initialize(EnemyWaveLevelController controller)
        {
            levelController = controller;
            levelProgressManager = FindFirstObjectByType<LevelProgressManager>(
                FindObjectsInactive.Include);
            gameUiController = FindFirstObjectByType<GameUIControlller>(
                FindObjectsInactive.Include);
            var shop = GetComponent<DefenseTowerShop>();
            if (shop == null)
                shop = gameObject.AddComponent<DefenseTowerShop>();
            shop.Initialize(this);
            battleUi = GetComponent<BattlePauseUI>();
            if (battleUi == null) battleUi = gameObject.AddComponent<BattlePauseUI>();
            battleUi.Initialize(this, controller);
            ConfigureResultButtons();
            ConfigureEditorStartButton();
            SetEditorStartButtonVisible(!controller.WaveStarted);
            CloseResultPages();
        }

        public void ShowSuccessPage()
        {
            levelProgressManager?.CompleteCurrentLevel();
            SetResultPages(successPage, failurePage);
            onLevelWon.Invoke();
        }

        public void ShowFailurePage()
        {
            SetResultPages(failurePage, successPage);
            onLevelFailed.Invoke();
        }

        public void RestartLevel()
        {
            CloseResultPages();
            levelController?.RestartLevel();
        }

        public void StartLevelForEditor()
        {
            if (Application.isEditor)
            {
                levelController?.HandleBattleControl();
            }
        }

        public void SetEditorStartButtonVisible(bool visible)
        {
            if (HasBattleUi)
            {
                RefreshBattleUi();
                return;
            }
            if (editorStartButton != null)
            {
                editorStartButton.SetActive(Application.isEditor && visible);
            }
        }

        public void GoToNextLevel()
        {
            levelProgressManager?.CompleteCurrentLevel();
            levelProgressManager?.SelectNextLevel();
        }

        public void ReturnToMainMenu()
        {
            CloseResultPages();
            levelController?.ReturnToLevelMenu();
            gameUiController?.ReturnToMainMenu();
        }

        public void CloseResultPages()
        {
            SetResultPages(null, successPage, failurePage);
        }

        [ContextMenu("Auto Bind Terrain UI")]
        public void AutoBind()
        {
            foreach (var text in GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (enemyCountText == null && HasName(text.gameObject, "enemy", "monster", "怪物"))
                {
                    enemyCountText = text;
                }
                else if (goldText == null && HasName(text.gameObject, "gold", "coin", "金币"))
                {
                    goldText = text;
                }
            }

            foreach (var image in GetComponentsInChildren<Image>(true))
            {
                if (baseHealthFillImage == null && HasName(image.gameObject, "health", "hp", "blood", "血量"))
                {
                    baseHealthFillImage = image;
                }
            }

            foreach (var child in GetComponentsInChildren<Transform>(true))
            {
                if (editorStartButton == null
                    && !HasName(child.gameObject, "poke")
                    && HasName(child.gameObject, "start", "开始"))
                {
                    editorStartButton = child.gameObject;
                }

                if (pokeStartButton == null && HasName(child.gameObject, "pokestart", "poke start", "poke开始"))
                {
                    pokeStartButton = child.gameObject;
                }
            }

        }

        private static bool HasName(GameObject target, params string[] keywords)
        {
            foreach (var keyword in keywords)
            {
                if (target.name.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private void ConfigureResultButtons()
        {
            if (nextLevelButton != null)
                nextLevelButton.gameObject.SetActive(!levelController.IsDesignedLevel);
            ConfigureButton(nextLevelButton, GoToNextLevel);
            ConfigureButton(restartLevelButton, RestartLevel);
            ConfigureButton(returnToMainMenuButton, ReturnToMainMenu);
            ConfigureButton(failureRestartLevelButton, RestartLevel);
            ConfigureButton(failureReturnToMainMenuButton, ReturnToMainMenu);
        }

        private void ConfigureEditorStartButton()
        {
            if (!Application.isEditor || editorStartButton == null)
            {
                return;
            }

            var button = editorStartButton.GetComponentInChildren<Button>(true);
            ConfigureButton(button, StartLevelForEditor);
        }

        private static void ConfigureButton(Button button, UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            // Replace the event object because RemoveAllListeners does not remove
            // persistent Inspector callbacks stored on the terrain prefab.
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(action);
            UIClickAudioPlayer.Register(button);
        }

        private static void SetResultPages(GameObject visiblePage, params GameObject[] hiddenPages)
        {
            if (visiblePage != null)
            {
                visiblePage.SetActive(true);
            }

            foreach (var page in hiddenPages)
            {
                if (page != null && page != visiblePage)
                {
                    page.SetActive(false);
                }
            }
        }
    }
}
