using System;
using System.Collections.Generic;
using UnityEngine;
using SevenBattles.Battle.Units;
using SevenBattles.Core;
using SevenBattles.Core.Battle;

namespace SevenBattles.Battle.Turn
{
    /// <summary>
    /// Service managing turn progression: turn advancement, round tracking, and battle outcome evaluation.
    /// Extracted from SimpleTurnOrderController to follow SRP.
    /// </summary>
    public class BattleTurnProgressionService : MonoBehaviour
    {
        private int _turnIndex;
        private bool _battleEnded;
        private BattleOutcome _battleOutcome = BattleOutcome.None;

        /// <summary>
        /// Current turn number (1-indexed). 0 indicates battle has not started or has ended.
        /// </summary>
        public int TurnIndex => _turnIndex;

        /// <summary>
        /// Whether the battle has ended.
        /// </summary>
        public bool HasBattleEnded => _battleEnded;

        /// <summary>
        /// The outcome of the battle if it has ended.
        /// </summary>
        public BattleOutcome Outcome => _battleOutcome;

        /// <summary>
        /// Raised when the battle ends.
        /// </summary>
        public event Action<BattleOutcome> BattleEnded;

        /// <summary>
        /// Initializes turn tracking for a new battle.
        /// </summary>
        public void InitializeForBattle()
        {
            _turnIndex = 0;
            _battleEnded = false;
            _battleOutcome = BattleOutcome.None;
        }

        /// <summary>
        /// Sets the turn index to the specified value (used for save/load).
        /// </summary>
        public void SetTurnIndex(int turnIndex)
        {
            _turnIndex = Mathf.Max(0, turnIndex);
        }

        /// <summary>
        /// Increments the turn index by 1.
        /// </summary>
        public void IncrementTurnIndex()
        {
            _turnIndex = Mathf.Max(1, _turnIndex + 1);
        }

        /// <summary>
        /// Advances to the next valid unit in the turn order.
        /// Returns the new active unit index, or -1 if no valid units remain.
        /// Automatically increments turn index when wrapping around to the start of the list.
        /// </summary>
        /// <param name="units">List of units in initiative order.</param>
        /// <param name="currentIndex">Current active unit index.</param>
        /// <param name="isAdvancing">Whether this is called during turn advancement (prevents nested calls).</param>
        /// <param name="isUnitValid">Function to check if a unit is alive and valid.</param>
        public int AdvanceToNext<T>(List<T> units, int currentIndex, bool isAdvancing, Func<T, bool> isUnitValid)
        {
            if (units == null || units.Count == 0)
            {
                return -1;
            }

            int startIndex = currentIndex;
            int count = units.Count;
            if (startIndex < 0 || startIndex >= count)
            {
                startIndex = -1;
            }

            int attempts = 0;
            int idx = startIndex;
            while (attempts < count)
            {
                idx = (idx + 1) % count;
                if (isUnitValid(units[idx]))
                {
                    // If we wrapped around, increment turn index
                    if (startIndex >= 0 && idx <= startIndex)
                    {
                        IncrementTurnIndex();
                    }
                    return idx;
                }
                attempts++;
            }

            // No valid units remain
            return -1;
        }

        /// <summary>
        /// Evaluates the battle outcome based on remaining units.
        /// Returns true if the battle has ended, false otherwise.
        /// Raises BattleEnded event if the battle ends.
        /// </summary>
        /// <param name="units">List of all units in the battle.</param>
        /// <param name="isUnitValid">Function to check if a unit is alive and valid.</param>
        /// <param name="isPlayerControlled">Function to check if a unit is player-controlled.</param>
        public bool EvaluateOutcome<T>(List<T> units, Func<T, bool> isUnitValid, Func<T, bool> isPlayerControlled)
        {
            if (_battleEnded)
            {
                return true;
            }

            int playerAlive = 0;
            int enemyAlive = 0;

            for (int i = 0; i < units.Count; i++)
            {
                var unit = units[i];
                if (!isUnitValid(unit))
                {
                    continue;
                }

                if (isPlayerControlled(unit))
                {
                    playerAlive++; 
                }
                else
                {
                    enemyAlive++;
                }
            }

            // Determine outcome
            BattleOutcome outcome = BattleOutcome.None;
            if (playerAlive <= 0 && enemyAlive > 0)
            {
                outcome = BattleOutcome.PlayerDefeat;
                Debug.Log("[Battle] Player defeated. All player units are dead.", this);
            }
            else if (enemyAlive <= 0 && playerAlive > 0)
            {
                outcome = BattleOutcome.PlayerVictory;
                Debug.Log("[Battle] Player victory. All enemy units are dead.", this);
            }
            else if (playerAlive <= 0 && enemyAlive <= 0)
            {
                // Simultaneous wipe treated as defeat
                outcome = BattleOutcome.PlayerDefeat;
                Debug.Log("[Battle] Player defeated (simultaneous wipe). Both squads have no units remaining.", this);
            }
            else
            {
                // Battle continues
                return false;
            }

            // Battle has ended
            _battleEnded = true;
            _battleOutcome = outcome;
            _turnIndex = 0;

            BattleEnded?.Invoke(outcome);
            return true;
        }

        /// <summary>
        /// Finds the first valid unit index in the list, or -1 if none exist.
        /// Used for initializing the battle or recovering from invalid state.
        /// </summary>
        public int FindFirstValidUnitIndex<T>(List<T> units, Func<T, bool> isUnitValid)
        {
            if (units == null || units.Count == 0) return -1;

            for (int i = 0; i < units.Count; i++)
            {
                if (isUnitValid(units[i]))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
