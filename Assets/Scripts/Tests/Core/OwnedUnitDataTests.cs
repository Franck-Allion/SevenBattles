using NUnit.Framework;
using SevenBattles.Core.Items;
using SevenBattles.Core.Players;
using UnityEngine;

namespace SevenBattles.Tests.Core
{
    public class OwnedUnitDataTests
    {
        [Test]
        public void Constructor_InitializesAllEquipmentSlotsInExpectedOrder()
        {
            var owned = new OwnedUnitData();

            Assert.IsNotNull(owned.EquippedItems);
            Assert.AreEqual(8, owned.EquippedItems.Length);

            AssertSlot(owned.EquippedItems[0], EquipmentSlotType.Weapon);
            AssertSlot(owned.EquippedItems[1], EquipmentSlotType.Shield);
            AssertSlot(owned.EquippedItems[2], EquipmentSlotType.Helmet);
            AssertSlot(owned.EquippedItems[3], EquipmentSlotType.Armor);
            AssertSlot(owned.EquippedItems[4], EquipmentSlotType.Gloves);
            AssertSlot(owned.EquippedItems[5], EquipmentSlotType.Boots);
            AssertSlot(owned.EquippedItems[6], EquipmentSlotType.Ring);
            AssertSlot(owned.EquippedItems[7], EquipmentSlotType.Amulet);
        }

        [Test]
        public void JsonSerialization_RoundTripsEquippedItemsWithDefinitionId()
        {
            var owned = new OwnedUnitData
            {
                OwnedUnitId = "owned_1"
            };

            EquipmentSlotEntry weapon = owned.EquippedItems[0];
            weapon.DefinitionId = "eq.weapon.iron_sword";
            owned.EquippedItems[0] = weapon;

            EquipmentSlotEntry amulet = owned.EquippedItems[7];
            amulet.DefinitionId = "eq.amulet.mana";
            owned.EquippedItems[7] = amulet;

            string json = JsonUtility.ToJson(owned);
            StringAssert.Contains("\"EquippedItems\"", json);
            StringAssert.Contains("\"DefinitionId\":\"eq.weapon.iron_sword\"", json);
            StringAssert.Contains("\"DefinitionId\":\"eq.amulet.mana\"", json);

            OwnedUnitData loaded = JsonUtility.FromJson<OwnedUnitData>(json);
            Assert.IsNotNull(loaded);
            Assert.IsNotNull(loaded.EquippedItems);
            Assert.AreEqual(8, loaded.EquippedItems.Length);
            Assert.AreEqual(EquipmentSlotType.Weapon, loaded.EquippedItems[0].SlotType);
            Assert.AreEqual("eq.weapon.iron_sword", loaded.EquippedItems[0].DefinitionId);
            Assert.AreEqual(EquipmentSlotType.Amulet, loaded.EquippedItems[7].SlotType);
            Assert.AreEqual("eq.amulet.mana", loaded.EquippedItems[7].DefinitionId);
        }

        [Test]
        public void JsonDeserialization_MissingEquippedItems_UsesDefaultSlots()
        {
            const string json = "{ \"OwnedUnitId\": \"owned_1\" }";

            OwnedUnitData loaded = JsonUtility.FromJson<OwnedUnitData>(json);

            Assert.IsNotNull(loaded);
            Assert.IsNotNull(loaded.EquippedItems);
            Assert.AreEqual(8, loaded.EquippedItems.Length);
            Assert.AreEqual(EquipmentSlotType.Weapon, loaded.EquippedItems[0].SlotType);
            Assert.AreEqual(EquipmentSlotType.Armor, loaded.EquippedItems[3].SlotType);
            Assert.AreEqual(EquipmentSlotType.Amulet, loaded.EquippedItems[7].SlotType);
        }

        private static void AssertSlot(EquipmentSlotEntry entry, EquipmentSlotType expectedSlot)
        {
            Assert.AreEqual(expectedSlot, entry.SlotType);
            Assert.IsTrue(string.IsNullOrEmpty(entry.DefinitionId));
        }
    }
}
