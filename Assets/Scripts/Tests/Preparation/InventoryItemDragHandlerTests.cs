using NUnit.Framework;
using SevenBattles.Core.Items;
using SevenBattles.Preparation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SevenBattles.Tests.Preparation
{
    public class InventoryItemDragHandlerTests
    {
        [SetUp]
        public void SetUp()
        {
            ResetInventoryDragStatics();
            ResetUnitDragStatics();
        }

        [Test]
        public void BeginAndEndDrag_EquipmentEntry_SetsAndClearsStatics()
        {
            var eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            var handlerGo = new GameObject("InventoryItem", typeof(RectTransform), typeof(CanvasGroup), typeof(InventoryItemDragHandler));
            var ghostGo = new GameObject("DragGhost", typeof(RectTransform), typeof(Image));
            ghostGo.SetActive(false);

            var handler = handlerGo.GetComponent<InventoryItemDragHandler>();
            var canvasGroup = handlerGo.GetComponent<CanvasGroup>();
            var ghostRect = ghostGo.GetComponent<RectTransform>();
            var ghostImage = ghostGo.GetComponent<Image>();

            Texture2D texture = new Texture2D(2, 2);
            Sprite icon = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f));
            var equipment = ScriptableObject.CreateInstance<EquipmentDefinition>();
            equipment.Id = "eq.staff";
            equipment.Icon = icon;
            equipment.SlotType = EquipmentSlotType.Weapon;

            var entry = new InventoryEntry
            {
                Kind = InventoryEntry.EntryKind.Equipment,
                DefinitionId = "eq.staff",
                Quantity = 1
            };

            handler.ConfigureDragPayload(entry, equipment);
            handler.SetDragGhostRoot(ghostRect);

            canvasGroup.blocksRaycasts = true;
            canvasGroup.alpha = 1f;

            var beginEvent = new PointerEventData(EventSystem.current)
            {
                position = new Vector2(120f, 240f)
            };

            handler.OnBeginDrag(beginEvent);

            Assert.IsTrue(InventoryItemDragHandler.IsDraggingItem);
            Assert.AreSame(entry, InventoryItemDragHandler.DraggingEntry);
            Assert.AreSame(equipment, InventoryItemDragHandler.DraggingEquipmentDefinition);
            Assert.IsNull(InventoryItemDragHandler.DraggingItemDefinition);
            Assert.IsFalse(canvasGroup.blocksRaycasts);
            Assert.AreEqual(0.4f, canvasGroup.alpha, 0.0001f);
            Assert.IsTrue(ghostGo.activeSelf);
            Assert.AreSame(icon, ghostImage.sprite);
            Assert.IsTrue(ghostImage.enabled);

            var dragEvent = new PointerEventData(EventSystem.current)
            {
                position = new Vector2(300f, 480f)
            };
            handler.OnDrag(dragEvent);
            Assert.AreEqual(300f, ghostRect.position.x, 0.001f);
            Assert.AreEqual(480f, ghostRect.position.y, 0.001f);

            handler.OnEndDrag(beginEvent);

            Assert.IsFalse(InventoryItemDragHandler.IsDraggingItem);
            Assert.IsNull(InventoryItemDragHandler.DraggingEntry);
            Assert.IsNull(InventoryItemDragHandler.DraggingEquipmentDefinition);
            Assert.IsNull(InventoryItemDragHandler.DraggingItemDefinition);
            Assert.IsTrue(canvasGroup.blocksRaycasts);
            Assert.AreEqual(1f, canvasGroup.alpha, 0.0001f);
            Assert.IsFalse(ghostGo.activeSelf);

            Object.DestroyImmediate(equipment);
            Object.DestroyImmediate(icon);
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(ghostGo);
            Object.DestroyImmediate(handlerGo);
            Object.DestroyImmediate(eventSystemGo);
        }

        [Test]
        public void BeginAndEndDrag_ConsumableItemEntry_SetsAndClearsItemPayload()
        {
            var eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            var handlerGo = new GameObject("InventoryItem", typeof(RectTransform), typeof(CanvasGroup), typeof(InventoryItemDragHandler));
            var ghostGo = new GameObject("DragGhost", typeof(RectTransform), typeof(Image));
            ghostGo.SetActive(false);

            var handler = handlerGo.GetComponent<InventoryItemDragHandler>();
            var canvasGroup = handlerGo.GetComponent<CanvasGroup>();
            var ghostRect = ghostGo.GetComponent<RectTransform>();
            var ghostImage = ghostGo.GetComponent<Image>();

            Texture2D texture = new Texture2D(2, 2);
            Sprite icon = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f));
            var itemDef = ScriptableObject.CreateInstance<ItemDefinition>();
            itemDef.Id = "item.potion";
            itemDef.IsConsumable = true;
            itemDef.Icon = icon;

            var entry = new InventoryEntry
            {
                Kind = InventoryEntry.EntryKind.Item,
                DefinitionId = "item.potion",
                Quantity = 1
            };

            handler.ConfigureDragPayload(entry, null, itemDef);
            handler.SetDragGhostRoot(ghostRect);

            canvasGroup.blocksRaycasts = true;
            canvasGroup.alpha = 1f;

            var beginEvent = new PointerEventData(EventSystem.current)
            {
                position = new Vector2(120f, 240f)
            };

            handler.OnBeginDrag(beginEvent);

            Assert.IsTrue(InventoryItemDragHandler.IsDraggingItem);
            Assert.AreSame(entry, InventoryItemDragHandler.DraggingEntry);
            Assert.IsNull(InventoryItemDragHandler.DraggingEquipmentDefinition);
            Assert.AreSame(itemDef, InventoryItemDragHandler.DraggingItemDefinition);
            Assert.AreSame(icon, ghostImage.sprite);
            Assert.IsTrue(ghostImage.enabled);

            handler.OnEndDrag(beginEvent);

            Assert.IsFalse(InventoryItemDragHandler.IsDraggingItem);
            Assert.IsNull(InventoryItemDragHandler.DraggingEntry);
            Assert.IsNull(InventoryItemDragHandler.DraggingEquipmentDefinition);
            Assert.IsNull(InventoryItemDragHandler.DraggingItemDefinition);
            Assert.IsTrue(canvasGroup.blocksRaycasts);
            Assert.AreEqual(1f, canvasGroup.alpha, 0.0001f);
            Assert.IsFalse(ghostGo.activeSelf);

            Object.DestroyImmediate(itemDef);
            Object.DestroyImmediate(icon);
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(ghostGo);
            Object.DestroyImmediate(handlerGo);
            Object.DestroyImmediate(eventSystemGo);
        }

        [Test]
        public void BeginDrag_NonEquipmentEntry_DoesNotStartDrag()
        {
            var eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            var handlerGo = new GameObject("InventoryItem", typeof(RectTransform), typeof(CanvasGroup), typeof(InventoryItemDragHandler));
            var ghostGo = new GameObject("DragGhost", typeof(RectTransform), typeof(Image));
            ghostGo.SetActive(false);

            var handler = handlerGo.GetComponent<InventoryItemDragHandler>();
            var canvasGroup = handlerGo.GetComponent<CanvasGroup>();
            var ghostRect = ghostGo.GetComponent<RectTransform>();

            var equipment = ScriptableObject.CreateInstance<EquipmentDefinition>();
            equipment.Id = "eq.boots";
            equipment.SlotType = EquipmentSlotType.Boots;

            var entry = new InventoryEntry
            {
                Kind = InventoryEntry.EntryKind.Item,
                DefinitionId = "item.potion",
                Quantity = 2
            };

            handler.ConfigureDragPayload(entry, equipment);
            handler.SetDragGhostRoot(ghostRect);

            canvasGroup.blocksRaycasts = true;
            canvasGroup.alpha = 1f;

            var beginEvent = new PointerEventData(EventSystem.current)
            {
                position = new Vector2(90f, 110f)
            };

            handler.OnBeginDrag(beginEvent);

            Assert.IsFalse(InventoryItemDragHandler.IsDraggingItem);
            Assert.IsNull(InventoryItemDragHandler.DraggingEntry);
            Assert.IsNull(InventoryItemDragHandler.DraggingEquipmentDefinition);
            Assert.IsTrue(canvasGroup.blocksRaycasts);
            Assert.AreEqual(1f, canvasGroup.alpha, 0.0001f);
            Assert.IsFalse(ghostGo.activeSelf);

            Object.DestroyImmediate(equipment);
            Object.DestroyImmediate(ghostGo);
            Object.DestroyImmediate(handlerGo);
            Object.DestroyImmediate(eventSystemGo);
        }

        [Test]
        public void BeginDrag_UsesBoundDataFromEntryView_WhenHandlerPayloadNotConfigured()
        {
            var eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            var handlerGo = new GameObject(
                "InventoryItem",
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(PreparationInventoryItemEntryView),
                typeof(InventoryItemDragHandler));
            var ghostGo = new GameObject("DragGhost", typeof(RectTransform), typeof(Image));
            ghostGo.SetActive(false);

            var view = handlerGo.GetComponent<PreparationInventoryItemEntryView>();
            var handler = handlerGo.GetComponent<InventoryItemDragHandler>();
            var ghostRect = ghostGo.GetComponent<RectTransform>();
            handler.SetDragGhostRoot(ghostRect);

            var equipment = ScriptableObject.CreateInstance<EquipmentDefinition>();
            equipment.Id = "eq.helm";
            equipment.SlotType = EquipmentSlotType.Helmet;

            var entry = new InventoryEntry
            {
                Kind = InventoryEntry.EntryKind.Equipment,
                DefinitionId = equipment.Id,
                Quantity = 1
            };
            view.SetBoundData(entry, equipment);

            var beginEvent = new PointerEventData(EventSystem.current)
            {
                position = new Vector2(12f, 24f)
            };

            handler.OnBeginDrag(beginEvent);

            Assert.IsTrue(InventoryItemDragHandler.IsDraggingItem);
            Assert.AreSame(entry, InventoryItemDragHandler.DraggingEntry);
            Assert.AreSame(equipment, InventoryItemDragHandler.DraggingEquipmentDefinition);

            handler.OnEndDrag(beginEvent);
            Assert.IsFalse(InventoryItemDragHandler.IsDraggingItem);

            Object.DestroyImmediate(equipment);
            Object.DestroyImmediate(ghostGo);
            Object.DestroyImmediate(handlerGo);
            Object.DestroyImmediate(eventSystemGo);
        }

        [Test]
        public void OnDisable_WhileDragging_CancelsDragAndHidesGhost()
        {
            var eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            var handlerGo = new GameObject("InventoryItem", typeof(RectTransform), typeof(CanvasGroup), typeof(InventoryItemDragHandler));
            var ghostGo = new GameObject("DragGhost", typeof(RectTransform), typeof(Image));
            ghostGo.SetActive(false);

            var handler = handlerGo.GetComponent<InventoryItemDragHandler>();
            var ghostRect = ghostGo.GetComponent<RectTransform>();
            handler.SetDragGhostRoot(ghostRect);

            var equipment = ScriptableObject.CreateInstance<EquipmentDefinition>();
            equipment.Id = "eq.amulet";
            equipment.SlotType = EquipmentSlotType.Amulet;
            var entry = new InventoryEntry
            {
                Kind = InventoryEntry.EntryKind.Equipment,
                DefinitionId = equipment.Id,
                Quantity = 1
            };
            handler.ConfigureDragPayload(entry, equipment);

            var beginEvent = new PointerEventData(EventSystem.current)
            {
                position = new Vector2(200f, 120f)
            };

            handler.OnBeginDrag(beginEvent);
            Assert.IsTrue(InventoryItemDragHandler.IsDraggingItem);
            Assert.IsTrue(ghostGo.activeSelf);

            handlerGo.SetActive(false);

            Assert.IsFalse(InventoryItemDragHandler.IsDraggingItem, "OnDisable should cancel active drag.");
            Assert.IsNull(InventoryItemDragHandler.DraggingEntry);
            Assert.IsNull(InventoryItemDragHandler.DraggingEquipmentDefinition);
            Assert.IsFalse(ghostGo.activeSelf, "Ghost should be hidden after OnDisable cancel.");

            Object.DestroyImmediate(equipment);
            Object.DestroyImmediate(ghostGo);
            Object.DestroyImmediate(handlerGo);
            Object.DestroyImmediate(eventSystemGo);
        }

        [Test]
        public void BeginDrag_WhenAnotherItemDragIsActive_IgnoresSecondDrag()
        {
            var eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            var firstGo = new GameObject("ItemA", typeof(RectTransform), typeof(CanvasGroup), typeof(InventoryItemDragHandler));
            var secondGo = new GameObject("ItemB", typeof(RectTransform), typeof(CanvasGroup), typeof(InventoryItemDragHandler));
            var ghostGo = new GameObject("DragGhost", typeof(RectTransform), typeof(Image));
            ghostGo.SetActive(false);

            var firstHandler = firstGo.GetComponent<InventoryItemDragHandler>();
            var secondHandler = secondGo.GetComponent<InventoryItemDragHandler>();
            var secondCanvasGroup = secondGo.GetComponent<CanvasGroup>();
            var ghostRect = ghostGo.GetComponent<RectTransform>();

            var firstDef = ScriptableObject.CreateInstance<EquipmentDefinition>();
            firstDef.Id = "eq.weapon.first";
            firstDef.SlotType = EquipmentSlotType.Weapon;
            var secondDef = ScriptableObject.CreateInstance<EquipmentDefinition>();
            secondDef.Id = "eq.weapon.second";
            secondDef.SlotType = EquipmentSlotType.Weapon;

            var firstEntry = new InventoryEntry
            {
                Kind = InventoryEntry.EntryKind.Equipment,
                DefinitionId = firstDef.Id,
                Quantity = 1
            };
            var secondEntry = new InventoryEntry
            {
                Kind = InventoryEntry.EntryKind.Equipment,
                DefinitionId = secondDef.Id,
                Quantity = 1
            };

            firstHandler.SetDragGhostRoot(ghostRect);
            secondHandler.SetDragGhostRoot(ghostRect);
            firstHandler.ConfigureDragPayload(firstEntry, firstDef);
            secondHandler.ConfigureDragPayload(secondEntry, secondDef);
            secondCanvasGroup.blocksRaycasts = true;
            secondCanvasGroup.alpha = 1f;

            var beginEvent = new PointerEventData(EventSystem.current)
            {
                position = new Vector2(128f, 96f)
            };

            firstHandler.OnBeginDrag(beginEvent);
            Assert.IsTrue(InventoryItemDragHandler.IsDraggingItem);
            Assert.AreSame(firstEntry, InventoryItemDragHandler.DraggingEntry);

            secondHandler.OnBeginDrag(beginEvent);

            Assert.AreSame(firstEntry, InventoryItemDragHandler.DraggingEntry, "Second drag should be ignored while first drag is active.");
            Assert.AreEqual(1f, secondCanvasGroup.alpha, 0.0001f, "Ignored second drag should not fade second slot.");
            Assert.IsTrue(secondCanvasGroup.blocksRaycasts, "Ignored second drag should not disable raycasts.");

            firstHandler.OnEndDrag(beginEvent);

            Object.DestroyImmediate(secondDef);
            Object.DestroyImmediate(firstDef);
            Object.DestroyImmediate(ghostGo);
            Object.DestroyImmediate(secondGo);
            Object.DestroyImmediate(firstGo);
            Object.DestroyImmediate(eventSystemGo);
        }

        [Test]
        public void InvalidDrop_RestoresCurrentAnchoredPosition_NotStaleCachedPosition()
        {
            var eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            var handlerGo = new GameObject("InventoryItem", typeof(RectTransform), typeof(CanvasGroup), typeof(InventoryItemDragHandler));
            var ghostGo = new GameObject("DragGhost", typeof(RectTransform), typeof(Image));
            ghostGo.SetActive(false);

            var handler = handlerGo.GetComponent<InventoryItemDragHandler>();
            var rectTransform = handlerGo.GetComponent<RectTransform>();
            var ghostRect = ghostGo.GetComponent<RectTransform>();
            handler.SetDragGhostRoot(ghostRect);

            var equipment = ScriptableObject.CreateInstance<EquipmentDefinition>();
            equipment.Id = "eq.invalid-drop";
            equipment.SlotType = EquipmentSlotType.Weapon;
            var entry = new InventoryEntry
            {
                Kind = InventoryEntry.EntryKind.Equipment,
                DefinitionId = equipment.Id,
                Quantity = 1
            };
            handler.ConfigureDragPayload(entry, equipment);

            // Simulate layout moving the slot after initial component setup.
            rectTransform.anchoredPosition = new Vector2(160f, -32f);

            var beginEvent = new PointerEventData(EventSystem.current)
            {
                position = new Vector2(300f, 240f)
            };

            handler.OnBeginDrag(beginEvent);
            handler.OnEndDrag(beginEvent); // invalid drop path -> shake

            var stopShake = typeof(InventoryItemDragHandler).GetMethod(
                "StopInvalidShake",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(stopShake);
            stopShake.Invoke(handler, null);

            Assert.AreEqual(160f, rectTransform.anchoredPosition.x, 0.001f);
            Assert.AreEqual(-32f, rectTransform.anchoredPosition.y, 0.001f);

            Object.DestroyImmediate(equipment);
            Object.DestroyImmediate(ghostGo);
            Object.DestroyImmediate(handlerGo);
            Object.DestroyImmediate(eventSystemGo);
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

        private static void SetStaticAutoProperty(System.Type type, string propertyName, object value)
        {
            string fieldName = $"<{propertyName}>k__BackingField";
            var field = type.GetField(fieldName, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' was not found on type '{type.FullName}'.");
            field.SetValue(null, value);
        }
    }
}
