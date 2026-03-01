using NUnit.Framework;
using SevenBattles.Core.Items;
using SevenBattles.Core.Players;
using UnityEngine;

namespace SevenBattles.Tests.Core
{
    public class EquipmentServiceTests
    {
        [Test]
        public void TryEquip_ValidSlot_RemovesFromInventory_SetsOnUnit()
        {
            var inventory = new FakeInventoryGateway();
            var resolver = new FakeDefinitionResolver();
            var service = new EquipmentService(inventory, resolver);

            var unit = new OwnedUnitData();
            var newEquipment = CreateEquipment("eq.weapon.sword", EquipmentSlotType.Weapon);
            inventory.RemoveResultByDefinitionId[newEquipment.Id] = true;

            bool eventRaised = false;
            service.EquipmentChanged += (changedUnit, slot, equipment) =>
            {
                eventRaised = changedUnit == unit &&
                              slot == EquipmentSlotType.Weapon &&
                              equipment == newEquipment;
            };

            bool result = service.TryEquip(unit, newEquipment);

            Assert.IsTrue(result);
            Assert.AreEqual(1, inventory.RemoveCallsCount);
            Assert.AreEqual(newEquipment.Id, inventory.LastRemovedDefinitionId);
            Assert.AreEqual(0, inventory.AddedEquipment.Count);
            Assert.AreEqual(newEquipment.Id, GetSlotDefinitionId(unit, EquipmentSlotType.Weapon));
            Assert.IsTrue(eventRaised);

            Object.DestroyImmediate(newEquipment);
        }

        [Test]
        public void TryEquip_OccupiedSlot_SwapsOldToInventory()
        {
            var inventory = new FakeInventoryGateway();
            var resolver = new FakeDefinitionResolver();
            var service = new EquipmentService(inventory, resolver);

            var unit = new OwnedUnitData();
            var oldEquipment = CreateEquipment("eq.weapon.old", EquipmentSlotType.Weapon);
            var newEquipment = CreateEquipment("eq.weapon.new", EquipmentSlotType.Weapon);
            SetSlotDefinitionId(unit, EquipmentSlotType.Weapon, oldEquipment.Id);

            resolver.ById[oldEquipment.Id] = oldEquipment;
            inventory.RemoveResultByDefinitionId[newEquipment.Id] = true;

            bool result = service.TryEquip(unit, newEquipment);

            Assert.IsTrue(result);
            Assert.AreEqual(1, inventory.RemoveCallsCount);
            Assert.AreEqual(newEquipment.Id, inventory.LastRemovedDefinitionId);
            Assert.AreEqual(1, inventory.AddedEquipment.Count);
            Assert.AreSame(oldEquipment, inventory.AddedEquipment[0]);
            Assert.AreEqual(newEquipment.Id, GetSlotDefinitionId(unit, EquipmentSlotType.Weapon));

            Object.DestroyImmediate(newEquipment);
            Object.DestroyImmediate(oldEquipment);
        }

        [Test]
        public void TryEquip_WrongSlotType_ReturnsFalse()
        {
            var inventory = new FakeInventoryGateway();
            var resolver = new FakeDefinitionResolver();
            var service = new EquipmentService(inventory, resolver);

            var unit = new OwnedUnitData
            {
                EquippedItems = new[]
                {
                    new EquipmentSlotEntry
                    {
                        SlotType = EquipmentSlotType.Shield,
                        DefinitionId = null
                    }
                }
            };
            var equipment = CreateEquipment("eq.weapon.sword", EquipmentSlotType.Weapon);

            bool result = service.TryEquip(unit, equipment);

            Assert.IsFalse(result);
            Assert.AreEqual(0, inventory.RemoveCallsCount);
            Assert.AreEqual(0, inventory.AddedEquipment.Count);
            Assert.AreEqual(1, unit.EquippedItems.Length);
            Assert.AreEqual(EquipmentSlotType.Shield, unit.EquippedItems[0].SlotType);

            Object.DestroyImmediate(equipment);
        }

