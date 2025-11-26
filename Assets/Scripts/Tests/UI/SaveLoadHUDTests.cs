using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SevenBattles.Core.Save;
using SevenBattles.UI;

namespace SevenBattles.Tests.UI
{
    public class SaveLoadHUDTests
    {
        private class FakeSaveService : MonoBehaviour, ISaveGameService
        {
            public int MaxSlots => 8;

            public System.Threading.Tasks.Task<SaveSlotMetadata[]> LoadAllSlotMetadataAsync()
            {
                var slots = new SaveSlotMetadata[MaxSlots];
                for (int i = 0; i < MaxSlots; i++)
                {
                    slots[i] = new SaveSlotMetadata(i + 1, false, null, 0);
                }

                return System.Threading.Tasks.Task.FromResult(slots);
            }

            public System.Threading.Tasks.Task<SaveSlotMetadata> SaveSlotAsync(int slotIndex)
            {
                return System.Threading.Tasks.Task.FromResult(new SaveSlotMetadata(slotIndex, true, "2025-01-01 12:00:00", 1));
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
        public void HideImmediate_DisablesCanvasGroup_AndBlocksRaycasts()
        {
            var rootGo = new GameObject("SaveLoadHUD");
            var rect = rootGo.AddComponent<RectTransform>();
            var cg = rootGo.AddComponent<CanvasGroup>();

            var hud = rootGo.AddComponent<SaveLoadHUD>();

            SetPrivate(hud, "_rootCanvasGroup", cg);
            SetPrivate(hud, "_panelRoot", rect);

            CallPrivate(hud, "HideImmediate");

            Assert.AreEqual(0f, cg.alpha, 1e-4f);
            Assert.IsFalse(cg.interactable, "CanvasGroup should not be interactable after HideImmediate.");
            Assert.IsFalse(cg.blocksRaycasts, "CanvasGroup should not block raycasts after HideImmediate.");
            Assert.IsFalse(cg.gameObject.activeSelf, "Root CanvasGroup GameObject should be inactive after HideImmediate.");

            Object.DestroyImmediate(rootGo);
        }

        [Test]
        public void ShowSave_SetsCanvasGroupBlocksRaycasts()
        {
            var rootGo = new GameObject("SaveLoadHUD");
            var rect = rootGo.AddComponent<RectTransform>();
            var cg = rootGo.AddComponent<CanvasGroup>();

            var btnGo = new GameObject("SlotButton");
            btnGo.transform.SetParent(rootGo.transform);
            var btn = btnGo.AddComponent<Button>();
            var label = btnGo.AddComponent<TextMeshProUGUI>();

            var serviceGo = new GameObject("SaveService");
            serviceGo.transform.SetParent(rootGo.transform);
            var fakeService = serviceGo.AddComponent<FakeSaveService>();

            var hud = rootGo.AddComponent<SaveLoadHUD>();

            SetPrivate(hud, "_rootCanvasGroup", cg);
            SetPrivate(hud, "_panelRoot", rect);
            SetPrivate(hud, "_slotButtons", new[] { btn });
            SetPrivate(hud, "_slotLabels", new[] { (TMP_Text)label });
            SetPrivate(hud, "_saveGameServiceBehaviour", fakeService);

            CallPrivate(hud, "Awake");

            hud.ShowSave();

            Assert.IsTrue(cg.blocksRaycasts, "CanvasGroup should block raycasts when SaveLoadHUD is shown.");

            Object.DestroyImmediate(rootGo);
        }
    }
}
