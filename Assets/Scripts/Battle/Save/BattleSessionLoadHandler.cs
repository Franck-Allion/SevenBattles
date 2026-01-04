using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using SevenBattles.Core;
using SevenBattles.Core.Battle;
using SevenBattles.Core.Save;
using SevenBattles.Core.Units;

namespace SevenBattles.Battle.Save
{
    /// <summary>
    /// Handles restoring BattleSessionConfig from save data during load.
    /// Resolves unit IDs back to UnitDefinition ScriptableObjects.
    /// </summary>
    public class BattleSessionLoadHandler : MonoBehaviour, IGameStateLoadHandler
    {
        [Header("References")]
        [SerializeField, Tooltip("Battle session service to restore session into.")]
        private MonoBehaviour _sessionServiceBehaviour;

        [SerializeField, Tooltip("Registry to resolve unit IDs to UnitDefinition ScriptableObjects.")]
        private UnitDefinitionRegistry _unitRegistry;

        [SerializeField, Tooltip("Registry to resolve spell IDs to SpellDefinition ScriptableObjects.")]
        private SpellDefinitionRegistry _spellRegistry;

        private IBattleSessionService _sessionService;
        private readonly Dictionary<string, SpellDefinition> _spellLookup = new Dictionary<string, SpellDefinition>(System.StringComparer.Ordinal);
        private readonly Dictionary<string, UnitDefinition> _unitLookup = new Dictionary<string, UnitDefinition>(System.StringComparer.Ordinal);
        private bool _warnedMissingUnitRegistry;

        private void Awake()
        {
            if (_sessionServiceBehaviour != null)
            {
                _sessionService = _sessionServiceBehaviour as IBattleSessionService;
            }

            if (_sessionService == null)
            {
                // Auto-find if not assigned
                var behaviours = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
                foreach (var behaviour in behaviours)
                {
                    if (behaviour is IBattleSessionService service)
                    {
                        _sessionService = service;
                        _sessionServiceBehaviour = behaviour;
                        break;
                    }
                }
            }
        }

        public void ApplyLoadedGame(SaveGameData data)
        {
            if (data.BattleSession == null)
            {
                Debug.LogWarning("BattleSessionLoadHandler: No BattleSession data in save file. Using legacy fallback.");
                return;
            }

            if (_sessionService == null)
            {
                Debug.LogError("BattleSessionLoadHandler: No BattleSessionService available to restore session.");
                return;
            }

            // If a save is missing all squad identity data, do not overwrite an existing runtime session with an empty one.
            // This commonly happens when BattleSessionSaveProvider was not wired into the save pipeline for older saves.
            if (!HasAnySquadData(data))
            {
                if (_sessionService.CurrentSession != null)
                {
                    Debug.LogWarning("BattleSessionLoadHandler: Save contains no BattleSession squad data; keeping existing runtime session.");
                    return;
                }

                Debug.LogWarning("BattleSessionLoadHandler: Save contains no BattleSession squad data and no runtime session exists; skipping session restore.");
                return;
            }

            _spellLookup.Clear();
            _unitLookup.Clear();

            var existing = _sessionService.CurrentSession;

            var playerIds = data.BattleSession.PlayerSquadIds;
            if ((playerIds == null || playerIds.Length == 0) && data.PlayerSquad != null && data.PlayerSquad.WizardIds != null && data.PlayerSquad.WizardIds.Length > 0)
            {
                playerIds = data.PlayerSquad.WizardIds;
            }

            var resolvedPlayer = ResolveLoadouts(data.BattleSession.PlayerSquadUnits, playerIds);
            var resolvedEnemy = ResolveLoadouts(data.BattleSession.EnemySquadUnits, data.BattleSession.EnemySquadIds);

            if ((resolvedPlayer == null || resolvedPlayer.Length == 0) && existing != null && existing.PlayerSquad != null && existing.PlayerSquad.Length > 0)
            {
                resolvedPlayer = UnitSpellLoadout.CloneArray(existing.PlayerSquad);
            }

            if ((resolvedEnemy == null || resolvedEnemy.Length == 0) && existing != null && existing.EnemySquad != null && existing.EnemySquad.Length > 0)
            {
                resolvedEnemy = UnitSpellLoadout.CloneArray(existing.EnemySquad);
            }

            if ((resolvedPlayer == null || resolvedPlayer.Length == 0) && (resolvedEnemy == null || resolvedEnemy.Length == 0))
            {
                if (existing != null)
                {
                    Debug.LogWarning("BattleSessionLoadHandler: Unable to resolve any squads from save; keeping existing runtime session.");
                }
                else
                {
                    Debug.LogWarning("BattleSessionLoadHandler: Unable to resolve any squads from save and no runtime session exists; skipping session restore.");
                }
                return;
            }

            var config = new BattleSessionConfig
            {
                PlayerSquad = resolvedPlayer ?? System.Array.Empty<UnitSpellLoadout>(),
                EnemySquad = resolvedEnemy ?? System.Array.Empty<UnitSpellLoadout>(),
                BattleType = data.BattleSession.BattleType ?? "unknown",
                Difficulty = data.BattleSession.Difficulty,
                CampaignMissionId = data.BattleSession.CampaignMissionId,
                BattlefieldId = string.IsNullOrEmpty(data.BattleSession.BattlefieldId) ? null : data.BattleSession.BattlefieldId
            };

            _sessionService.InitializeSession(config);
            Debug.Log($"BattleSessionLoadHandler: Restored session with {config.PlayerSquad.Length} player units, {config.EnemySquad.Length} enemy units.");
        }

