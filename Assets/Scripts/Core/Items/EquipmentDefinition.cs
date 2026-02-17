using SevenBattles.Core.Battle;
using UnityEngine;

namespace SevenBattles.Core.Items
{
    [CreateAssetMenu(menuName = "SevenBattles/Items/Equipment Definition", fileName = "EquipmentDefinition")]
    public sealed class EquipmentDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable identifier for save/load and referencing.")]
        public string Id;

        [Tooltip("Data-only display name. UI should localize when shown.")]
        public string Name;

        [Tooltip("Localization key for the equipment description.")]
        public string DescriptionLocalizationKey;

        [Header("Presentation")]
        [Tooltip("Icon displayed in inventory and reward UI.")]
        public Sprite Icon;

        [Header("Equipment")]
        [Tooltip("Slot where this equipment can be equipped.")]
        public EquipmentSlotType SlotType;

        [Tooltip("Permanent stat bonus granted by this equipment.")]
        public EnchantmentStatBonus StatBonus;
    }
}
