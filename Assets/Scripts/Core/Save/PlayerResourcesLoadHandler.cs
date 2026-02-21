using System;
using System.Collections.Generic;
using UnityEngine;
using SevenBattles.Core.Battle;
using SevenBattles.Core.Players;
using SevenBattles.Core.Units;

namespace SevenBattles.Core.Save
{
    /// <summary>
    /// Applies player resources (gold/gems) from save data to PlayerContext.
    /// </summary>
    public sealed class PlayerResourcesLoadHandler : MonoBehaviour, IGameStateLoadHandler
    {
        [SerializeField, Tooltip("Player context whose resources are restored from save data.")]
        private PlayerContext _playerContext;
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

            if (_playerContext == null)
            {
                return;
            }

            var resources = data.PlayerResources;
            int gold = resources != null ? resources.Gold : 0;
            int gems = resources != null ? resources.Gems : 0;
            _playerContext.SetResources(gold, gems);

            var progress = data.TournamentProgress;
            int currentRound = progress != null ? progress.CurrentRoundIndex : 1;
            bool[] completedBattles = progress != null ? progress.CompletedBattles : null;
            _playerContext.SetTournamentProgress(currentRound, completedBattles, TournamentDefinition.BattleCount);

            ApplyOwnedUnitsState(data);
        }

        private void ApplyOwnedUnitsState(SaveGameData data)
        {
            _unitLookup.Clear();
            _spellLookup.Clear();

            PlayerOwnedUnitsSaveData ownedSave = data.PlayerOwnedUnits;
            if (ownedSave != null)
            {
                ApplyOwnedUnitsFromOwnedSave(ownedSave);
                return;
            }

            _playerContext.SetOwnedUnits(Array.Empty<OwnedUnitData>());
            _playerContext.SetActiveSquadOwnedUnitIds(Array.Empty<string>());
        }

        private void ApplyOwnedUnitsFromOwnedSave(PlayerOwnedUnitsSaveData save)
        {
            if (save == null || save.Units == null || save.Units.Length == 0)
            {
                _playerContext.SetOwnedUnits(Array.Empty<OwnedUnitData>());
                _playerContext.SetActiveSquadOwnedUnitIds(Array.Empty<string>());
                return;
            }

            var ownedUnits = new List<OwnedUnitData>(save.Units.Length);
            var ownedById = new Dictionary<string, OwnedUnitData>(StringComparer.Ordinal);

            for (int i = 0; i < save.Units.Length; i++)
            {
                OwnedUnitSaveData entry = save.Units[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.UnitId))
                {
                    continue;
                }

                UnitDefinition definition = ResolveUnitDefinition(entry.UnitId);
                if (definition == null)
                {
                    continue;
                }

                string ownedId = string.IsNullOrWhiteSpace(entry.OwnedUnitId)
                    ? Guid.NewGuid().ToString("N")
                    : entry.OwnedUnitId;
                while (ownedById.ContainsKey(ownedId))
                {
                    ownedId = Guid.NewGuid().ToString("N");
                }

                var ownedUnit = new OwnedUnitData
                {
                    OwnedUnitId = ownedId,
                    CustomName = entry.CustomName,
                    Definition = definition,
                    Level = entry.Level > 0 ? entry.Level : UnitSpellLoadout.DefaultLevel,
                    Xp = entry.Xp > 0 ? entry.Xp : 0,
                    Spells = ResolveSpells(entry.SpellIds, definition.Spells)
                };

                ownedUnits.Add(ownedUnit);
                ownedById.Add(ownedId, ownedUnit);
            }

            OwnedUnitNamingPolicy.NormalizeAllInPlace(ownedUnits);

            var activeIds = new List<string>();
            if (save.ActiveSquadOwnedUnitIds != null)
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < save.ActiveSquadOwnedUnitIds.Length && activeIds.Count < _playerContext.MaxSquadSize; i++)
                {
                    string id = save.ActiveSquadOwnedUnitIds[i];
                    if (string.IsNullOrWhiteSpace(id) || !ownedById.ContainsKey(id) || !seen.Add(id))
                    {
                        continue;
                    }

                    activeIds.Add(id);
                }
            }

            if (activeIds.Count == 0)
            {
                for (int i = 0; i < ownedUnits.Count && activeIds.Count < _playerContext.MaxSquadSize; i++)
                {
                    activeIds.Add(ownedUnits[i].OwnedUnitId);
                }
            }

            _playerContext.SetOwnedUnits(ownedUnits);
            _playerContext.SetActiveSquadOwnedUnitIds(activeIds);
        }

        private UnitDefinition ResolveUnitDefinition(string unitId)
        {
            if (string.IsNullOrWhiteSpace(unitId))
            {
                return null;
            }

            if (_unitRegistry != null)
            {
                return _unitRegistry.GetById(unitId);
            }

            if (_unitLookup.TryGetValue(unitId, out UnitDefinition cached))
            {
                return cached;
            }

            var allDefinitions = Resources.FindObjectsOfTypeAll<UnitDefinition>();
            for (int i = 0; i < allDefinitions.Length; i++)
            {
                UnitDefinition definition = allDefinitions[i];
                if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
                {
                    continue;
                }

                if (!_unitLookup.ContainsKey(definition.Id))
                {
                    _unitLookup.Add(definition.Id, definition);
                }
            }

            return _unitLookup.TryGetValue(unitId, out UnitDefinition resolved) ? resolved : null;
        }

        private SpellDefinition[] ResolveSpells(string[] spellIds, SpellDefinition[] fallbackSpells)
        {
            if (spellIds == null || spellIds.Length == 0)
            {
                return fallbackSpells != null ? (SpellDefinition[])fallbackSpells.Clone() : Array.Empty<SpellDefinition>();
            }

            var resolved = new List<SpellDefinition>(spellIds.Length);
            for (int i = 0; i < spellIds.Length; i++)
            {
                SpellDefinition spell = ResolveSpellDefinition(spellIds[i]);
                if (spell != null)
                {
                    resolved.Add(spell);
                }
            }

            if (resolved.Count == 0)
            {
                return fallbackSpells != null ? (SpellDefinition[])fallbackSpells.Clone() : Array.Empty<SpellDefinition>();
            }

            return resolved.ToArray();
        }

        private SpellDefinition ResolveSpellDefinition(string spellId)
        {
            if (string.IsNullOrWhiteSpace(spellId))
            {
                return null;
            }

            if (_spellRegistry != null)
            {
                return _spellRegistry.GetById(spellId);
            }

            if (_spellLookup.TryGetValue(spellId, out SpellDefinition cached))
            {
                return cached;
            }

            var allSpells = Resources.FindObjectsOfTypeAll<SpellDefinition>();
            for (int i = 0; i < allSpells.Length; i++)
            {
                SpellDefinition spell = allSpells[i];
                if (spell == null || string.IsNullOrWhiteSpace(spell.Id))
                {
                    continue;
                }

                if (!_spellLookup.ContainsKey(spell.Id))
                {
                    _spellLookup.Add(spell.Id, spell);
                }
            }

            return _spellLookup.TryGetValue(spellId, out SpellDefinition resolved) ? resolved : null;
        }
    }
}
