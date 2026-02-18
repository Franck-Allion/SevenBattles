using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SevenBattles.Core.Battle;
using SevenBattles.Core.Items;
using SevenBattles.UI;

namespace SevenBattles.Tests.UI
{
    public class BattleRewardPresenterTests
    {
        [Test]
        public void RewardItemView_SetGold_UpdatesLabelAndAmount()
        {
            var root = BuildRewardItemObject("RewardItem");
            var view = root.GetComponent<RewardItemView>();

            view.SetGold(125);

            var label = GetPrivate<TMP_Text>(view, "_label");
            var amount = GetPrivate<TMP_Text>(view, "_amountText");
            Assert.AreEqual("Gold", label.text);
            Assert.IsTrue(amount.gameObject.activeSelf);
            Assert.AreEqual("125", amount.text);

            Object.DestroyImmediate(root);
        }

        [Test]
        public void RewardItemView_SetReward_GoldUsesConfiguredGlowAndIconSprites()
        {
            var root = BuildRewardItemObject("RewardItem");
            var view = root.GetComponent<RewardItemView>();
            var goldGlow = CreateSolidSprite(new Color32(255, 200, 0, 255));
            var goldIcon = CreateSolidSprite(new Color32(255, 240, 120, 255));
            var goldGlowColor = new Color(1f, 0.82f, 0.2f, 1f);

            SetPrivate(view, "_goldGlowSprite", goldGlow);
            SetPrivate(view, "_goldIconSprite", goldIcon);
            SetPrivate(view, "_goldGlowColor", goldGlowColor);

            view.SetReward(new BattleRewardResultEntry(BattleRewardType.Gold, 33));

            var glow = GetPrivate<Image>(view, "_glow");
            var icon = GetPrivate<Image>(view, "_icon");
            Assert.AreSame(goldGlow, glow.sprite);
            Assert.AreSame(goldIcon, icon.sprite);
            Assert.AreEqual(goldGlowColor, glow.color);

            Object.DestroyImmediate(goldGlow.texture);
            Object.DestroyImmediate(goldGlow);
            Object.DestroyImmediate(goldIcon.texture);
            Object.DestroyImmediate(goldIcon);
            Object.DestroyImmediate(root);
        }

        [Test]
        public void RewardItemView_SetReward_ItemWithoutStack_HidesAmount()
        {
            var root = BuildRewardItemObject("RewardItem");
            var view = root.GetComponent<RewardItemView>();
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            item.Name = "Potion";
            var itemGlowColor = new Color(0.2f, 0.8f, 1f, 1f);

            SetPrivate(view, "_itemGlowColor", itemGlowColor);

            view.SetReward(new BattleRewardResultEntry(item, 1));

            var label = GetPrivate<TMP_Text>(view, "_label");
            var amount = GetPrivate<TMP_Text>(view, "_amountText");
            var glow = GetPrivate<Image>(view, "_glow");
            Assert.AreEqual("Potion", label.text);
            Assert.IsFalse(amount.gameObject.activeSelf);
            Assert.AreEqual(itemGlowColor, glow.color);

            Object.DestroyImmediate(root);
            Object.DestroyImmediate(item);
        }

        [Test]
        public void Show_CreatesGoldAndBonusEntries_AndClearRemovesAll()
        {
            var presenterRoot = new GameObject("Presenter");
            var container = new GameObject("Container").transform;
            container.SetParent(presenterRoot.transform);
            var presenter = presenterRoot.AddComponent<BattleRewardPresenter>();

            var rewardPrefab = BuildRewardItemObject("RewardItemPrefab");
            SetPrivate(presenter, "_rewardItemPrefab", rewardPrefab);
            SetPrivate(presenter, "_container", container);

            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            item.Name = "Potion";
            var result = new BattleRewardResult(77, new[] { new BattleRewardResultEntry(item, 1) });

            presenter.Show(result);

            Assert.AreEqual(2, container.childCount);

            var goldView = container.GetChild(0).GetComponent<RewardItemView>();
            var goldAmount = GetPrivate<TMP_Text>(goldView, "_amountText");
            Assert.AreEqual("77", goldAmount.text);

            var bonusView = container.GetChild(1).GetComponent<RewardItemView>();
            var bonusLabel = GetPrivate<TMP_Text>(bonusView, "_label");
            Assert.AreEqual("Potion", bonusLabel.text);

            presenter.Clear();
            Assert.AreEqual(0, container.childCount);

            Object.DestroyImmediate(item);
            Object.DestroyImmediate(rewardPrefab);
            Object.DestroyImmediate(presenterRoot);
        }

        private static GameObject BuildRewardItemObject(string name)
        {
            var root = new GameObject(name);
            var view = root.AddComponent<RewardItemView>();

            var glow = new GameObject("Glow").AddComponent<Image>();
            glow.transform.SetParent(root.transform);

            var icon = new GameObject("ItemIcon").AddComponent<Image>();
            icon.transform.SetParent(root.transform);

            var label = new GameObject("Text").AddComponent<TextMeshProUGUI>();
            label.transform.SetParent(root.transform);

            var amount = new GameObject("Amount").AddComponent<TextMeshProUGUI>();
            amount.transform.SetParent(root.transform);

            SetPrivate(view, "_glow", glow);
            SetPrivate(view, "_icon", icon);
            SetPrivate(view, "_label", label);
            SetPrivate(view, "_amountText", amount);

            return root;
        }

        private static void SetPrivate(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' was not found.");
            field.SetValue(target, value);
        }

        private static T GetPrivate<T>(object target, string fieldName) where T : class
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' was not found.");
            return field.GetValue(target) as T;
        }

        private static Sprite CreateSolidSprite(Color32 color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }
    }
}
