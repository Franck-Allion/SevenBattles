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

            data.BattleSession = new BattleSessionSaveData
            {
                PlayerSquadIds = session.PlayerSquad?.Select(u => u != null ? u.Id : null).Where(id => id != null).ToArray() ?? System.Array.Empty<string>(),
                EnemySquadIds = session.EnemySquad?.Select(u => u != null ? u.Id : null).Where(id => id != null).ToArray() ?? System.Array.Empty<string>(),
                BattleType = session.BattleType,
                Difficulty = session.Difficulty,
                CampaignMissionId = session.CampaignMissionId
            };

            Debug.Log($"BattleSessionSaveProvider: Saved session with {data.BattleSession.PlayerSquadIds.Length} player units, {data.BattleSession.EnemySquadIds.Length} enemy units.");
        }
    }
}
