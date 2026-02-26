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

        [Test]
        public void InventoryItemHover_AfterDelay_ShowsTooltipAndExitHidesIt()
        {
            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            UnitTooltipController controller = UnitTooltipController.ResolveFor(canvasGo.transform);
            Assert.IsNotNull(controller);
            controller.SetShowDelaySeconds(0f);

            var itemGo = new GameObject(
                "Item",
                typeof(RectTransform),
                typeof(Image),
                typeof(PreparationInventoryItemEntryView));
            itemGo.transform.SetParent(canvasGo.transform, false);

            var handler = itemGo.GetComponent<PreparationInventoryItemTooltipHandler>();
            Assert.IsNotNull(handler);

            handler.SetTooltipController(controller);
            handler.SetHoverDelaySeconds(1f);
            handler.SetTooltipText("Potion");

            handler.OnPointerEnter(null);
            Assert.IsFalse(controller.IsVisible, "Tooltip should remain hidden until delay elapses.");

            SetPrivate(handler, "_pendingShowTime", Time.unscaledTime - 0.01f);
            itemGo.SendMessage("Update");
            Assert.IsTrue(controller.IsVisible, "Tooltip should appear after hover delay.");
            Assert.AreSame(handler, controller.CurrentOwner);

            handler.OnPointerExit(null);
            Assert.IsFalse(controller.IsVisible, "Tooltip should hide on pointer exit.");
            Assert.IsNull(controller.CurrentOwner);

            Object.DestroyImmediate(canvasGo);
        }

        [Test]
        public void Show_BringsTooltipControllerToFrontInCanvasHierarchy()
        {
            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            UnitTooltipController controller = UnitTooltipController.ResolveFor(canvasGo.transform);
            Assert.IsNotNull(controller);

            var overlay = new GameObject("Overlay", typeof(RectTransform), typeof(Image));
            overlay.transform.SetParent(canvasGo.transform, false);
            overlay.transform.SetAsLastSibling();

            Assert.AreNotEqual(
                canvasGo.transform.childCount - 1,
                controller.transform.GetSiblingIndex(),
                "Precondition failed: controller should not already be top-most.");

            controller.Show("Potion", overlay);

            Assert.AreEqual(canvasGo.transform.childCount - 1, controller.transform.GetSiblingIndex());

            Object.DestroyImmediate(canvasGo);
        }

        [Test]
        public void InventoryItemHover_ExitIntoChild_DoesNotCancelDelayedTooltip()
        {
            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            UnitTooltipController controller = UnitTooltipController.ResolveFor(canvasGo.transform);
            Assert.IsNotNull(controller);
            controller.SetShowDelaySeconds(0f);

            var itemGo = new GameObject(
                "Item",
                typeof(RectTransform),
                typeof(Image),
                typeof(PreparationInventoryItemEntryView));
            itemGo.transform.SetParent(canvasGo.transform, false);

            var childGo = new GameObject("ItemIcon", typeof(RectTransform), typeof(Image));
            childGo.transform.SetParent(itemGo.transform, false);

            var handler = itemGo.GetComponent<PreparationInventoryItemTooltipHandler>();
            Assert.IsNotNull(handler);
            handler.SetTooltipController(controller);
            handler.SetHoverDelaySeconds(1f);
            handler.SetTooltipText("Potion");

            handler.OnPointerEnter(null);

            var exitEvent = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
            {
                pointerCurrentRaycast = new UnityEngine.EventSystems.RaycastResult { gameObject = childGo }
            };
            handler.OnPointerExit(exitEvent);

            SetPrivate(handler, "_pendingShowTime", Time.unscaledTime - 0.01f);
            itemGo.SendMessage("Update");

            Assert.IsTrue(controller.IsVisible, "Tooltip should still show when exit target is a child of the same item.");
            Assert.AreSame(handler, controller.CurrentOwner);

            Object.DestroyImmediate(canvasGo);
        }

        [Test]
        public void ResolveFor_ContextWithoutCanvas_ReturnsNull()
        {
            var orphan = new GameObject("Orphan", typeof(RectTransform));

            UnitTooltipController controller = UnitTooltipController.ResolveFor(orphan.transform);

            Assert.IsNull(controller);

            Object.DestroyImmediate(orphan);
        }

        [Test]
        public void InventoryItemTooltip_AppliesAndRestoresCustomCursorOffset()
        {
            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            UnitTooltipController controller = UnitTooltipController.ResolveFor(canvasGo.transform);
            Assert.IsNotNull(controller);
            controller.SetCursorOffset(10f, -8f);
            controller.SetShowDelaySeconds(0f);

            var itemGo = new GameObject(
                "Item",
                typeof(RectTransform),
                typeof(Image),
                typeof(PreparationInventoryItemEntryView));
            itemGo.transform.SetParent(canvasGo.transform, false);

            var handler = itemGo.GetComponent<PreparationInventoryItemTooltipHandler>();
            Assert.IsNotNull(handler);
            handler.SetTooltipController(controller);
            handler.SetHoverDelaySeconds(0f);
            handler.SetTooltipText("Potion");

            SetPrivate(handler, "_overrideTooltipCursorOffset", true);
            SetPrivate(handler, "_tooltipCursorOffset", new Vector2(44f, -36f));

            handler.OnPointerEnter(null);
            Assert.IsTrue(controller.IsVisible);
            Assert.AreEqual(44f, controller.CursorOffsetX, 0.001f);
            Assert.AreEqual(-36f, controller.CursorOffsetY, 0.001f);

            handler.OnPointerExit(null);
            Assert.IsFalse(controller.IsVisible);
            Assert.AreEqual(10f, controller.CursorOffsetX, 0.001f);
            Assert.AreEqual(-8f, controller.CursorOffsetY, 0.001f);

            Object.DestroyImmediate(canvasGo);
        }
    }
}
