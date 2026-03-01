using System;
using SevenBattles.Core.Items;
using SevenBattles.Core.Players;

namespace SevenBattles.Core
{
    /// <summary>
    /// Runtime service for equipping and unequipping consumable items on player-owned units.
    /// </summary>
    public interface IItemEquipService
    {
        event Action<OwnedUnitData, ConsumableSlotType, ItemDefinition> ConsumableChanged;

        bool TryEquip(OwnedUnitData unit, ItemDefinition itemDef, ConsumableSlotType slotType);
        bool TryEquip(OwnedUnitData unit, ItemDefinition itemDef, ConsumableSlotType slotType, InventoryEntry sourceInventoryEntry);
        bool TryUnequip(OwnedUnitData unit, ConsumableSlotType slotType);
        ItemDefinition GetEquipped(OwnedUnitData unit, ConsumableSlotType slotType);
    }
}
