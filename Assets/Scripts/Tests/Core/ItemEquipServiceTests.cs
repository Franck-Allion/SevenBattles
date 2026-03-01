using NUnit.Framework;
using SevenBattles.Core.Items;
using SevenBattles.Core.Players;
using UnityEngine;

namespace SevenBattles.Tests.Core
{
    public class ItemEquipServiceTests
    {
        [Test]
        public void TryEquip_LastInventoryItem_RemovesEntryAndSetsSlot()
        {
            var inventory = ScriptableObject.CreateInstance<PlayerInventory>();
            var potion = CreateItem("item.potion");
            inventory.AddItem(potion, 1);

            var gateway = new PlayerInventoryGateway(inventory);
            var resolver = new FakeDefinitionResolver();
            resolver.ById[potion.Id] = potion;
            var service = new ItemEquipService(gateway, resolver);
            var unit = new OwnedUnitData();

            bool result = service.TryEquip(unit, potion, ConsumableSlotType.Object1);

            Assert.IsTrue(result);
            Assert.IsNull(inventory.FindEntry(potion.Id), "Last item should be removed from inventory after equip.");
            Assert.AreEqual(potion.Id, GetSlotDefinitionId(unit, ConsumableSlotType.Object1));

            Object.DestroyImmediate(potion);
            Object.DestroyImmediate(inventory);
        }

        [Test]
        public void TryEquip_OccupiedSlot_ReturnsPreviousItemToInventory()
        {
            var inventory = ScriptableObject.CreateInstance<PlayerInventory>();
            var oldPotion = CreateItem("item.potion.old");
            var newPotion = CreateItem("item.potion.new");
            inventory.AddItem(newPotion, 1);

            var gateway = new PlayerInventoryGateway(inventory);
            var resolver = new FakeDefinitionResolver();
            resolver.ById[oldPotion.Id] = oldPotion;
            resolver.ById[newPotion.Id] = newPotion;
            var service = new ItemEquipService(gateway, resolver);
            var unit = new OwnedUnitData();
            SetSlotDefinitionId(unit, ConsumableSlotType.Object1, oldPotion.Id);

            bool result = service.TryEquip(unit, newPotion, ConsumableSlotType.Object1);

            Assert.IsTrue(result);
            Assert.AreEqual(newPotion.Id, GetSlotDefinitionId(unit, ConsumableSlotType.Object1));

            InventoryEntry oldPotionEntry = inventory.FindEntry(oldPotion.Id);
            Assert.IsNotNull(oldPotionEntry, "Previously equipped item should return to inventory.");
            Assert.AreEqual(1, oldPotionEntry.Quantity);
            Assert.IsNull(inventory.FindEntry(newPotion.Id), "Consumed item should be removed when quantity reaches zero.");

            Object.DestroyImmediate(oldPotion);
            Object.DestroyImmediate(newPotion);
            Object.DestroyImmediate(inventory);
        }

        [Test]
        public void TryUnequip_AddsEquippedItemBackToInventory()
        {
            var inventory = ScriptableObject.CreateInstance<PlayerInventory>();
            var potion = CreateItem("item.potion");

            var gateway = new PlayerInventoryGateway(inventory);
            var resolver = new FakeDefinitionResolver();
            resolver.ById[potion.Id] = potion;
            var service = new ItemEquipService(gateway, resolver);
            var unit = new OwnedUnitData();
            SetSlotDefinitionId(unit, ConsumableSlotType.Object2, potion.Id);

            bool result = service.TryUnequip(unit, ConsumableSlotType.Object2);

            Assert.IsTrue(result);
            Assert.IsTrue(string.IsNullOrWhiteSpace(GetSlotDefinitionId(unit, ConsumableSlotType.Object2)));

            InventoryEntry entry = inventory.FindEntry(potion.Id);
            Assert.IsNotNull(entry);
            Assert.AreEqual(1, entry.Quantity);

            Object.DestroyImmediate(potion);
            Object.DestroyImmediate(inventory);
        }

        [Test]
        public void TryEquip_SameDefinitionAlreadyEquipped_DoesNotConsumeInventory()
        {
            var inventory = ScriptableObject.CreateInstance<PlayerInventory>();
            var potion = CreateItem("item.potion");
            inventory.AddItem(potion, 2);

            var gateway = new PlayerInventoryGateway(inventory);
            var resolver = new FakeDefinitionResolver();
            resolver.ById[potion.Id] = potion;
            var service = new ItemEquipService(gateway, resolver);
            var unit = new OwnedUnitData();
            SetSlotDefinitionId(unit, ConsumableSlotType.Object3, potion.Id);

            bool result = service.TryEquip(unit, potion, ConsumableSlotType.Object3);

            Assert.IsTrue(result);
            Assert.AreEqual(potion.Id, GetSlotDefinitionId(unit, ConsumableSlotType.Object3));

            InventoryEntry entry = inventory.FindEntry(potion.Id);
            Assert.IsNotNull(entry);
            Assert.AreEqual(2, entry.Quantity, "Re-equipping same definition should be a no-op for inventory.");

            Object.DestroyImmediate(potion);
            Object.DestroyImmediate(inventory);
        }

