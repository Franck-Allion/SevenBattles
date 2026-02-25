using NUnit.Framework;
using SevenBattles.Core.Items;
using SevenBattles.Core.Players;
using SevenBattles.Preparation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SevenBattles.Tests.Preparation
{
    public class PreparationInventoryListPresenterTests
    {
        private const int PAGE_SIZE = 30;

        [TearDown]
        public void TearDown()
        {
            PlayerContext.SetRuntimeInstance(null);
        }

        [Test]
        public void RefreshNow_DisplaysFixedThirtySlots_WithBoundVisualsAndEmptyPadding()
        {
            var context = ScriptableObject.CreateInstance<PlayerContext>();
            var inventory = ScriptableObject.CreateInstance<PlayerInventory>();
            context.Inventory = inventory;
            PlayerContext.SetRuntimeInstance(context);

            var equipmentDef = ScriptableObject.CreateInstance<EquipmentDefinition>();
            equipmentDef.Id = "eq.sword";
            equipmentDef.Icon = CreateSprite(Color.red);
            equipmentDef.InventoryBackgroundColor = new Color(0.2f, 0.4f, 0.6f, 1f);

            var itemDef = ScriptableObject.CreateInstance<ItemDefinition>();
            itemDef.Id = "item.potion";
            itemDef.Icon = CreateSprite(Color.green);
            itemDef.InventoryBackgroundColor = new Color(0.7f, 0.5f, 0.2f, 1f);

            inventory.Entries.Add(new InventoryEntry
            {
                Kind = InventoryEntry.EntryKind.Spell,
                DefinitionId = "spell.firebolt",
                Quantity = 1
            });
            inventory.Entries.Add(new InventoryEntry
            {
                Kind = InventoryEntry.EntryKind.Item,
                DefinitionId = itemDef.Id,
                Quantity = 4
            });
            inventory.Entries.Add(new InventoryEntry
            {
                Kind = InventoryEntry.EntryKind.Equipment,
                DefinitionId = equipmentDef.Id,
                Quantity = 1
            });

            var root = new GameObject("PresenterRoot");
            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(root.transform, false);

            var presenter = root.AddComponent<PreparationInventoryListPresenter>();
            var itemTemplate = CreateEntryTemplate("ItemTemplate");
            var emptyTemplate = CreateEmptyTemplate("ItemEmptyTemplate");
            presenter.Configure(context, null, content, itemTemplate.gameObject, emptyTemplate, null, null);
            presenter.RefreshNow();

            Assert.AreEqual(PAGE_SIZE, CountActiveChildren(content));
            Assert.AreEqual(2, CountActiveItemViews(content));
            Assert.AreEqual(PAGE_SIZE - 2, CountActiveEmptySlots(content));

            GameObject firstSlot = GetActiveSlotAt(content, 0);
            var firstView = firstSlot.GetComponent<PreparationInventoryItemEntryView>();
            var firstBg = firstView.GetComponent<Image>();
            var firstIcon = firstView.transform.Find("ItemIcon")?.GetComponent<Image>();
            var firstText = firstView.GetComponentInChildren<TMP_Text>(true);
            Assert.IsNotNull(firstView);
            Assert.AreEqual("1", firstText.text);
            Assert.AreEqual(equipmentDef.InventoryBackgroundColor, firstBg.color);
            Assert.AreEqual(equipmentDef.Icon, firstIcon.sprite);

            GameObject secondSlot = GetActiveSlotAt(content, 1);
            var secondView = secondSlot.GetComponent<PreparationInventoryItemEntryView>();
            var secondBg = secondView.GetComponent<Image>();
            var secondIcon = secondView.transform.Find("ItemIcon")?.GetComponent<Image>();
            var secondText = secondView.GetComponentInChildren<TMP_Text>(true);
            Assert.IsNotNull(secondView);
            Assert.AreEqual("4", secondText.text);
            Assert.AreEqual(itemDef.InventoryBackgroundColor, secondBg.color);
            Assert.AreEqual(itemDef.Icon, secondIcon.sprite);

            GameObject thirdSlot = GetActiveSlotAt(content, 2);
            Assert.IsNotNull(thirdSlot);
            Assert.AreEqual("ItemEmpty", thirdSlot.name);

            Object.DestroyImmediate(itemTemplate.gameObject);
            Object.DestroyImmediate(emptyTemplate);
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(itemDef.Icon.texture);
            Object.DestroyImmediate(equipmentDef.Icon.texture);
            Object.DestroyImmediate(itemDef);
            Object.DestroyImmediate(equipmentDef);
            Object.DestroyImmediate(inventory);
            Object.DestroyImmediate(context);
        }

        [Test]
        public void InventoryChanged_ReusesPool_AndAvoidsDuplicateChildren()
        {
            var context = ScriptableObject.CreateInstance<PlayerContext>();
            var inventory = ScriptableObject.CreateInstance<PlayerInventory>();
            context.Inventory = inventory;
            PlayerContext.SetRuntimeInstance(context);

            var firstItem = ScriptableObject.CreateInstance<ItemDefinition>();
            firstItem.Id = "item.potion";
            var secondItem = ScriptableObject.CreateInstance<ItemDefinition>();
            secondItem.Id = "item.elixir";

            inventory.AddItem(firstItem, 2);

            var root = new GameObject("PresenterRoot");
            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(root.transform, false);

            var presenter = root.AddComponent<PreparationInventoryListPresenter>();
            var itemTemplate = CreateEntryTemplate("ItemTemplate");
            var emptyTemplate = CreateEmptyTemplate("ItemEmptyTemplate");
            presenter.Configure(context, null, content, itemTemplate.gameObject, emptyTemplate, null, null);
            presenter.RefreshNow();

            int firstChildCount = content.childCount;
            Assert.AreEqual(PAGE_SIZE * 2, firstChildCount);
            var firstSlot = GetActiveSlotAt(content, 0);

            presenter.RefreshNow();
            presenter.RefreshNow();
            Assert.AreEqual(firstChildCount, content.childCount);
            Assert.AreSame(firstSlot, GetActiveSlotAt(content, 0));

            inventory.AddItem(secondItem, 1);

            Assert.AreEqual(PAGE_SIZE, CountActiveChildren(content));
            Assert.AreEqual(2, CountActiveItemViews(content));
            Assert.AreEqual(PAGE_SIZE * 2, content.childCount);

            presenter.RefreshNow();
            Assert.AreEqual(PAGE_SIZE * 2, content.childCount);
            Assert.AreEqual(PAGE_SIZE, CountActiveChildren(content));

            Object.DestroyImmediate(itemTemplate.gameObject);
            Object.DestroyImmediate(emptyTemplate);
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(secondItem);
            Object.DestroyImmediate(firstItem);
            Object.DestroyImmediate(inventory);
            Object.DestroyImmediate(context);
        }

        [Test]
        public void PageButtons_AreBuiltFromPrefab_ForEveryRequiredPage_AndCanNavigateBeyondThreePages()
        {
            var context = ScriptableObject.CreateInstance<PlayerContext>();
            var inventory = ScriptableObject.CreateInstance<PlayerInventory>();
            context.Inventory = inventory;
            PlayerContext.SetRuntimeInstance(context);

            for (int i = 1; i <= 95; i++)
            {
                inventory.Entries.Add(new InventoryEntry
                {
                    Kind = InventoryEntry.EntryKind.Item,
                    DefinitionId = $"item.{i:000}",
                    Quantity = i
                });
            }

            var inventoryPanel = new GameObject("InventoryPanel", typeof(RectTransform));
            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(inventoryPanel.transform, false);
            var pageRoot = new GameObject("Pages", typeof(RectTransform)).GetComponent<RectTransform>();
            pageRoot.SetParent(inventoryPanel.transform, false);

            var pageTemplate = CreatePageButtonTemplate("PageTemplate", "1");
            pageTemplate.SetActive(false);

            var presenterRoot = new GameObject("PresenterRoot");
            var presenter = presenterRoot.AddComponent<PreparationInventoryListPresenter>();
            var itemTemplate = CreateEntryTemplate("ItemTemplate");
            var emptyTemplate = CreateEmptyTemplate("ItemEmptyTemplate");
            presenter.Configure(
                context,
                inventoryPanel,
                content,
                itemTemplate.gameObject,
                emptyTemplate,
                pageRoot,
                pageTemplate,
                null,
                null);
            presenter.RefreshNow();

            Assert.AreEqual(4, CountActivePageButtons(pageRoot));
            Assert.AreEqual(PAGE_SIZE, CountActiveItemViews(content));
            Assert.AreEqual(0, CountActiveEmptySlots(content));
            Assert.AreEqual("1", GetQuantityAtSlot(content, 0));

            Button page4 = FindActivePageButtonByLabel(pageRoot, "4");
            Assert.IsNotNull(page4);

            page4.onClick.Invoke();
            Assert.AreEqual(5, CountActiveItemViews(content));
            Assert.AreEqual(PAGE_SIZE - 5, CountActiveEmptySlots(content));
            Assert.AreEqual("91", GetQuantityAtSlot(content, 0));

            Object.DestroyImmediate(itemTemplate.gameObject);
            Object.DestroyImmediate(emptyTemplate);
            Object.DestroyImmediate(pageTemplate);
            Object.DestroyImmediate(presenterRoot);
            Object.DestroyImmediate(inventoryPanel);
            Object.DestroyImmediate(inventory);
            Object.DestroyImmediate(context);
        }

        [Test]
        public void RefreshNow_MissingDefinition_UsesFallbackValues()
        {
            var context = ScriptableObject.CreateInstance<PlayerContext>();
            var inventory = ScriptableObject.CreateInstance<PlayerInventory>();
            context.Inventory = inventory;
            PlayerContext.SetRuntimeInstance(context);

            inventory.Entries.Add(new InventoryEntry
            {
                Kind = InventoryEntry.EntryKind.Equipment,
                DefinitionId = "eq.missing",
                Quantity = 1
            });

            var root = new GameObject("PresenterRoot");
            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(root.transform, false);

            var presenter = root.AddComponent<PreparationInventoryListPresenter>();
            var itemTemplate = CreateEntryTemplate("ItemTemplate");
            var emptyTemplate = CreateEmptyTemplate("ItemEmptyTemplate");
            presenter.Configure(context, null, content, itemTemplate.gameObject, emptyTemplate, null, null);
            presenter.RefreshNow();

            Assert.AreEqual(PAGE_SIZE, CountActiveChildren(content));
            Assert.AreEqual(1, CountActiveItemViews(content));
            Assert.AreEqual(PAGE_SIZE - 1, CountActiveEmptySlots(content));

            var view = GetActiveSlotAt(content, 0).GetComponent<PreparationInventoryItemEntryView>();
            var bg = view.GetComponent<Image>();
            var txt = view.GetComponentInChildren<TMP_Text>(true);
            Assert.AreEqual(Color.white, bg.color);
            Assert.AreEqual("1", txt.text);

            Object.DestroyImmediate(itemTemplate.gameObject);
            Object.DestroyImmediate(emptyTemplate);
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(inventory);
            Object.DestroyImmediate(context);
        }

        private static PreparationInventoryItemEntryView CreateEntryTemplate(string name)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(PreparationInventoryItemEntryView));
            var icon = new GameObject("ItemIcon", typeof(RectTransform), typeof(Image));
            icon.transform.SetParent(root.transform, false);
            var quantity = new GameObject("Text (TMP)", typeof(RectTransform), typeof(TextMeshProUGUI));
            quantity.transform.SetParent(root.transform, false);
            return root.GetComponent<PreparationInventoryItemEntryView>();
        }

        private static GameObject CreateEmptyTemplate(string name)
        {
            return new GameObject(name, typeof(RectTransform), typeof(Image));
        }

        private static GameObject CreatePageButtonTemplate(string name, string labelValue)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));

            var labelObject = new GameObject("Text (TMP)", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(buttonObject.transform, false);
            var label = labelObject.GetComponent<TextMeshProUGUI>();
            label.text = labelValue;

            return buttonObject;
        }

        private static int CountActivePageButtons(Transform pageRoot)
        {
            int count = 0;
            for (int i = 0; i < pageRoot.childCount; i++)
            {
                var child = pageRoot.GetChild(i);
                if (!child.gameObject.activeSelf)
                {
                    continue;
                }

                if (child.GetComponent<Button>() != null || child.GetComponentInChildren<Button>(true) != null)
                {
                    count++;
                }
            }

            return count;
        }

        private static Button FindActivePageButtonByLabel(Transform pageRoot, string labelValue)
        {
            for (int i = 0; i < pageRoot.childCount; i++)
            {
                var child = pageRoot.GetChild(i);
                if (!child.gameObject.activeSelf)
                {
                    continue;
                }

                Button button = child.GetComponent<Button>();
                if (button == null)
                {
                    button = child.GetComponentInChildren<Button>(true);
                }

                if (button == null)
                {
                    continue;
                }

                TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
                if (text != null && string.Equals(text.text, labelValue, System.StringComparison.Ordinal))
                {
                    return button;
                }
            }

            return null;
        }

        private static int CountActiveChildren(Transform content)
        {
            int count = 0;
            for (int i = 0; i < content.childCount; i++)
            {
                if (content.GetChild(i).gameObject.activeSelf)
                {
                    count++;
                }
            }

            return count;
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

        private static string GetQuantityAtSlot(Transform content, int slotIndex)
        {
            GameObject slot = GetActiveSlotAt(content, slotIndex);
            if (slot == null)
            {
                return string.Empty;
            }

            var text = slot.GetComponentInChildren<TMP_Text>(true);
            return text != null ? text.text : string.Empty;
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
    }
}
