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

        private static T GetPrivate<T>(object target, string fieldName)
        {
            var type = target.GetType();
            var field = type.GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' was not found on type '{type.FullName}'.");
            return (T)field.GetValue(target);
        }

        private static T GetNestedPrivate<T>(object target, string fieldName)
        {
            Assert.IsNotNull(target, "Target object must not be null.");
            var type = target.GetType();
            var field = type.GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            Assert.IsNotNull(field, $"Field '{fieldName}' was not found on type '{type.FullName}'.");
            return (T)field.GetValue(target);
        }

        private static void CallPrivate(object target, string methodName, params object[] args)
        {
            var type = target.GetType();
            var method = type.GetMethod(methodName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Method '{methodName}' was not found on type '{type.FullName}'.");
            method.Invoke(target, args);
        }

        private static T CallPrivateAndReturn<T>(object target, string methodName, params object[] args)
        {
            var type = target.GetType();
            var method = type.GetMethod(methodName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Method '{methodName}' was not found on type '{type.FullName}'.");
            return (T)method.Invoke(target, args);
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

        [Test]
        public void OnEnable_WithZeroGoldGain_DoesNotStartGoldCascadeSfx()
        {
            var root = new GameObject("ResourcesPanel");
            var presenter = root.AddComponent<PreparationResourcesPanelPresenter>();
            var context = ScriptableObject.CreateInstance<PlayerContext>();
            context.SetResources(100, 10);

            var goldTmp = new GameObject("CoinValue").AddComponent<TextMeshProUGUI>();
            goldTmp.transform.SetParent(root.transform);
            var gemsTmp = new GameObject("GemValue").AddComponent<TextMeshProUGUI>();
            gemsTmp.transform.SetParent(root.transform);

            var clip = AudioClip.Create("coin", 64, 1, 44100, false);
            SetPrivate(presenter, "_playerContext", context);
            SetPrivate(presenter, "_goldValueTMP", goldTmp);
            SetPrivate(presenter, "_gemsValueTMP", gemsTmp);
            SetPrivate(presenter, "_goldCollectionSfxClip", clip);

            BattleVictoryRewardTransfer.SetPending(100, 3, 0, 7);
            CallPrivate(presenter, "OnEnable");

            object sfxState = GetPrivate<object>(presenter, "_goldCascadeSfx");
            Assert.IsNull(sfxState, "Gold cascade SFX should not start when gold gained is 0.");

            CallPrivate(presenter, "OnDisable");
            Object.DestroyImmediate(clip);
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(context);
        }

        [Test]
        public void BuildGoldCascadeClipPool_WithVariants_UsesOnlyVariantClips()
        {
            var root = new GameObject("ResourcesPanel");
            var presenter = root.AddComponent<PreparationResourcesPanelPresenter>();

            var fallback = AudioClip.Create("coin_fallback", 64, 1, 44100, false);
            var variantA = AudioClip.Create("coin_a", 64, 1, 44100, false);
            var variantB = AudioClip.Create("coin_b", 64, 1, 44100, false);

            SetPrivate(presenter, "_goldCollectionSfxClip", fallback);
            SetPrivate(presenter, "_goldCollectionSfxVariants", new[] { variantA, null, variantB });

            AudioClip[] pool = CallPrivateAndReturn<AudioClip[]>(presenter, "BuildGoldCascadeClipPool");

            Assert.AreEqual(2, pool.Length);
            CollectionAssert.Contains(pool, variantA);
            CollectionAssert.Contains(pool, variantB);
            CollectionAssert.DoesNotContain(pool, fallback);

            Object.DestroyImmediate(variantB);
            Object.DestroyImmediate(variantA);
            Object.DestroyImmediate(fallback);
            Object.DestroyImmediate(root);
        }

        [Test]
        public void BuildGoldCascadeClipPool_WithoutVariants_FallsBackToSingleClip()
        {
            var root = new GameObject("ResourcesPanel");
            var presenter = root.AddComponent<PreparationResourcesPanelPresenter>();

            var fallback = AudioClip.Create("coin_fallback", 64, 1, 44100, false);
            SetPrivate(presenter, "_goldCollectionSfxClip", fallback);
            SetPrivate(presenter, "_goldCollectionSfxVariants", System.Array.Empty<AudioClip>());

            AudioClip[] pool = CallPrivateAndReturn<AudioClip[]>(presenter, "BuildGoldCascadeClipPool");

            Assert.AreEqual(1, pool.Length);
            Assert.AreSame(fallback, pool[0]);

            Object.DestroyImmediate(fallback);
            Object.DestroyImmediate(root);
        }

        [Test]
        public void CreateGoldCascadeSfxState_LargeRewardStartsFaster_AndSlowsDown()
        {
            var root = new GameObject("ResourcesPanel");
            var presenter = root.AddComponent<PreparationResourcesPanelPresenter>();

            object smallState = CallPrivateAndReturn<object>(presenter, "CreateGoldCascadeSfxState", 8, 2.5f);
            object largeState = CallPrivateAndReturn<object>(presenter, "CreateGoldCascadeSfxState", 400, 2.5f);

            float smallStart = GetNestedPrivate<float>(smallState, "StartIntervalSeconds");
            float largeStart = GetNestedPrivate<float>(largeState, "StartIntervalSeconds");
            float largeEnd = GetNestedPrivate<float>(largeState, "EndIntervalSeconds");
            int smallStepsPerTick = GetNestedPrivate<int>(smallState, "IncrementsPerTick");
            int largeStepsPerTick = GetNestedPrivate<int>(largeState, "IncrementsPerTick");
            float maxTicksPerSecond = GetPrivate<float>(presenter, "_goldCollectionMaxTicksPerSecond");
            float minIntervalByCap = 1f / Mathf.Max(1f, maxTicksPerSecond);

            Assert.Less(largeStart, smallStart, "Large rewards should begin with a faster cadence.");
            Assert.Greater(largeEnd, largeStart, "Cascade should slow down near the end.");
            Assert.GreaterOrEqual(largeStepsPerTick, smallStepsPerTick, "Large rewards should group more increments per tick.");
            Assert.GreaterOrEqual(largeStart, minIntervalByCap, "Cadence must respect the max ticks-per-second cap.");

            Object.DestroyImmediate(root);
        }

        [Test]
        public void OnDisable_StopsGoldCascadeSfxImmediately()
        {
            var root = new GameObject("ResourcesPanel");
            var presenter = root.AddComponent<PreparationResourcesPanelPresenter>();
            var context = ScriptableObject.CreateInstance<PlayerContext>();
            context.SetResources(100, 10);

            var goldTmp = new GameObject("CoinValue").AddComponent<TextMeshProUGUI>();
            goldTmp.transform.SetParent(root.transform);
            var gemsTmp = new GameObject("GemValue").AddComponent<TextMeshProUGUI>();
            gemsTmp.transform.SetParent(root.transform);

            var clip = AudioClip.Create("coin", 64, 1, 44100, false);
            SetPrivate(presenter, "_playerContext", context);
            SetPrivate(presenter, "_goldValueTMP", goldTmp);
            SetPrivate(presenter, "_gemsValueTMP", gemsTmp);
            SetPrivate(presenter, "_goldCollectionSfxClip", clip);

            BattleVictoryRewardTransfer.SetPending(55, 3, 45, 2);
            CallPrivate(presenter, "OnEnable");

            object startedState = GetPrivate<object>(presenter, "_goldCascadeSfx");
            Assert.IsNotNull(startedState, "Gold cascade SFX should start when gold is gained.");

            CallPrivate(presenter, "OnDisable");

            object stoppedState = GetPrivate<object>(presenter, "_goldCascadeSfx");
            Assert.IsNull(stoppedState, "Gold cascade SFX should stop immediately when animation is interrupted.");

            Object.DestroyImmediate(clip);
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(context);
        }
    }
}
