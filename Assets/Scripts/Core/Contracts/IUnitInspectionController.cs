using System;
using UnityEngine;

namespace SevenBattles.Core
{
    /// <summary>
    /// Cross-domain contract for inspecting a non-active unit's stats (e.g., enemies).
    /// UI should depend on this interface rather than Battle-domain types.
    /// </summary>
    public interface IUnitInspectionController
    {
        event Action InspectedUnitChanged;
        event Action InspectedUnitStatsChanged;

        bool HasInspectedEnemy { get; }
        Sprite InspectedEnemyPortrait { get; }

        bool TryGetInspectedEnemyStats(out UnitStatsViewData stats);

        void ClearInspectedEnemy();
    }
}
