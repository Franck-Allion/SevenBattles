using NUnit.Framework;
using SevenBattles.Core.Items;
using UnityEngine;

namespace SevenBattles.Tests.Core
{
    public class PlayerInventoryTests
    {
        [Test]
        public void RemoveEquipment_WhenEntryExists_DecreasesQuantityByOne()
        {
            var inventory = ScriptableObject.CreateInstance<PlayerInventory>();
            inventory.Entries.Add(new InventoryEntry
            {
                Kind = InventoryEntry.EntryKind.Equipment,
                DefinitionId = "eq.sword",
                Quantity = 3
            });

            bool removed = inventory.RemoveEquipment("eq.sword");

            Assert.IsTrue(removed);
            Assert.AreEqual(1, inventory.Entries.Count);
            Assert.AreEqual(2, inventory.Entries[0].Quantity);

            Object.DestroyImmediate(inventory);
        }

        [Test]
        public void RemoveEquipment_WhenLastQuantity_RemovesEntry()
        {
            var inventory = ScriptableObject.CreateInstance<PlayerInventory>();
            inventory.Entries.Add(new InventoryEntry
            {
                Kind = InventoryEntry.EntryKind.Equipment,
                DefinitionId = "eq.sword",
                Quantity = 1
            });

            bool removed = inventory.RemoveEquipment("eq.sword");

            Assert.IsTrue(removed);
            Assert.AreEqual(0, inventory.Entries.Count);

            Object.DestroyImmediate(inventory);
        }

        [Test]
        public void RemoveEquipment_WhenDefinitionDoesNotExist_ReturnsFalse()
        {
            var inventory = ScriptableObject.CreateInstance<PlayerInventory>();
            inventory.Entries.Add(new InventoryEntry
            {
                Kind = InventoryEntry.EntryKind.Equipment,
                DefinitionId = "eq.staff",
                Quantity = 1
            });

            bool removed = inventory.RemoveEquipment("eq.sword");

            Assert.IsFalse(removed);
            Assert.AreEqual(1, inventory.Entries.Count);
            Assert.AreEqual("eq.staff", inventory.Entries[0].DefinitionId);

            Object.DestroyImmediate(inventory);
        }

        [Test]
        public void RemoveEquipment_WhenSuccessful_FiresInventoryChanged()
        {
            var inventory = ScriptableObject.CreateInstance<PlayerInventory>();
            inventory.Entries.Add(new InventoryEntry
            {
                Kind = InventoryEntry.EntryKind.Equipment,
                DefinitionId = "eq.sword",
                Quantity = 1
            });

            int inventoryChangedCount = 0;
            inventory.InventoryChanged += () => inventoryChangedCount++;

            bool removed = inventory.RemoveEquipment("eq.sword");

            Assert.IsTrue(removed);
            Assert.AreEqual(1, inventoryChangedCount);

            Object.DestroyImmediate(inventory);
        }
    }
}
