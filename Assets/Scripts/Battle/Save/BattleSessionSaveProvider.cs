using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using SevenBattles.Core;
using SevenBattles.Core.Battle;
using SevenBattles.Core.Save;

namespace SevenBattles.Battle.Save
{
    /// <summary>
    /// Handles restoring BattleSessionConfig from save data.
    /// This ensures the original battle configuration is restored when loading a save.
    /// </summary>
    public class BattleSessionSaveProvider : MonoBehaviour, IGameStateSaveProvider
    {
        [Header("References")]
        [SerializeField, Tooltip("Battle session service to save from.")]
        private MonoBehaviour _sessionServiceBehaviour;

        private IBattleSessionService _sessionService;

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

        public void PopulateGameState(SaveGameData data)
        {
            if (_sessionService?.CurrentSession == null)
            {
                Debug.LogWarning("BattleSessionSaveProvider: No current session to save.");
                return;
            }

            var session = _sessionService.CurrentSession;
            var playerLoadouts = session.PlayerSquad ?? System.Array.Empty<UnitSpellLoadout>();
            var enemyLoadouts = session.EnemySquad ?? System.Array.Empty<UnitSpellLoadout>();

            data.BattleSession = new BattleSessionSaveData
            {
                PlayerSquadIds = playerLoadouts.Select(u => u != null && u.Definition != null ? u.Definition.Id : null).Where(id => id != null).ToArray(),
                EnemySquadIds = enemyLoadouts.Select(u => u != null && u.Definition != null ? u.Definition.Id : null).Where(id => id != null).ToArray(),
                PlayerSquadUnits = BuildUnitLoadoutSaveData(playerLoadouts),
                EnemySquadUnits = BuildUnitLoadoutSaveData(enemyLoadouts),
                BattleType = session.BattleType,
                Difficulty = session.Difficulty,
                CampaignMissionId = session.CampaignMissionId,
                BattlefieldId = ResolveBattlefieldId(session)
            };

            Debug.Log($"BattleSessionSaveProvider: Saved session with {data.BattleSession.PlayerSquadIds.Length} player units, {data.BattleSession.EnemySquadIds.Length} enemy units.");
        }

        private static UnitSpellLoadoutSaveData[] BuildUnitLoadoutSaveData(UnitSpellLoadout[] squad)
        {
            if (squad == null || squad.Length == 0)
            {
                return System.Array.Empty<UnitSpellLoadoutSaveData>();
            }

            var list = new List<UnitSpellLoadoutSaveData>(squad.Length);
            for (int i = 0; i < squad.Length; i++)
            {
                var loadout = squad[i];
                if (loadout == null || loadout.Definition == null)
                {
                    continue;
                }

                var spellIds = loadout.Spells != null
                    ? loadout.Spells.Where(spell => spell != null && !string.IsNullOrEmpty(spell.Id)).Select(spell => spell.Id).ToArray()
                    : System.Array.Empty<string>();

                list.Add(new UnitSpellLoadoutSaveData
                {
                    UnitId = loadout.Definition.Id,
                    SpellIds = spellIds,
                    Level = loadout.EffectiveLevel
                });
            }

            return list.ToArray();
        }

        private static string ResolveBattlefieldId(BattleSessionConfig session)
        {
            if (session == null)
            {
                return null;
            }

            if (session.Battlefield != null && !string.IsNullOrEmpty(session.Battlefield.Id))
            {
                return session.Battlefield.Id;
            }

            return string.IsNullOrEmpty(session.BattlefieldId) ? null : session.BattlefieldId;
        }
    }
}
