using NUnit.Framework;
using SevenBattles.Core.Items;
using SevenBattles.Preparation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SevenBattles.Tests.Preparation
{
    public sealed class UnitInventorySlotFramesViewTests
    {
        [Test]
        public void SetSlotState_LockedAndUnlocked_TogglesLockAndIconVisuals()
        {
            var root = new GameObject("InventoryPanel", typeof(RectTransform));
            BuildObjectSlotFrame(root.transform, "IconFrame_Object1", ConsumableSlotType.Object1, out GameObject lockIcon, out GameObject lockLevelObject, out GameObject iconObject, out ConsumableDropSlotView slotView);

            var framesView = root.AddComponent<UnitInventorySlotFramesView>();
            framesView.Configure(root.transform);
            framesView.BindSlotView(ConsumableSlotType.Object1, slotView);

            framesView.SetSlotState(ConsumableSlotType.Object1, isUnlocked: false, requiredLevel: 10, exists: true);

            Assert.IsTrue(lockIcon.activeSelf);
            Assert.IsTrue(lockLevelObject.activeSelf);
            Assert.IsFalse(iconObject.activeSelf);
            Assert.IsTrue(slotView.IsSlotLocked);
            Assert.AreEqual("10", lockLevelObject.GetComponent<TMP_Text>().text);

            framesView.SetSlotState(ConsumableSlotType.Object1, isUnlocked: true, requiredLevel: 10, exists: true);

            Assert.IsFalse(lockIcon.activeSelf);
            Assert.IsFalse(lockLevelObject.activeSelf);
            Assert.IsTrue(iconObject.activeSelf);
            Assert.IsFalse(slotView.IsSlotLocked);

            Object.DestroyImmediate(root);
        }

        [Test]
        public void SetSlotState_LegacyTextChild_IsResolvedAndHiddenWhenUnlocked()
        {
            var root = new GameObject("InventoryPanel", typeof(RectTransform));
            BuildObjectSlotFrame(
                root.transform,
                "IconFrame_Object4",
                ConsumableSlotType.Object4,
                out GameObject lockIcon,
                out GameObject lockLevelObject,
                out GameObject iconObject,
                out ConsumableDropSlotView slotView,
                "Text");

            var framesView = root.AddComponent<UnitInventorySlotFramesView>();
            framesView.Configure(root.transform);
            framesView.BindSlotView(ConsumableSlotType.Object4, slotView);

            framesView.SetSlotState(ConsumableSlotType.Object4, isUnlocked: false, requiredLevel: 7, exists: true);

            Assert.IsTrue(lockIcon.activeSelf);
            Assert.IsTrue(lockLevelObject.activeSelf);
            Assert.AreEqual("7", lockLevelObject.GetComponent<TMP_Text>().text);

            framesView.SetSlotState(ConsumableSlotType.Object4, isUnlocked: true, requiredLevel: 7, exists: true);

            Assert.IsFalse(lockIcon.activeSelf);
            Assert.IsFalse(lockLevelObject.activeSelf);
            Assert.IsTrue(iconObject.activeSelf);
            Assert.IsFalse(slotView.IsSlotLocked);

            Object.DestroyImmediate(root);
        }

        [Test]
        public void SetSlotState_NonExistentSlot_HidesFrameAndLocksDrop()
        {
            var root = new GameObject("InventoryPanel", typeof(RectTransform));
            BuildObjectSlotFrame(root.transform, "IconFrame_Object3", ConsumableSlotType.Object3, out _, out _, out _, out ConsumableDropSlotView slotView);

            var framesView = root.AddComponent<UnitInventorySlotFramesView>();
            framesView.Configure(root.transform);
            framesView.BindSlotView(ConsumableSlotType.Object3, slotView);

            framesView.SetSlotState(ConsumableSlotType.Object3, isUnlocked: false, requiredLevel: 99, exists: false);

            Transform frame = root.transform.Find("IconFrame_Object3");
            Assert.IsNotNull(frame);
            Assert.IsFalse(frame.gameObject.activeSelf);
            Assert.IsTrue(slotView.IsSlotLocked);

            Object.DestroyImmediate(root);
        }

        private static void BuildObjectSlotFrame(
            Transform parent,
            string frameName,
            ConsumableSlotType slotType,
            out GameObject lockIcon,
            out GameObject lockLevelObject,
            out GameObject iconObject,
            out ConsumableDropSlotView slotView,
            string lockLevelTextObjectName = "LockLevelText")
        {
            var frame = new GameObject(frameName, typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            frame.transform.SetParent(parent, false);

            lockIcon = new GameObject("LockIcon", typeof(RectTransform), typeof(Image));
            lockIcon.transform.SetParent(frame.transform, false);
            lockIcon.SetActive(false);

            lockLevelObject = new GameObject(lockLevelTextObjectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            lockLevelObject.transform.SetParent(frame.transform, false);
            lockLevelObject.SetActive(false);

            iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(frame.transform, false);
            iconObject.SetActive(true);

            slotView = frame.AddComponent<ConsumableDropSlotView>();
            slotView.SetSlotType(slotType);
        }
    }
}
