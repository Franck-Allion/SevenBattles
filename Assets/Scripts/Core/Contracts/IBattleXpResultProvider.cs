using System;
using SevenBattles.Core.Battle;

namespace SevenBattles.Core.Contracts
{
    public interface IBattleXpResultProvider
    {
        bool HasAwardedXp { get; }
        BattleXpAwardResult LastResult { get; }
        event Action<BattleXpAwardResult> XpAwarded;
    }
}

