using NUnit.Framework;
using TMPro;
using UnityEngine;
using SevenBattles.Core.Battle;
using SevenBattles.Core.Players;
using SevenBattles.Preparation;

namespace SevenBattles.Tests.Preparation
{
    public class PreparationResourcesPanelPresenterTests
    {
        [SetUp]
        public void SetUp()
        {
            BattleVictoryRewardTransfer.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            BattleVictoryRewardTransfer.Clear();
        }

        private static void SetPrivate(object target, string fieldName, object value)
        {
            var type = target.GetType();
            var field = type.GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' was not found on type '{type.FullName}'.");
            field.SetValue(target, value);
        }

        private static void CallPrivate(object target, string methodName)
        {
            var type = target.GetType();
            var method = type.GetMethod(methodName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Method '{methodName}' was not found on type '{type.FullName}'.");
            method.Invoke(target, null);
        }

        [Test]
        public void Refresh_UpdatesGoldAndGemsText()
        {
            var root = new GameObject("ResourcesPanel");
            var presenter = root.AddComponent<PreparationResourcesPanelPresenter>();

            var context = ScriptableObject.CreateInstance<PlayerContext>();
            context.SetResources(2500, 44);

            var goldTmp = new GameObject("CoinValue").AddComponent<TextMeshProUGUI>();
            goldTmp.transform.SetParent(root.transform);
            var gemsTmp = new GameObject("GemValue").AddComponent<TextMeshProUGUI>();
            gemsTmp.transform.SetParent(root.transform);

            SetPrivate(presenter, "_playerContext", context);
            SetPrivate(presenter, "_goldValueTMP", goldTmp);
            SetPrivate(presenter, "_gemsValueTMP", gemsTmp);

            presenter.Refresh();

            Assert.AreEqual("2500", goldTmp.text);
            Assert.AreEqual("44", gemsTmp.text);

            Object.DestroyImmediate(root);
            Object.DestroyImmediate(context);
        }

        [Test]
        public void OnEnable_SubscribesToContextChanges_AndRefreshes()
        {
            var root = new GameObject("ResourcesPanel");
            var presenter = root.AddComponent<PreparationResourcesPanelPresenter>();

            var context = ScriptableObject.CreateInstance<PlayerContext>();
            context.SetResources(10, 1);

            var goldTmp = new GameObject("CoinValue").AddComponent<TextMeshProUGUI>();
            goldTmp.transform.SetParent(root.transform);
            var gemsTmp = new GameObject("GemValue").AddComponent<TextMeshProUGUI>();
            gemsTmp.transform.SetParent(root.transform);

            SetPrivate(presenter, "_playerContext", context);
            SetPrivate(presenter, "_goldValueTMP", goldTmp);
            SetPrivate(presenter, "_gemsValueTMP", gemsTmp);

            CallPrivate(presenter, "OnEnable");

            context.SetResources(77, 5);

            Assert.AreEqual("77", goldTmp.text);
            Assert.AreEqual("5", gemsTmp.text);

            CallPrivate(presenter, "OnDisable");

            Object.DestroyImmediate(root);
            Object.DestroyImmediate(context);
        }

        [Test]
        public void OnEnable_WithPendingVictoryRewards_StartsFromPreBattleValues_ThenOnDisableFinalizes()
        {
            var root = new GameObject("ResourcesPanel");
            var presenter = root.AddComponent<PreparationResourcesPanelPresenter>();

            var context = ScriptableObject.CreateInstance<PlayerContext>();
            context.SetResources(100, 10);

            var goldTmp = new GameObject("CoinValue").AddComponent<TextMeshProUGUI>();
            goldTmp.transform.SetParent(root.transform);
            var gemsTmp = new GameObject("GemValue").AddComponent<TextMeshProUGUI>();
            gemsTmp.transform.SetParent(root.transform);

            SetPrivate(presenter, "_playerContext", context);
            SetPrivate(presenter, "_goldValueTMP", goldTmp);
            SetPrivate(presenter, "_gemsValueTMP", gemsTmp);

            BattleVictoryRewardTransfer.SetPending(55, 2, 45, 8);
            CallPrivate(presenter, "OnEnable");

            Assert.AreEqual("55", goldTmp.text);
            Assert.AreEqual("2", gemsTmp.text);

            CallPrivate(presenter, "OnDisable");

            Assert.AreEqual("100", goldTmp.text);
            Assert.AreEqual("10", gemsTmp.text);

            Object.DestroyImmediate(root);
            Object.DestroyImmediate(context);
        }
    }
}