        [Test]
        public void TryEquip_DuplicateDefinitionEntries_ConsumesProvidedSourceEntry()
        {
            var inventory = ScriptableObject.CreateInstance<PlayerInventory>();
            var potion = CreateItem("item.potion");
            inventory.Entries.Add(new InventoryEntry
            {
                Kind = InventoryEntry.EntryKind.Item,
                DefinitionId = potion.Id,
                Quantity = 5
            });
            inventory.Entries.Add(new InventoryEntry
            {
                Kind = InventoryEntry.EntryKind.Item,
                DefinitionId = potion.Id,
                Quantity = 2
            });

            InventoryEntry sourceEntry = inventory.Entries[1];

            var gateway = new PlayerInventoryGateway(inventory);
            var resolver = new FakeDefinitionResolver();
            resolver.ById[potion.Id] = potion;
            var service = new ItemEquipService(gateway, resolver);
            var unit = new OwnedUnitData();

            bool result = service.TryEquip(unit, potion, ConsumableSlotType.Object4, sourceEntry);

            Assert.IsTrue(result);
            Assert.AreEqual(potion.Id, GetSlotDefinitionId(unit, ConsumableSlotType.Object4));
            Assert.AreEqual(5, inventory.Entries[0].Quantity, "First stack must stay unchanged.");
            Assert.AreEqual(1, sourceEntry.Quantity, "Dragged stack must be the one decremented.");

            Object.DestroyImmediate(potion);
            Object.DestroyImmediate(inventory);
        }

        private static ItemDefinition CreateItem(string id)
        {
            var definition = ScriptableObject.CreateInstance<ItemDefinition>();
            definition.Id = id;
            definition.IsConsumable = true;
            return definition;
        }

        private static void SetSlotDefinitionId(OwnedUnitData unit, ConsumableSlotType slotType, string definitionId)
        {
            for (int i = 0; i < unit.EquippedConsumables.Length; i++)
            {
                if (unit.EquippedConsumables[i].SlotType != slotType)
                {
                    continue;
                }

                ConsumableSlotEntry slotEntry = unit.EquippedConsumables[i];
                slotEntry.DefinitionId = definitionId;
                unit.EquippedConsumables[i] = slotEntry;
                return;
            }
        }

        private static string GetSlotDefinitionId(OwnedUnitData unit, ConsumableSlotType slotType)
        {
            for (int i = 0; i < unit.EquippedConsumables.Length; i++)
            {
                if (unit.EquippedConsumables[i].SlotType == slotType)
                {
                    return unit.EquippedConsumables[i].DefinitionId;
                }
            }

            return null;
        }

        private sealed class PlayerInventoryGateway : ItemEquipService.IInventoryGateway
        {
            private readonly PlayerInventory _inventory;

            public PlayerInventoryGateway(PlayerInventory inventory)
            {
                _inventory = inventory;
            }

            public bool RemoveItem(string definitionId, int quantity = 1)
            {
                if (_inventory == null)
                {
                    return false;
                }

                return _inventory.RemoveItem(definitionId, quantity);
            }

            public bool RemoveItem(InventoryEntry entry, int quantity = 1)
            {
                if (_inventory == null)
                {
                    return false;
                }

                return _inventory.RemoveItem(entry, quantity);
            }

            public void AddItem(ItemDefinition itemDef, int quantity = 1)
            {
                if (_inventory == null || itemDef == null)
                {
                    return;
                }

                _inventory.AddItem(itemDef, quantity);
            }
        }

        private sealed class FakeDefinitionResolver : ItemEquipService.IDefinitionResolver
        {
            public readonly System.Collections.Generic.Dictionary<string, ItemDefinition> ById =
                new System.Collections.Generic.Dictionary<string, ItemDefinition>(System.StringComparer.Ordinal);

            public ItemDefinition GetById(string definitionId)
            {
                if (string.IsNullOrWhiteSpace(definitionId))
                {
                    return null;
                }

                ById.TryGetValue(definitionId, out ItemDefinition definition);
                return definition;
            }
        }
    }
}
