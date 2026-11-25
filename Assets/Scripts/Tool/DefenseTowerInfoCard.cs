using System.Collections.Generic;
using System.Globalization;
using MRStrategyRTS.Config;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Shared presentation for shop and placed towers; never intercepts grab/poke input.</summary>
[DisallowMultipleComponent]
public sealed class DefenseTowerInfoCard : MonoBehaviour
{
    private readonly Dictionary<string, TextMeshProUGUI> labels = new();
    private readonly Dictionary<string, float> changes = new();
    private readonly Dictionary<string, Color> normalColors = new();
    private readonly Dictionary<string, Vector3> normalScales = new();
    [SerializeField] private TowerDefinition definition;
    [SerializeField] private CanvasGroup group;
    [SerializeField] private Image selection;
    [SerializeField] private RectTransform content;
    [SerializeField] private TextMeshProUGUI[] parameterLabels;
    [SerializeField] private Color availableColor = new(.62f, .88f, .52f);
    [SerializeField] private Color unavailableColor = new(1f, .57f, .48f);
    [SerializeField] private Color purchasedColor = new(.7f, .75f, .72f);
    [SerializeField] private Color updateTint = new(.9f, .75f, .39f);
    private bool shopDisplay;
    private bool affordable = true;
    private bool selected;
    private float nextRefresh;
    private float stateFlash;
    public RectTransform Content => content;
    public CanvasGroup Visibility => group;
    private static string Number(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private void Awake() => BindReferences();

    public void Initialize(TowerDefinition towerDefinition)
    {
        definition = towerDefinition;
        BindReferences();
        Refresh(false);
    }

    private void BindReferences()
    {
        if (labels.Count != 0 || parameterLabels == null) return;
        foreach (var label in parameterLabels)
        {
            if (label == null) continue;
            labels[label.name] = label;
            changes[label.name] = 0;
            normalColors[label.name] = label.color;
            normalScales[label.name] = label.rectTransform.localScale;
        }
        // Font, layout, icons, background and raycast settings are authored in the prefab.
    }

    public void SetPurchaseState(bool isShopDisplay, bool canAfford)
    {
        if (shopDisplay == isShopDisplay && affordable == canAfford) return;
        shopDisplay = isShopDisplay;
        affordable = canAfford;
        Refresh(true);
    }

    public void SetSelected(bool value) => selected = value;

    public void Refresh(bool animate = true)
    {
        if (definition == null || labels.Count == 0) return;
        SetText("Tower Name", definition.displayName, animate);
        SetText("Role", definition.roleName, animate);
        SetText("Price", definition.buildCost + " 金币", animate);
        SetText("Damage", "伤害 " + definition.damage, animate);
        SetText("Range", "射程 " + Number(definition.attackRange * 100) + " 厘米", animate);
        SetText("Fire Interval", "每 " + Number(definition.attackInterval) + " 秒发射", animate);
        SetText("Mechanic", definition.featureSummary, animate);
        SetText("Purchase Hint", shopDisplay ? (affordable ? "可购买 · 抓取放置" : "金币不足 · 暂不可购买")
            : "已购买 · 抓取可调整", animate);
    }

    private void SetText(string key, string value, bool animate)
    {
        value ??= "";
        var text = labels[key];
        if (text.text == value) return;
        var textFont = text.font;
        if (textFont != null && textFont.atlasPopulationMode == AtlasPopulationMode.Dynamic)
        {
            textFont.isMultiAtlasTexturesEnabled = true;
            // TMP returns false when there are no new glyphs to add as well.
            // Check actual coverage rather than interpreting that result as missing text.
            if (!textFont.HasCharacters(value)) textFont.TryAddCharacters(value, out _);
            if (!textFont.HasCharacters(value, out List<char> missing) && missing != null && missing.Count > 0)
                Debug.LogWarning("防御塔参数字体缺少字符：" + new string(missing.ToArray()), this);
        }
        text.text = value;
        // A hidden card opens with fresh values rather than a stale update flash.
        changes[key] = animate && group.alpha > 0 ? DefenseTowerInfoStyle.ChangeDuration : 0;
        if (key == "Purchase Hint") stateFlash = changes[key];
    }

    private void Update()
    {
        if (Time.unscaledTime >= nextRefresh)
        {
            nextRefresh = Time.unscaledTime + DefenseTowerInfoStyle.RefreshInterval;
            Refresh();
        }
        foreach (var pair in labels)
        {
            var remaining = Mathf.Max(0, changes[pair.Key] - Time.unscaledDeltaTime);
            changes[pair.Key] = remaining;
            var pulse = Mathf.Sin(remaining / DefenseTowerInfoStyle.ChangeDuration * Mathf.PI);
            pair.Value.rectTransform.localScale = normalScales[pair.Key] * (1 + pulse * .025f);
            var baseColor = pair.Key == "Purchase Hint" ? (shopDisplay ? (affordable ? availableColor
                : unavailableColor) : purchasedColor) : normalColors[pair.Key];
            pair.Value.color = Color.Lerp(baseColor, updateTint, pulse * .35f);
        }
        stateFlash = Mathf.Max(0, stateFlash - Time.unscaledDeltaTime);
        var alpha = selected ? .45f + .2f * (Mathf.Sin(Time.unscaledTime * 4f) * .5f + .5f)
            : .4f * Mathf.Sin(stateFlash / DefenseTowerInfoStyle.ChangeDuration * Mathf.PI);
        selection.color = new Color(1, 1, 1, alpha);
    }
}
