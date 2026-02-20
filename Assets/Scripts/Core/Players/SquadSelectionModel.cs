using System;
using System.Collections.Generic;
using SevenBattles.Core.Battle;

namespace SevenBattles.Core.Players
{
    /// <summary>
    /// Pure C# model for squad selection rules and active squad composition.
    /// </summary>
    public sealed class SquadSelectionModel
    {
        private readonly List<UnitSpellLoadout> _allOwned;
        private readonly List<UnitSpellLoadout> _activeSquad;
        private readonly int _maxSize;

        public SquadSelectionModel(int maxSize, List<UnitSpellLoadout> allOwnedUnits, List<UnitSpellLoadout> initialSquad)
        {
            _maxSize = System.Math.Max(1, maxSize);
            _allOwned = allOwnedUnits != null ? new List<UnitSpellLoadout>(allOwnedUnits) : new List<UnitSpellLoadout>();
            _activeSquad = new List<UnitSpellLoadout>();

            if (initialSquad == null)
            {
                return;
            }

            for (int i = 0; i < initialSquad.Count; i++)
            {
                UnitSpellLoadout loadout = initialSquad[i];
                if (loadout == null || _activeSquad.Count >= _maxSize)
                {
                    continue;
                }

                if (!ContainsReference(_allOwned, loadout) || ContainsReference(_activeSquad, loadout))
                {
                    continue;
                }

                _activeSquad.Add(loadout);
            }
        }

        public bool IsFull => _activeSquad.Count >= _maxSize;

        public event Action<UnitSpellLoadout> Added;
        public event Action<UnitSpellLoadout> Removed;
        public event Action Changed;

        public bool TryAdd(UnitSpellLoadout loadout)
        {
            if (loadout == null || IsFull)
            {
                return false;
            }

            if (!ContainsReference(_allOwned, loadout) || ContainsReference(_activeSquad, loadout))
            {
                return false;
            }

            _activeSquad.Add(loadout);
            Added?.Invoke(loadout);
            Changed?.Invoke();
            return true;
        }

        public bool TryRemove(UnitSpellLoadout loadout)
        {
            int index = IndexOfReference(_activeSquad, loadout);
            if (index < 0)
            {
                return false;
            }

            UnitSpellLoadout removed = _activeSquad[index];
            _activeSquad.RemoveAt(index);
            Removed?.Invoke(removed);
            Changed?.Invoke();
            return true;
        }

        public IReadOnlyList<UnitSpellLoadout> GetAvailable()
        {
            var available = new List<UnitSpellLoadout>(_allOwned.Count);
            for (int i = 0; i < _allOwned.Count; i++)
            {
                UnitSpellLoadout candidate = _allOwned[i];
                if (!ContainsReference(_activeSquad, candidate))
                {
                    available.Add(candidate);
                }
            }

            return available;
        }

        public IReadOnlyList<UnitSpellLoadout> GetActive()
        {
            return _activeSquad;
        }

        public bool Contains(UnitSpellLoadout loadout)
        {
            return ContainsReference(_activeSquad, loadout);
        }

        private static bool ContainsReference(List<UnitSpellLoadout> list, UnitSpellLoadout loadout)
        {
            return IndexOfReference(list, loadout) >= 0;
        }

        private static int IndexOfReference(List<UnitSpellLoadout> list, UnitSpellLoadout loadout)
        {
            if (list == null || loadout == null)
            {
                return -1;
            }

            for (int i = 0; i < list.Count; i++)
            {
                if (ReferenceEquals(list[i], loadout))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
