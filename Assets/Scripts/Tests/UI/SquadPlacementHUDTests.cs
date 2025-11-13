using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SevenBattles.Core;
using SevenBattles.UI;

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

            // After locking, either the label or the entire HUD root is hidden
            Assert.IsFalse(instructionsGo.activeSelf || hudGo.activeSelf,
                "Instructions should be hidden after placement is locked");

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

