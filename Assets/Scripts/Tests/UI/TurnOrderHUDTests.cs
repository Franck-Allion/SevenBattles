using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;
using TMPro;
using SevenBattles.Core;
using SevenBattles.UI;

namespace SevenBattles.Tests.UI
{
    public class TurnOrderHUDTests
    {
        private class TestHealthBarLogic : MonoBehaviour, IHealthBarView
        {
            public float Value { get; set; }
        }

        private class FakeTurnController : MonoBehaviour, IBattleTurnController
        {
            public bool HasActiveUnit { get; set; }
            public bool IsActiveUnitPlayerControlled { get; set; }
            public Sprite ActiveUnitPortrait { get; set; }

            public event System.Action ActiveUnitChanged;
            public event System.Action ActiveUnitActionPointsChanged;
            public event System.Action ActiveUnitStatsChanged;

            public bool EndTurnRequested { get; private set; }
            public UnitStatsViewData ActiveStats;
            public int ActiveUnitCurrentActionPoints { get; set; }
            public int ActiveUnitMaxActionPoints { get; set; }
            public bool IsInteractionLocked { get; private set; }
            public int TurnIndex { get; set; }

            public void RequestEndTurn()
            {
                EndTurnRequested = true;
            }

            public void FireChanged()
            {
                ActiveUnitChanged?.Invoke();
            }

            public void FireActionPointsChanged()
            {
                ActiveUnitActionPointsChanged?.Invoke();
            }

