using NUnit.Framework;
using SevenBattles.Core.Battle;
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
        [TearDown]
        public void TearDown()
        {
            PlayerContext.SetRuntimeInstance(null);
        }

        [Test]
        public void RefreshNow_DisplaysOnlyEquipmentAndItems_WithBoundVisuals()
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
            var template = CreateEntryTemplate("ItemTemplate");
            presenter.Configure(context, null, content, template.gameObject, null, null);
            presenter.RefreshNow();

            Assert.AreEqual(2, CountActiveEntryChildren(content));

            var firstView = content.GetChild(0).GetComponent<PreparationInventoryItemEntryView>();
            var firstBg = firstView.GetComponent<Image>();
            var firstIcon = firstView.transform.Find("ItemIcon")?.GetComponent<Image>();
            var firstText = firstView.GetComponentInChildren<TMP_Text>(true);
            Assert.IsNotNull(firstView);
            Assert.AreEqual("1", firstText.text);
            Assert.AreEqual(equipmentDef.InventoryBackgroundColor, firstBg.color);
            Assert.AreEqual(equipmentDef.Icon, firstIcon.sprite);

            var secondView = content.GetChild(1).GetComponent<PreparationInventoryItemEntryView>();
            var secondBg = secondView.GetComponent<Image>();
            var secondIcon = secondView.transform.Find("ItemIcon")?.GetComponent<Image>();
            var secondText = secondView.GetComponentInChildren<TMP_Text>(true);
            Assert.IsNotNull(secondView);
            Assert.AreEqual("4", secondText.text);
            Assert.AreEqual(itemDef.InventoryBackgroundColor, secondBg.color);
            Assert.AreEqual(itemDef.Icon, secondIcon.sprite);

            Object.DestroyImmediate(template.gameObject);
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
            var template = CreateEntryTemplate("ItemTemplate");
            presenter.Configure(context, null, content, template.gameObject, null, null);
            presenter.RefreshNow();

            int firstChildCount = content.childCount;
            var firstChild = content.GetChild(0);

            presenter.RefreshNow();
            presenter.RefreshNow();
            Assert.AreEqual(firstChildCount, content.childCount);
            Assert.AreSame(firstChild, content.GetChild(0));

            inventory.AddItem(secondItem, 1);

            Assert.AreEqual(2, CountActiveEntryChildren(content));
            Assert.AreEqual(2, content.childCount);

            presenter.RefreshNow();
            Assert.AreEqual(2, content.childCount);
            Assert.AreEqual(2, CountActiveEntryChildren(content));

            Object.DestroyImmediate(template.gameObject);
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(secondItem);
            Object.DestroyImmediate(firstItem);
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
            var template = CreateEntryTemplate("ItemTemplate");
            presenter.Configure(context, null, content, template.gameObject, null, null);
            presenter.RefreshNow();

            Assert.AreEqual(1, CountActiveEntryChildren(content));
            var view = content.GetChild(0).GetComponent<PreparationInventoryItemEntryView>();
            var bg = view.GetComponent<Image>();
            var txt = view.GetComponentInChildren<TMP_Text>(true);
            Assert.AreEqual(Color.white, bg.color);
            Assert.AreEqual("1", txt.text);

            Object.DestroyImmediate(template.gameObject);
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

        private static int CountActiveEntryChildren(Transform content)
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
