using MRStrategyRTS;
using MRStrategyRTS.RTS;
using MRStrategyRTS.Shared;
using UnityEngine;

/// <summary>Applies distinct purchase colors to shop instances without changing shared materials.</summary>
[DisallowMultipleComponent]
public sealed class DefenseTowerPurchaseVisual : MonoBehaviour
{
    private static readonly int Unavailable = Shader.PropertyToID("_PurchaseUnavailable");
    private static readonly int Available = Shader.PropertyToID("_PurchaseAvailable");
    private static readonly int Accent = Shader.PropertyToID("_PurchaseAccent");
    private XRGrabSnapPlacement placement;
    private DefenseTower tower;
    private Renderer[] renderers;
    private MaterialPropertyBlock properties;
    public bool IsAffordable { get; private set; }

    private void Awake()
    {
        placement = GetComponent<XRGrabSnapPlacement>();
        tower = GetComponentInChildren<DefenseTower>(true);
        renderers = GetComponentsInChildren<Renderer>(true);
        properties = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        GameSessionState.EconomyChanged += Refresh;
        Refresh();
    }

    public void Refresh()
    {
        var shopDisplay = placement != null && placement.IsShopDisplay;
        IsAffordable = !shopDisplay || tower != null
            && (tower.BuildCost == 0 || GameSessionState.CanAffordGold(tower.BuildCost));
        SetPurchaseVisual(shopDisplay, IsAffordable);
        GetComponent<DefenseTowerPokeInfo>()?.SetPurchaseState(shopDisplay, IsAffordable);
    }

    public static Color GetAvailableAccent(Color color)
    {
        Color.RGBToHSV(color, out var hue, out var saturation, out _);
        return Color.HSVToRGB(hue, Mathf.Max(0.85f, saturation), 1f);
    }

    private void SetPurchaseVisual(bool shopDisplay, bool affordable)
    {
        var accent = GetAvailableAccent(tower != null && tower.TowerDefinition != null
            ? tower.TowerDefinition.accentColor : Color.cyan);
        foreach (var renderer in renderers)
        {
            if (renderer == null) continue;
            renderer.GetPropertyBlock(properties);
            properties.SetFloat(Unavailable, shopDisplay && !affordable ? 1f : 0f);
            properties.SetFloat(Available, shopDisplay && affordable ? 1f : 0f);
            properties.SetColor(Accent, accent);
            renderer.SetPropertyBlock(properties);
        }
    }

    private void OnDisable()
    {
        GameSessionState.EconomyChanged -= Refresh;
        SetPurchaseVisual(false, true);
    }
}
