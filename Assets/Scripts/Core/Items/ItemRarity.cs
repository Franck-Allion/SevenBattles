namespace SevenBattles.Core.Items
{
    public enum ItemRarity
    {
        Common = 0,
        Uncommon = 1,
        Rare = 2,
        Epic = 3,
        Legendary = 4
    }

    [UnityEngine.CreateAssetMenu(menuName = "SevenBattles/Items/Item Rarity Color Palette", fileName = "ItemRarityColorPalette")]
    public sealed class ItemRarityColorPalette : UnityEngine.ScriptableObject
    {
        [UnityEngine.SerializeField] private UnityEngine.Color _commonColor = new UnityEngine.Color(0.58f, 0.62f, 0.67f, 1f);
        [UnityEngine.SerializeField] private UnityEngine.Color _uncommonColor = new UnityEngine.Color(0.27f, 0.74f, 0.31f, 1f);
        [UnityEngine.SerializeField] private UnityEngine.Color _rareColor = new UnityEngine.Color(0.24f, 0.53f, 0.95f, 1f);
        [UnityEngine.SerializeField] private UnityEngine.Color _epicColor = new UnityEngine.Color(0.65f, 0.35f, 0.93f, 1f);
        [UnityEngine.SerializeField] private UnityEngine.Color _legendaryColor = new UnityEngine.Color(0.95f, 0.66f, 0.17f, 1f);

        public UnityEngine.Color GetInventoryBackgroundColor(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Uncommon:
                    return _uncommonColor;
                case ItemRarity.Rare:
                    return _rareColor;
                case ItemRarity.Epic:
                    return _epicColor;
                case ItemRarity.Legendary:
                    return _legendaryColor;
                case ItemRarity.Common:
                default:
                    return _commonColor;
            }
        }
    }

    /// <summary>
    /// Centralized UI palette for inventory/equipment rarity backgrounds.
    /// </summary>
    public static class ItemRarityColorUtility
    {
        public static UnityEngine.Color GetInventoryBackgroundColor(ItemRarity rarity)
        {
            return GetInventoryBackgroundColor(rarity, null);
        }

        public static UnityEngine.Color GetInventoryBackgroundColor(ItemRarity rarity, ItemRarityColorPalette palette)
        {
            if (palette != null)
            {
                return palette.GetInventoryBackgroundColor(rarity);
            }

            switch (rarity)
            {
                case ItemRarity.Uncommon:
                    return new UnityEngine.Color(0.27f, 0.74f, 0.31f, 1f);
                case ItemRarity.Rare:
                    return new UnityEngine.Color(0.24f, 0.53f, 0.95f, 1f);
                case ItemRarity.Epic:
                    return new UnityEngine.Color(0.65f, 0.35f, 0.93f, 1f);
                case ItemRarity.Legendary:
                    return new UnityEngine.Color(0.95f, 0.66f, 0.17f, 1f);
                case ItemRarity.Common:
                default:
                    return new UnityEngine.Color(0.58f, 0.62f, 0.67f, 1f);
            }
        }
    }
}
