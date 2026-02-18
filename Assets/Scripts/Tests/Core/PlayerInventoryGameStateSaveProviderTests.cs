using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using SevenBattles.Core.Items;
using SevenBattles.Core.Players;
using SevenBattles.Core.Save;

namespace SevenBattles.Tests.Core
{
    public class PlayerInventoryGameStateSaveProviderTests
    {
        [Test]
        public void PopulateGameState_WithInventory_ConvertsEntriesToSaveData()
        {
            var context = ScriptableObject.CreateInstance<PlayerContext>();
            var inventory = ScriptableObject.CreateInstance<PlayerInventory>();
            context.Inventory = inventory;

            inventory.Entries.Add(new InventoryEntry
            {
                Kind = InventoryEntry.EntryKind.Equipment,
                DefinitionId = "eq.sword",
                Quantity = 99
            });
            inventory.Entries.Add(new InventoryEntry
            {
                Kind = InventoryEntry.EntryKind.Spell,
                DefinitionId = "spell.firebolt",
                Quantity = 4
            });
            inventory.Entries.Add(new InventoryEntry
            {
                Kind = InventoryEntry.EntryKind.Item,
                DefinitionId = "item.potion",
                Quantity = 0
            });
            inventory.Entries.Add(new InventoryEntry
            {
                Kind = InventoryEntry.EntryKind.Item,
                DefinitionId = "",
                Quantity = 3
            });

            var go = new GameObject("PlayerInventoryGameStateSaveProvider");
            var provider = go.AddComponent<PlayerInventoryGameStateSaveProvider>();
            SetPrivate(provider, "_playerContext", context);

            var data = new SaveGameData();
            provider.PopulateGameState(data);

            Assert.IsNotNull(data.PlayerInventory);
            Assert.IsNotNull(data.PlayerInventory.Entries);
            Assert.AreEqual(3, data.PlayerInventory.Entries.Length);

            Assert.AreEqual("Equipment", data.PlayerInventory.Entries[0].Kind);
            Assert.AreEqual("eq.sword", data.PlayerInventory.Entries[0].DefinitionId);
            Assert.AreEqual(1, data.PlayerInventory.Entries[0].Quantity);

            Assert.AreEqual("Spell", data.PlayerInventory.Entries[1].Kind);
            Assert.AreEqual("spell.firebolt", data.PlayerInventory.Entries[1].DefinitionId);
            Assert.AreEqual(1, data.PlayerInventory.Entries[1].Quantity);

            Assert.AreEqual("Item", data.PlayerInventory.Entries[2].Kind);
            Assert.AreEqual("item.potion", data.PlayerInventory.Entries[2].DefinitionId);
            Assert.AreEqual(1, data.PlayerInventory.Entries[2].Quantity);

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(inventory);
            Object.DestroyImmediate(context);
        }

        [Test]
        public void PopulateGameState_WithoutContext_WritesEmptyInventory()
        {
            var go = new GameObject("PlayerInventoryGameStateSaveProvider");
            var provider = go.AddComponent<PlayerInventoryGameStateSaveProvider>();
            var data = new SaveGameData();

            provider.PopulateGameState(data);

            Assert.IsNotNull(data.PlayerInventory);
            Assert.IsNotNull(data.PlayerInventory.Entries);
            Assert.AreEqual(0, data.PlayerInventory.Entries.Length);

            Object.DestroyImmediate(go);
        }

        private static void SetPrivate(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' was not found.");
            field.SetValue(target, value);
        }
    }
}
