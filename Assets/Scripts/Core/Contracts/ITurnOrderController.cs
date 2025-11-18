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

        /// <summary>
        /// Tries to expose the active unit's stats in a UI-friendly snapshot.
        /// Returns false when there is no active unit or stats are not available.
        /// </summary>
        bool TryGetActiveUnitStats(out UnitStatsViewData stats);

        void RequestEndTurn();
    }
}
