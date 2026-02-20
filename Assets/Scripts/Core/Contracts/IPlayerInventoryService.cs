using System;
using System.Collections.Generic;
using SevenBattles.Core.Players;

namespace SevenBattles.Core
{
    /// <summary>
    /// Manages player-owned unit collection.
    /// </summary>
    public interface IPlayerInventoryService
    {
        IReadOnlyList<OwnedUnitData> OwnedUnits { get; }

        event Action OwnedUnitsChanged;
        event Action<OwnedUnitData> OwnedUnitAdded;

        void InitializeFromContext();
        bool ContainsOwnedUnit(string ownedUnitId);
        bool TryGetOwnedUnit(string ownedUnitId, out OwnedUnitData ownedUnit);
        bool TryAddOwnedUnit(string unitDefinitionId, int level, int xp, out OwnedUnitData addedUnit);
    }
}
