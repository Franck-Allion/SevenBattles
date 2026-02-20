using System;
using System.Collections.Generic;
using SevenBattles.Core;

namespace SevenBattles.Core.Players
{
    /// <summary>
    /// Runtime service that manages the active squad as a subset of owned units.
    /// </summary>
    public sealed class SquadService : ISquadService
    {
        private readonly PlayerContext _playerContext;
        private readonly IPlayerInventoryService _inventoryService;

        private readonly List<OwnedUnitData> _activeSquadCache = new List<OwnedUnitData>();
        private readonly List<OwnedUnitData> _availableUnitsCache = new List<OwnedUnitData>();
        private readonly Dictionary<string, OwnedUnitData> _ownedById = new Dictionary<string, OwnedUnitData>(StringComparer.Ordinal);
        private readonly HashSet<string> _activeIdSet = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> _sanitizedActiveIdsBuffer = new List<string>();
        private bool _cacheDirty = true;

        public SquadService(PlayerContext playerContext, IPlayerInventoryService inventoryService)
        {
            _playerContext = playerContext;
            _inventoryService = inventoryService;
            if (_inventoryService != null)
            {
                _inventoryService.OwnedUnitsChanged += HandleOwnedUnitsChanged;
            }
        }

        public int MaxSquadSize => _playerContext != null ? _playerContext.MaxSquadSize : 1;
        public int ActiveSquadCount
        {
            get
            {
                EnsureCaches();
                return _activeSquadCache.Count;
            }
        }

        public bool IsSquadFull => ActiveSquadCount >= MaxSquadSize;
        public IReadOnlyList<OwnedUnitData> ActiveSquad
        {
            get
            {
                EnsureCaches();
                return _activeSquadCache;
            }
        }

        public IReadOnlyList<OwnedUnitData> AvailableUnits
        {
            get
            {
                EnsureCaches();
                return _availableUnitsCache;
            }
        }

        public event Action SquadChanged;
        public event Action<OwnedUnitData> UnitAddedToSquad;
        public event Action<OwnedUnitData> UnitRemovedFromSquad;
        public event Action<OwnedUnitData> UnitSelected;

        public void InitializeFromContext()
        {
            _inventoryService?.InitializeFromContext();
            _cacheDirty = true;
            EnsureCaches();
        }

        public bool TryAddToSquad(string ownedUnitId)
        {
            EnsureCaches();
            if (string.IsNullOrWhiteSpace(ownedUnitId) || IsSquadFull || _activeIdSet.Contains(ownedUnitId))
            {
                return false;
            }

            if (!_ownedById.TryGetValue(ownedUnitId, out OwnedUnitData ownedUnit) || ownedUnit == null)
            {
                return false;
            }

            var nextIds = new List<string>(_sanitizedActiveIdsBuffer.Count + 1);
            for (int i = 0; i < _sanitizedActiveIdsBuffer.Count; i++)
            {
                nextIds.Add(_sanitizedActiveIdsBuffer[i]);
            }
            nextIds.Add(ownedUnitId);

            _playerContext.SetActiveSquadOwnedUnitIds(nextIds);
            _cacheDirty = true;
            EnsureCaches();

            UnitAddedToSquad?.Invoke(ownedUnit);
            SquadChanged?.Invoke();
            return true;
        }

        public bool TryRemoveFromSquad(string ownedUnitId)
        {
            EnsureCaches();
            if (string.IsNullOrWhiteSpace(ownedUnitId) || !_activeIdSet.Contains(ownedUnitId))
            {
                return false;
            }

            var nextIds = new List<string>(_sanitizedActiveIdsBuffer.Count);
            for (int i = 0; i < _sanitizedActiveIdsBuffer.Count; i++)
            {
                string id = _sanitizedActiveIdsBuffer[i];
                if (!string.Equals(id, ownedUnitId, StringComparison.Ordinal))
                {
                    nextIds.Add(id);
                }
            }

            _playerContext.SetActiveSquadOwnedUnitIds(nextIds);
            _cacheDirty = true;
            EnsureCaches();

            if (_ownedById.TryGetValue(ownedUnitId, out OwnedUnitData ownedUnit))
            {
                UnitRemovedFromSquad?.Invoke(ownedUnit);
            }
            SquadChanged?.Invoke();
            return true;
        }

        public void SelectUnit(string ownedUnitId)
        {
            EnsureCaches();
            if (string.IsNullOrWhiteSpace(ownedUnitId))
            {
                UnitSelected?.Invoke(null);
                return;
            }

            _ownedById.TryGetValue(ownedUnitId, out OwnedUnitData ownedUnit);
            UnitSelected?.Invoke(ownedUnit);
        }

        private void EnsureCaches()
        {
            if (!_cacheDirty)
            {
                return;
            }

            _ownedById.Clear();
            _activeIdSet.Clear();
            _activeSquadCache.Clear();
            _availableUnitsCache.Clear();
            _sanitizedActiveIdsBuffer.Clear();

            if (_playerContext == null)
            {
                _cacheDirty = false;
                return;
            }

            IReadOnlyList<OwnedUnitData> ownedUnits = _inventoryService != null ? _inventoryService.OwnedUnits : _playerContext.OwnedUnits;
            for (int i = 0; i < ownedUnits.Count; i++)
            {
                OwnedUnitData unit = ownedUnits[i];
                if (unit == null || unit.Definition == null || string.IsNullOrWhiteSpace(unit.OwnedUnitId))
                {
                    continue;
                }

                if (!_ownedById.ContainsKey(unit.OwnedUnitId))
                {
                    _ownedById.Add(unit.OwnedUnitId, unit);
                }
            }

            IReadOnlyList<string> activeIds = _playerContext.ActiveSquadOwnedUnitIds;
            bool activeListChanged = false;
            for (int i = 0; i < activeIds.Count && _sanitizedActiveIdsBuffer.Count < MaxSquadSize; i++)
            {
                string id = activeIds[i];
                if (string.IsNullOrWhiteSpace(id) || !_ownedById.ContainsKey(id) || !_activeIdSet.Add(id))
                {
                    activeListChanged = true;
                    continue;
                }

                _sanitizedActiveIdsBuffer.Add(id);
            }

            if (!activeListChanged && activeIds.Count != _sanitizedActiveIdsBuffer.Count)
            {
                activeListChanged = true;
            }

            if (activeListChanged)
            {
                _playerContext.SetActiveSquadOwnedUnitIds(_sanitizedActiveIdsBuffer);
            }

            for (int i = 0; i < _sanitizedActiveIdsBuffer.Count; i++)
            {
                string id = _sanitizedActiveIdsBuffer[i];
                if (_ownedById.TryGetValue(id, out OwnedUnitData unit))
                {
                    _activeSquadCache.Add(unit);
                }
            }

            for (int i = 0; i < ownedUnits.Count; i++)
            {
                OwnedUnitData unit = ownedUnits[i];
                if (unit == null || string.IsNullOrWhiteSpace(unit.OwnedUnitId))
                {
                    continue;
                }

                if (!_activeIdSet.Contains(unit.OwnedUnitId))
                {
                    _availableUnitsCache.Add(unit);
                }
            }

            _cacheDirty = false;
        }

        private void HandleOwnedUnitsChanged()
        {
            _cacheDirty = true;
            EnsureCaches();
            SquadChanged?.Invoke();
        }
    }
}
