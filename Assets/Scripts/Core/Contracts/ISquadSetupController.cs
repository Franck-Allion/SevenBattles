using System;
using System.Collections.Generic;
using SevenBattles.Core.Battle;

namespace SevenBattles.Core
{
    public interface ISquadSetupController
    {
        int MaxSquadSize { get; }
        int ActiveSquadCount { get; }
        bool IsSquadFull { get; }
        UnitSpellLoadout SelectedUnit { get; }
        IReadOnlyList<UnitSpellLoadout> AllAvailableUnits { get; }
        IReadOnlyList<UnitSpellLoadout> ActiveSquad { get; }

        bool TryAddToSquad(UnitSpellLoadout loadout);
        bool TryRemoveFromSquad(UnitSpellLoadout loadout);
        void SelectUnit(UnitSpellLoadout loadout);
        string ResolveDisplayName(UnitSpellLoadout loadout);

        event Action<UnitSpellLoadout> UnitAddedToSquad;
        event Action<UnitSpellLoadout> UnitRemovedFromSquad;
        event Action SquadChanged;
        event Action<UnitSpellLoadout> UnitSelected;
    }
}
