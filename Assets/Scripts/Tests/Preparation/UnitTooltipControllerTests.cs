using NUnit.Framework;
using SevenBattles.Core.Battle;
using SevenBattles.Core.Units;
using SevenBattles.Preparation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenBattles.Tests.Preparation
{
    public class UnitTooltipControllerTests
    {
        private static void SetPrivate(object target, string fieldName, object value)
        {
            var type = target.GetType();
            var field = type.GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' was not found on type '{type.FullName}'.");
            field.SetValue(target, value);
        }

        [Test]
        public void ResolveFor_SameCanvas_ReturnsSharedController()
        {
            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            UnitTooltipController first = UnitTooltipController.ResolveFor(canvasGo.transform);
            UnitTooltipController second = UnitTooltipController.ResolveFor(canvasGo.transform);

            Assert.IsNotNull(first);
            Assert.AreSame(first, second);

            Object.DestroyImmediate(canvasGo);
        }

        [Test]
        public void PortraitHandler_PointerEnterAndExit_ShowsAndHidesTooltip()
        {
            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            UnitTooltipController controller = UnitTooltipController.ResolveFor(canvasGo.transform);
            Assert.IsNotNull(controller);

            var portraitGo = new GameObject("Portrait", typeof(RectTransform), typeof(UnitPortraitView));
            portraitGo.transform.SetParent(canvasGo.transform, false);

            var view = portraitGo.GetComponent<UnitPortraitView>();
            var handler = portraitGo.GetComponent<UnitPortraitTooltipHandler>();
            Assert.IsNotNull(handler);
            handler.SetTooltipController(controller);

            UnitDefinition definition = ScriptableObject.CreateInstance<UnitDefinition>();
            definition.Id = "unit_test";
            definition.name = "Sentinel";
            var loadout = new UnitSpellLoadout { Definition = definition };
            view.Bind(loadout, "Nova");

            handler.OnPointerEnter(null);
            Assert.IsTrue(controller.IsVisible);
            Assert.AreSame(handler, controller.CurrentOwner);

            handler.OnPointerExit(null);
            Assert.IsFalse(controller.IsVisible);
            Assert.IsNull(controller.CurrentOwner);

            Object.DestroyImmediate(definition);
            Object.DestroyImmediate(canvasGo);
        }

        [Test]
        public void SetCursorOffset_UpdatesOffsetXAndY()
        {
            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            UnitTooltipController controller = UnitTooltipController.ResolveFor(canvasGo.transform);
            Assert.IsNotNull(controller);

            controller.SetCursorOffset(42f, -12f);

            Assert.AreEqual(42f, controller.CursorOffsetX);
            Assert.AreEqual(-12f, controller.CursorOffsetY);

            Object.DestroyImmediate(canvasGo);
        }

        [Test]
        public void ShowDelaySeconds_CanBeConfiguredAndClampsToZero()
        {
            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            UnitTooltipController controller = UnitTooltipController.ResolveFor(canvasGo.transform);
            Assert.IsNotNull(controller);

            controller.SetShowDelaySeconds(0.35f);
            Assert.AreEqual(0.35f, controller.ShowDelaySeconds);

            controller.ShowDelaySeconds = -1f;
            Assert.AreEqual(0f, controller.ShowDelaySeconds);

            Object.DestroyImmediate(canvasGo);
        }

        [Test]
        public void UnitTooltipView_SetText_RespectsMinimumDimensions()
        {
            var root = new GameObject("Tooltip", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(UnitTooltipView));
            var labelRoot = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelRoot.transform.SetParent(root.transform, false);

            RectTransform rootRect = root.GetComponent<RectTransform>();
            CanvasGroup canvasGroup = root.GetComponent<CanvasGroup>();
            Image background = root.GetComponent<Image>();
            TMP_Text label = labelRoot.GetComponent<TextMeshProUGUI>();
            UnitTooltipView view = root.GetComponent<UnitTooltipView>();
            view.SetRuntimeReferences(rootRect, canvasGroup, background, label);

            SetPrivate(view, "_minWidth", 220f);
            SetPrivate(view, "_minHeight", 72f);
            SetPrivate(view, "_maxWidth", 420f);
            SetPrivate(view, "_padding", Vector2.zero);

            view.SetText("A");

            Assert.GreaterOrEqual(rootRect.rect.width, 220f);
            Assert.GreaterOrEqual(rootRect.rect.height, 72f);

            Object.DestroyImmediate(root);
        }
    }
}
