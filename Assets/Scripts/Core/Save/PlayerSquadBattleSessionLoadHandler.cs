using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using SevenBattles.Core.Battle;
using SevenBattles.Core.Players;
using SevenBattles.Core.Units;

namespace SevenBattles.Core.Save
{
    /// <summary>
    /// Updates PlayerContext owned active squad from BattleSession save data, so progression
    /// (XP/levels/spells) survives save/load cycles.
    /// </summary>
    public class PlayerSquadBattleSessionLoadHandler : MonoBehaviour, IGameStateLoadHandler
    {
        [SerializeField, Tooltip("Player context whose owned active squad will be updated from save data.")]
        private PlayerContext _playerContext;
        [SerializeField, Tooltip("If enabled, applies loaded progression into PlayerContext owned active squad (this mutates ScriptableObject assets in-editor).")]
        private bool _applyToPlayerContextAssets;

        [Header("Optional registries (recommended)")]
        [SerializeField] private UnitDefinitionRegistry _unitRegistry;
        [SerializeField] private SpellDefinitionRegistry _spellRegistry;

        private readonly Dictionary<string, UnitDefinition> _unitLookup = new Dictionary<string, UnitDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, SpellDefinition> _spellLookup = new Dictionary<string, SpellDefinition>(StringComparer.Ordinal);

        public void ApplyLoadedGame(SaveGameData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (!_applyToPlayerContextAssets)
            {
                return;
            }

            if (_playerContext == null)
            {
                return;
            }

            var session = data.BattleSession;
            var savedUnits = session != null ? session.PlayerSquadUnits : null;
            if (savedUnits == null || savedUnits.Length == 0)
            {
                return;
            }

            _unitLookup.Clear();
            _spellLookup.Clear();

            var loadouts = new UnitSpellLoadout[savedUnits.Length];
            for (int i = 0; i < savedUnits.Length; i++)
            {
                var saved = savedUnits[i];
                if (saved == null || string.IsNullOrEmpty(saved.UnitId))
                {
                    continue;
                }

                var def = ResolveUnitDefinition(saved.UnitId);
                if (def == null)
                {
                    continue;
                }

                var spells = ResolveSpells(saved.SpellIds);
                loadouts[i] = new UnitSpellLoadout
                {
                    Definition = def,
                    Level = saved.Level > 0 ? saved.Level : UnitSpellLoadout.DefaultLevel,
                    Xp = saved.Xp > 0 ? saved.Xp : 0,
                    Spells = spells
                };
            }

            loadouts = loadouts.Where(l => l != null).ToArray();
            ApplyOwnedUnitsFromBattleSession(loadouts);
        }

        private void ApplyOwnedUnitsFromBattleSession(UnitSpellLoadout[] loadouts)
        {
            if (_playerContext == null)
            {
                return;
            }

            if (loadouts == null || loadouts.Length == 0)
            {
                return;
            }

            var owned = new List<OwnedUnitData>();
            var activeIds = new List<string>(Mathf.Min(_playerContext.MaxSquadSize, loadouts.Length));

            IReadOnlyList<OwnedUnitData> existingOwned = _playerContext.OwnedUnits;
            IReadOnlyList<string> existingActiveIds = _playerContext.ActiveSquadOwnedUnitIds;

            int count = Mathf.Min(existingActiveIds.Count, loadouts.Length);
            for (int i = 0; i < count; i++)
            {
                string existingOwnedId = existingActiveIds[i];
                OwnedUnitData matched = FindOwnedById(existingOwned, existingOwnedId);
                UnitSpellLoadout loadout = loadouts[i];
                if (matched == null || loadout == null || loadout.Definition == null)
                {
                    continue;
                }

                var updated = new OwnedUnitData
                {
                    OwnedUnitId = matched.OwnedUnitId,
                    Definition = loadout.Definition,
                    Level = loadout.EffectiveLevel,
                    Xp = loadout.EffectiveXp,
                    Spells = loadout.Spells != null ? (SpellDefinition[])loadout.Spells.Clone() : Array.Empty<SpellDefinition>()
                };

                owned.Add(updated);
                activeIds.Add(updated.OwnedUnitId);
            }

            for (int i = count; i < loadouts.Length; i++)
            {
                UnitSpellLoadout loadout = loadouts[i];
                if (loadout == null || loadout.Definition == null)
                {
                    continue;
                }

                var created = new OwnedUnitData
                {
                    OwnedUnitId = Guid.NewGuid().ToString("N"),
                    Definition = loadout.Definition,
                    Level = loadout.EffectiveLevel,
                    Xp = loadout.EffectiveXp,
                    Spells = loadout.Spells != null ? (SpellDefinition[])loadout.Spells.Clone() : Array.Empty<SpellDefinition>()
                };

                owned.Add(created);
                if (activeIds.Count < _playerContext.MaxSquadSize)
                {
                    activeIds.Add(created.OwnedUnitId);
                }
            }

            if (owned.Count == 0)
            {
                return;
            }

            _playerContext.SetOwnedUnits(owned);
            _playerContext.SetActiveSquadOwnedUnitIds(activeIds);
        }

        private static OwnedUnitData FindOwnedById(IReadOnlyList<OwnedUnitData> ownedUnits, string ownedUnitId)
        {
            if (ownedUnits == null || string.IsNullOrWhiteSpace(ownedUnitId))
            {
                return null;
            }

            for (int i = 0; i < ownedUnits.Count; i++)
            {
                OwnedUnitData candidate = ownedUnits[i];
                if (candidate == null)
                {
                    continue;
                }

                if (string.Equals(candidate.OwnedUnitId, ownedUnitId, StringComparison.Ordinal))
                {
                    return candidate;
                }
            }

            return null;
        }

        private UnitDefinition ResolveUnitDefinition(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            if (_unitRegistry != null)
            {
                return _unitRegistry.GetById(id);
            }

            if (_unitLookup.TryGetValue(id, out var cached))
            {
                return cached;
            }

            var allDefs = Resources.FindObjectsOfTypeAll<UnitDefinition>();
            for (int i = 0; i < allDefs.Length; i++)
            {
                var def = allDefs[i];
                if (def == null || string.IsNullOrEmpty(def.Id))
                {
                    continue;
                }

                if (!_unitLookup.ContainsKey(def.Id))
                {
                    _unitLookup.Add(def.Id, def);
                }
            }

            return _unitLookup.TryGetValue(id, out var resolved) ? resolved : null;
        }

        private SpellDefinition[] ResolveSpells(string[] ids)
        {
            if (ids == null || ids.Length == 0)
            {
                return Array.Empty<SpellDefinition>();
            }

            var list = new List<SpellDefinition>(ids.Length);
            for (int i = 0; i < ids.Length; i++)
            {
                var spell = ResolveSpellDefinition(ids[i]);
                if (spell != null)
                {
                    list.Add(spell);
                }
            }

            return list.ToArray();
        }

        private SpellDefinition ResolveSpellDefinition(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            if (_spellRegistry != null)
            {
                return _spellRegistry.GetById(id);
            }

            if (_spellLookup.TryGetValue(id, out var cached))
            {
                return cached;
            }

            var allSpells = Resources.FindObjectsOfTypeAll<SpellDefinition>();
            for (int i = 0; i < allSpells.Length; i++)
            {
                var spell = allSpells[i];
                if (spell == null || string.IsNullOrEmpty(spell.Id))
                {
                    continue;
                }

                if (!_spellLookup.ContainsKey(spell.Id))
                {
                    _spellLookup.Add(spell.Id, spell);
                }
            }

            return _spellLookup.TryGetValue(id, out var resolved) ? resolved : null;
        }
    }
}
