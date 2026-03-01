using System;
using SevenBattles.Core.Items;

namespace SevenBattles.Core.Players
{
    /// <summary>
    /// Orchestrates consumable transfers between inventory and owned-unit consumable slots.
    /// </summary>
    public sealed class ItemEquipService : IItemEquipService
    {
        public interface IInventoryGateway
        {
            bool RemoveItem(string definitionId, int quantity = 1);
            bool RemoveItem(InventoryEntry entry, int quantity = 1);
            void AddItem(ItemDefinition itemDef, int quantity = 1);
        }

        public interface IDefinitionResolver
        {
            ItemDefinition GetById(string definitionId);
        }

        private readonly IInventoryGateway _inventoryGateway;
        private readonly IDefinitionResolver _definitionResolver;

        public ItemEquipService(ItemDefinitionRegistry itemDefinitionRegistry)
            : this(
                inventoryGateway: null,
                definitionResolver: new RegistryDefinitionResolver(itemDefinitionRegistry))
        {
        }

        public ItemEquipService(PlayerContext playerContext, ItemDefinitionRegistry itemDefinitionRegistry)
            : this(
                inventoryGateway: new PlayerContextInventoryGateway(playerContext),
                definitionResolver: new RegistryDefinitionResolver(itemDefinitionRegistry))
        {
        }

        public ItemEquipService(IInventoryGateway inventoryGateway, IDefinitionResolver definitionResolver)
        {
            _inventoryGateway = inventoryGateway;
            _definitionResolver = definitionResolver;
        }

        public event Action<OwnedUnitData, ConsumableSlotType, ItemDefinition> ConsumableChanged;

        public bool TryEquip(OwnedUnitData unit, ItemDefinition itemDef, ConsumableSlotType slotType)
        {
            return TryEquip(unit, itemDef, slotType, null);
        }

        public bool TryEquip(OwnedUnitData unit, ItemDefinition itemDef, ConsumableSlotType slotType, InventoryEntry sourceInventoryEntry)
        {
            if (unit == null || itemDef == null || !itemDef.IsConsumable)
            {
                return false;
            }

            if (!TryGetSlotIndex(unit, slotType, out int slotIndex))
            {
                return false;
            }

            string previousDefinitionId = unit.EquippedConsumables[slotIndex].DefinitionId;
            if (string.Equals(previousDefinitionId, itemDef.Id, StringComparison.Ordinal))
            {
                return true;
            }

            ItemDefinition previouslyEquippedDefinition = null;
            bool removedFromInventory = false;

            if (_inventoryGateway != null)
            {
                if (string.IsNullOrWhiteSpace(itemDef.Id))
                {
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(previousDefinitionId))
                {
                    if (_definitionResolver == null)
                    {
                        return false;
                    }

                    previouslyEquippedDefinition = _definitionResolver.GetById(previousDefinitionId);
                    if (previouslyEquippedDefinition == null)
                    {
                        return false;
                    }
                }

                if (!TryRemoveDraggedItemFromInventory(itemDef, sourceInventoryEntry))
                {
                    return false;
                }

                removedFromInventory = true;
            }

            if (!TrySetDefinitionId(unit, slotIndex, itemDef.Id))
            {
                if (removedFromInventory)
                {
                    _inventoryGateway.AddItem(itemDef, 1);
                }

                return false;
            }

            if (removedFromInventory && previouslyEquippedDefinition != null)
            {
                _inventoryGateway.AddItem(previouslyEquippedDefinition, 1);
            }

            ConsumableChanged?.Invoke(unit, slotType, itemDef);
            return true;
        }

        public bool TryUnequip(OwnedUnitData unit, ConsumableSlotType slotType)
        {
            if (unit == null)
            {
                return false;
            }

            if (!TryGetSlotIndex(unit, slotType, out int slotIndex))
            {
                return false;
            }

            string equippedDefinitionId = unit.EquippedConsumables[slotIndex].DefinitionId;
            if (string.IsNullOrWhiteSpace(equippedDefinitionId))
            {
                return false;
            }

            ItemDefinition equippedDefinition = null;
            if (_inventoryGateway != null)
            {
                if (_definitionResolver == null)
                {
                    return false;
                }

                equippedDefinition = _definitionResolver.GetById(equippedDefinitionId);
                if (equippedDefinition == null)
                {
                    return false;
                }
            }

            if (!TrySetDefinitionId(unit, slotIndex, null))
            {
                return false;
            }

            if (_inventoryGateway != null && equippedDefinition != null)
            {
                _inventoryGateway.AddItem(equippedDefinition, 1);
            }

            ConsumableChanged?.Invoke(unit, slotType, null);
            return true;
        }

