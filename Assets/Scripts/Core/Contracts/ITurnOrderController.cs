using System;
using UnityEngine;

namespace SevenBattles.Core
{
    // Cross-domain contract for turn order so UI can depend on Core only.
    public interface ITurnOrderController
    {
        bool HasActiveUnit { get; }
        bool IsActiveUnitPlayerControlled { get; }
        Sprite ActiveUnitPortrait { get; }

        event Action ActiveUnitChanged;

        void RequestEndTurn();
    }
}

