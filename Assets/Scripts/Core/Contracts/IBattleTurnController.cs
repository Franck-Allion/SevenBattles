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
    }
}
