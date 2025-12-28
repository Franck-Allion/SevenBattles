using System;
using UnityEngine;

namespace SevenBattles.Core
{
    // Cross-domain contract for squad placement so UI can depend on Core only.
    public interface ISquadPlacementController
    {
        int SquadSize { get; }
        bool IsReady { get; }
        bool IsLocked { get; }

        bool IsPlaced(int index);
        Sprite GetPortrait(int index);
        int GetLevel(int index);

        void SelectWizard(int index);
        void ConfirmAndLock();

        event Action<int> WizardSelected;
        event Action<int> WizardPlaced;
        event Action<int> WizardRemoved;
        event Action<bool> ReadyChanged;
        event Action PlacementLocked;
    }
}
