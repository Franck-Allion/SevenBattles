using NUnit.Framework;
using SevenBattles.Core;
using SevenBattles.Core.Items;
using SevenBattles.Core.Players;
using SevenBattles.Preparation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SevenBattles.Tests.Preparation
{
    public class InventoryDropZoneTests
    {
        [SetUp]
        public void SetUp()
        {
            ResetInventoryDragStatics();
            ResetUnitDragStatics();
            ResetEquipmentSlotDragStatics();
        }

        [Test]
        public void OnDrop_EquipmentDrag_UsesDraggedSlotFallback_WhenZoneServiceOrUnitMissing()
        {
            var eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            var dropZoneGo = new GameObject("InventoryDropZone", typeof(RectTransform), typeof(InventoryDropZone));
            var slotGo = new GameObject(
                "WeaponSlot",
                typeof(RectTransform),
                typeof(Image),
                typeof(CanvasGroup),
                typeof(EquipmentDropSlotView));

            var unitDefinition = ScriptableObject.CreateInstance<SevenBattles.Core.Units.UnitDefinition>();
            unitDefinition.Id = "unit.test";
            unitDefinition.BaseStats = new SevenBattles.Core.Units.UnitStatsData();

            var equipmentDefinition = ScriptableObject.CreateInstance<EquipmentDefinition>();
            equipmentDefinition.Id = "eq.weapon.test";
            equipmentDefinition.SlotType = EquipmentSlotType.Weapon;

            var equipmentRegistry = ScriptableObject.CreateInstance<EquipmentDefinitionRegistry>();
            SetPrivateField(equipmentRegistry, "_definitions", new[] { equipmentDefinition });

            var inventory = ScriptableObject.CreateInstance<PlayerInventory>();
            var playerContext = ScriptableObject.CreateInstance<PlayerContext>();
            playerContext.Inventory = inventory;

            var ownedUnit = new OwnedUnitData
            {
                OwnedUnitId = "owned_test",
                Definition = unitDefinition,
                EquippedItems = OwnedUnitData.CreateDefaultEquippedItems()
            };
            SetEquippedDefinition(ownedUnit, EquipmentSlotType.Weapon, equipmentDefinition.Id);

            IEquipmentService equipmentService = new EquipmentService(playerContext, equipmentRegistry);

            var slot = slotGo.GetComponent<EquipmentDropSlotView>();
            slot.SetSlotType(EquipmentSlotType.Weapon);
            slot.SetEquipmentService(equipmentService);
            slot.SetSelectedUnit(ownedUnit);
            slot.SetEquippedItem(equipmentDefinition.Id, equipmentDefinition);

            var dropZone = dropZoneGo.GetComponent<InventoryDropZone>();
            // Intentionally do not inject zone service or selected unit.

            var pointerData = new PointerEventData(EventSystem.current)
            {
                pointerDrag = slotGo,
                position = new Vector2(400f, 260f)
            };

            slot.OnBeginDrag(pointerData);
            Assert.IsTrue(EquipmentDropSlotView.IsDraggingEquippedItem, "Expected equipped-item drag to start.");

            dropZone.OnDrop(pointerData);
            slot.OnEndDrag(pointerData);

            InventoryEntry restored = inventory.FindEntry(equipmentDefinition.Id);
            Assert.IsNotNull(restored, "Unequip should restore equipment to inventory using dragged-slot fallback context.");
            Assert.AreEqual(InventoryEntry.EntryKind.Equipment, restored.Kind);
            Assert.IsTrue(string.IsNullOrWhiteSpace(GetEquippedDefinition(ownedUnit, EquipmentSlotType.Weapon)), "Unit weapon slot should be cleared after unequip.");

            Object.DestroyImmediate(slotGo);
            Object.DestroyImmediate(dropZoneGo);
            Object.DestroyImmediate(eventSystemGo);
            Object.DestroyImmediate(unitDefinition);
            Object.DestroyImmediate(equipmentDefinition);
            Object.DestroyImmediate(equipmentRegistry);
            Object.DestroyImmediate(inventory);
            Object.DestroyImmediate(playerContext);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' was not found on type '{target.GetType().FullName}'.");
            field.SetValue(target, value);
        }

        private static void SetEquippedDefinition(OwnedUnitData unit, EquipmentSlotType slotType, string definitionId)
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

            Assert.Fail($"Could not find slot '{slotType}' in OwnedUnitData.");
        }

        private static string GetEquippedDefinition(OwnedUnitData unit, EquipmentSlotType slotType)
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

        private static void ResetInventoryDragStatics()
        {
            var method = typeof(InventoryItemDragHandler).GetMethod(
                "ResetStaticState",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            method.Invoke(null, null);
        }

        private static void ResetUnitDragStatics()
        {
            SetStaticAutoProperty(typeof(UnitDragHandler), "IsDragging", false);
            SetStaticAutoProperty(typeof(UnitDragHandler), "DraggingLoadout", null);
        }

        private static void ResetEquipmentSlotDragStatics()
        {
            var method = typeof(EquipmentDropSlotView).GetMethod(
                "ResetStaticState",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            method.Invoke(null, null);
        }

        private static void SetStaticAutoProperty(System.Type type, string propertyName, object value)
        {
            string fieldName = $"<{propertyName}>k__BackingField";
            var field = type.GetField(fieldName, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' was not found on type '{type.FullName}'.");
            field.SetValue(null, value);
        }
    }
}