        private static bool HasAnySquadData(SaveGameData data)
        {
            if (data == null || data.BattleSession == null)
            {
                return false;
            }

            var session = data.BattleSession;
            if (session.PlayerSquadUnits != null && session.PlayerSquadUnits.Length > 0) return true;
            if (session.EnemySquadUnits != null && session.EnemySquadUnits.Length > 0) return true;
            if (session.PlayerSquadIds != null && session.PlayerSquadIds.Length > 0) return true;
            if (session.EnemySquadIds != null && session.EnemySquadIds.Length > 0) return true;

            if (data.PlayerSquad != null && data.PlayerSquad.WizardIds != null && data.PlayerSquad.WizardIds.Length > 0) return true;

            return false;
        }

        private UnitSpellLoadout[] ResolveLoadouts(UnitSpellLoadoutSaveData[] savedUnits, string[] legacyIds)
        {
            if (savedUnits != null && savedUnits.Length > 0)
            {
                var resolved = ResolveUnitSaveData(savedUnits);
                if (resolved.Length > 0)
                {
                    return resolved;
                }
            }

            var legacyUnits = ResolveUnits(legacyIds);
            if (legacyUnits.Length == 0)
            {
                return System.Array.Empty<UnitSpellLoadout>();
            }

            var fallback = new UnitSpellLoadout[legacyUnits.Length];
            for (int i = 0; i < legacyUnits.Length; i++)
            {
                var def = legacyUnits[i];
                fallback[i] = new UnitSpellLoadout
                {
                    Definition = def,
                    Level = UnitSpellLoadout.DefaultLevel,
                    Spells = def != null ? def.Spells : System.Array.Empty<SpellDefinition>()
                };
            }

            return fallback;
        }

        private UnitSpellLoadout[] ResolveUnitSaveData(UnitSpellLoadoutSaveData[] savedUnits)
        {
            var results = new List<UnitSpellLoadout>(savedUnits.Length);
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
                results.Add(new UnitSpellLoadout
                {
                    Definition = def,
                    Level = saved.Level > 0 ? saved.Level : UnitSpellLoadout.DefaultLevel,
                    Xp = saved.Xp > 0 ? saved.Xp : 0,
                    Spells = spells
                });
            }

            return results.ToArray();
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

            if (!_warnedMissingUnitRegistry)
            {
                _warnedMissingUnitRegistry = true;
                Debug.LogWarning("BattleSessionLoadHandler: No UnitDefinitionRegistry assigned. Falling back to scanning loaded UnitDefinition assets (slower).");
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
                return System.Array.Empty<SpellDefinition>();
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

        private UnitDefinition[] ResolveUnits(string[] ids)
        {
            if (ids == null || ids.Length == 0)
            {
                return System.Array.Empty<UnitDefinition>();
            }

            if (_unitRegistry == null)
            {
                Debug.LogWarning("BattleSessionLoadHandler: No UnitDefinitionRegistry assigned. Cannot resolve unit IDs.");
                return System.Array.Empty<UnitDefinition>();
            }

            return ids
                .Select(id => _unitRegistry.GetById(id))
                .Where(def => def != null)
                .ToArray();
        }
    }
}
