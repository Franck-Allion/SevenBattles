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
    /// Updates PlayerContext.PlayerSquad from BattleSession save data, so progression
    /// (XP/levels/spells) survives save/load cycles.
    /// </summary>
    public class PlayerSquadBattleSessionLoadHandler : MonoBehaviour, IGameStateLoadHandler
    {
        [SerializeField, Tooltip("Player context whose PlayerSquad will be updated from save data.")]
        private PlayerContext _playerContext;
        [SerializeField, Tooltip("If enabled, applies loaded progression into PlayerContext.PlayerSquad (this mutates ScriptableObject assets in-editor).")]
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

            if (_playerContext == null || _playerContext.PlayerSquad == null)
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
            _playerContext.PlayerSquad.UnitLoadouts = loadouts;
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
