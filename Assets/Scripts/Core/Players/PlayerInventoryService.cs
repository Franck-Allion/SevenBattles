using System;
using System.Collections.Generic;
using SevenBattles.Core;
using SevenBattles.Core.Battle;
using SevenBattles.Core.Units;
using UnityEngine;

namespace SevenBattles.Core.Players
{
    /// <summary>
    /// Runtime inventory service for player-owned units.
    /// </summary>
    public sealed class PlayerInventoryService : IPlayerInventoryService
    {
        private readonly PlayerContext _playerContext;
        private readonly IUnitCatalog _unitCatalog;

        public PlayerInventoryService(PlayerContext playerContext, IUnitCatalog unitCatalog)
        {
            _playerContext = playerContext;
            _unitCatalog = unitCatalog;
        }

        public IReadOnlyList<OwnedUnitData> OwnedUnits =>
            _playerContext != null ? _playerContext.OwnedUnits : Array.Empty<OwnedUnitData>();

        public event Action OwnedUnitsChanged;
        public event Action<OwnedUnitData> OwnedUnitAdded;

        public void InitializeFromContext()
        {
            if (_playerContext == null)
            {
                return;
            }

            bool changed = false;

            if (_playerContext.ActiveSquadOwnedUnitIds.Count == 0 && _playerContext.OwnedUnits.Count > 0)
            {
                var activeIds = new List<string>(System.Math.Min(_playerContext.MaxSquadSize, _playerContext.OwnedUnits.Count));
                for (int i = 0; i < _playerContext.OwnedUnits.Count && activeIds.Count < _playerContext.MaxSquadSize; i++)
                {
                    OwnedUnitData unit = _playerContext.OwnedUnits[i];
                    if (unit == null || string.IsNullOrWhiteSpace(unit.OwnedUnitId))
                    {
                        continue;
                    }

                    activeIds.Add(unit.OwnedUnitId);
                }

                _playerContext.SetActiveSquadOwnedUnitIds(activeIds);
                changed = true;
            }

            if (changed)
            {
                OwnedUnitsChanged?.Invoke();
            }
        }

        public bool ContainsOwnedUnit(string ownedUnitId)
        {
            if (string.IsNullOrWhiteSpace(ownedUnitId) || _playerContext == null)
            {
                return false;
            }

            for (int i = 0; i < _playerContext.OwnedUnits.Count; i++)
            {
                OwnedUnitData unit = _playerContext.OwnedUnits[i];
                if (unit == null)
                {
                    continue;
                }

                if (string.Equals(unit.OwnedUnitId, ownedUnitId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryGetOwnedUnit(string ownedUnitId, out OwnedUnitData ownedUnit)
        {
            ownedUnit = null;
            if (string.IsNullOrWhiteSpace(ownedUnitId) || _playerContext == null)
            {
                return false;
            }

            for (int i = 0; i < _playerContext.OwnedUnits.Count; i++)
            {
                OwnedUnitData candidate = _playerContext.OwnedUnits[i];
                if (candidate == null)
                {
                    continue;
                }

                if (string.Equals(candidate.OwnedUnitId, ownedUnitId, StringComparison.Ordinal))
                {
                    ownedUnit = candidate;
                    return true;
                }
            }

            return false;
        }

        public bool TryAddOwnedUnit(string unitDefinitionId, int level, int xp, out OwnedUnitData addedUnit)
        {
            addedUnit = null;
            if (_playerContext == null || string.IsNullOrWhiteSpace(unitDefinitionId))
            {
                return false;
            }

            if (_unitCatalog == null || !_unitCatalog.TryGetById(unitDefinitionId, out UnitDefinition definition) || definition == null)
            {
                return false;
            }

            addedUnit = new OwnedUnitData
            {
                OwnedUnitId = Guid.NewGuid().ToString("N"),
                Definition = definition,
                Level = Mathf.Max(UnitSpellLoadout.DefaultLevel, level),
                Xp = Mathf.Max(0, xp),
                Spells = definition.Spells != null ? (SpellDefinition[])definition.Spells.Clone() : Array.Empty<SpellDefinition>()
            };

            var next = new List<OwnedUnitData>(_playerContext.OwnedUnits.Count + 1);
            for (int i = 0; i < _playerContext.OwnedUnits.Count; i++)
            {
                OwnedUnitData existing = OwnedUnitData.Clone(_playerContext.OwnedUnits[i]);
                if (existing != null)
                {
                    next.Add(existing);
                }
            }
            next.Add(addedUnit);

            _playerContext.SetOwnedUnits(next);
            OwnedUnitAdded?.Invoke(addedUnit);
            OwnedUnitsChanged?.Invoke();
            return true;
        }
    }
}
