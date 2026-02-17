using System;
using SevenBattles.Core.Items;
using UnityEngine;

namespace SevenBattles.Core.Battle
{
    [Serializable]
    public struct BattleRewardEntry
    {
        public BattleRewardType Type;

        [Tooltip("For Gold/Gems: minimum amount. Ignored for Equipment/Spell/Item.")]
        [Min(0)]
        public int MinAmount;

        [Tooltip("For Gold/Gems: maximum amount (inclusive). Ignored for Equipment/Spell/Item.")]
        [Min(0)]
        public int MaxAmount;

        [Tooltip("Equipment to drop (only when Type == Equipment).")]
        public EquipmentDefinition EquipmentRef;

        [Tooltip("Spell to drop (only when Type == Spell).")]
        public SpellDefinition SpellRef;

        [Tooltip("Item to drop (only when Type == Item).")]
        public ItemDefinition ItemRef;

        [Range(0f, 1f)]
        [Tooltip("Relative drop weight within the bonus pool. Higher = more likely.")]
        public float Weight;
    }
}
