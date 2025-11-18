using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SevenBattles.Core;
using SevenBattles.UI;

namespace SevenBattles.Tests.UI
{
    public class TurnOrderHUDTests
    {
        private class FakeTurnController : MonoBehaviour, ITurnOrderController
        {
            public bool HasActiveUnit { get; set; }
            public bool IsActiveUnitPlayerControlled { get; set; }
            public Sprite ActiveUnitPortrait { get; set; }

            public event System.Action ActiveUnitChanged;

            public bool EndTurnRequested { get; private set; }
            public UnitStatsViewData ActiveStats;

            public void RequestEndTurn()
            {
                EndTurnRequested = true;
            }

            public void FireChanged()
            {
                ActiveUnitChanged?.Invoke();
            }

            public bool TryGetActiveUnitStats(out UnitStatsViewData stats)
            {
                stats = ActiveStats;
                return HasActiveUnit;
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
            Assert.AreEqual("25", lifeText.text);
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
            fake.ActiveStats = new UnitStatsViewData { Life = 30 };

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
            Assert.AreEqual("30", lifeText.text);

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
            fake.ActiveStats = new UnitStatsViewData { Life = 10 };

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

            // Active unit changes -> panel should close automatically
            fake.FireChanged();
            Assert.IsFalse(panelGo.activeSelf, "Stats panel should close when active unit changes.");

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
            fake.ActiveStats = new UnitStatsViewData { Life = 10 };

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

        private static void SetPrivate(object obj, string field, object value)
        {
            var fi = obj.GetType().GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            fi.SetValue(obj, value);
        }

        private static void CallPrivate(object obj, string method)
        {
            var mi = obj.GetType().GetMethod(method, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            mi.Invoke(obj, null);
        }
    }
}
