using System;
using UnityEngine;
using SevenBattles.Core.Players;

namespace SevenBattles.Core.Save
{
    /// <summary>
    /// Default game state provider that captures the current PlayerSquad into SaveGameData.
    /// This is a first step towards full game state capture; additional fields can be added later.
    /// </summary>
    public class PlayerSquadGameStateSaveProvider : MonoBehaviour, IGameStateSaveProvider
    {
        [SerializeField, Tooltip("Player context containing the current player's squad.")]
        private PlayerContext _playerContext;

        public void PopulateGameState(SaveGameData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            int gold = 0;
            int gems = 0;
            if (_playerContext != null)
            {
                gold = _playerContext.Gold;
                gems = _playerContext.Gems;
            }

            data.PlayerResources = new PlayerResourcesSaveData
            {
                Gold = gold,
                Gems = gems
            };

            var tournamentProgress = _playerContext != null ? _playerContext.TournamentProgress : null;
            data.TournamentProgress = new TournamentProgressSaveData
            {
                CurrentRoundIndex = tournamentProgress != null ? tournamentProgress.CurrentRoundIndex : 1,
                CompletedBattles = tournamentProgress != null ? tournamentProgress.GetCompletedFlagsCopy() : Array.Empty<bool>()
            };

            var playerSquad = _playerContext != null ? _playerContext.PlayerSquad : null;

            var loadouts = playerSquad != null ? playerSquad.GetLoadouts() : null;

            if (playerSquad == null || loadouts == null || loadouts.Length == 0)
            {
                data.PlayerSquad = new PlayerSquadSaveData
                {
                    WizardIds = Array.Empty<string>()
                };
                return;
            }

            var ids = new string[loadouts.Length];

            for (int i = 0; i < loadouts.Length; i++)
            {
                var def = loadouts[i] != null ? loadouts[i].Definition : null;
                ids[i] = def != null ? def.Id : null;
            }

            data.PlayerSquad = new PlayerSquadSaveData
            {
                WizardIds = ids
            };
        }
    }
}
