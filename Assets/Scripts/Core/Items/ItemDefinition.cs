using UnityEngine;

namespace SevenBattles.Core.Items
{
    [CreateAssetMenu(menuName = "SevenBattles/Items/Item Definition", fileName = "ItemDefinition")]
    public sealed class ItemDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable identifier for save/load and referencing.")]
        public string Id;

        [Tooltip("Data-only display name. UI should localize when shown.")]
        public string Name;

        [Tooltip("Localization key for the item description.")]
        public string DescriptionLocalizationKey;

        [Header("Presentation")]
        [Tooltip("Icon displayed in inventory and reward UI.")]
        public Sprite Icon;
        [Tooltip("Rarity bucket used by inventory filtering/sorting logic.")]
        public ItemRarity Rarity = ItemRarity.Common;
        [Tooltip("Background tint used by inventory item tiles.")]
        public Color InventoryBackgroundColor = Color.white;

        [Header("Effect")]
        [Tooltip("Runtime effect category for this item.")]
        public ItemEffectType EffectType;

        [Tooltip("Amount used by the selected effect.")]
        public int EffectAmount;

        [Tooltip("Stat name targeted by BoostStat* effects.")]
        public string TargetStat;

        [Tooltip("If true, this item is consumed on use.")]
        public bool IsConsumable = true;
    }
}
