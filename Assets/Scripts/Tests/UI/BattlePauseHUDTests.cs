using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;
using SevenBattles.Core;
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

            public event Action ActiveUnitChanged;
            public event Action ActiveUnitActionPointsChanged;
            public event Action ActiveUnitStatsChanged;

            public int ActiveUnitCurrentActionPoints { get; set; }
            public int ActiveUnitMaxActionPoints { get; set; }

            public bool IsInteractionLocked { get; private set; }
            public int TurnIndex { get; set; }

            public UnitStatsViewData ActiveStats;

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
        public void Escape_TogglesPause_OnlyForPlayerTurn()
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

            SetPrivate(hud, "_controllerBehaviour", fake);
            SetPrivate(hud, "_menuCanvasGroup", menuCg);
            SetPrivate(hud, "_menuRoot", menuRoot);
            SetPrivate(hud, "_blurCanvasGroup", blurCg);
            SetPrivate(hud, "_fadeDuration", 0f);

            CallPrivate(hud, "Awake");
            CallPrivate(hud, "OnEnable");

            fake.HasActiveUnit = false;
            fake.IsActiveUnitPlayerControlled = false;
            CallPrivate(hud, "HandleEscapePressed");

            Assert.IsFalse(menuRootGo.activeSelf, "Pause menu should not open when there is no active unit.");

            fake.HasActiveUnit = true;
            fake.IsActiveUnitPlayerControlled = false;
            CallPrivate(hud, "HandleEscapePressed");
            Assert.IsFalse(menuRootGo.activeSelf, "Pause menu should not open during AI turn.");

            fake.IsActiveUnitPlayerControlled = true;
            CallPrivate(hud, "HandleEscapePressed");
            Assert.IsTrue(menuRootGo.activeSelf, "Pause menu should open during player turn.");

            CallPrivate(hud, "HandleEscapePressed");
            Assert.IsFalse(menuRootGo.activeSelf, "Pause menu should close when Escape is pressed again.");

            UnityEngine.Object.DestroyImmediate(hudGo);
            UnityEngine.Object.DestroyImmediate(ctrlGo);
        }

        [Test]
        public void Buttons_EmitEvents_WhenMenuOpen()
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

            var saveBtnGo = new GameObject("SaveButton");
            saveBtnGo.transform.SetParent(menuRootGo.transform);
            var saveBtn = saveBtnGo.AddComponent<Button>();

            var loadBtnGo = new GameObject("LoadButton");
            loadBtnGo.transform.SetParent(menuRootGo.transform);
            var loadBtn = loadBtnGo.AddComponent<Button>();

            var settingsBtnGo = new GameObject("SettingsButton");
            settingsBtnGo.transform.SetParent(menuRootGo.transform);
            var settingsBtn = settingsBtnGo.AddComponent<Button>();

            var quitBtnGo = new GameObject("QuitButton");
            quitBtnGo.transform.SetParent(menuRootGo.transform);
            var quitBtn = quitBtnGo.AddComponent<Button>();

            SetPrivate(hud, "_menuCanvasGroup", menuCg);
            SetPrivate(hud, "_menuRoot", menuRoot);
            SetPrivate(hud, "_blurCanvasGroup", blurCg);
            SetPrivate(hud, "_saveButton", saveBtn);
            SetPrivate(hud, "_loadButton", loadBtn);
            SetPrivate(hud, "_settingsButton", settingsBtn);
            SetPrivate(hud, "_quitButton", quitBtn);
            SetPrivate(hud, "_fadeDuration", 0f);

            bool saveCalled = false;
            bool loadCalled = false;
            bool settingsCalled = false;
            bool quitCalled = false;

            hud.SaveClicked += () => saveCalled = true;
            hud.LoadClicked += () => loadCalled = true;
            hud.SettingsClicked += () => settingsCalled = true;
            hud.QuitClicked += () => quitCalled = true;

            CallPrivate(hud, "Awake");
            CallPrivate(hud, "OnEnable");

            CallPrivate(hud, "OpenPauseMenu");

            saveBtn.onClick.Invoke();
            loadBtn.onClick.Invoke();
            settingsBtn.onClick.Invoke();
            quitBtn.onClick.Invoke();

            Assert.IsTrue(saveCalled, "SaveClicked event should be raised when Save button is pressed.");
            Assert.IsTrue(loadCalled, "LoadClicked event should be raised when Load button is pressed.");
            Assert.IsTrue(settingsCalled, "SettingsClicked event should be raised when Settings button is pressed.");
            Assert.IsTrue(quitCalled, "QuitClicked event should be raised when Quit button is pressed.");

            UnityEngine.Object.DestroyImmediate(hudGo);
        }
    }
}

