using System;
using SevenBattles.Core.Items;
using SevenBattles.Core.Players;

namespace SevenBattles.Core
{
    /// <summary>
    /// Runtime service for equipping and unequipping player-owned units.
    /// </summary>
    public interface IEquipmentService
    {
        event Action<OwnedUnitData, EquipmentSlotType, EquipmentDefinition> EquipmentChanged;

        bool CanEquip(OwnedUnitData unit, EquipmentDefinition equipmentDef);
        bool TryEquip(OwnedUnitData unit, EquipmentDefinition equipmentDef);
        bool TryUnequip(OwnedUnitData unit, EquipmentSlotType slotType);
        EquipmentDefinition GetEquipped(OwnedUnitData unit, EquipmentSlotType slotType);
    }
}
