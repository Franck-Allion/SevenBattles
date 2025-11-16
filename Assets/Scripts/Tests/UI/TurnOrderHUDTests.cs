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

            public void RequestEndTurn()
            {
                EndTurnRequested = true;
            }

            public void FireChanged()
            {
                ActiveUnitChanged?.Invoke();
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
