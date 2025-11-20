using System;

namespace SevenBattles.Core
{
    public interface IBattleTurnController : ITurnOrderController
    {
        void StartBattle();
        void SetInteractionLocked(bool locked);
        bool IsInteractionLocked { get; }
    }
}

