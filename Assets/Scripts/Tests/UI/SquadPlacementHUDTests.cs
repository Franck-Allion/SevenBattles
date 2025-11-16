using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SevenBattles.Core;
using SevenBattles.UI;
using UnityEngine.TestTools;
using System.Collections;

namespace SevenBattles.Tests.UI
{
    public class SquadPlacementHUDTests
    {
        private class FakePlacementController : MonoBehaviour, ISquadPlacementController
        {
            public int SquadSize { get; set; } = 3;
            public bool IsReady { get; set; }
            public bool IsLocked { get; private set; }

            public bool IsPlaced(int index) => false;
            public Sprite GetPortrait(int index) => null;

            public void SelectWizard(int index) { WizardSelected?.Invoke(index); }
            public void ConfirmAndLock()
            {
                IsLocked = true;
                PlacementLocked?.Invoke();
            }

            public event System.Action<int> WizardSelected;
            public event System.Action<int> WizardPlaced;
            public event System.Action<int> WizardRemoved;
            public event System.Action<bool> ReadyChanged;
            public event System.Action PlacementLocked;

            public void FireReady(bool ready)
            {
                IsReady = ready;
                ReadyChanged?.Invoke(ready);
            }
        }

        [Test]
        public void Instructions_AreVisible_DuringPlacement_AndHidden_WhenLocked()
        {
            // HUD root with a child TMP_Text used as the instructions label
            var hudGo = new GameObject("HUD");
            var hud = hudGo.AddComponent<SquadPlacementHUD>();

            var instructionsGo = new GameObject("Instructions");
            instructionsGo.transform.SetParent(hudGo.transform);
            var tmp = instructionsGo.AddComponent<TextMeshProUGUI>();

            // Fake controller
            var ctrlGo = new GameObject("FakeCtrl");
            var fake = ctrlGo.AddComponent<FakePlacementController>();

            // Inject private fields via reflection helpers
            SetPrivate(hud, "_controllerBehaviour", fake);
            SetPrivate(hud, "_instructionsTMP", tmp);

            // Simulate lifecycle
            CallPrivate(hud, "Awake");
            CallPrivate(hud, "OnEnable");

            // During placement (not locked), instructions should be visible
            Assert.IsTrue(instructionsGo.activeSelf, "Instructions should be visible during placement");

            // Lock placement
            fake.ConfirmAndLock();

            // After locking, the instructions label should be hidden immediately (HUD may remain active for fade)
            Assert.IsFalse(instructionsGo.activeSelf,
                "Instructions should be hidden after placement is locked");

            Object.DestroyImmediate(hudGo);
            Object.DestroyImmediate(ctrlGo);
        }

        [UnityTest]
        public IEnumerator StartButton_FadesAndHides_OnPlacementLocked()
        {
            // HUD root
            var hudGo = new GameObject("HUD");
            var hud = hudGo.AddComponent<SquadPlacementHUD>();

            // Battle HUD root (initially inactive)
            var battleHudGo = new GameObject("BattleHUD");
            battleHudGo.SetActive(false);
            SetPrivate(hud, "_battleHudRoot", battleHudGo);

            // Start button with CanvasGroup
            var startGo = new GameObject("StartButton");
            startGo.transform.SetParent(hudGo.transform);
            var btn = startGo.AddComponent<Button>();
            var cg = startGo.AddComponent<CanvasGroup>();
            cg.alpha = 1f;
            cg.interactable = true;
            cg.blocksRaycasts = true;

            // Minimal Text to satisfy potential lookups
            var label = new GameObject("Label").AddComponent<TextMeshProUGUI>();
            label.transform.SetParent(startGo.transform);

            // Fake controller
            var ctrlGo = new GameObject("FakeCtrl");
            var fake = ctrlGo.AddComponent<FakePlacementController>();
            fake.IsReady = true;

            // Inject fields
            SetPrivate(hud, "_controllerBehaviour", fake);
            SetPrivate(hud, "_startBattleButton", btn);
            SetPrivate(hud, "_startButtonTMP", label);
            SetPrivate(hud, "_startButtonCanvasGroup", cg);
            // Speed up the test fade duration
            SetPrivate(hud, "_startButtonFadeDuration", 0.1f);

            // Lifecycle
            CallPrivate(hud, "Awake");
            CallPrivate(hud, "OnEnable");

            // Ready makes button visible and interactable
            fake.FireReady(true);
            Assert.IsTrue(startGo.activeSelf, "Start button should be active when ready");
            Assert.IsTrue(btn.interactable, "Start button should be interactable when ready");
            Assert.IsFalse(battleHudGo.activeSelf, "Battle HUD should remain inactive during placement");

            // Lock triggers fade, should disable interaction immediately
            fake.ConfirmAndLock();
            Assert.IsFalse(btn.interactable, "Start button must become non-interactable on fade start");
            Assert.IsFalse(cg.blocksRaycasts, "Start button must stop blocking raycasts on fade start");

            // Wait for fade to complete
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.IsFalse(startGo.activeSelf, "Start button should be hidden after fade completes");
            Assert.IsTrue(battleHudGo.activeSelf, "Battle HUD should be activated when placement is locked");

            Object.DestroyImmediate(hudGo);
            Object.DestroyImmediate(battleHudGo);
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
