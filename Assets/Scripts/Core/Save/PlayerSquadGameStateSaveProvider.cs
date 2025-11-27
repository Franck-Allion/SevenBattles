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

            var playerSquad = _playerContext != null ? _playerContext.PlayerSquad : null;

            if (playerSquad == null || playerSquad.Wizards == null || playerSquad.Wizards.Length == 0)
            {
                data.PlayerSquad = new PlayerSquadSaveData
                {
                    WizardIds = Array.Empty<string>()
                };
                return;
            }

            var wizards = playerSquad.Wizards;
            var ids = new string[wizards.Length];

            for (int i = 0; i < wizards.Length; i++)
            {
                var def = wizards[i];
                ids[i] = def != null ? def.Id : null;
            }

            data.PlayerSquad = new PlayerSquadSaveData
            {
                WizardIds = ids
            };
        }
    }
}

