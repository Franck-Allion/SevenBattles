using System;

namespace SevenBattles.Core
{
    public interface IBattleTurnController : ITurnOrderController
    {
        void StartBattle();
        void SetInteractionLocked(bool locked);
        bool IsInteractionLocked { get; }

        /// <summary>
        /// Current battle turn index (1-based), incremented when the turn order loops
        /// back to the first unit after all valid units have acted. Returns 0 when
        /// there is no active turn (e.g., before battle start or after battle end).
        /// </summary>
        int TurnIndex { get; }

        /// <summary>
        /// Indicates whether the current battle has reached a terminal outcome
        /// (victory or defeat). When true, no further turns should be processed.
        /// </summary>
        bool HasBattleEnded { get; }

        /// <summary>
        /// Final battle outcome once <see cref="HasBattleEnded"/> is true.
        /// Returns <see cref="BattleOutcome.None"/> while the battle is ongoing.
        /// </summary>
        BattleOutcome Outcome { get; }

        /// <summary>
        /// Raised exactly once when the battle reaches a terminal outcome.
        /// </summary>
        event Action<BattleOutcome> BattleEnded;
    }
}
