using System;
using UnityEngine;
using SevenBattles.Core.Battle;

namespace SevenBattles.Core
{
    // Cross-domain contract for turn order so UI can depend on Core only.
    public interface ITurnOrderController
    {
        bool HasActiveUnit { get; }
        bool IsActiveUnitPlayerControlled { get; }
        Sprite ActiveUnitPortrait { get; }

        event Action ActiveUnitChanged;
        event Action ActiveUnitActionPointsChanged;
        event Action ActiveUnitStatsChanged;

        /// <summary>
        /// Current action points available for the active unit.
        /// Returns 0 when there is no active unit.
        /// </summary>
        int ActiveUnitCurrentActionPoints { get; }

        /// <summary>
        /// Maximum action points for the active unit at the start of the turn.
        /// Returns 0 when there is no active unit.
        /// </summary>
        int ActiveUnitMaxActionPoints { get; }

        /// <summary>
        /// Tries to expose the active unit's stats in a UI-friendly snapshot.
        /// Returns false when there is no active unit or stats are not available.
        /// </summary>
        bool TryGetActiveUnitStats(out UnitStatsViewData stats);

        /// <summary>
        /// Spells available to the active unit (data-driven). Returns an empty array when there is no active unit
        /// or the unit has no configured spells.
        /// </summary>
        SpellDefinition[] ActiveUnitSpells { get; }

        void RequestEndTurn();
    }
}
