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

        private static void CallPrivate(object target, string methodName)
        {
            var type = target.GetType();
            var method = type.GetMethod(methodName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Method '{methodName}' was not found on type '{type.FullName}'.");
            method.Invoke(target, null);
        }

        private static ConfirmationMessageBoxHUD CreateHud(out GameObject root, out Button confirmButton, out Button cancelButton)
        {
            root = new GameObject("ConfirmationHUD", typeof(RectTransform), typeof(CanvasGroup));
            var dialog = new GameObject("Dialog", typeof(RectTransform));
            dialog.transform.SetParent(root.transform, false);

            var confirmGo = new GameObject("ConfirmButton", typeof(RectTransform), typeof(Image), typeof(Button));
            confirmGo.transform.SetParent(dialog.transform, false);
            confirmButton = confirmGo.GetComponent<Button>();

            var cancelGo = new GameObject("CancelButton", typeof(RectTransform), typeof(Image), typeof(Button));
            cancelGo.transform.SetParent(dialog.transform, false);
            cancelButton = cancelGo.GetComponent<Button>();

            var hud = root.AddComponent<ConfirmationMessageBoxHUD>();
            SetPrivate(hud, "_rootCanvasGroup", root.GetComponent<CanvasGroup>());
            SetPrivate(hud, "_dialogRoot", dialog.GetComponent<RectTransform>());
            SetPrivate(hud, "_confirmButton", confirmButton);
            SetPrivate(hud, "_cancelButton", cancelButton);
            SetPrivate(hud, "_fadeDuration", 0f);
            CallPrivate(hud, "Awake");
            return hud;
        }

        private static object CreateLocalizedString(string tableName, string entryKey)
        {
            var localizedStringType = Type.GetType("UnityEngine.Localization.LocalizedString, Unity.Localization");
            Assert.IsNotNull(localizedStringType, "LocalizedString type was not found. Ensure Unity.Localization is available.");
            return Activator.CreateInstance(localizedStringType, new object[] { tableName, entryKey });
        }

        private static void CallLocalizedShow(ConfirmationMessageBoxHUD hud, object title, object message, object confirmLabel, object cancelLabel, Action onConfirm, Action onCancel)
        {
            var localizedStringType = Type.GetType("UnityEngine.Localization.LocalizedString, Unity.Localization");
            Assert.IsNotNull(localizedStringType, "LocalizedString type was not found. Ensure Unity.Localization is available.");

            var method = typeof(ConfirmationMessageBoxHUD).GetMethod(
                "Show",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public,
                null,
                new[] { localizedStringType, localizedStringType, localizedStringType, localizedStringType, typeof(Action), typeof(Action) },
                null);

            Assert.IsNotNull(method, "Expected ConfirmationMessageBoxHUD.Show(LocalizedString, ...) overload.");
            method.Invoke(hud, new[] { title, message, confirmLabel, cancelLabel, onConfirm, onCancel });
        }

        [Test]
        public void Show_WithNullCancelLabel_HidesCancelButton()
        {
            var hud = CreateHud(out var root, out var confirmButton, out var cancelButton);
            bool acknowledged = false;

            CallLocalizedShow(
                hud,
                CreateLocalizedString("UI.Common", "Confirm.StartBattleRequiresUnitTitle"),
                CreateLocalizedString("UI.Common", "Confirm.StartBattleRequiresUnitMessage"),
                CreateLocalizedString("UI.Common", "Common.OK"),
                null,
                () => acknowledged = true,
                () => { });

            Assert.IsTrue(confirmButton.gameObject.activeSelf);
            Assert.IsFalse(cancelButton.gameObject.activeSelf);

            confirmButton.onClick.Invoke();

            Assert.IsTrue(acknowledged);
            Assert.IsFalse(hud.IsVisible);

            UnityEngine.Object.DestroyImmediate(root);
        }

        [Test]
        public void Show_ReenablesCancelButton_AfterOneButtonDialog()
        {
            var hud = CreateHud(out var root, out var confirmButton, out var cancelButton);

            CallLocalizedShow(
                hud,
                CreateLocalizedString("UI.Common", "Confirm.StartBattleRequiresUnitTitle"),
                CreateLocalizedString("UI.Common", "Confirm.StartBattleRequiresUnitMessage"),
                CreateLocalizedString("UI.Common", "Common.OK"),
                null,
                () => { },
                () => { });
            confirmButton.onClick.Invoke();

            bool cancelled = false;
            CallLocalizedShow(
                hud,
                CreateLocalizedString("UI.Common", "Confirm.StartBattleTitle"),
                CreateLocalizedString("UI.Common", "Confirm.StartBattleMessage"),
                CreateLocalizedString("UI.Common", "Common.Yes"),
                CreateLocalizedString("UI.Common", "Common.No"),
                () => { },
                () => cancelled = true);

            Assert.IsTrue(cancelButton.gameObject.activeSelf);

            cancelButton.onClick.Invoke();
            Assert.IsTrue(cancelled);

            UnityEngine.Object.DestroyImmediate(root);
        }
    }
}
