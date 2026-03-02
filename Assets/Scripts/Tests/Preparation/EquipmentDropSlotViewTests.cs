using NUnit.Framework;
using SevenBattles.Core.Items;
using SevenBattles.Preparation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SevenBattles.Tests.Preparation
{
    public class EquipmentDropSlotViewTests
    {
        [SetUp]
        public void SetUp()
        {
            ResetInventoryDragStatics();
            ResetUnitDragStatics();
            ResetEquipmentSlotDragStatics();
        }

        [Test]
        public void OnDisable_WhileDragging_CancelsDragAndHidesGhost()
        {
            var eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            var slotGo = new GameObject(
                "WeaponSlot",
                typeof(RectTransform),
                typeof(Image),
                typeof(CanvasGroup),
                typeof(EquipmentDropSlotView));
            var ghostGo = new GameObject("DragGhost", typeof(RectTransform), typeof(Image));
            ghostGo.SetActive(false);

            var slot = slotGo.GetComponent<EquipmentDropSlotView>();
            slot.SetSlotType(EquipmentSlotType.Weapon);
            slot.SetDragGhostRoot(ghostGo.GetComponent<RectTransform>());

            var definition = ScriptableObject.CreateInstance<EquipmentDefinition>();
            definition.Id = "eq.weapon.drag";
            definition.SlotType = EquipmentSlotType.Weapon;
            definition.Icon = CreateSprite(Color.yellow);
            slot.SetEquippedItem(definition.Id, definition);

            var pointerData = new PointerEventData(EventSystem.current)
            {
                position = new Vector2(300f, 240f)
            };

            slot.OnBeginDrag(pointerData);
            Assert.IsTrue(EquipmentDropSlotView.IsDraggingEquippedItem);
            Assert.IsTrue(ghostGo.activeSelf);

            slotGo.SetActive(false);

            Assert.IsFalse(EquipmentDropSlotView.IsDraggingEquippedItem, "OnDisable should cancel active equipped drag.");
            Assert.IsNull(EquipmentDropSlotView.DraggingDefinitionId);
            Assert.IsFalse(ghostGo.activeSelf, "Ghost should be hidden after OnDisable cancel.");

            Object.DestroyImmediate(definition.Icon.texture);
            Object.DestroyImmediate(definition.Icon);
            Object.DestroyImmediate(definition);
            Object.DestroyImmediate(ghostGo);
            Object.DestroyImmediate(slotGo);
            Object.DestroyImmediate(eventSystemGo);
        }

        [Test]
        public void BeginDrag_WhenAnotherSlotDragIsActive_IgnoresSecondDrag()
        {
            var eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            var firstSlotGo = new GameObject(
                "FirstSlot",
                typeof(RectTransform),
                typeof(Image),
                typeof(CanvasGroup),
                typeof(EquipmentDropSlotView));
            var secondSlotGo = new GameObject(
                "SecondSlot",
                typeof(RectTransform),
                typeof(Image),
                typeof(CanvasGroup),
                typeof(EquipmentDropSlotView));
            var ghostGo = new GameObject("DragGhost", typeof(RectTransform), typeof(Image));
            ghostGo.SetActive(false);

            var firstSlot = firstSlotGo.GetComponent<EquipmentDropSlotView>();
            firstSlot.SetSlotType(EquipmentSlotType.Weapon);
            firstSlot.SetDragGhostRoot(ghostGo.GetComponent<RectTransform>());

            var secondSlot = secondSlotGo.GetComponent<EquipmentDropSlotView>();
            secondSlot.SetSlotType(EquipmentSlotType.Shield);
            secondSlot.SetDragGhostRoot(ghostGo.GetComponent<RectTransform>());
            var secondCanvasGroup = secondSlotGo.GetComponent<CanvasGroup>();
            secondCanvasGroup.blocksRaycasts = true;

            var firstDef = ScriptableObject.CreateInstance<EquipmentDefinition>();
            firstDef.Id = "eq.weapon.first";
            firstDef.SlotType = EquipmentSlotType.Weapon;
            firstDef.Icon = CreateSprite(Color.red);

            var secondDef = ScriptableObject.CreateInstance<EquipmentDefinition>();
            secondDef.Id = "eq.shield.second";
            secondDef.SlotType = EquipmentSlotType.Shield;
            secondDef.Icon = CreateSprite(Color.green);

            firstSlot.SetEquippedItem(firstDef.Id, firstDef);
            secondSlot.SetEquippedItem(secondDef.Id, secondDef);

            var pointerData = new PointerEventData(EventSystem.current)
            {
                position = new Vector2(400f, 260f)
            };

            firstSlot.OnBeginDrag(pointerData);
            Assert.IsTrue(EquipmentDropSlotView.IsDraggingEquippedItem);
            Assert.AreEqual(firstDef.Id, EquipmentDropSlotView.DraggingDefinitionId);
            Assert.AreEqual(EquipmentSlotType.Weapon, EquipmentDropSlotView.DraggingFromSlot);

            secondSlot.OnBeginDrag(pointerData);

            Assert.AreEqual(firstDef.Id, EquipmentDropSlotView.DraggingDefinitionId, "Second slot drag should be ignored while first slot drag is active.");
            Assert.AreEqual(EquipmentSlotType.Weapon, EquipmentDropSlotView.DraggingFromSlot);
            Assert.IsTrue(secondCanvasGroup.blocksRaycasts, "Ignored second slot drag should keep raycasts enabled.");

            firstSlot.OnEndDrag(pointerData);

            Object.DestroyImmediate(secondDef.Icon.texture);
            Object.DestroyImmediate(secondDef.Icon);
            Object.DestroyImmediate(secondDef);
            Object.DestroyImmediate(firstDef.Icon.texture);
            Object.DestroyImmediate(firstDef.Icon);
            Object.DestroyImmediate(firstDef);
            Object.DestroyImmediate(ghostGo);
            Object.DestroyImmediate(secondSlotGo);
            Object.DestroyImmediate(firstSlotGo);
            Object.DestroyImmediate(eventSystemGo);
        }

        [Test]
        public void SetEquippedItem_WhenCleared_RestoresAuthoredDefaultIconSprite()
        {
            var slotGo = new GameObject(
                "WeaponSlot",
                typeof(RectTransform),
                typeof(Image),
                typeof(CanvasGroup));
            var iconGo = new GameObject("EquippedIcon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(slotGo.transform, false);

            var iconImage = iconGo.GetComponent<Image>();
            Sprite defaultSprite = CreateSprite(Color.blue);
            iconImage.sprite = defaultSprite;
            iconImage.enabled = true;
            iconImage.color = new Color(1f, 1f, 1f, 0.9f);

            var slot = slotGo.AddComponent<EquipmentDropSlotView>();
            slot.SetSlotType(EquipmentSlotType.Weapon);

            Assert.AreSame(defaultSprite, iconImage.sprite, "Authored default icon should stay visible when no equipment is bound.");

            var equippedDefinition = ScriptableObject.CreateInstance<EquipmentDefinition>();
            equippedDefinition.Id = "eq.weapon.test";
            equippedDefinition.SlotType = EquipmentSlotType.Weapon;
            equippedDefinition.Icon = CreateSprite(Color.red);

            slot.SetEquippedItem(equippedDefinition.Id, equippedDefinition);
            Assert.AreSame(equippedDefinition.Icon, iconImage.sprite, "Equipped item icon should replace default icon.");
            Assert.IsTrue(iconImage.enabled);
            Assert.AreEqual(1f, iconImage.color.r, 0.0001f, "Equipped icon should use active tint and not placeholder tint.");
            Assert.AreEqual(1f, iconImage.color.g, 0.0001f, "Equipped icon should use active tint and not placeholder tint.");
            Assert.AreEqual(1f, iconImage.color.b, 0.0001f, "Equipped icon should use active tint and not placeholder tint.");
            Assert.AreEqual(1f, iconImage.color.a, 0.0001f, "Equipped icon alpha should be fully visible.");

            slot.SetEquippedItem(null, null);
            Assert.AreSame(defaultSprite, iconImage.sprite, "Default authored icon should be restored after unequip.");
            Assert.IsTrue(iconImage.enabled, "Default authored icon enabled state should be restored.");
            Assert.AreEqual(0.9f, iconImage.color.a, 0.0001f, "Default authored icon alpha should be restored.");

            Object.DestroyImmediate(equippedDefinition.Icon.texture);
            Object.DestroyImmediate(equippedDefinition.Icon);
            Object.DestroyImmediate(equippedDefinition);
            Object.DestroyImmediate(defaultSprite.texture);
            Object.DestroyImmediate(defaultSprite);
            Object.DestroyImmediate(slotGo);
        }

        [Test]
        public void SetEquippedItem_WhenPresenterInjectsIconImage_PreservesInjectedDefaultSprite()
        {
            var slotGo = new GameObject(
                "WeaponSlot",
                typeof(RectTransform),
                typeof(Image),
                typeof(CanvasGroup),
                typeof(EquipmentDropSlotView));
            var slot = slotGo.GetComponent<EquipmentDropSlotView>();
            slot.SetSlotType(EquipmentSlotType.Weapon);

            var injectedIconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            injectedIconGo.transform.SetParent(slotGo.transform, false);
            var injectedIconImage = injectedIconGo.GetComponent<Image>();
            Sprite defaultSprite = CreateSprite(Color.green);
            injectedIconImage.sprite = defaultSprite;
            injectedIconImage.enabled = true;

            SetPrivateField(slot, "_iconImage", injectedIconImage);
            slot.SetEquippedItem(null, null);

            Assert.AreSame(defaultSprite, injectedIconImage.sprite, "Injected icon default sprite should not be cleared.");
            Assert.IsTrue(injectedIconImage.enabled, "Injected icon enabled state should be preserved.");

            Object.DestroyImmediate(defaultSprite.texture);
            Object.DestroyImmediate(defaultSprite);
            Object.DestroyImmediate(slotGo);
        }

        [Test]
        public void SetEquippedItem_TintsBackgroundByRarity_AndRestoresDefaultOnClear()
        {
            var slotGo = new GameObject(
                "WeaponSlot",
                typeof(RectTransform),
                typeof(Image),
                typeof(CanvasGroup),
                typeof(EquipmentDropSlotView));
            var slot = slotGo.GetComponent<EquipmentDropSlotView>();
            slot.SetSlotType(EquipmentSlotType.Weapon);

            var bgGo = new GameObject("Bg", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(slotGo.transform, false);
            var bgImage = bgGo.GetComponent<Image>();
            Color defaultBg = new Color(0.17f, 0.21f, 0.28f, 1f);
            bgImage.color = defaultBg;
            bgImage.enabled = true;

            slot.SetBackgroundImage(bgImage);
            slot.SetEquippedItem(null, null);

            var equippedDefinition = ScriptableObject.CreateInstance<EquipmentDefinition>();
            equippedDefinition.Id = "eq.weapon.rare";
            equippedDefinition.SlotType = EquipmentSlotType.Weapon;
            equippedDefinition.Rarity = ItemRarity.Rare;

            slot.SetEquippedItem(equippedDefinition.Id, equippedDefinition);
            Assert.AreEqual(ItemRarityColorUtility.GetInventoryBackgroundColor(ItemRarity.Rare), bgImage.color);
            Assert.IsTrue(bgImage.enabled);

            slot.SetEquippedItem(null, null);
            Assert.AreEqual(defaultBg, bgImage.color);
            Assert.IsTrue(bgImage.enabled);

            Object.DestroyImmediate(equippedDefinition);
            Object.DestroyImmediate(slotGo);
        }

        [Test]
        public void SetEquippedItem_WithPaletteOverride_UsesPaletteColor()
        {
            var slotGo = new GameObject(
                "WeaponSlot",
                typeof(RectTransform),
                typeof(Image),
                typeof(CanvasGroup),
                typeof(EquipmentDropSlotView));
            var slot = slotGo.GetComponent<EquipmentDropSlotView>();
            slot.SetSlotType(EquipmentSlotType.Weapon);

            var bgGo = new GameObject("Bg", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(slotGo.transform, false);
            var bgImage = bgGo.GetComponent<Image>();
            bgImage.color = new Color(0.17f, 0.21f, 0.28f, 1f);
            bgImage.enabled = true;
            slot.SetBackgroundImage(bgImage);

            var palette = ScriptableObject.CreateInstance<ItemRarityColorPalette>();
            Color rareOverride = new Color(0.12f, 0.34f, 0.95f, 1f);
            SetPrivateField(palette, "_rareColor", rareOverride);
            slot.SetRarityColorPalette(palette);

            var equippedDefinition = ScriptableObject.CreateInstance<EquipmentDefinition>();
            equippedDefinition.Id = "eq.weapon.rare";
            equippedDefinition.SlotType = EquipmentSlotType.Weapon;
            equippedDefinition.Rarity = ItemRarity.Rare;

            slot.SetEquippedItem(equippedDefinition.Id, equippedDefinition);
            Assert.AreEqual(rareOverride, bgImage.color);

            Object.DestroyImmediate(equippedDefinition);
            Object.DestroyImmediate(palette);
            Object.DestroyImmediate(slotGo);
        }

        [Test]
        public void EnsureIconImage_PrefersAuthoredIconChild_AndDoesNotCreateFallbackIconObject()
        {
            var slotGo = new GameObject(
                "WeaponSlot",
                typeof(RectTransform),
                typeof(Image),
                typeof(CanvasGroup),
                typeof(EquipmentDropSlotView));
            var slot = slotGo.GetComponent<EquipmentDropSlotView>();
            slot.SetSlotType(EquipmentSlotType.Weapon);

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(slotGo.transform, false);
            var iconImage = iconGo.GetComponent<Image>();
            Sprite defaultSprite = CreateSprite(Color.magenta);
            iconImage.sprite = defaultSprite;
            iconImage.enabled = true;

            slot.SetEquippedItem(null, null);

            Transform fallback = slotGo.transform.Find("EquippedIcon");
            Assert.IsNull(fallback, "Authored Icon child should be reused instead of creating a runtime EquippedIcon.");
            Assert.AreSame(defaultSprite, iconImage.sprite);
            Assert.IsTrue(iconImage.enabled);

            Object.DestroyImmediate(defaultSprite.texture);
            Object.DestroyImmediate(defaultSprite);
            Object.DestroyImmediate(slotGo);
        }

        private static Sprite CreateSprite(Color color)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            var pixels = new Color[4];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f), 100f);
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

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' was not found on type '{target.GetType().FullName}'.");
            field.SetValue(target, value);
        }
    }
}
