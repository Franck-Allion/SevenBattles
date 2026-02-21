using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using SevenBattles.Preparation;

namespace SevenBattles.Tests.Preparation
{
    public class PreparationPopupMenuLocalizationControllerTests
    {
        private static void CallPrivate(object target, string methodName)
        {
            var type = target.GetType();
            var method = type.GetMethod(methodName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Method '{methodName}' was not found on type '{type.FullName}'.");
            method.Invoke(target, null);
        }

        private static T GetPrivate<T>(object target, string fieldName)
        {
            var type = target.GetType();
            var field = type.GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' was not found on type '{type.FullName}'.");
            return (T)field.GetValue(target);
        }

        private static void SetPrivate(object target, string fieldName, object value)
        {
            var type = target.GetType();
            var field = type.GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' was not found on type '{type.FullName}'.");
            field.SetValue(target, value);
        }

        private static GameObject CreateButton(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static bool HasHoverForwarder(GameObject go)
        {
            var components = go.GetComponents<MonoBehaviour>();
            for (int i = 0; i < components.Length; i++)
            {
                var component = components[i];
                if (component != null && component.GetType().Name == "MenuButtonHoverForwarder")
                {
                    return true;
                }
            }

            return false;
        }

        [Test]
        public void WireHoverFeedback_AutoFindsShopAndSquadButtons_AndAddsForwarders()
        {
            var root = new GameObject("PopupMenu");
            var controller = root.AddComponent<PreparationPopupMenuLocalizationController>();
            var shopButton = CreateButton(root.transform, "ShopButtonMenu");
            var squadButton = CreateButton(root.transform, "SquadButtonMenu");

            CallPrivate(controller, "ResolveButtonTargets");
            CallPrivate(controller, "WireHoverFeedback");

            Assert.IsTrue(HasHoverForwarder(shopButton));
            Assert.IsTrue(HasHoverForwarder(squadButton));

            Object.DestroyImmediate(root);
        }

        [Test]
        public void HandleMenuButtonPointerExit_NeverDropsHoveredCountBelowZero()
        {
            var root = new GameObject("PopupMenu");
            var controller = root.AddComponent<PreparationPopupMenuLocalizationController>();

            CallPrivate(controller, "HandleMenuButtonPointerEnter");
            CallPrivate(controller, "HandleMenuButtonPointerExit");
            CallPrivate(controller, "HandleMenuButtonPointerExit");

            int hoveredCount = GetPrivate<int>(controller, "_hoveredButtonCount");
            Assert.AreEqual(0, hoveredCount);

            Object.DestroyImmediate(root);
        }

        [Test]
        public void ButtonClick_PlaysConfiguredSfxPath()
        {
            var root = new GameObject("PopupMenu");
            var controller = root.AddComponent<PreparationPopupMenuLocalizationController>();
            var shopButtonGo = CreateButton(root.transform, "ShopButtonMenu");
            CreateButton(root.transform, "SquadButtonMenu");

            var clickClip = AudioClip.Create("TestClick", 64, 1, 44100, false);
            SetPrivate(controller, "_clickSfxClip", clickClip);

            CallPrivate(controller, "ResolveButtonTargets");
            CallPrivate(controller, "WireHoverFeedback");

            float before = GetPrivate<float>(controller, "_lastClickSfxTime");
            shopButtonGo.GetComponent<Button>().onClick.Invoke();
            float after = GetPrivate<float>(controller, "_lastClickSfxTime");

            Assert.Greater(after, before);

            Object.DestroyImmediate(clickClip);
            Object.DestroyImmediate(root);
        }

        [Test]
        public void SquadButtonClick_ShowsSquadPanel_AndEnablesPanelInteraction()
        {
            var root = new GameObject("PopupMenu");
            var controller = root.AddComponent<PreparationPopupMenuLocalizationController>();
            var squadButtonGo = CreateButton(root.transform, "SquadButtonMenu");
            var squadPanel = new GameObject("SquadPanel", typeof(RectTransform));
            squadPanel.transform.SetParent(root.transform, false);
            squadPanel.SetActive(false);

            SetPrivate(controller, "_squadPanelFadeDuration", 0f);

            CallPrivate(controller, "ResolveButtonTargets");
            CallPrivate(controller, "ResolveSquadPanel");
            CallPrivate(controller, "WireSquadPanelButton");

            squadButtonGo.GetComponent<Button>().onClick.Invoke();

            Assert.IsTrue(squadPanel.activeSelf);

            var canvasGroup = squadPanel.GetComponent<CanvasGroup>();
            Assert.IsNotNull(canvasGroup);
            Assert.AreEqual(1f, canvasGroup.alpha, 0.001f);
            Assert.IsTrue(canvasGroup.interactable);
            Assert.IsTrue(canvasGroup.blocksRaycasts);

            Object.DestroyImmediate(root);
        }

        [Test]
        public void ResolveSquadPanel_ForceHidesInitiallyActivePanel_UntilButtonClick()
        {
            var root = new GameObject("PopupMenu");
            var controller = root.AddComponent<PreparationPopupMenuLocalizationController>();
            var squadButtonGo = CreateButton(root.transform, "SquadButtonMenu");
            var squadPanel = new GameObject("SquadPanel", typeof(RectTransform));
            squadPanel.transform.SetParent(root.transform, false);
            squadPanel.SetActive(true);

            CallPrivate(controller, "ResolveButtonTargets");
            CallPrivate(controller, "ResolveSquadPanel");
            CallPrivate(controller, "WireSquadPanelButton");

            var canvasGroup = squadPanel.GetComponent<CanvasGroup>();
            Assert.IsNotNull(canvasGroup);
            Assert.IsFalse(squadPanel.activeSelf);
            Assert.AreEqual(0f, canvasGroup.alpha, 0.001f);
            Assert.IsFalse(canvasGroup.interactable);
            Assert.IsFalse(canvasGroup.blocksRaycasts);

            SetPrivate(controller, "_squadPanelFadeDuration", 0f);
            squadButtonGo.GetComponent<Button>().onClick.Invoke();

            Assert.IsTrue(squadPanel.activeSelf);
            Assert.AreEqual(1f, canvasGroup.alpha, 0.001f);
            Assert.IsTrue(canvasGroup.interactable);
            Assert.IsTrue(canvasGroup.blocksRaycasts);

            Object.DestroyImmediate(root);
        }

        [Test]
        public void BackButtonClick_HidesSquadPanel_AndDisablesPanelInteraction()
        {
            var root = new GameObject("PopupMenu");
            var controller = root.AddComponent<PreparationPopupMenuLocalizationController>();
            var squadButtonGo = CreateButton(root.transform, "SquadButtonMenu");
            var squadPanel = new GameObject("SquadPanel", typeof(RectTransform));
            squadPanel.transform.SetParent(root.transform, false);
            squadPanel.SetActive(true);
            var backButtonGo = CreateButton(squadPanel.transform, "Button_Back");

            SetPrivate(controller, "_squadPanelFadeDuration", 0f);

            CallPrivate(controller, "ResolveButtonTargets");
            CallPrivate(controller, "ResolveSquadPanel");
            CallPrivate(controller, "WireSquadPanelButton");

            squadButtonGo.GetComponent<Button>().onClick.Invoke();
            Assert.IsTrue(squadPanel.activeSelf);

            backButtonGo.GetComponent<Button>().onClick.Invoke();

            var canvasGroup = squadPanel.GetComponent<CanvasGroup>();
            Assert.IsNotNull(canvasGroup);
            Assert.IsFalse(squadPanel.activeSelf);
            Assert.AreEqual(0f, canvasGroup.alpha, 0.001f);
            Assert.IsFalse(canvasGroup.interactable);
            Assert.IsFalse(canvasGroup.blocksRaycasts);

            Object.DestroyImmediate(root);
        }
    }
}
