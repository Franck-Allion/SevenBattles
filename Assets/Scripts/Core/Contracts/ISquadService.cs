using System;
using System.Collections.Generic;
using SevenBattles.Core.Players;

namespace SevenBattles.Core
{
    /// <summary>
    /// Manages active squad as a subset of owned units.
    /// </summary>
    public interface ISquadService
    {
        int MaxSquadSize { get; }
        int ActiveSquadCount { get; }
        bool IsSquadFull { get; }
        IReadOnlyList<OwnedUnitData> ActiveSquad { get; }
        IReadOnlyList<OwnedUnitData> AvailableUnits { get; }

        event Action SquadChanged;
        event Action<OwnedUnitData> UnitAddedToSquad;
        event Action<OwnedUnitData> UnitRemovedFromSquad;
        event Action<OwnedUnitData> UnitSelected;

        void InitializeFromContext();
        bool TryAddToSquad(string ownedUnitId);
        bool TryRemoveFromSquad(string ownedUnitId);
        void SelectUnit(string ownedUnitId);
    }
}
