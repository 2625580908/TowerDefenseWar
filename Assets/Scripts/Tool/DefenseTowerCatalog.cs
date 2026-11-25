using MRStrategyRTS.Config;
using TMPro;
using UnityEngine;

[CreateAssetMenu(menuName = "MRStrategyRTS/Defense Tower Catalog")]
public sealed class DefenseTowerCatalog : ScriptableObject
{
    public TowerDefinition[] towers;
    [Tooltip("商品标签使用的中文 TMP 字体，需包含塔名和属性中文字形。")]
    public TMP_FontAsset labelFont;
}
