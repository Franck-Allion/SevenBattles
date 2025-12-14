using System;

namespace SevenBattles.Core
{
    /// <summary>
    /// High-level outcome of a battle.
    /// Used by battle controllers to expose a single source of truth for
    /// victory/defeat so UI and scene flow can react without depending on
    /// battle-domain types.
    /// </summary>
    public enum BattleOutcome
    {
        None = 0,
        PlayerVictory = 1,
        PlayerDefeat = 2
    }
}

