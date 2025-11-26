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
        [SerializeField, Tooltip("Player squad asset representing the current player's squad.")]
        private PlayerSquad _playerSquad;

        public void PopulateGameState(SaveGameData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (_playerSquad == null || _playerSquad.Wizards == null || _playerSquad.Wizards.Length == 0)
            {
                data.PlayerSquad = new PlayerSquadSaveData
                {
                    WizardIds = Array.Empty<string>()
                };
                return;
            }

            var wizards = _playerSquad.Wizards;
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

