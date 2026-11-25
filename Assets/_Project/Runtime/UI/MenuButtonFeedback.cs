using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MRStrategyRTS.UI
{
    /// <summary>Visual feedback for the authored Menu buttons, including XR UI pointer events.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class MenuButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler, ISubmitHandler
    {
        private Button button;
        private RectTransform visual;
        private Image image;
        private Outline rim;
        private Color originalColor;
        private readonly HashSet<int> hoveringPointers = new();
        private readonly HashSet<int> pressingPointers = new();
        private bool focused;
        private float clickUntil;
        private float emphasis;
        private float compression;
        private bool initialized;

        public static void Install(GameObject menu)
        {
            if (menu == null) return;
            foreach (var candidate in menu.GetComponentsInChildren<Button>(true))
                if (candidate.GetComponent<MenuButtonFeedback>() == null)
                    candidate.gameObject.AddComponent<MenuButtonFeedback>();
        }

        private void Awake()
        {
            button = GetComponent<Button>();
            var source = button.targetGraphic as Image;
            // Main Menu uses an Image on the Button root, with optional image labels beneath it.
            if (source == null || source.transform != transform) return;
            originalColor = UiButtonPalette.Normal;
            var labels = new List<Transform>();
            foreach (Transform child in transform) labels.Add(child);
            visual = new GameObject("Menu Button Visual", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(LayoutElement)).GetComponent<RectTransform>();
            visual.SetParent(transform, false);
            visual.anchorMin = Vector2.zero;
            visual.anchorMax = Vector2.one;
            visual.sizeDelta = Vector2.zero;
            visual.anchoredPosition = Vector2.zero;
            visual.GetComponent<LayoutElement>().ignoreLayout = true;
            image = visual.GetComponent<Image>();
            image.sprite = source.sprite;
            image.type = source.type;
            image.preserveAspect = source.preserveAspect;
            image.material = source.material;
            image.color = originalColor;
            image.raycastTarget = false;
            foreach (var label in labels) label.SetParent(visual, false);
            // Animate only the art; the Button's layout and pointer hit area remain stable.
            source.color = Color.clear;
            source.raycastTarget = true;
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            rim = visual.gameObject.AddComponent<Outline>();
            rim.effectDistance = new Vector2(3f, -3f);
            rim.effectColor = new Color(1f, .76f, .25f, 0f);
            rim.useGraphicAlpha = true;
            button.onClick.AddListener(OnClicked);
            UIClickAudioPlayer.Register(button);
            initialized = true;
        }

        private bool CanInteract => initialized && button.IsActive() && button.IsInteractable();

        private void Update()
        {
            if (!initialized) return;
            bool enabledState = CanInteract;
            bool pressed = enabledState && pressingPointers.Count > 0;
            bool highlighted = enabledState && (hoveringPointers.Count > 0 || focused);
            float flash = enabledState ? Mathf.Clamp01((clickUntil - Time.unscaledTime) / .18f) : 0f;
            float delta = Time.unscaledDeltaTime;
            emphasis = Mathf.MoveTowards(emphasis, highlighted ? 1f : 0f, delta / .12f);
            compression = Mathf.MoveTowards(compression, pressed ? 1f : 0f, delta / .065f);
            float scale = 1f + emphasis * .035f - compression * .07f + flash * .012f;
            visual.localScale = Vector3.one * scale;
            visual.anchoredPosition = new Vector2(0f, -compression * 6f);
            Color tint = pressed ? UiButtonPalette.Pressed : highlighted ? UiButtonPalette.Hover : UiButtonPalette.Normal;
            if (!enabledState) tint = UiButtonPalette.Disabled;
            image.color = tint;
            float pulse = .88f + .12f * Mathf.Sin(Time.unscaledTime * 3f);
            rim.effectColor = new Color(1f, .76f, .25f,
                enabledState ? Mathf.Clamp01(emphasis * .78f * pulse + compression * .22f + flash * .5f) : 0f);
        }

        public void OnPointerEnter(PointerEventData data)
        {
            if (CanInteract) hoveringPointers.Add(data.pointerId);
            RefreshTint();
        }

        public void OnPointerExit(PointerEventData data)
        {
            hoveringPointers.Remove(data.pointerId);
            pressingPointers.Remove(data.pointerId);
            RefreshTint();
        }

        public void OnPointerDown(PointerEventData data)
        {
            if (CanInteract && data.button == PointerEventData.InputButton.Left)
                pressingPointers.Add(data.pointerId);
            RefreshTint();
        }

        private void RefreshTint()
        {
            if (!initialized) return;
            image.color = !CanInteract ? UiButtonPalette.Disabled : pressingPointers.Count > 0 ? UiButtonPalette.Pressed
                : hoveringPointers.Count > 0 || focused ? UiButtonPalette.Hover : UiButtonPalette.Normal;
        }

        public void OnPointerUp(PointerEventData data) { pressingPointers.Remove(data.pointerId); RefreshTint(); }
        public void OnSelect(BaseEventData data) { focused = !(data is PointerEventData); RefreshTint(); }
        public void OnDeselect(BaseEventData _) { focused = false; RefreshTint(); }
        public void OnSubmit(BaseEventData _) { if (CanInteract) clickUntil = Time.unscaledTime + .18f; }
        private void OnClicked() { if (CanInteract) clickUntil = Time.unscaledTime + .18f; }

        private void OnDisable()
        {
            hoveringPointers.Clear();
            pressingPointers.Clear();
            focused = false;
            clickUntil = emphasis = compression = 0f;
            if (!initialized) return;
            visual.localScale = Vector3.one;
            visual.anchoredPosition = Vector2.zero;
            image.color = originalColor;
            rim.effectColor = new Color(1f, .76f, .25f, 0f);
        }

        private void OnDestroy()
        {
            if (button != null) button.onClick.RemoveListener(OnClicked);
        }
    }
}
