using System;
using UnityEngine;
using SevenBattles.Core.Battle;
using SevenBattles.Core.Players;

namespace SevenBattles.Core.Save
{
    /// <summary>
    /// Applies player resources (gold/gems) from save data to PlayerContext.
    /// </summary>
    public sealed class PlayerResourcesLoadHandler : MonoBehaviour, IGameStateLoadHandler
    {
        [SerializeField, Tooltip("Player context whose resources are restored from save data.")]
        private PlayerContext _playerContext;

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
        }
    }
}
