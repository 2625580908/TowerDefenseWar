using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using MRStrategyRTS.UI;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace MRStrategyRTS.Shared
{
    /// <summary>One visual button, with separate mouse and physical Poke routes.</summary>
    [DisallowMultipleComponent]
    public sealed class BattleUiButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler
    {
        private static readonly Dictionary<string, Sprite> Sprites = new();
        private Image image;
        private Button button;
        private UnityAction action;
        private readonly HashSet<int> hoveredPointers = new();
        private readonly HashSet<int> pressedPointers = new();
        private bool pokeHovered;
        private bool pokePressed;
        private XRSimpleInteractable poke;
        [Header("预制体固定外框（配置后状态切换只更新内容）")]
        [SerializeField] private TextMeshProUGUI stateLabel;
        [SerializeField] private Image stateIcon;
        [SerializeField] private Sprite startIcon;
        [SerializeField] private Sprite pauseIcon;
        [SerializeField] private string startLabel = "开始战斗";
        [SerializeField] private string pauseLabel = "暂停游戏";
        [SerializeField] private string pausedLabel = "暂停中";

        public static Sprite LoadSprite(string key)
        {
            if (!Sprites.TryGetValue(key, out var sprite) || sprite == null)
            {
                sprite = Resources.Load<Sprite>("BattleUI/" + key);
                if (sprite == null) Debug.LogError("Missing Battle UI sprite: " + key);
                Sprites[key] = sprite;
            }
            return sprite;
        }

        public void Initialize(string key, UnityAction onClick, bool enablePoke)
        {
            image = GetComponent<Image>();
            button = GetComponent<Button>();
            action = onClick;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.enabled = Application.isEditor;
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(InvokeAction);
            UIClickAudioPlayer.Register(button);
            poke = GetComponent<XRSimpleInteractable>();
            if (poke != null)
            {
                poke.enabled = enablePoke && !Application.isEditor;
                poke.firstHoverEntered.AddListener(OnHoverEntered);
                poke.lastHoverExited.AddListener(OnHoverExited);
                poke.selectEntered.AddListener(OnSelected);
                poke.lastSelectExited.AddListener(OnReleased);
            }
            SetStyle(key);
        }

        public void SetStyle(string key, bool paused = false)
        {
            if (stateLabel != null && stateIcon != null)
            {
                stateLabel.text = paused ? pausedLabel : key == "HudStart" ? startLabel : pauseLabel;
                stateIcon.sprite = key == "HudStart" && !paused ? startIcon : pauseIcon;
            }
            else image.sprite = LoadSprite(paused ? "HudPaused" : key + "_Normal");
            image.overrideSprite = null;
            // Paused is a label/content state, not a disabled interaction state.
            button.transition = Selectable.Transition.None;
            RefreshColor();
        }

        private void InvokeAction()
        {
            if (action == null) return;
            UIClickAudioPlayer.PlayClick();
            action.Invoke();
        }

        private void OnHoverEntered(HoverEnterEventArgs _)
        {
            pokeHovered = true;
            RefreshColor();
        }

        private void OnHoverExited(HoverExitEventArgs _)
        {
            pokeHovered = false;
            pokePressed = false;
            RefreshColor();
        }

        private void OnSelected(SelectEnterEventArgs _)
        {
            pokePressed = true;
            RefreshColor();
            InvokeAction();
        }

        private void OnReleased(SelectExitEventArgs _)
        {
            pokePressed = false;
            RefreshColor();
        }

        private void RefreshColor()
        {
            if (image == null) return;
            image.color = !button.interactable ? UiButtonPalette.Disabled
                : pokePressed || pressedPointers.Count > 0 ? UiButtonPalette.Pressed
                : pokeHovered || hoveredPointers.Count > 0 ? UiButtonPalette.Hover : UiButtonPalette.Normal;
        }

        private void LateUpdate() => RefreshColor();
        public void OnPointerEnter(PointerEventData data) { hoveredPointers.Add(data.pointerId); RefreshColor(); }
        public void OnPointerExit(PointerEventData data) { hoveredPointers.Remove(data.pointerId); pressedPointers.Remove(data.pointerId); RefreshColor(); }
        public void OnPointerDown(PointerEventData data)
        {
            if (button.interactable && data.button == PointerEventData.InputButton.Left) pressedPointers.Add(data.pointerId);
            RefreshColor();
        }
        public void OnPointerUp(PointerEventData data) { pressedPointers.Remove(data.pointerId); RefreshColor(); }
        private void OnDisable()
        {
            hoveredPointers.Clear();
            pressedPointers.Clear();
            pokeHovered = pokePressed = false;
            RefreshColor();
        }
    }
}