            public void FireStatsChanged()
            {
                ActiveUnitStatsChanged?.Invoke();
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

        [Test]
        public void UpdatesPortrait_And_ButtonState_OnActiveChange()
        {
            var hudGo = new GameObject("HUD");
            var hud = hudGo.AddComponent<TurnOrderHUD>();

            var portraitGo = new GameObject("Portrait");
            portraitGo.transform.SetParent(hudGo.transform);
            var portraitImg = portraitGo.AddComponent<Image>();

            var buttonGo = new GameObject("EndTurnButton");
            buttonGo.transform.SetParent(hudGo.transform);
            var btn = buttonGo.AddComponent<Button>();
            var cg = buttonGo.AddComponent<CanvasGroup>();
            var tmp = new GameObject("Label").AddComponent<TextMeshProUGUI>();
            tmp.transform.SetParent(buttonGo.transform);

            var ctrlGo = new GameObject("FakeCtrl");
            var fake = ctrlGo.AddComponent<FakeTurnController>();

            SetPrivate(hud, "_controllerBehaviour", fake);
            SetPrivate(hud, "_activePortraitImage", portraitImg);
            SetPrivate(hud, "_endTurnButton", btn);
            SetPrivate(hud, "_endTurnCanvasGroup", cg);
            SetPrivate(hud, "_endTurnTMP", tmp);
            SetPrivate(hud, "_disabledAlpha", 0.5f);

            CallPrivate(hud, "Awake");
            CallPrivate(hud, "OnEnable");

            // Initial state: no active unit, button disabled.
            Assert.IsFalse(btn.interactable);
            Assert.IsFalse(portraitImg.enabled);
            Assert.AreEqual(0.5f, cg.alpha, 1e-4f, "End Turn button alpha should be dimmed when disabled.");

            // Player unit becomes active.
            fake.HasActiveUnit = true;
            fake.IsActiveUnitPlayerControlled = true;
            var tex = Texture2D.blackTexture;
            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            fake.ActiveUnitPortrait = sprite;
            fake.FireChanged();

            Assert.IsTrue(btn.interactable, "End Turn button should be interactable during player turn.");
            Assert.AreEqual(sprite, portraitImg.sprite);
            Assert.IsTrue(portraitImg.enabled, "Portrait should be visible when there is an active unit.");
            Assert.AreEqual(1f, cg.alpha, 1e-4f, "End Turn button alpha should be 1 when enabled.");

            // AI unit becomes active.
            fake.IsActiveUnitPlayerControlled = false;
            fake.FireChanged();
            Assert.IsFalse(btn.interactable, "End Turn button should be disabled during AI turn.");
            Assert.AreEqual(0.5f, cg.alpha, 1e-4f, "End Turn button alpha should be dimmed during AI turn.");

            Object.DestroyImmediate(hudGo);
            Object.DestroyImmediate(ctrlGo);
        }

        [Test]
        public void ClickingEndTurn_DelegatesToController()
        {
            var hudGo = new GameObject("HUD");
            var hud = hudGo.AddComponent<TurnOrderHUD>();

            var buttonGo = new GameObject("EndTurnButton");
            buttonGo.transform.SetParent(hudGo.transform);
            var btn = buttonGo.AddComponent<Button>();

            var ctrlGo = new GameObject("FakeCtrl");
            var fake = ctrlGo.AddComponent<FakeTurnController>();
            fake.HasActiveUnit = true;
            fake.IsActiveUnitPlayerControlled = true;

            SetPrivate(hud, "_controllerBehaviour", fake);
            SetPrivate(hud, "_endTurnButton", btn);

            CallPrivate(hud, "Awake");
            CallPrivate(hud, "OnEnable");

            Assert.IsFalse(fake.EndTurnRequested);
            btn.onClick.Invoke();
            Assert.IsTrue(fake.EndTurnRequested, "End turn request should be forwarded to controller.");

            Object.DestroyImmediate(hudGo);
            Object.DestroyImmediate(ctrlGo);
        }

        [Test]
        public void ActionPointsBar_Updates_OnActiveUnitChange_AndApChange()
        {
            var hudGo = new GameObject("HUD");
            var hud = hudGo.AddComponent<TurnOrderHUD>();

            var apRootGo = new GameObject("APRoot");
            apRootGo.transform.SetParent(hudGo.transform);
            var apRoot = apRootGo.AddComponent<RectTransform>();

            var slots = new Image[8];
            for (int i = 0; i < slots.Length; i++)
            {
                var slotGo = new GameObject("Slot" + i);
                slotGo.transform.SetParent(apRootGo.transform);
                slots[i] = slotGo.AddComponent<Image>();
            }

            var fullTex = new Texture2D(2, 2);
            var fullSprite = Sprite.Create(fullTex, new Rect(0, 0, fullTex.width, fullTex.height), new Vector2(0.5f, 0.5f));
            var emptyTex = new Texture2D(2, 2);
            var emptySprite = Sprite.Create(emptyTex, new Rect(0, 0, emptyTex.width, emptyTex.height), new Vector2(0.5f, 0.5f));

            var ctrlGo = new GameObject("FakeCtrl");
            var fake = ctrlGo.AddComponent<FakeTurnController>();

            SetPrivate(hud, "_controllerBehaviour", fake);
            SetPrivate(hud, "_actionPointBarRoot", apRoot);
            SetPrivate(hud, "_actionPointSlots", slots);
            SetPrivate(hud, "_actionPointFullSprite", fullSprite);
            SetPrivate(hud, "_actionPointEmptySprite", emptySprite);

            CallPrivate(hud, "Awake");
            CallPrivate(hud, "OnEnable");

            // No active unit -> bar hidden.
            Assert.IsFalse(apRootGo.activeSelf, "AP bar should be hidden when there is no active unit.");

            // Active unit with 3/5 AP.
            fake.HasActiveUnit = true;
            fake.ActiveUnitMaxActionPoints = 5;
            fake.ActiveUnitCurrentActionPoints = 3;
            fake.FireChanged();

            Assert.IsTrue(apRootGo.activeSelf, "AP bar should be visible when the active unit has AP.");
            for (int i = 0; i < slots.Length; i++)
            {
                if (i < 5)
                {
                    Assert.IsTrue(slots[i].gameObject.activeSelf, $"Slot {i} should be visible within max AP range.");
                    var expected = i < 3 ? fullSprite : emptySprite;
                    Assert.AreEqual(expected, slots[i].sprite, $"Slot {i} should use correct sprite for full/empty.");
                }
                else
                {
                    Assert.IsTrue(slots[i].gameObject.activeSelf, $"Slot {i} GameObject should remain active for layout.");
                    Assert.IsFalse(slots[i].enabled, $"Slot {i} Image should be disabled beyond max AP.");
                }
            }

            // Spend AP: 1/5 left.
            fake.ActiveUnitCurrentActionPoints = 1;
            fake.FireActionPointsChanged();

            Assert.IsTrue(apRootGo.activeSelf, "AP bar should remain visible while AP > 0.");
            for (int i = 0; i < slots.Length; i++)
            {
                if (i < 5)
                {
                    Assert.IsTrue(slots[i].gameObject.activeSelf, $"Slot {i} should remain visible within max AP range.");
                    var expected = i < 1 ? fullSprite : emptySprite;
                    Assert.AreEqual(expected, slots[i].sprite, $"Slot {i} should update sprite based on remaining AP.");
                }
            }

            // No AP left -> bar still visible, all slots empty.
            fake.ActiveUnitCurrentActionPoints = 0;
            fake.FireActionPointsChanged();
            Assert.IsTrue(apRootGo.activeSelf, "AP bar should remain visible even when the active unit has 0 AP.");
            for (int i = 0; i < slots.Length; i++)
            {
                if (i < 5)
                {
                    Assert.IsTrue(slots[i].gameObject.activeSelf, $"Slot {i} should remain visible within max AP range at 0 AP.");
                    Assert.AreEqual(emptySprite, slots[i].sprite, $"Slot {i} should use empty sprite when no AP remain.");
                }
            }

            Object.DestroyImmediate(hudGo);
            Object.DestroyImmediate(ctrlGo);
        }

        [Test]
        public void PortraitClick_TogglesStatsPanel_AndLoadsStats()
        {
            var hudGo = new GameObject("HUD");
            var hud = hudGo.AddComponent<TurnOrderHUD>();

            var portraitGo = new GameObject("Portrait");
            portraitGo.transform.SetParent(hudGo.transform);
            var portraitImg = portraitGo.AddComponent<Image>();
            var portraitBtn = portraitGo.AddComponent<Button>();

            var panelGo = new GameObject("StatsPanel");
            panelGo.transform.SetParent(hudGo.transform);
            var panelRect = panelGo.AddComponent<RectTransform>();
            var panelCg = panelGo.AddComponent<CanvasGroup>();

            var lifeText = new GameObject("Life").AddComponent<TextMeshProUGUI>();
            lifeText.transform.SetParent(panelGo.transform);
            var speedText = new GameObject("Speed").AddComponent<TextMeshProUGUI>();
            speedText.transform.SetParent(panelGo.transform);
            var initText = new GameObject("Init").AddComponent<TextMeshProUGUI>();
            initText.transform.SetParent(panelGo.transform);

            var buttonGo = new GameObject("EndTurnButton");
            buttonGo.transform.SetParent(hudGo.transform);
            var btn = buttonGo.AddComponent<Button>();

            var ctrlGo = new GameObject("FakeCtrl");
            var fake = ctrlGo.AddComponent<FakeTurnController>();
            fake.HasActiveUnit = true;
            fake.IsActiveUnitPlayerControlled = true;
            fake.ActiveStats = new UnitStatsViewData
            {
                Life = 25,
                MaxLife = 25,
                Speed = 7,
                Initiative = 12
            };

            SetPrivate(hud, "_controllerBehaviour", fake);
            SetPrivate(hud, "_activePortraitImage", portraitImg);
            SetPrivate(hud, "_portraitButton", portraitBtn);
            SetPrivate(hud, "_endTurnButton", btn);
            SetPrivate(hud, "_statsPanelRoot", panelRect);
            SetPrivate(hud, "_statsPanelCanvasGroup", panelCg);
            SetPrivate(hud, "_lifeText", lifeText);
            SetPrivate(hud, "_speedText", speedText);
            SetPrivate(hud, "_initiativeText", initText);

            CallPrivate(hud, "Awake");
            CallPrivate(hud, "OnEnable");

            Assert.IsFalse(panelGo.activeSelf, "Stats panel should start hidden.");

            // First click opens panel and populates stats
            portraitBtn.onClick.Invoke();
            Assert.IsTrue(panelGo.activeSelf, "Stats panel should be visible after portrait click.");
            Assert.AreEqual("25 / 25", lifeText.text);
            Assert.AreEqual("7", speedText.text);
            Assert.AreEqual("12", initText.text);

            // Second click closes panel
            portraitBtn.onClick.Invoke();
            Assert.IsFalse(panelGo.activeSelf, "Stats panel should be hidden when clicking portrait again.");

            Object.DestroyImmediate(hudGo);
            Object.DestroyImmediate(ctrlGo);
        }

        [Test]
        public void PortraitButton_CanBeParentOfImage()
        {
            var hudGo = new GameObject("HUD");
            var hud = hudGo.AddComponent<TurnOrderHUD>();

            // Button root
            var buttonRoot = new GameObject("PortraitButton");
            buttonRoot.transform.SetParent(hudGo.transform);
            var portraitBtn = buttonRoot.AddComponent<Button>();

            // Portrait image as child of the button
            var portraitChild = new GameObject("PortraitImage");
            portraitChild.transform.SetParent(buttonRoot.transform);
            var portraitImg = portraitChild.AddComponent<Image>();

            // Stats panel
            var panelGo = new GameObject("StatsPanel");
            panelGo.transform.SetParent(hudGo.transform);
            var panelRect = panelGo.AddComponent<RectTransform>();
            var panelCg = panelGo.AddComponent<CanvasGroup>();

            var lifeText = new GameObject("Life").AddComponent<TextMeshProUGUI>();
            lifeText.transform.SetParent(panelGo.transform);

            var buttonGo = new GameObject("EndTurnButton");
            buttonGo.transform.SetParent(hudGo.transform);
            var endTurnBtn = buttonGo.AddComponent<Button>();

            var ctrlGo = new GameObject("FakeCtrl");
            var fake = ctrlGo.AddComponent<FakeTurnController>();
            fake.HasActiveUnit = true;
            fake.IsActiveUnitPlayerControlled = true;
            fake.ActiveStats = new UnitStatsViewData { Life = 30, MaxLife = 30 };

            SetPrivate(hud, "_controllerBehaviour", fake);
            SetPrivate(hud, "_activePortraitImage", portraitImg);
            // Intentionally do NOT assign _portraitButton to ensure auto-wiring via parent lookup.
            SetPrivate(hud, "_endTurnButton", endTurnBtn);
            SetPrivate(hud, "_statsPanelRoot", panelRect);
            SetPrivate(hud, "_statsPanelCanvasGroup", panelCg);
            SetPrivate(hud, "_lifeText", lifeText);

            CallPrivate(hud, "Awake");
            CallPrivate(hud, "OnEnable");

            Assert.IsFalse(panelGo.activeSelf, "Stats panel should start hidden.");

            // Click the button root (parent of the image).
            portraitBtn.onClick.Invoke();
            Assert.IsTrue(panelGo.activeSelf, "Stats panel should open when clicking a Button parent of the portrait image.");
            Assert.AreEqual("30 / 30", lifeText.text);

            Object.DestroyImmediate(hudGo);
            Object.DestroyImmediate(ctrlGo);
        }

        [Test]
        public void StatsPanel_Closes_OnActiveUnitChange()
        {
            var hudGo = new GameObject("HUD");
            var hud = hudGo.AddComponent<TurnOrderHUD>();

            var portraitGo = new GameObject("Portrait");
            portraitGo.transform.SetParent(hudGo.transform);
            var portraitImg = portraitGo.AddComponent<Image>();
            var portraitBtn = portraitGo.AddComponent<Button>();

            var panelGo = new GameObject("StatsPanel");
            panelGo.transform.SetParent(hudGo.transform);
            var panelRect = panelGo.AddComponent<RectTransform>();
            var panelCg = panelGo.AddComponent<CanvasGroup>();

            var lifeText = new GameObject("Life").AddComponent<TextMeshProUGUI>();
            lifeText.transform.SetParent(panelGo.transform);

            var buttonGo = new GameObject("EndTurnButton");
            buttonGo.transform.SetParent(hudGo.transform);
            var btn = buttonGo.AddComponent<Button>();

            var ctrlGo = new GameObject("FakeCtrl");
            var fake = ctrlGo.AddComponent<FakeTurnController>();
            fake.HasActiveUnit = true;
            fake.IsActiveUnitPlayerControlled = true;
            fake.ActiveStats = new UnitStatsViewData { Life = 10, MaxLife = 10 };

            SetPrivate(hud, "_controllerBehaviour", fake);
            SetPrivate(hud, "_activePortraitImage", portraitImg);
            SetPrivate(hud, "_portraitButton", portraitBtn);
            SetPrivate(hud, "_endTurnButton", btn);
            SetPrivate(hud, "_statsPanelRoot", panelRect);
            SetPrivate(hud, "_statsPanelCanvasGroup", panelCg);
            SetPrivate(hud, "_lifeText", lifeText);

            CallPrivate(hud, "Awake");
            CallPrivate(hud, "OnEnable");

            // Open the stats panel
            portraitBtn.onClick.Invoke();
            Assert.IsTrue(panelGo.activeSelf, "Stats panel should be visible after portrait click.");

            // Active unit changes -> panel should stay open and refresh stats
            fake.FireChanged();
            Assert.IsTrue(panelGo.activeSelf, "Stats panel should remain open when active unit changes.");

            Object.DestroyImmediate(hudGo);
            Object.DestroyImmediate(ctrlGo);
        }

        [Test]
        public void ClickingOutside_ClosesStatsPanel()
        {
            var hudGo = new GameObject("HUD");
            var hud = hudGo.AddComponent<TurnOrderHUD>();

            var portraitGo = new GameObject("Portrait");
            portraitGo.transform.SetParent(hudGo.transform);
            var portraitImg = portraitGo.AddComponent<Image>();
            var portraitBtn = portraitGo.AddComponent<Button>();

            var panelGo = new GameObject("StatsPanel");
            panelGo.transform.SetParent(hudGo.transform);
            var panelRect = panelGo.AddComponent<RectTransform>();
            var panelCg = panelGo.AddComponent<CanvasGroup>();

            var lifeText = new GameObject("Life").AddComponent<TextMeshProUGUI>();
            lifeText.transform.SetParent(panelGo.transform);

            var overlayGo = new GameObject("Overlay");
            overlayGo.transform.SetParent(hudGo.transform);
            var overlayBtn = overlayGo.AddComponent<Button>();

            var buttonGo = new GameObject("EndTurnButton");
            buttonGo.transform.SetParent(hudGo.transform);
            var btn = buttonGo.AddComponent<Button>();

            var ctrlGo = new GameObject("FakeCtrl");
            var fake = ctrlGo.AddComponent<FakeTurnController>();
            fake.HasActiveUnit = true;
            fake.IsActiveUnitPlayerControlled = true;
            fake.ActiveStats = new UnitStatsViewData { Life = 10, MaxLife = 10 };

            SetPrivate(hud, "_controllerBehaviour", fake);
            SetPrivate(hud, "_activePortraitImage", portraitImg);
            SetPrivate(hud, "_portraitButton", portraitBtn);
            SetPrivate(hud, "_endTurnButton", btn);
            SetPrivate(hud, "_statsPanelRoot", panelRect);
            SetPrivate(hud, "_statsPanelCanvasGroup", panelCg);
            SetPrivate(hud, "_lifeText", lifeText);
            SetPrivate(hud, "_statsPanelBackgroundButton", overlayBtn);

            CallPrivate(hud, "Awake");
            CallPrivate(hud, "OnEnable");

            // Open the stats panel
            portraitBtn.onClick.Invoke();
            Assert.IsTrue(panelGo.activeSelf, "Stats panel should be visible after portrait click.");
            Assert.IsTrue(overlayGo.activeSelf, "Overlay should be active while stats panel is open.");

            // Click outside via overlay
            overlayBtn.onClick.Invoke();
            Assert.IsFalse(panelGo.activeSelf, "Stats panel should be hidden after clicking outside.");
            Assert.IsFalse(overlayGo.activeSelf, "Overlay should be hidden when stats panel is closed.");

            Object.DestroyImmediate(hudGo);
            Object.DestroyImmediate(ctrlGo);
        }

        [Test]
        public void StatsLabelHandlers_UpdateLabelText()
        {
            var hudGo = new GameObject("HUD");
            var hud = hudGo.AddComponent<TurnOrderHUD>();

            var panelGo = new GameObject("StatsPanel");
            panelGo.transform.SetParent(hudGo.transform);
            var panelRect = panelGo.AddComponent<RectTransform>();

            var lifeLabelGo = new GameObject("LifeLabel");
            lifeLabelGo.transform.SetParent(panelGo.transform);
            var lifeLabel = lifeLabelGo.AddComponent<TextMeshProUGUI>();

            SetPrivate(hud, "_statsPanelRoot", panelRect);
            SetPrivate(hud, "_lifeLabel", lifeLabel);

            CallPrivate(hud, "HandleLifeLabelChanged", "Life");

            Assert.AreEqual("Life", lifeLabel.text);

            Object.DestroyImmediate(hudGo);
        }

        [Test]
        public void LifeValue_Updates_WhenStatsChangeAndEventRaised()
        {
            var hudGo = new GameObject("HUD");
            var hud = hudGo.AddComponent<TurnOrderHUD>();

            var portraitGo = new GameObject("Portrait");
            portraitGo.transform.SetParent(hudGo.transform);
            var portraitImg = portraitGo.AddComponent<Image>();
            var portraitBtn = portraitGo.AddComponent<Button>();

            var panelGo = new GameObject("StatsPanel");
            panelGo.transform.SetParent(hudGo.transform);
            var panelRect = panelGo.AddComponent<RectTransform>();
            var panelCg = panelGo.AddComponent<CanvasGroup>();

            var lifeText = new GameObject("Life").AddComponent<TextMeshProUGUI>();
            lifeText.transform.SetParent(panelGo.transform);

            var endTurnButtonGo = new GameObject("EndTurnButton");
            endTurnButtonGo.transform.SetParent(hudGo.transform);
            var endTurnBtn = endTurnButtonGo.AddComponent<Button>();

            var ctrlGo = new GameObject("FakeCtrl");
            var fake = ctrlGo.AddComponent<FakeTurnController>();
            fake.HasActiveUnit = true;
            fake.IsActiveUnitPlayerControlled = true;
            fake.ActiveStats = new UnitStatsViewData { Life = 20, MaxLife = 25 };

            SetPrivate(hud, "_controllerBehaviour", fake);
            SetPrivate(hud, "_activePortraitImage", portraitImg);
            SetPrivate(hud, "_portraitButton", portraitBtn);
            SetPrivate(hud, "_endTurnButton", endTurnBtn);
            SetPrivate(hud, "_statsPanelRoot", panelRect);
            SetPrivate(hud, "_statsPanelCanvasGroup", panelCg);
            SetPrivate(hud, "_lifeText", lifeText);

            CallPrivate(hud, "Awake");
            CallPrivate(hud, "OnEnable");

            // Open panel once, should show initial stats.
            portraitBtn.onClick.Invoke();
            Assert.IsTrue(panelGo.activeSelf, "Stats panel should be visible after portrait click.");
            Assert.AreEqual("20 / 25", lifeText.text);

            // Change stats and raise controller event.
            fake.ActiveStats = new UnitStatsViewData { Life = 10, MaxLife = 25 };
            fake.FireChanged();

            // Panel should remain open and reflect new life value.
            Assert.IsTrue(panelGo.activeSelf, "Stats panel should remain open after stats change.");
            Assert.AreEqual("10 / 25", lifeText.text);

            Object.DestroyImmediate(hudGo);
            Object.DestroyImmediate(ctrlGo);
        }

        [Test]
        public void HealthBar_Updates_WhenStatsChangeAndEventRaised()
        {
            var hudGo = new GameObject("HUD");
            var hud = hudGo.AddComponent<TurnOrderHUD>();

            var ctrlGo = new GameObject("FakeCtrl");
            var fake = ctrlGo.AddComponent<FakeTurnController>();
            fake.HasActiveUnit = true;
            fake.IsActiveUnitPlayerControlled = true;
            fake.ActiveStats = new UnitStatsViewData { Life = 50, MaxLife = 100 };

            var healthBarGo = new GameObject("HealthBar");
            var healthBar = healthBarGo.AddComponent<TestHealthBarLogic>();

            SetPrivate(hud, "_controllerBehaviour", fake);
            SetPrivate(hud, "_healthBarBehaviour", healthBar);

            CallPrivate(hud, "Awake");
            CallPrivate(hud, "OnEnable");

            fake.FireStatsChanged();
            Assert.IsTrue(healthBarGo.activeSelf, "Health bar should be active when there is an active unit.");
            Assert.That(healthBar.Value, Is.EqualTo(0.5f).Within(1e-4f), "Health bar value should reflect current life percentage.");

            fake.ActiveStats = new UnitStatsViewData { Life = 25, MaxLife = 100 };
            fake.FireStatsChanged();
            Assert.That(healthBar.Value, Is.EqualTo(0.25f).Within(1e-4f), "Health bar value should update when life changes.");

            Object.DestroyImmediate(hudGo);
            Object.DestroyImmediate(ctrlGo);
        }

        [Test]
        public void HealthBar_Hidden_WhenNoActiveUnit()
        {
            var hudGo = new GameObject("HUD");
            var hud = hudGo.AddComponent<TurnOrderHUD>();

            var ctrlGo = new GameObject("FakeCtrl");
            var fake = ctrlGo.AddComponent<FakeTurnController>();
            fake.HasActiveUnit = true;
            fake.IsActiveUnitPlayerControlled = true;
            fake.ActiveStats = new UnitStatsViewData { Life = 10, MaxLife = 10 };

            var healthBarGo = new GameObject("HealthBar");
            var healthBar = healthBarGo.AddComponent<TestHealthBarLogic>();

            SetPrivate(hud, "_controllerBehaviour", fake);
            SetPrivate(hud, "_healthBarBehaviour", healthBar);

            CallPrivate(hud, "Awake");
            CallPrivate(hud, "OnEnable");

            fake.FireStatsChanged();
            Assert.IsTrue(healthBarGo.activeSelf, "Health bar should be active while there is an active unit.");

            fake.HasActiveUnit = false;
            fake.FireChanged();

            Assert.IsFalse(healthBarGo.activeSelf, "Health bar should be hidden when there is no active unit.");

            Object.DestroyImmediate(hudGo);
            Object.DestroyImmediate(ctrlGo);
        }

        [Test]
        public void HealthBar_ShowsZero_ForDeadActiveUnit()
        {
            var hudGo = new GameObject("HUD");
            var hud = hudGo.AddComponent<TurnOrderHUD>();

            var ctrlGo = new GameObject("FakeCtrl");
            var fake = ctrlGo.AddComponent<FakeTurnController>();
            fake.HasActiveUnit = true;
            fake.IsActiveUnitPlayerControlled = true;
            fake.ActiveStats = new UnitStatsViewData { Life = 0, MaxLife = 100 };

            var healthBarGo = new GameObject("HealthBar");
            var healthBar = healthBarGo.AddComponent<TestHealthBarLogic>();

            SetPrivate(hud, "_controllerBehaviour", fake);
            SetPrivate(hud, "_healthBarBehaviour", healthBar);

            CallPrivate(hud, "Awake");
            CallPrivate(hud, "OnEnable");

            fake.FireStatsChanged();

            Assert.IsTrue(healthBarGo.activeSelf, "Health bar should be visible for a dead active unit still in turn order.");
            Assert.That(healthBar.Value, Is.EqualTo(0f).Within(1e-4f), "Health bar value should be 0 for a dead unit.");

            Object.DestroyImmediate(hudGo);
            Object.DestroyImmediate(ctrlGo);
        }

        private static void SetPrivate(object obj, string field, object value)
        {
            var fi = obj.GetType().GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            fi.SetValue(obj, value);
        }

        private static void CallPrivate(object obj, string method, params object[] args)
        {
            var mi = obj.GetType().GetMethod(method, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            mi.Invoke(obj, args);
        }

        [UnityTest]
        public IEnumerator TurnStartBanner_LocksAndReleasesInteraction()
        {
            var hudGo = new GameObject("HUD");
            var hud = hudGo.AddComponent<TurnOrderHUD>();

            var bannerRoot = new GameObject("TurnBanner");
            bannerRoot.transform.SetParent(hudGo.transform);
            var bannerCanvasGroup = bannerRoot.AddComponent<CanvasGroup>();
            var bannerText = bannerRoot.AddComponent<TextMeshProUGUI>();

            var ctrlGo = new GameObject("FakeCtrl");
            var fake = ctrlGo.AddComponent<FakeTurnController>();
            fake.HasActiveUnit = true;
            fake.IsActiveUnitPlayerControlled = true;

            SetPrivate(hud, "_controllerBehaviour", fake);
            SetPrivate(hud, "_turnStartCanvasGroup", bannerCanvasGroup);
            SetPrivate(hud, "_turnStartText", bannerText);
            SetPrivate(hud, "_turnStartVisibleDuration", 0.05f);
            SetPrivate(hud, "_turnStartFadeDuration", 0.05f);

            CallPrivate(hud, "Awake");
            CallPrivate(hud, "OnEnable");

            Assert.IsFalse(fake.IsInteractionLocked, "Interaction should start unlocked.");

            fake.FireChanged();

            yield return null;

            Assert.IsTrue(fake.IsInteractionLocked, "Interaction should be locked while the turn banner is visible.");
            Assert.IsTrue(bannerRoot.activeSelf);
            Assert.Greater(bannerCanvasGroup.alpha, 0f);

            yield return new WaitForSecondsRealtime(0.2f);

            Assert.IsFalse(fake.IsInteractionLocked, "Interaction lock should be released after the turn banner fades out.");
            Assert.IsFalse(bannerRoot.activeSelf);
            Assert.AreEqual(0f, bannerCanvasGroup.alpha, 1e-4f);

            Object.DestroyImmediate(hudGo);
            Object.DestroyImmediate(ctrlGo);
        }
    }
}
