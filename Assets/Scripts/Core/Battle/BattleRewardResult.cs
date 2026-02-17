using System;
using SevenBattles.Core.Items;
using UnityEngine;

namespace SevenBattles.Core.Battle
{
    public sealed class BattleRewardResult
    {
        public int GoldAmount { get; }
        public BattleRewardResultEntry[] BonusRewards { get; }

        public BattleRewardResult(int goldAmount, BattleRewardResultEntry[] bonusRewards)
        {
            GoldAmount = Mathf.Max(0, goldAmount);
            BonusRewards = bonusRewards ?? Array.Empty<BattleRewardResultEntry>();
        }
    }

    public sealed class BattleRewardResultEntry
    {
        public BattleRewardType Type { get; }
        public int Amount { get; }
        public EquipmentDefinition EquipmentDef { get; }
        public SpellDefinition SpellDef { get; }
        public ItemDefinition ItemDef { get; }
        public Sprite Icon { get; }
        public string DisplayName { get; }

        public BattleRewardResultEntry(BattleRewardType type, int amount)
        {
            Type = type;
            Amount = Mathf.Max(0, amount);
            EquipmentDef = null;
            SpellDef = null;
            ItemDef = null;
            Icon = null;
            DisplayName = type.ToString();
        }

        public BattleRewardResultEntry(EquipmentDefinition equipmentDef)
        {
            Type = BattleRewardType.Equipment;
            Amount = 1;
            EquipmentDef = equipmentDef;
            SpellDef = null;
            ItemDef = null;
            Icon = equipmentDef != null ? equipmentDef.Icon : null;
            DisplayName = equipmentDef != null ? equipmentDef.Name : string.Empty;
        }

        public BattleRewardResultEntry(SpellDefinition spellDef)
        {
            Type = BattleRewardType.Spell;
            Amount = 1;
            EquipmentDef = null;
            SpellDef = spellDef;
            ItemDef = null;
            Icon = spellDef != null ? spellDef.Icon : null;
            DisplayName = spellDef != null ? spellDef.Name : string.Empty;
        }

        public BattleRewardResultEntry(ItemDefinition itemDef, int amount = 1)
        {
            Type = BattleRewardType.Item;
            Amount = Mathf.Max(1, amount);
            EquipmentDef = null;
            SpellDef = null;
            ItemDef = itemDef;
            Icon = itemDef != null ? itemDef.Icon : null;
            DisplayName = itemDef != null ? itemDef.Name : string.Empty;
        }
    }
}
