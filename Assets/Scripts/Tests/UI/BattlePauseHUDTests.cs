using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;
using SevenBattles.Core;
using SevenBattles.Core.Battle;
using SevenBattles.UI;

namespace SevenBattles.Tests.UI
{
    public class BattlePauseHUDTests
    {
        private class FakeTurnController : MonoBehaviour, IBattleTurnController
        {
            public bool HasActiveUnit { get; set; }
            public bool IsActiveUnitPlayerControlled { get; set; }
            public Sprite ActiveUnitPortrait { get; set; }
            public SpellDefinition[] ActiveUnitSpells => Array.Empty<SpellDefinition>();

            public event Action ActiveUnitChanged;
            public event Action ActiveUnitActionPointsChanged;
            public event Action ActiveUnitStatsChanged;

            public int ActiveUnitCurrentActionPoints { get; set; }
            public int ActiveUnitMaxActionPoints { get; set; }

            public bool IsInteractionLocked { get; private set; }
            public int TurnIndex { get; set; }
            public bool HasBattleEnded { get; set; }
            public BattleOutcome Outcome { get; set; }

            public UnitStatsViewData ActiveStats;

            public event Action<BattleOutcome> BattleEnded;

            public void RequestEndTurn()
            {
            }

            public bool TryGetActiveUnitStats(out UnitStatsViewData stats)
            {
                stats = ActiveStats;
                return HasActiveUnit;
            }

            public void StartBattle()
            {
            }

            public void SetInteractionLocked(bool locked)
            {
                IsInteractionLocked = locked;
            }
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
        public void PauseMenu_OpensAndCloses_WithTimeScaleAndInteractionLock()
        {
            var hudGo = new GameObject("PauseHUD");
            var hud = hudGo.AddComponent<BattlePauseHUD>();

            var menuRootGo = new GameObject("MenuRoot");
            menuRootGo.transform.SetParent(hudGo.transform);
            var menuRoot = menuRootGo.AddComponent<RectTransform>();
            var menuCg = menuRootGo.AddComponent<CanvasGroup>();

            var blurGo = new GameObject("Blur");
            blurGo.transform.SetParent(hudGo.transform);
            var blurCg = blurGo.AddComponent<CanvasGroup>();

            var ctrlGo = new GameObject("FakeCtrl");
            var fake = ctrlGo.AddComponent<FakeTurnController>();
            fake.HasActiveUnit = true;
            fake.IsActiveUnitPlayerControlled = true;

            SetPrivate(hud, "_controllerBehaviour", fake);
            SetPrivate(hud, "_menuCanvasGroup", menuCg);
            SetPrivate(hud, "_menuRoot", menuRoot);
            SetPrivate(hud, "_blurCanvasGroup", blurCg);
            SetPrivate(hud, "_fadeDuration", 0f);

            CallPrivate(hud, "Awake");
            CallPrivate(hud, "OnEnable");

            Time.timeScale = 1f;

            CallPrivate(hud, "OpenPauseMenu");

            Assert.IsTrue(menuRootGo.activeSelf);
            Assert.AreEqual(0f, Time.timeScale, 1e-4f, "Time scale should be zero while paused.");
            Assert.IsTrue(fake.IsInteractionLocked, "Interaction should be locked while pause menu is open.");
            Assert.AreEqual(1f, menuCg.alpha, 1e-4f);
            Assert.AreEqual(1f, blurCg.alpha, 1e-4f);

            CallPrivate(hud, "ClosePauseMenu");

            Assert.IsFalse(menuRootGo.activeSelf, "Menu root should be inactive after closing.");
            Assert.AreEqual(1f, Time.timeScale, 1e-4f, "Time scale should be restored after closing pause menu.");
            Assert.IsFalse(fake.IsInteractionLocked, "Interaction lock should be released after closing pause menu.");

            UnityEngine.Object.DestroyImmediate(hudGo);
            UnityEngine.Object.DestroyImmediate(ctrlGo);
        }

        [Test]
        public void CancelButton_ClosesPauseMenu_AndRestoresState()
        {
            var hudGo = new GameObject("PauseHUD");
            var hud = hudGo.AddComponent<BattlePauseHUD>();

            var menuRootGo = new GameObject("MenuRoot");
            menuRootGo.transform.SetParent(hudGo.transform);
            var menuRoot = menuRootGo.AddComponent<RectTransform>();
            var menuCg = menuRootGo.AddComponent<CanvasGroup>();

            var blurGo = new GameObject("Blur");
            blurGo.transform.SetParent(hudGo.transform);
            var blurCg = blurGo.AddComponent<CanvasGroup>();

            var ctrlGo = new GameObject("FakeCtrl");
            var fake = ctrlGo.AddComponent<FakeTurnController>();
            fake.HasActiveUnit = true;
            fake.IsActiveUnitPlayerControlled = true;

            var cancelButtonGo = new GameObject("CancelButton");
            cancelButtonGo.transform.SetParent(hudGo.transform);
            var cancelButton = cancelButtonGo.AddComponent<Button>();

            SetPrivate(hud, "_controllerBehaviour", fake);
            SetPrivate(hud, "_menuCanvasGroup", menuCg);
            SetPrivate(hud, "_menuRoot", menuRoot);
            SetPrivate(hud, "_blurCanvasGroup", blurCg);
            SetPrivate(hud, "_fadeDuration", 0f);
            SetPrivate(hud, "_cancelButton", cancelButton);

            CallPrivate(hud, "Awake");
            CallPrivate(hud, "OnEnable");

            Time.timeScale = 1f;
            CallPrivate(hud, "OpenPauseMenu");

            Assert.IsTrue(menuRootGo.activeSelf, "Menu should be active after opening.");
            Assert.AreEqual(0f, Time.timeScale, 1e-4f, "Time scale should be zero while paused.");
            Assert.IsTrue(fake.IsInteractionLocked, "Interaction should be locked while pause menu is open.");

            cancelButton.onClick.Invoke();

            Assert.IsFalse(menuRootGo.activeSelf, "Menu should be inactive after clicking Cancel.");
            Assert.AreEqual(1f, Time.timeScale, 1e-4f, "Time scale should be restored after clicking Cancel.");
            Assert.IsFalse(fake.IsInteractionLocked, "Interaction lock should be released after clicking Cancel.");

            UnityEngine.Object.DestroyImmediate(hudGo);
            UnityEngine.Object.DestroyImmediate(ctrlGo);
        }
    }
}
