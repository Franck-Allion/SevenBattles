using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using SevenBattles.UI;

namespace SevenBattles.Tests.UI
{
    public class ConfirmationMessageBoxHUDTests
    {
        private static void SetPrivate(object target, string fieldName, object value)
        {
            var type = target.GetType();
            var field = type.GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' was not found on type '{type.FullName}'.");
            field.SetValue(target, value);
        }

        [Test]
        public void Show_AndConfirm_InvokesCallback_AndHides()
        {
            var rootGo = new GameObject("ConfirmRoot");
            var rect = rootGo.AddComponent<RectTransform>();
            var cg = rootGo.AddComponent<CanvasGroup>();

            var confirmBtnGo = new GameObject("ConfirmButton");
            confirmBtnGo.transform.SetParent(rootGo.transform);
            var confirmBtn = confirmBtnGo.AddComponent<Button>();

            var cancelBtnGo = new GameObject("CancelButton");
            cancelBtnGo.transform.SetParent(rootGo.transform);
            var cancelBtn = cancelBtnGo.AddComponent<Button>();

            var hud = rootGo.AddComponent<ConfirmationMessageBoxHUD>();

            SetPrivate(hud, "_rootCanvasGroup", cg);
            SetPrivate(hud, "_dialogRoot", rect);
            SetPrivate(hud, "_confirmButton", confirmBtn);
            SetPrivate(hud, "_cancelButton", cancelBtn);
            SetPrivate(hud, "_fadeDuration", 0f);

            var confirmed = false;
            var cancelled = false;

            hud.Show(() => confirmed = true, () => cancelled = true);

            Assert.IsTrue(cg.gameObject.activeSelf, "Confirmation dialog should be active after Show.");
            Assert.IsTrue(hud.IsVisible, "IsVisible should be true after Show.");

            confirmBtn.onClick.Invoke();

            Assert.IsTrue(confirmed, "Confirm callback should be invoked when confirm button is pressed.");
            Assert.IsFalse(cancelled, "Cancel callback should not be invoked when confirm button is pressed.");
            Assert.IsFalse(hud.IsVisible, "Dialog should hide after confirm.");
            Assert.IsFalse(cg.gameObject.activeSelf, "Root should be inactive after hide.");

            UnityEngine.Object.DestroyImmediate(rootGo);
        }

        [Test]
        public void Show_AndCancel_InvokesCancelCallback_AndHides()
        {
            var rootGo = new GameObject("ConfirmRoot");
            var rect = rootGo.AddComponent<RectTransform>();
            var cg = rootGo.AddComponent<CanvasGroup>();

            var confirmBtnGo = new GameObject("ConfirmButton");
            confirmBtnGo.transform.SetParent(rootGo.transform);
            var confirmBtn = confirmBtnGo.AddComponent<Button>();

            var cancelBtnGo = new GameObject("CancelButton");
            cancelBtnGo.transform.SetParent(rootGo.transform);
            var cancelBtn = cancelBtnGo.AddComponent<Button>();

            var hud = rootGo.AddComponent<ConfirmationMessageBoxHUD>();

            SetPrivate(hud, "_rootCanvasGroup", cg);
            SetPrivate(hud, "_dialogRoot", rect);
            SetPrivate(hud, "_confirmButton", confirmBtn);
            SetPrivate(hud, "_cancelButton", cancelBtn);
            SetPrivate(hud, "_fadeDuration", 0f);

            var confirmed = false;
            var cancelled = false;

            hud.Show(() => confirmed = true, () => cancelled = true);

            cancelBtn.onClick.Invoke();

            Assert.IsFalse(confirmed, "Confirm callback should not be invoked when cancel button is pressed.");
            Assert.IsTrue(cancelled, "Cancel callback should be invoked when cancel button is pressed.");
            Assert.IsFalse(hud.IsVisible, "Dialog should hide after cancel.");
            Assert.IsFalse(cg.gameObject.activeSelf, "Root should be inactive after hide.");

            UnityEngine.Object.DestroyImmediate(rootGo);
        }
    }
}