        [Test]
        public void TryUnequip_ReturnsToInventory()
        {
            var inventory = new FakeInventoryGateway();
            var resolver = new FakeDefinitionResolver();
            var service = new EquipmentService(inventory, resolver);

            var unit = new OwnedUnitData();
            var equipped = CreateEquipment("eq.weapon.staff", EquipmentSlotType.Weapon);
            SetSlotDefinitionId(unit, EquipmentSlotType.Weapon, equipped.Id);
            resolver.ById[equipped.Id] = equipped;

            bool eventRaised = false;
            service.EquipmentChanged += (changedUnit, slot, equipment) =>
            {
                eventRaised = changedUnit == unit &&
                              slot == EquipmentSlotType.Weapon &&
                              equipment == null;
            };

            bool result = service.TryUnequip(unit, EquipmentSlotType.Weapon);

            Assert.IsTrue(result);
            Assert.AreEqual(1, inventory.AddedEquipment.Count);
            Assert.AreSame(equipped, inventory.AddedEquipment[0]);
            Assert.IsTrue(string.IsNullOrWhiteSpace(GetSlotDefinitionId(unit, EquipmentSlotType.Weapon)));
            Assert.IsTrue(eventRaised);

            Object.DestroyImmediate(equipped);
        }

        [Test]
        public void CanEquip_NullDef_ReturnsFalse()
        {
            var inventory = new FakeInventoryGateway();
            var resolver = new FakeDefinitionResolver();
            var service = new EquipmentService(inventory, resolver);
            var unit = new OwnedUnitData();

            bool canEquip = service.CanEquip(unit, null);

            Assert.IsFalse(canEquip);
        }

        private static EquipmentDefinition CreateEquipment(string id, EquipmentSlotType slotType)
        {
            var definition = ScriptableObject.CreateInstance<EquipmentDefinition>();
            definition.Id = id;
            definition.SlotType = slotType;
            return definition;
        }

        private static void SetSlotDefinitionId(OwnedUnitData unit, EquipmentSlotType slotType, string definitionId)
        {
            for (int i = 0; i < unit.EquippedItems.Length; i++)
            {
                if (unit.EquippedItems[i].SlotType != slotType)
                {
                    continue;
                }

                EquipmentSlotEntry entry = unit.EquippedItems[i];
                entry.DefinitionId = definitionId;
                unit.EquippedItems[i] = entry;
                return;
            }
        }

        private static string GetSlotDefinitionId(OwnedUnitData unit, EquipmentSlotType slotType)
        {
            for (int i = 0; i < unit.EquippedItems.Length; i++)
            {
                if (unit.EquippedItems[i].SlotType == slotType)
                {
                    return unit.EquippedItems[i].DefinitionId;
                }
            }

            return null;
        }

        private sealed class FakeInventoryGateway : EquipmentService.IInventoryGateway
        {
            public readonly System.Collections.Generic.Dictionary<string, bool> RemoveResultByDefinitionId =
                new System.Collections.Generic.Dictionary<string, bool>(System.StringComparer.Ordinal);

            public readonly System.Collections.Generic.List<EquipmentDefinition> AddedEquipment =
                new System.Collections.Generic.List<EquipmentDefinition>();

            public int RemoveCallsCount { get; private set; }
            public string LastRemovedDefinitionId { get; private set; }

            public bool RemoveEquipment(string definitionId)
            {
                RemoveCallsCount++;
                LastRemovedDefinitionId = definitionId;
                if (RemoveResultByDefinitionId.TryGetValue(definitionId, out bool result))
                {
                    return result;
                }

                return false;
            }

            public void AddEquipment(EquipmentDefinition equipmentDef)
            {
                if (equipmentDef != null)
                {
                    AddedEquipment.Add(equipmentDef);
                }
            }
        }

        private sealed class FakeDefinitionResolver : EquipmentService.IDefinitionResolver
        {
            public readonly System.Collections.Generic.Dictionary<string, EquipmentDefinition> ById =
                new System.Collections.Generic.Dictionary<string, EquipmentDefinition>(System.StringComparer.Ordinal);

            public EquipmentDefinition GetById(string definitionId)
            {
                if (string.IsNullOrWhiteSpace(definitionId))
                {
                    return null;
                }

                ById.TryGetValue(definitionId, out EquipmentDefinition definition);
                return definition;
            }
        }
    }
}
