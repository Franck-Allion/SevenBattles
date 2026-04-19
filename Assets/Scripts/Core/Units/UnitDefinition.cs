using UnityEngine;
using SevenBattles.Core.Battle;
using SevenBattles.Core.Items;

namespace SevenBattles.Core.Units
{
    [CreateAssetMenu(menuName = "SevenBattles/Wizard Definition", fileName = "WizardDefinition")]
    public class UnitDefinition : ScriptableObject
    {
        private static readonly int[] DefaultConsumableSlotUnlockLevels = { 1, 1, 1, 1 };

        [Header("Identity")]
        public string Id;

        [Header("Presentation")]
        public Sprite Portrait;

        [Header("Progression")]
        [Min(0f), Tooltip("Threat factor used for end-of-battle XP calculation. This is data-driven and can be tuned per unit type (e.g., Common=0.7, Uncommon=1.0, Rare=1.4, Epic=2.0, Legend=3.0).")]
        public float ThreatFactor = 1f;

        [Min(1), Tooltip("Maximum level this unit can reach.")]
        public int MaxLevel = 10;

        [Tooltip("XP thresholds required to advance from level N to level N+1. Index 0 = level 1 -> 2.")]
        public int[] XpToNextLevel = System.Array.Empty<int>();

        [Header("Audio")]
        [Tooltip("Optional SFX played when a unit of this type dies (e.g., Assets/Art/SFX/Wizard_Death.wav).")]
        public AudioClip DeathSfx;
        [Range(0f, 1.5f)]
        [Tooltip("Volume multiplier for the death SFX (0 = silent, 1 = default, >1 = boosted).")]
        public float DeathSfxVolume = 1f;

        [Header("Prefab")]
        public GameObject Prefab;

        [Header("Base Stats")]
        public UnitStatsData BaseStats;

        [Header("Level Scaling")]
        public UnitLevelBonusData LevelBonus;

        [Header("Spells (Legacy)")]
        [Tooltip("Legacy default spells for this unit type. Runtime uses per-unit loadouts; only used as a fallback when no per-unit spells are assigned.")]
        public SpellDefinition[] Spells;

        [Header("Inventory Slots")]
        [Range(0, 4), Tooltip("How many consumable inventory slots (Object1..Object4) this unit type can use.")]
        public int InventoryConsumableSlotCount = 4;
        [Tooltip("Required unit level to unlock each consumable slot index (0=Object1 ... 3=Object4). Values <= 0 fallback to 1.")]
        public int[] InventoryConsumableSlotUnlockLevels = { 1, 1, 1, 1 };

        /// <summary>
        /// Resolves whether the consumable slot exists and which level unlocks it.
        /// Slot index is 0-based (0=Object1, 3=Object4).
        /// </summary>
        public bool TryGetConsumableSlotUnlock(int slotIndex, out bool exists, out int requiredLevel)
        {
            exists = false;
            requiredLevel = 1;
            if (slotIndex < 0 || slotIndex >= 4)
            {
                return false;
            }

            int clampedCount = Mathf.Clamp(InventoryConsumableSlotCount, 0, 4);
            exists = slotIndex < clampedCount;
            if (!exists)
            {
                requiredLevel = int.MaxValue;
                return true;
            }

            int[] sourceLevels = InventoryConsumableSlotUnlockLevels;
            if (sourceLevels == null || sourceLevels.Length == 0)
            {
                sourceLevels = DefaultConsumableSlotUnlockLevels;
            }

            int configuredLevel = slotIndex < sourceLevels.Length
                ? sourceLevels[slotIndex]
                : 1;
            requiredLevel = Mathf.Max(1, configuredLevel);
            return true;
        }

        public bool TryGetConsumableSlotUnlock(ConsumableSlotType slotType, out bool exists, out int requiredLevel)
        {
            int slotIndex = ConsumableSlotTypeToIndex(slotType);
            return TryGetConsumableSlotUnlock(slotIndex, out exists, out requiredLevel);
        }

        private static int ConsumableSlotTypeToIndex(ConsumableSlotType slotType)
        {
            switch (slotType)
            {
                case ConsumableSlotType.Object1:
                    return 0;
                case ConsumableSlotType.Object2:
                    return 1;
                case ConsumableSlotType.Object3:
                    return 2;
                case ConsumableSlotType.Object4:
                    return 3;
                default:
                    return -1;
            }
        }
    }
}