        public ItemDefinition GetEquipped(OwnedUnitData unit, ConsumableSlotType slotType)
        {
            if (unit == null || _definitionResolver == null)
            {
                return null;
            }

            if (!TryGetSlotIndex(unit, slotType, out int slotIndex))
            {
                return null;
            }

            string definitionId = unit.EquippedConsumables[slotIndex].DefinitionId;
            if (string.IsNullOrWhiteSpace(definitionId))
            {
                return null;
            }

            return _definitionResolver.GetById(definitionId);
        }

        private bool TryRemoveDraggedItemFromInventory(ItemDefinition itemDef, InventoryEntry sourceInventoryEntry)
        {
            if (_inventoryGateway == null || itemDef == null || string.IsNullOrWhiteSpace(itemDef.Id))
            {
                return false;
            }

            if (sourceInventoryEntry != null &&
                sourceInventoryEntry.Kind == InventoryEntry.EntryKind.Item &&
                string.Equals(sourceInventoryEntry.DefinitionId, itemDef.Id, StringComparison.Ordinal))
            {
                if (_inventoryGateway.RemoveItem(sourceInventoryEntry, 1))
                {
                    return true;
                }
            }

            return _inventoryGateway.RemoveItem(itemDef.Id, 1);
        }

        private static bool TrySetDefinitionId(OwnedUnitData unit, int slotIndex, string definitionId)
        {
            if (unit == null || unit.EquippedConsumables == null || slotIndex < 0 || slotIndex >= unit.EquippedConsumables.Length)
            {
                return false;
            }

            ConsumableSlotEntry slotEntry = unit.EquippedConsumables[slotIndex];
            slotEntry.DefinitionId = definitionId;
            unit.EquippedConsumables[slotIndex] = slotEntry;
            return true;
        }

        private static bool TryGetSlotIndex(OwnedUnitData unit, ConsumableSlotType slotType, out int slotIndex)
        {
            slotIndex = -1;
            if (unit == null)
            {
                return false;
            }

            EnsureConsumableSlots(unit);
            if (unit.EquippedConsumables == null)
            {
                return false;
            }

            for (int i = 0; i < unit.EquippedConsumables.Length; i++)
            {
                if (unit.EquippedConsumables[i].SlotType == slotType)
                {
                    slotIndex = i;
                    return true;
                }
            }

            return false;
        }

        private static void EnsureConsumableSlots(OwnedUnitData unit)
        {
            if (unit == null)
            {
                return;
            }

            if (unit.EquippedConsumables == null || unit.EquippedConsumables.Length == 0)
            {
                unit.EquippedConsumables = OwnedUnitData.CreateDefaultEquippedConsumables();
            }
        }

        private sealed class PlayerContextInventoryGateway : IInventoryGateway
        {
            private readonly PlayerContext _playerContext;

            public PlayerContextInventoryGateway(PlayerContext playerContext)
            {
                _playerContext = playerContext;
            }

            public bool RemoveItem(string definitionId, int quantity = 1)
            {
                if (_playerContext == null || _playerContext.Inventory == null)
                {
                    return false;
                }

                return _playerContext.Inventory.RemoveItem(definitionId, quantity);
            }

            public bool RemoveItem(InventoryEntry entry, int quantity = 1)
            {
                if (_playerContext == null || _playerContext.Inventory == null)
                {
                    return false;
                }

                return _playerContext.Inventory.RemoveItem(entry, quantity);
            }

            public void AddItem(ItemDefinition itemDef, int quantity = 1)
            {
                if (itemDef == null || _playerContext == null || _playerContext.Inventory == null)
                {
                    return;
                }

                _playerContext.Inventory.AddItem(itemDef, quantity);
            }
        }

        private sealed class RegistryDefinitionResolver : IDefinitionResolver
        {
            private readonly ItemDefinitionRegistry _itemRegistry;

            public RegistryDefinitionResolver(ItemDefinitionRegistry itemRegistry)
            {
                _itemRegistry = itemRegistry;
            }

            public ItemDefinition GetById(string definitionId)
            {
                if (_itemRegistry == null || string.IsNullOrWhiteSpace(definitionId))
                {
                    return null;
                }

                return _itemRegistry.GetById(definitionId);
            }
        }
    }
}
