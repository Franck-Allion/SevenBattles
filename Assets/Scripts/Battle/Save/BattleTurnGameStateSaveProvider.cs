using System;
using UnityEngine;
using SevenBattles.Battle.Start;
using SevenBattles.Battle.Turn;
using SevenBattles.Core.Save;

namespace SevenBattles.Battle.Save
{
    /// <summary>
    /// Captures high-level battle turn state: whether the game is in placement or battle,
    /// the current turn index, and the active unit identity and action points (if any).
    /// </summary>
    public class BattleTurnGameStateSaveProvider : MonoBehaviour, IGameStateSaveProvider
    {
        public void PopulateGameState(SaveGameData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            var battleTurn = data.BattleTurn ?? new BattleTurnSaveData();

            var turnController = UnityEngine.Object.FindFirstObjectByType<SimpleTurnOrderController>();
            if (turnController != null && turnController.TurnIndex > 0 && turnController.HasActiveUnit)
            {
                // We are in an active battle turn.
                battleTurn.Phase = "battle";
                battleTurn.TurnIndex = turnController.TurnIndex;
                battleTurn.ActiveUnitCurrentActionPoints = turnController.ActiveUnitCurrentActionPoints;
                battleTurn.ActiveUnitMaxActionPoints = turnController.ActiveUnitMaxActionPoints;
                battleTurn.ActiveUnitHasMoved = turnController.ActiveUnitHasMoved;

                var activeMeta = turnController.ActiveUnitMetadata;
                if (activeMeta != null)
                {
                    var def = activeMeta.Definition;
                    battleTurn.ActiveUnitId = def != null ? def.Id : null;
                    battleTurn.ActiveUnitInstanceId = activeMeta.SaveInstanceId;
                    battleTurn.ActiveUnitTeam = activeMeta.IsPlayerControlled ? "player" : "enemy";
                }
                else
                {
                    battleTurn.ActiveUnitId = null;
                    battleTurn.ActiveUnitInstanceId = null;
                    battleTurn.ActiveUnitTeam = null;
                }
            }
            else
            {
                // No active battle turn; assume placement or unknown.
                var placementController = UnityEngine.Object.FindFirstObjectByType<WorldSquadPlacementController>();
                if (placementController != null && !placementController.IsLocked)
                {
                    battleTurn.Phase = "placement";
                }
                else
                {
                    battleTurn.Phase = "unknown";
                }

                battleTurn.TurnIndex = 0;
                battleTurn.ActiveUnitId = null;
                battleTurn.ActiveUnitInstanceId = null;
                battleTurn.ActiveUnitTeam = null;
                battleTurn.ActiveUnitCurrentActionPoints = 0;
                battleTurn.ActiveUnitMaxActionPoints = 0;
                battleTurn.ActiveUnitHasMoved = false;
            }

            data.BattleTurn = battleTurn;
        }
    }
}
