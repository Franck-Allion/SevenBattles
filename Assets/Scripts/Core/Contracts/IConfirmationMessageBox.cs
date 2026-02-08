using System;
using UnityEngine.Localization;

namespace SevenBattles.Core.Contracts
{
    public interface IConfirmationMessageBox
    {
        bool IsVisible { get; }

        void Show(LocalizedString title, LocalizedString message, LocalizedString confirmLabel, LocalizedString cancelLabel, Action onConfirm, Action onCancel = null);

        void Cancel();
    }
}
