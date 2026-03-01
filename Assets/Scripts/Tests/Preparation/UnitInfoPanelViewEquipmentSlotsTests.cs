using NUnit.Framework;
using SevenBattles.Core.Battle;
using SevenBattles.Core.Items;
using SevenBattles.Core.Players;
using SevenBattles.Core.Units;
using SevenBattles.Preparation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SevenBattles.Tests.Preparation
{
    public class UnitInfoPanelViewEquipmentSlotsTests
    {
        private static void SetPrivate(object target, string fieldName, object value)
        {
            var type = target.GetType();
            var field = type.GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' was not found on type '{type.FullName}'.");
            field.SetValue(target, value);
        }

        [Test]
        public void ShowUnit_CreatesEquipmentSlots_AndRefreshesCompletionForSelectedOwnedUnit()
        {
            PlayerContext.SetRuntimeInstance(null);

            var context = ScriptableObject.CreateInstance<PlayerContext>();
            var definition = ScriptableObject.CreateInstance<UnitDefinition>();
            definition.Id = "unit.mage";
            definition.BaseStats = new UnitStatsData();

            context.SetOwnedUnits(new[]
            {
                new OwnedUnitData
                {
                    OwnedUnitId = "owned_1",
                    Definition = definition,
                    EquippedItems = new[]
                    {
                        new EquipmentSlotEntry
                        {
                            SlotType = EquipmentSlotType.Weapon,
                            EquipmentDefinitionId = "eq.weapon.staff"
                        }
                    }
                },
                new OwnedUnitData
                {
                    OwnedUnitId = "owned_2",
                    Definition = definition,
                    EquippedItems = System.Array.Empty<EquipmentSlotEntry>()
                }
            });

            var root = new GameObject("UnitInfoPanel", typeof(RectTransform), typeof(UnitInfoPanelView));
            var view = root.GetComponent<UnitInfoPanelView>();

            var statsContainer = new GameObject("StatsContainer", typeof(RectTransform));
            statsContainer.transform.SetParent(root.transform, false);

            SetPrivate(view, "_playerContext", context);
            SetPrivate(view, "_enableEquipmentSlots", true);
            SetPrivate(view, "_statsContainer", statsContainer);

            var loadout = new UnitSpellLoadout
            {
                Definition = definition,
                Level = 1
            };

            view.ShowUnit(loadout, "owned_1", "Mage One");

            EquipmentSlotLayoutBuilder builder = statsContainer.GetComponentInChildren<EquipmentSlotLayoutBuilder>(true);
            Assert.IsNotNull(builder, "EquipmentSlotLayoutBuilder should be created inside the panel.");
            Assert.AreEqual(7, builder.SlotViews.Count, "Exactly seven equipment slots should exist.");

            EquipmentDropSlotView weaponSlot = FindSlot(builder, EquipmentSlotType.Weapon);
            Assert.IsNotNull(weaponSlot, "Weapon slot should exist.");
            Assert.IsTrue(weaponSlot.IsCompletionVisualActive, "Weapon slot should show completion for owned_1.");

            view.ShowUnit(loadout, "owned_2", "Mage Two");

            Assert.IsFalse(weaponSlot.IsCompletionVisualActive, "Weapon slot completion should clear for owned_2.");

            Object.DestroyImmediate(root);
            Object.DestroyImmediate(definition);
            Object.DestroyImmediate(context);
            PlayerContext.SetRuntimeInstance(null);
        }

        [Test]
        public void DragDrop_FromInventoryToEquipmentSlot_EquipsAndRefreshesInventoryAndSlotVisual()
        {
            ResetInventoryDragStatics();
            ResetUnitDragStatics();
            ResetEquipmentSlotDragStatics();
            PlayerContext.SetRuntimeInstance(null);

            var context = ScriptableObject.CreateInstance<PlayerContext>();
            var inventory = ScriptableObject.CreateInstance<PlayerInventory>();
            context.Inventory = inventory;
            PlayerContext.SetRuntimeInstance(context);

            var unitDefinition = ScriptableObject.CreateInstance<UnitDefinition>();
            unitDefinition.Id = "unit.knight";
            unitDefinition.BaseStats = new UnitStatsData();

            var equipmentDefinition = ScriptableObject.CreateInstance<EquipmentDefinition>();
            equipmentDefinition.Id = "eq.weapon.sword";
            equipmentDefinition.SlotType = EquipmentSlotType.Weapon;
            equipmentDefinition.Icon = CreateSprite(Color.yellow);

            var equipmentRegistry = ScriptableObject.CreateInstance<EquipmentDefinitionRegistry>();
            SetPrivate(equipmentRegistry, "_definitions", new[] { equipmentDefinition });

            inventory.AddEquipment(equipmentDefinition);
            context.SetOwnedUnits(new[]
            {
                new OwnedUnitData
                {
                    OwnedUnitId = "owned_drag_target",
                    Definition = unitDefinition,
                    EquippedItems = System.Array.Empty<EquipmentSlotEntry>()
                }
            });

            var canvasRoot = new GameObject("CanvasRoot", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            var inventoryContent = new GameObject("InventoryContent", typeof(RectTransform)).GetComponent<RectTransform>();
            inventoryContent.SetParent(canvasRoot.transform, false);
            var presenterRoot = new GameObject("InventoryPresenter");
            presenterRoot.transform.SetParent(canvasRoot.transform, false);

            var presenter = presenterRoot.AddComponent<PreparationInventoryListPresenter>();
            var itemTemplate = CreateEntryTemplate("ItemTemplate");
            var emptyTemplate = CreateEmptyTemplate("ItemEmptyTemplate");
            presenter.Configure(context, null, inventoryContent, itemTemplate.gameObject, emptyTemplate, equipmentRegistry, null);
            presenter.RefreshNow();

            Assert.AreEqual(1, CountActiveItemViews(inventoryContent));
            Assert.AreEqual(29, CountActiveEmptySlots(inventoryContent));

            var panelRoot = new GameObject("UnitInfoPanel", typeof(RectTransform), typeof(UnitInfoPanelView));
            panelRoot.transform.SetParent(canvasRoot.transform, false);
            var panelView = panelRoot.GetComponent<UnitInfoPanelView>();
            var statsContainer = new GameObject("StatsContainer", typeof(RectTransform));
            statsContainer.transform.SetParent(panelRoot.transform, false);

            SetPrivate(panelView, "_playerContext", context);
            SetPrivate(panelView, "_enableEquipmentSlots", true);
            SetPrivate(panelView, "_equipmentDefinitionRegistry", equipmentRegistry);
            SetPrivate(panelView, "_statsContainer", statsContainer);

            var loadout = new UnitSpellLoadout
            {
                Definition = unitDefinition,
                Level = 1
            };
            panelView.ShowUnit(loadout, "owned_drag_target", "Knight");

            EquipmentSlotLayoutBuilder builder = statsContainer.GetComponentInChildren<EquipmentSlotLayoutBuilder>(true);
            Assert.IsNotNull(builder);

            EquipmentDropSlotView weaponSlot = FindSlot(builder, EquipmentSlotType.Weapon);
            Assert.IsNotNull(weaponSlot);
            Assert.IsFalse(weaponSlot.IsCompletionVisualActive);

            var eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            var pointerData = new PointerEventData(EventSystem.current)
            {
                position = new Vector2(640f, 360f)
            };

            GameObject firstInventorySlot = GetActiveSlotAt(inventoryContent, 0);
            Assert.IsNotNull(firstInventorySlot);

            var dragHandler = firstInventorySlot.GetComponent<InventoryItemDragHandler>();
            Assert.IsNotNull(dragHandler);

            dragHandler.OnBeginDrag(pointerData);
            Assert.IsTrue(InventoryItemDragHandler.IsDraggingItem);

            InvokePrivate(panelView, "UpdateEquipmentSlotDragPreviews");
            Assert.IsTrue(weaponSlot.IsDragPreviewActive);
            Assert.IsTrue(weaponSlot.IsValidDropPreview);

            weaponSlot.OnDrop(pointerData);
            dragHandler.OnEndDrag(pointerData);

            Assert.IsFalse(InventoryItemDragHandler.IsDraggingItem);
            Assert.IsNull(inventory.FindEntry(equipmentDefinition.Id));

            OwnedUnitData equippedOwnedUnit = context.OwnedUnits[0];
            Assert.AreEqual(1, equippedOwnedUnit.EquippedItems.Length);
            Assert.AreEqual(EquipmentSlotType.Weapon, equippedOwnedUnit.EquippedItems[0].SlotType);
            Assert.AreEqual(equipmentDefinition.Id, equippedOwnedUnit.EquippedItems[0].EquipmentDefinitionId);
            Assert.IsTrue(weaponSlot.IsCompletionVisualActive);

            Assert.AreEqual(0, CountActiveItemViews(inventoryContent));
            Assert.AreEqual(30, CountActiveEmptySlots(inventoryContent));

            Object.DestroyImmediate(eventSystemGo);
            Object.DestroyImmediate(itemTemplate.gameObject);
            Object.DestroyImmediate(emptyTemplate);
            Object.DestroyImmediate(panelRoot);
            Object.DestroyImmediate(presenterRoot);
            Object.DestroyImmediate(inventoryContent.gameObject);
            Object.DestroyImmediate(canvasRoot);
            Object.DestroyImmediate(equipmentRegistry);
            Object.DestroyImmediate(equipmentDefinition.Icon.texture);
            Object.DestroyImmediate(equipmentDefinition);
            Object.DestroyImmediate(unitDefinition);
            Object.DestroyImmediate(inventory);
            Object.DestroyImmediate(context);
            PlayerContext.SetRuntimeInstance(null);
        }

        [Test]
        public void DragDrop_FromEquippedSlotToInventory_UnequipsAndRefreshesInventoryAndSlotVisual()
        {
            ResetInventoryDragStatics();
            ResetUnitDragStatics();
            ResetEquipmentSlotDragStatics();
            PlayerContext.SetRuntimeInstance(null);

            var context = ScriptableObject.CreateInstance<PlayerContext>();
            var inventory = ScriptableObject.CreateInstance<PlayerInventory>();
            context.Inventory = inventory;
            PlayerContext.SetRuntimeInstance(context);

            var unitDefinition = ScriptableObject.CreateInstance<UnitDefinition>();
            unitDefinition.Id = "unit.knight";
            unitDefinition.BaseStats = new UnitStatsData();

            var equipmentDefinition = ScriptableObject.CreateInstance<EquipmentDefinition>();
            equipmentDefinition.Id = "eq.weapon.sword";
            equipmentDefinition.SlotType = EquipmentSlotType.Weapon;
            equipmentDefinition.Icon = CreateSprite(Color.cyan);

            var equipmentRegistry = ScriptableObject.CreateInstance<EquipmentDefinitionRegistry>();
            SetPrivate(equipmentRegistry, "_definitions", new[] { equipmentDefinition });

            context.SetOwnedUnits(new[]
            {
                new OwnedUnitData
                {
                    OwnedUnitId = "owned_drag_source",
                    Definition = unitDefinition,
                    EquippedItems = new[]
                    {
                        new EquipmentSlotEntry
                        {
                            SlotType = EquipmentSlotType.Weapon,
                            EquipmentDefinitionId = equipmentDefinition.Id
                        }
                    }
                }
            });

            var canvasRoot = new GameObject("CanvasRoot", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            var inventoryPanel = new GameObject("InventoryPanel", typeof(RectTransform));
            inventoryPanel.transform.SetParent(canvasRoot.transform, false);
            var inventoryContent = new GameObject("InventoryContent", typeof(RectTransform)).GetComponent<RectTransform>();
            inventoryContent.SetParent(inventoryPanel.transform, false);
            var presenterRoot = new GameObject("InventoryPresenter");
            presenterRoot.transform.SetParent(canvasRoot.transform, false);

            var presenter = presenterRoot.AddComponent<PreparationInventoryListPresenter>();
            var itemTemplate = CreateEntryTemplate("ItemTemplate");
            var emptyTemplate = CreateEmptyTemplate("ItemEmptyTemplate");
            presenter.Configure(context, inventoryPanel, inventoryContent, itemTemplate.gameObject, emptyTemplate, equipmentRegistry, null);
            presenter.RefreshNow();

            InventoryDropZone inventoryDropZone = inventoryPanel.GetComponent<InventoryDropZone>();
            Assert.IsNotNull(inventoryDropZone, "Inventory presenter should ensure an InventoryDropZone exists.");
            Assert.AreEqual(0, CountActiveItemViews(inventoryContent));
            Assert.AreEqual(30, CountActiveEmptySlots(inventoryContent));

            var panelRoot = new GameObject("UnitInfoPanel", typeof(RectTransform), typeof(UnitInfoPanelView));
            panelRoot.transform.SetParent(canvasRoot.transform, false);
            var panelView = panelRoot.GetComponent<UnitInfoPanelView>();
            var statsContainer = new GameObject("StatsContainer", typeof(RectTransform));
            statsContainer.transform.SetParent(panelRoot.transform, false);

            SetPrivate(panelView, "_playerContext", context);
            SetPrivate(panelView, "_enableEquipmentSlots", true);
            SetPrivate(panelView, "_equipmentDefinitionRegistry", equipmentRegistry);
            SetPrivate(panelView, "_statsContainer", statsContainer);

            var loadout = new UnitSpellLoadout
            {
                Definition = unitDefinition,
                Level = 1
            };
            panelView.ShowUnit(loadout, "owned_drag_source", "Knight");

            InvokePrivate(panelView, "EnsureInventoryDropZone");
            InvokePrivate(panelView, "WireInventoryDropZoneEvents");

            EquipmentSlotLayoutBuilder builder = statsContainer.GetComponentInChildren<EquipmentSlotLayoutBuilder>(true);
            Assert.IsNotNull(builder);
            EquipmentDropSlotView weaponSlot = FindSlot(builder, EquipmentSlotType.Weapon);
            Assert.IsNotNull(weaponSlot);
            Assert.IsTrue(weaponSlot.IsCompletionVisualActive);
            Assert.IsTrue(weaponSlot.HasEquippedItem);

            var eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            var pointerData = new PointerEventData(EventSystem.current)
            {
                position = new Vector2(960f, 360f)
            };

            weaponSlot.OnBeginDrag(pointerData);
            Assert.IsTrue(EquipmentDropSlotView.IsDraggingEquippedItem);
            Assert.AreEqual(EquipmentSlotType.Weapon, EquipmentDropSlotView.DraggingFromSlot);

            inventoryDropZone.OnDrop(pointerData);
            weaponSlot.OnEndDrag(pointerData);

            Assert.IsFalse(EquipmentDropSlotView.IsDraggingEquippedItem);
            Assert.AreEqual(0, context.OwnedUnits[0].EquippedItems.Length);
            Assert.IsFalse(weaponSlot.IsCompletionVisualActive);

            InventoryEntry restoredEntry = inventory.FindEntry(equipmentDefinition.Id);
            Assert.IsNotNull(restoredEntry, "Unequip should restore equipment entry to inventory.");
            Assert.AreEqual(InventoryEntry.EntryKind.Equipment, restoredEntry.Kind);

            Assert.AreEqual(1, CountActiveItemViews(inventoryContent));
            Assert.AreEqual(29, CountActiveEmptySlots(inventoryContent));

            Object.DestroyImmediate(eventSystemGo);
            Object.DestroyImmediate(itemTemplate.gameObject);
            Object.DestroyImmediate(emptyTemplate);
            Object.DestroyImmediate(panelRoot);
            Object.DestroyImmediate(presenterRoot);
            Object.DestroyImmediate(inventoryPanel);
            Object.DestroyImmediate(canvasRoot);
            Object.DestroyImmediate(equipmentRegistry);
            Object.DestroyImmediate(equipmentDefinition.Icon.texture);
            Object.DestroyImmediate(equipmentDefinition);
            Object.DestroyImmediate(unitDefinition);
            Object.DestroyImmediate(inventory);
            Object.DestroyImmediate(context);
            PlayerContext.SetRuntimeInstance(null);
        }

        private static EquipmentDropSlotView FindSlot(EquipmentSlotLayoutBuilder builder, EquipmentSlotType slotType)
        {
            if (builder == null || builder.SlotViews == null)
            {
                return null;
            }

            for (int i = 0; i < builder.SlotViews.Count; i++)
            {
                EquipmentDropSlotView slot = builder.SlotViews[i];
                if (slot != null && slot.SlotType == slotType)
                {
                    return slot;
                }
            }

            return null;
        }

        private static PreparationInventoryItemEntryView CreateEntryTemplate(string name)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(PreparationInventoryItemEntryView));
            var icon = new GameObject("ItemIcon", typeof(RectTransform), typeof(Image));
            icon.transform.SetParent(root.transform, false);
            var quantity = new GameObject("Text (TMP)", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
            quantity.transform.SetParent(root.transform, false);
            return root.GetComponent<PreparationInventoryItemEntryView>();
        }

        private static GameObject CreateEmptyTemplate(string name)
        {
            return new GameObject(name, typeof(RectTransform), typeof(Image));
        }

        private static int CountActiveItemViews(Transform content)
        {
            int count = 0;
            for (int i = 0; i < content.childCount; i++)
            {
                Transform child = content.GetChild(i);
                if (!child.gameObject.activeSelf)
                {
                    continue;
                }

                if (child.GetComponent<PreparationInventoryItemEntryView>() != null)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountActiveEmptySlots(Transform content)
        {
            int count = 0;
            for (int i = 0; i < content.childCount; i++)
            {
                Transform child = content.GetChild(i);
                if (!child.gameObject.activeSelf)
                {
                    continue;
                }

                if (string.Equals(child.name, "ItemEmpty", System.StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        private static GameObject GetActiveSlotAt(Transform content, int slotIndex)
        {
            for (int i = 0; i < content.childCount; i++)
            {
                Transform child = content.GetChild(i);
                if (!child.gameObject.activeSelf)
                {
                    continue;
                }

                if (child.GetSiblingIndex() == slotIndex)
                {
                    return child.gameObject;
                }
            }

            return null;
        }

        private static Sprite CreateSprite(Color color)
        {
            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var pixels = new Color[16];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        }

        private static void InvokePrivate(object target, string methodName)
        {
            var method = target.GetType().GetMethod(
                methodName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Method '{methodName}' was not found on type '{target.GetType().FullName}'.");
            method.Invoke(target, null);
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
