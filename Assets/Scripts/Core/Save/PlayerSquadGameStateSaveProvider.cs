using System;
using System.Collections.Generic;
using UnityEngine;
using SevenBattles.Core.Battle;
using SevenBattles.Core.Players;

namespace SevenBattles.Core.Save
{
    /// <summary>
    /// Default game state provider that captures player progression into SaveGameData.
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

            data.PlayerOwnedUnits = BuildOwnedUnitsSaveData(_playerContext);
        }

        private static PlayerOwnedUnitsSaveData BuildOwnedUnitsSaveData(PlayerContext context)
        {
            if (context == null)
            {
                return new PlayerOwnedUnitsSaveData
                {
                    Units = Array.Empty<OwnedUnitSaveData>(),
                    ActiveSquadOwnedUnitIds = Array.Empty<string>()
                };
            }

            var units = new List<OwnedUnitSaveData>(context.OwnedUnits.Count);
            for (int i = 0; i < context.OwnedUnits.Count; i++)
            {
                OwnedUnitData owned = context.OwnedUnits[i];
                if (owned == null || owned.Definition == null || string.IsNullOrWhiteSpace(owned.OwnedUnitId))
                {
                    continue;
                }

                SpellDefinition[] spells = owned.Spells ?? Array.Empty<SpellDefinition>();
                var spellIds = new List<string>(spells.Length);
                for (int j = 0; j < spells.Length; j++)
                {
                    SpellDefinition spell = spells[j];
                    if (spell != null && !string.IsNullOrWhiteSpace(spell.Id))
                    {
                        spellIds.Add(spell.Id);
                    }
                }

                units.Add(new OwnedUnitSaveData
                {
                    OwnedUnitId = owned.OwnedUnitId,
                    UnitId = owned.Definition.Id,
                    Level = owned.EffectiveLevel,
                    Xp = owned.EffectiveXp,
                    SpellIds = spellIds.Count > 0 ? spellIds.ToArray() : Array.Empty<string>()
                });
            }

            var activeIds = new List<string>(context.ActiveSquadOwnedUnitIds.Count);
            for (int i = 0; i < context.ActiveSquadOwnedUnitIds.Count; i++)
            {
                string id = context.ActiveSquadOwnedUnitIds[i];
                if (!string.IsNullOrWhiteSpace(id))
                {
                    activeIds.Add(id);
                }
            }

            return new PlayerOwnedUnitsSaveData
            {
                Units = units.Count > 0 ? units.ToArray() : Array.Empty<OwnedUnitSaveData>(),
                ActiveSquadOwnedUnitIds = activeIds.Count > 0 ? activeIds.ToArray() : Array.Empty<string>()
            };
        }
    }
}
