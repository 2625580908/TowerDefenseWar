using System.Collections.Generic;
using MRStrategyRTS.Config;
using MRStrategyRTS.Input;
using MRStrategyRTS.RTS;
using MRStrategyRTS.Shared;
using TMPro;
using UnityEngine;

/// <summary>Binds the purchasable tower lineup authored in terrain prefabs.</summary>
[DisallowMultipleComponent]
public sealed class DefenseTowerShop : MonoBehaviour
{
    [Tooltip("按商品顺序指定卡槽；未指定时自动读取 Minecraft Tower Defense 商品区的 Tile 卡槽。")]
    [SerializeField] private Transform[] displaySlots;
    [Tooltip("按商品顺序绑定已摆放在地形预制体卡槽上的新防御塔。")]
    [SerializeField] private XRGrabSnapPlacement[] displayTowers;
    [Tooltip("模型底面与卡槽顶面的间隙，单位为卡槽本地坐标。")]
    [SerializeField, Min(0)] private float surfaceClearance = 0.005f;
    private const string GeneratedShopName = "Six Defense Tower Shop";
    private bool initialized;

    public void Initialize(LevelTerrainBindings bindings)
    {
        // Move the complete authored merchandise block before binding its lineup.
        GetComponent<TerrainShopLayout>()?.RefreshLayout();
        if (initialized) return;
        var catalog = Resources.Load<DefenseTowerCatalog>("DefenseTowerCatalog");
        if (catalog == null || catalog.towers == null || catalog.towers.Length == 0) return;
        foreach (var definition in catalog.towers)
        {
            if (definition == null || definition.prefab == null
                || definition.prefab.GetComponent<XRGrabSnapPlacement>() == null)
            {
                Debug.LogWarning("防御塔商店配置需要带抓取/吸附组件的塔预制体。", this);
                return;
            }
        }

        var slots = ResolveDisplaySlots(bindings.transform);
        if (slots.Count < catalog.towers.Length)
        {
            Debug.LogWarning("防御塔商店没有足够的商品卡槽，请设置 Display Slots。", this);
            return;
        }
        // Grid generators can rebuild their Tile children during Awake.
        // Keep the authored tower instances and bind the replacement slot transforms.
        displaySlots = slots.ToArray();
        var shopRoot = bindings.transform.Find("Minecraft Tower Defense");
        if (displayTowers == null || displayTowers.Length == 0)
        {
            var group = shopRoot != null ? shopRoot.Find(GeneratedShopName) : null;
            if (group != null)
                displayTowers = group.GetComponentsInChildren<XRGrabSnapPlacement>(true);
        }
        if (displayTowers == null || displayTowers.Length != catalog.towers.Length)
        {
            Debug.LogError("请在地形预制体的商品卡槽上保存并绑定全部新防御塔。", this);
            return;
        }
        for (var i = 0; i < displayTowers.Length; i++)
        {
            var tower = displayTowers[i] != null
                ? displayTowers[i].GetComponentInChildren<DefenseTower>(true) : null;
            if (tower == null || tower.TowerDefinition != catalog.towers[i])
            {
                Debug.LogError("商店展示塔与商品配置顺序不匹配，请检查地形预制体。", this);
                return;
            }
        }
        initialized = true;
        var font = catalog.labelFont != null
            ? catalog.labelFont
            : (bindings.goldText != null ? bindings.goldText.font : TMP_Settings.defaultFontAsset);
        for (var i = 0; i < catalog.towers.Length; i++)
        {
            var definition = catalog.towers[i];
            var slot = slots[i];
            var display = displayTowers[i];
            ShopDisplaySurfacePlacement.PlaceOnSlot(display.transform, slot, surfaceClearance, shopRoot);
            display.ConfigureAsShopDisplay();
            ConfigurePokeCard(display.transform, font, definition);
        }
    }

    private static void AddUniqueSlot(List<Transform> slots, Transform slot, Transform shopRoot)
    {
        if (slot == null) return;
        for (var current = slot; current != shopRoot && current != null; current = current.parent)
            if (!current.gameObject.activeSelf) return;
        foreach (var existing in slots)
            if (existing == slot || (existing.position - slot.position).sqrMagnitude < 0.00000001f)
                return;
        slots.Add(slot);
    }

    private List<Transform> ResolveDisplaySlots(Transform terrainRoot)
    {
        var slots = new List<Transform>();
        if (displaySlots != null && displaySlots.Length > 0)
        {
            foreach (var slot in displaySlots)
                AddUniqueSlot(slots, slot, terrainRoot);
            if (slots.Count == displaySlots.Length) return slots;
            slots.Clear();
        }
        // Search only the authored merchandise block, never the playable map tiles.
        var shopRoot = terrainRoot.Find("Minecraft Tower Defense/_GeneratedTerrainTiles");
        if (shopRoot == null) return slots;
        foreach (var child in shopRoot.GetComponentsInChildren<Transform>(true))
        {
            if (child.name.StartsWith("Tile_", System.StringComparison.Ordinal))
                AddUniqueSlot(slots, child, shopRoot);
        }
        slots.Sort((left, right) =>
        {
            var leftPosition = shopRoot.InverseTransformPoint(left.position);
            var rightPosition = shopRoot.InverseTransformPoint(right.position);
            var rowComparison = leftPosition.z.CompareTo(rightPosition.z);
            return rowComparison != 0 ? rowComparison : leftPosition.x.CompareTo(rightPosition.x);
        });
        return slots;
    }

    private static void ConfigurePokeCard(Transform display, TMP_FontAsset font,
        TowerDefinition definition)
    {
        var card = display.GetComponent<DefenseTowerPokeInfo>();
        if (card != null) card.Initialize(definition, font);
        else Debug.LogError("商店塔预制体未绑定 DefenseTowerPokeInfo 和参数 UI。", display);
    }

}
