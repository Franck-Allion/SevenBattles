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

            var config = new BattleSessionConfig
            {
                PlayerSquad = ResolveUnits(data.BattleSession.PlayerSquadIds),
                EnemySquad = ResolveUnits(data.BattleSession.EnemySquadIds),
                BattleType = data.BattleSession.BattleType ?? "unknown",
                Difficulty = data.BattleSession.Difficulty,
                CampaignMissionId = data.BattleSession.CampaignMissionId
            };

            _sessionService.InitializeSession(config);
            Debug.Log($"BattleSessionLoadHandler: Restored session with {config.PlayerSquad.Length} player units, {config.EnemySquad.Length} enemy units.");
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
