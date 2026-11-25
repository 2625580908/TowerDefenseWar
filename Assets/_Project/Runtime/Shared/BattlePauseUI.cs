using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace MRStrategyRTS.Shared
{
    /// <summary>Binds the battle controls authored in each terrain prefab.</summary>
    [DisallowMultipleComponent]
    public sealed class BattlePauseUI : MonoBehaviour
    {
        private EnemyWaveLevelController controller;
        private LevelTerrainBindings bindings;
        [SerializeField] private BattleUiButton editorControl;
        [SerializeField] private BattleUiButton pokeControl;
        [SerializeField] private RectTransform pausePanel;
        [SerializeField] private GameObject actions;
        [SerializeField] private GameObject confirmation;
        [SerializeField] private TextMeshProUGUI confirmationTitle;
        [SerializeField] private TextMeshProUGUI confirmationBody;
        [SerializeField] private BattleUiButton confirmButton;
        [SerializeField] private TextMeshProUGUI levelLabel;
        [SerializeField] private TextMeshProUGUI readyLevelLabel;
        [SerializeField] private GameObject readyPanel;
        [SerializeField] private GameObject readyHint;
        private bool initialized;
        private bool confirmingRestart;
        public bool IsInitialized => initialized;
        private readonly List<(Renderer renderer, MaterialPropertyBlock original)> shadedRenderers = new();

        public void Initialize(LevelTerrainBindings terrainBindings, EnemyWaveLevelController levelController)
        {
            if (initialized) return;
            bindings = terrainBindings;
            controller = levelController;
            if (editorControl == null || pokeControl == null || pausePanel == null
                || actions == null || confirmation == null || confirmationTitle == null
                || confirmationBody == null || confirmButton == null || levelLabel == null
                || readyLevelLabel == null || readyPanel == null || readyHint == null)
            {
                Debug.LogError("地形预制体未绑定完整的开始/暂停 UI，请检查 BattlePauseUI 引用。", this);
                return;
            }
            BindButton(editorControl, "HudStart", controller.HandleBattleControl, false);
            // The level controller owns the Poke listener, preserving device/editor routing.
            BindButton(pokeControl, "HudStart", null, true);
            bindings.editorStartButton = editorControl.gameObject;
            bindings.pokeStartButton = pokeControl.gameObject;
            BindButton(actions.transform.Find("ResumeBattle").GetComponent<BattleUiButton>(),
                "Resume", controller.ResumeGame, true);
            BindButton(actions.transform.Find("RestartBattle").GetComponent<BattleUiButton>(),
                "Restart", () => RequestConfirmation(true), true);
            BindButton(actions.transform.Find("ReturnToMenu").GetComponent<BattleUiButton>(),
                "Menu", () => RequestConfirmation(false), true);
            BindButton(confirmButton, "ConfirmRestart", ConfirmAction, true);
            BindButton(confirmation.transform.Find("Cancel").GetComponent<BattleUiButton>(),
                "Cancel", () => ShowConfirmation(false), true);
            initialized = true;
            Refresh();
        }

        public void Refresh()
        {
            if (!initialized || controller == null) return;
            var visible = !controller.IsLevelFinished;
            editorControl.gameObject.SetActive(Application.isEditor && visible);
            pokeControl.gameObject.SetActive(!Application.isEditor && visible);
            var key = controller.WaveStarted ? "HudPause" : "HudStart";
            editorControl.SetStyle(key, controller.IsPaused);
            pokeControl.SetStyle(key, controller.IsPaused);
            pausePanel.gameObject.SetActive(controller.IsPaused);
            readyPanel.SetActive(!controller.WaveStarted && visible);
            readyHint.SetActive(!controller.WaveStarted && visible);
            ShadeBoard(controller.IsPaused);
            if (!controller.IsPaused) ShowConfirmation(false);
            if (controller.CurrentLevelDefinition != null)
                levelLabel.text = controller.CurrentLevelDefinition.displayName;
            readyLevelLabel.text = levelLabel.text;
        }

        private void RequestConfirmation(bool restart)
        {
            if (!controller.IsPaused) return;
            confirmingRestart = restart;
            confirmationTitle.text = restart ? "确认重新开始？" : "确认返回主菜单？";
            confirmationBody.text = restart ? "本局进度将会清空。\n确定要重新开始吗？" : "本局进度将会清空。\n确定要离开战场吗？";
            confirmButton.SetStyle(restart ? "ConfirmRestart" : "ConfirmMenu");
            ShowConfirmation(true);
            Physics.SyncTransforms();
        }

        private void ShowConfirmation(bool show)
        {
            if (actions == null) return;
            actions.SetActive(!show);
            confirmation.SetActive(show);
            if (controller != null && controller.IsPaused) Physics.SyncTransforms();
        }

        private void ShadeBoard(bool shade)
        {
            if (!shade)
            {
                foreach (var entry in shadedRenderers)
                    if (entry.renderer != null) entry.renderer.SetPropertyBlock(entry.original);
                shadedRenderers.Clear();
                return;
            }
            if (shadedRenderers.Count != 0) return;
            var tiles = transform.Find("_GeneratedTerrainTiles");
            var scenery = transform.Find("Campaign Scenery");
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                if ((tiles == null || !renderer.transform.IsChildOf(tiles))
                    && (scenery == null || !renderer.transform.IsChildOf(scenery))) continue;
                var material = renderer.sharedMaterial;
                if (material == null) continue;
                var original = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(original);
                var darker = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(darker);
                foreach (var property in new[] { "_Color", "_BaseColor", "_EmissionColor", "baseColorFactor", "emissiveFactor" })
                {
                    if (!material.HasProperty(property)) continue;
                    var color = original.HasColor(property) ? original.GetColor(property) : material.GetColor(property);
                    darker.SetColor(property, new Color(color.r * .42f, color.g * .42f, color.b * .42f, color.a));
                }
                if (material.HasProperty("_BattleDim"))
                {
                    var dim = original.HasFloat("_BattleDim") ? original.GetFloat("_BattleDim") : material.GetFloat("_BattleDim");
                    darker.SetFloat("_BattleDim", dim * .42f);
                }
                shadedRenderers.Add((renderer, original));
                renderer.SetPropertyBlock(darker);
            }
        }

        private void ConfirmAction()
        {
            if (!controller.IsPaused) return;
            if (confirmingRestart) bindings.RestartLevel();
            else bindings.ReturnToMainMenu();
        }

        private static void BindButton(BattleUiButton visual, string key,
            UnityEngine.Events.UnityAction action, bool enablePoke)
        {
            if (enablePoke)
            {
                foreach (var collider in visual.GetComponents<Collider>())
                    collider.enabled = !Application.isEditor;
                var filter = visual.GetComponent<XRPokeFilter>();
                if (filter != null) filter.enabled = !Application.isEditor;
            }
            visual.Initialize(key, action, enablePoke);
        }

        private void OnDisable()
        {
            if (initialized && controller != null) controller.ResumeGame();
        }
    }
}
