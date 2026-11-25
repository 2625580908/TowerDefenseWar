using System.Collections.Generic;
using MRStrategyRTS.Config;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>Shows a tower's information above its model only while a fingertip touches it.</summary>
[DefaultExecutionOrder(200), DisallowMultipleComponent]
public sealed class DefenseTowerPokeInfo : MonoBehaviour
{
    [SerializeField, Min(0)] private float touchDistance = 0.008f;
    [SerializeField, Min(0)] private float releaseMargin = 0.004f;
    [SerializeField, Min(0)] private float heightAboveModel = 0.035f;
    private const float openDuration = DefenseTowerInfoStyle.OpenDuration;
    private const float closeDuration = DefenseTowerInfoStyle.CloseDuration;

    private TowerDefinition definition;
    private Collider[] towerColliders;
    private Renderer[] modelRenderers;
    private Transform canvasParent;
    private RectTransform card;
    private RectTransform animatedContent;
    private Vector2 contentPosition;
    private Vector3 contentScale;
    private CanvasGroup visibility;
    private Camera viewer;
    private readonly Vector3[] cardCorners = new Vector3[4];
    private bool touching;
    private float progress;
    private bool shopDisplay;
    private bool affordable = true;
    [SerializeField] private DefenseTowerInfoCard infoCard;
    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grab;

    public void SetPurchaseState(bool isShopDisplay, bool canAfford)
    {
        shopDisplay = isShopDisplay;
        affordable = canAfford;
        RefreshPurchaseText();
    }

    private void RefreshPurchaseText()
    {
        if (card == null) return;
        infoCard.SetPurchaseState(shopDisplay, affordable);
    }

    private static readonly List<XRHandSubsystem> hands = new();
    private static readonly List<Vector3> fingertips = new();
    private static XRPokeInteractor[] pokeInteractors = System.Array.Empty<XRPokeInteractor>();
    private static int sampledFrame = -1;
    private static float nextInteractorRefresh;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetTrackingCache()
    {
        hands.Clear();
        fingertips.Clear();
        pokeInteractors = System.Array.Empty<XRPokeInteractor>();
        sampledFrame = -1;
        nextInteractorRefresh = 0;
    }

    public void Initialize(TowerDefinition towerDefinition, TMP_FontAsset labelFont = null)
    {
        if (towerDefinition == null) return;
        definition = towerDefinition;
        towerColliders = GetComponentsInChildren<Collider>(true);
        grab = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        // The Canvas is a sibling, so it never contributes to the tower's bounds.
        modelRenderers = GetComponentsInChildren<Renderer>(true);
        var terrain = GetComponentInParent<MRStrategyRTS.Shared.TerrainTileGridGenerator>();
        if (terrain != null) canvasParent = terrain.transform;
        else if (canvasParent == null) canvasParent = transform.parent;
        if (infoCard == null) infoCard = GetComponentInChildren<DefenseTowerInfoCard>(true);
        if (infoCard == null)
        {
            Debug.LogError("防御塔预制体未绑定参数 UI，请在预制体中添加并绑定 Tower Parameters UI。", this);
            return;
        }
        if (card == null)
        {
            card = infoCard.GetComponent<RectTransform>();
            card.SetParent(canvasParent, false);
            visibility = infoCard.Visibility;
            animatedContent = infoCard.Content;
            contentPosition = animatedContent.anchoredPosition;
            contentScale = animatedContent.localScale;
            visibility.alpha = 0;
            card.gameObject.SetActive(false);
        }
        infoCard.Initialize(definition);
        RefreshPurchaseText();
    }

    private void Update()
    {
        if (definition == null || towerColliders == null) return;
        SampleFingertips();
        touching = IsTouching(fingertips, touching ? touchDistance + releaseMargin : touchDistance);
#if UNITY_EDITOR
        // Mouse hover previews the same animation without requiring a headset.
        if (!touching && Application.isPlaying && fingertips.Count == 0)
        {
            if (viewer == null) viewer = Camera.main;
            if (viewer != null && Physics.Raycast(viewer.ScreenPointToRay(UnityEngine.Input.mousePosition),
                    out var hit, 10f, ~0, QueryTriggerInteraction.Ignore))
                touching = hit.collider.GetComponentInParent<DefenseTowerPokeInfo>() == this;
        }
#endif
        var show = touching || (grab != null && grab.isSelected);
        UpdateVisibility(show, Time.unscaledDeltaTime);
        if (infoCard != null) infoCard.SetSelected(show);
    }

