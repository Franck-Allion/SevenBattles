using System;
using SevenBattles.Core.Items;

namespace SevenBattles.Core.Players
{
    /// <summary>
    /// Orchestrates equipment transfers between inventory and owned-unit equipment slots.
    /// </summary>
    public sealed class EquipmentService : IEquipmentService
    {
        public interface IInventoryGateway
        {
            bool RemoveEquipment(string definitionId);
            void AddEquipment(EquipmentDefinition equipmentDef);
        }

        public interface IDefinitionResolver
        {
            EquipmentDefinition GetById(string definitionId);
        }

        private readonly IInventoryGateway _inventoryGateway;
        private readonly IDefinitionResolver _definitionResolver;

        public EquipmentService(PlayerContext playerContext, EquipmentDefinitionRegistry equipmentRegistry)
            : this(
                new PlayerContextInventoryGateway(playerContext),
                new RegistryDefinitionResolver(equipmentRegistry))
        {
        }

        public EquipmentService(IInventoryGateway inventoryGateway, IDefinitionResolver definitionResolver)
        {
            _inventoryGateway = inventoryGateway;
            _definitionResolver = definitionResolver;
        }

        public event Action<OwnedUnitData, EquipmentSlotType, EquipmentDefinition> EquipmentChanged;

        public bool CanEquip(OwnedUnitData unit, EquipmentDefinition equipmentDef)
        {
            if (unit == null || equipmentDef == null || string.IsNullOrWhiteSpace(equipmentDef.Id))
            {
                return false;
            }

            return TryGetSlotIndex(unit, equipmentDef.SlotType, out _);
        }

        public bool TryEquip(OwnedUnitData unit, EquipmentDefinition equipmentDef)
        {
            if (!CanEquip(unit, equipmentDef) || _inventoryGateway == null)
            {
                return false;
            }

            EquipmentSlotType slotType = equipmentDef.SlotType;
            EquipmentDefinition previouslyEquipped = GetEquipped(unit, slotType);

            if (!_inventoryGateway.RemoveEquipment(equipmentDef.Id))
            {
                return false;
            }

            if (previouslyEquipped != null)
            {
                _inventoryGateway.AddEquipment(previouslyEquipped);
            }

            if (!TrySetDefinitionId(unit, slotType, equipmentDef.Id))
            {
                // Defensive rollback: if slot cannot be updated, restore inventory state.
                _inventoryGateway.AddEquipment(equipmentDef);
                return false;
            }

            EquipmentChanged?.Invoke(unit, slotType, equipmentDef);
            return true;
        }

        public bool TryUnequip(OwnedUnitData unit, EquipmentSlotType slotType)
        {
            if (unit == null || _inventoryGateway == null || _definitionResolver == null)
            {
                return false;
            }

            if (!TryGetSlotIndex(unit, slotType, out int slotIndex))
            {
                return false;
            }

            EquipmentSlotEntry slotEntry = unit.EquippedItems[slotIndex];
            if (string.IsNullOrWhiteSpace(slotEntry.DefinitionId))
            {
                return false;
            }

            EquipmentDefinition equippedDefinition = _definitionResolver.GetById(slotEntry.DefinitionId);
            if (equippedDefinition == null)
            {
                return false;
            }

            _inventoryGateway.AddEquipment(equippedDefinition);

            slotEntry.DefinitionId = null;
            unit.EquippedItems[slotIndex] = slotEntry;

            EquipmentChanged?.Invoke(unit, slotType, null);
            return true;
        }

        public EquipmentDefinition GetEquipped(OwnedUnitData unit, EquipmentSlotType slotType)
        {
            if (unit == null || _definitionResolver == null)
            {
                return null;
            }

            if (!TryGetSlotIndex(unit, slotType, out int slotIndex))
            {
                return null;
            }

            string definitionId = unit.EquippedItems[slotIndex].DefinitionId;
            if (string.IsNullOrWhiteSpace(definitionId))
            {
                return null;
            }

            return _definitionResolver.GetById(definitionId);
        }

        private static bool TryGetSlotIndex(OwnedUnitData unit, EquipmentSlotType slotType, out int slotIndex)
        {
            slotIndex = -1;
            if (unit == null || unit.EquippedItems == null)
            {
                return false;
            }

            for (int i = 0; i < unit.EquippedItems.Length; i++)
            {
                if (unit.EquippedItems[i].SlotType == slotType)
                {
                    slotIndex = i;
                    return true;
                }
            }

            return false;
        }

        private static bool TrySetDefinitionId(OwnedUnitData unit, EquipmentSlotType slotType, string definitionId)
        {
            if (!TryGetSlotIndex(unit, slotType, out int slotIndex))
            {
                return false;
            }

            EquipmentSlotEntry slotEntry = unit.EquippedItems[slotIndex];
            slotEntry.DefinitionId = definitionId;
            unit.EquippedItems[slotIndex] = slotEntry;
            return true;
        }

        private sealed class PlayerContextInventoryGateway : IInventoryGateway
        {
            private readonly PlayerContext _playerContext;

            public PlayerContextInventoryGateway(PlayerContext playerContext)
            {
                _playerContext = playerContext;
            }

            public bool RemoveEquipment(string definitionId)
            {
                if (_playerContext == null || _playerContext.Inventory == null)
                {
                    return false;
                }

                return _playerContext.Inventory.RemoveEquipment(definitionId);
            }

            public void AddEquipment(EquipmentDefinition equipmentDef)
            {
                if (equipmentDef == null || _playerContext == null || _playerContext.Inventory == null)
                {
                    return;
                }

                _playerContext.Inventory.AddEquipment(equipmentDef);
            }
        }

        private sealed class RegistryDefinitionResolver : IDefinitionResolver
        {
            private readonly EquipmentDefinitionRegistry _equipmentRegistry;

            public RegistryDefinitionResolver(EquipmentDefinitionRegistry equipmentRegistry)
            {
                _equipmentRegistry = equipmentRegistry;
            }

            public EquipmentDefinition GetById(string definitionId)
            {
                if (_equipmentRegistry == null || string.IsNullOrWhiteSpace(definitionId))
                {
                    return null;
                }

                return _equipmentRegistry.GetById(definitionId);
            }
        }
    }
}