    private bool IsTouching(List<Vector3> points, float distance)
    {
        var squaredDistance = distance * distance;
        foreach (var point in points)
            foreach (var collider in towerColliders)
            {
                if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy) continue;
                if ((collider.ClosestPoint(point) - point).sqrMagnitude <= squaredDistance) return true;
            }
        return false;
    }

    private void UpdateVisibility(bool show, float deltaTime)
    {
        if (card == null) return;
        if (show && progress == 0) infoCard.Refresh(false);
        progress = Mathf.MoveTowards(progress, show ? 1f : 0f,
            deltaTime / Mathf.Max(0.01f, show ? openDuration : closeDuration));
        card.gameObject.SetActive(progress > 0);
        var eased = progress * progress * (3 - 2 * progress);
        visibility.alpha = eased;
        animatedContent.localScale = contentScale * Mathf.Lerp(0.82f, 1f, eased);
        animatedContent.anchoredPosition = contentPosition + new Vector2(0, Mathf.Lerp(-20f, 0f, eased));
    }

    private void LateUpdate()
    {
        if (card == null || !card.gameObject.activeSelf) return;
        if (viewer == null || !viewer.isActiveAndEnabled) viewer = Camera.main;
        var bounds = new Bounds(transform.position, Vector3.zero);
        var found = false;
        foreach (var renderer in modelRenderers)
        {
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
            if (!found) { bounds = renderer.bounds; found = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        card.position = new Vector3(bounds.center.x, bounds.max.y + heightAboveModel, bounds.center.z);
        var direction = viewer != null ? card.position - viewer.transform.position : Vector3.forward;
        if (direction.sqrMagnitude > 0.000001f)
            card.rotation = Quaternion.LookRotation(direction, viewer != null ? viewer.transform.up : Vector3.up);
        // Keep the complete panel above the visible tower head, including a rolled viewer.
        card.GetWorldCorners(cardCorners);
        var lowestCorner = cardCorners[0].y;
        for (var i = 1; i < cardCorners.Length; i++) lowestCorner = Mathf.Min(lowestCorner, cardCorners[i].y);
        var lift = bounds.max.y + heightAboveModel - lowestCorner;
        if (lift > 0) card.position += Vector3.up * lift;
    }

    private static void SampleFingertips()
    {
        if (sampledFrame == Time.frameCount) return;
        sampledFrame = Time.frameCount;
        fingertips.Clear();
        hands.Clear();
        SubsystemManager.GetSubsystems(hands);
        var hasHandSubsystem = false;
        foreach (var subsystem in hands)
        {
            if (!subsystem.running) continue;
            hasHandSubsystem = true;
            AddTip(subsystem.leftHand);
            AddTip(subsystem.rightHand);
        }
        // A running hand subsystem is authoritative: tracking loss closes the card.
        if (hasHandSubsystem) return;
        if (Time.unscaledTime >= nextInteractorRefresh)
        {
            pokeInteractors = FindObjectsByType<XRPokeInteractor>(FindObjectsSortMode.None);
            nextInteractorRefresh = Time.unscaledTime + 0.5f;
        }
        foreach (var poke in pokeInteractors)
            if (poke != null && poke.isActiveAndEnabled)
                fingertips.Add(poke.GetAttachTransform(null).position);
    }

    private static void AddTip(XRHand hand)
    {
        if (hand.isTracked && hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out var pose))
            fingertips.Add(pose.position);
    }

    private void OnDisable()
    {
        touching = false;
        progress = 0;
        if (card != null) card.gameObject.SetActive(false);
    }

    private void OnDestroy() => DestroyCard();

    private void DestroyCard()
    {
        if (card != null)
        {
            card.gameObject.SetActive(false);
            Destroy(card.gameObject);
        }
        card = null;
        infoCard = null;
        touching = false;
        progress = 0;
    }
}
