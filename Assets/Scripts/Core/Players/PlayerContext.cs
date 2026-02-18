using System;
using SevenBattles.Core.Battle;
using SevenBattles.Core.Items;
using UnityEngine;

namespace SevenBattles.Core.Players
{
    /// <summary>
    /// Holds runtime context for the player, such as their current squad.
    /// This allows sharing the player's state across different systems (Save, Battle, etc.)
    /// without duplicating the reference.
    /// </summary>
    [CreateAssetMenu(menuName = "SevenBattles/Player Context", fileName = "PlayerContext")]
    public class PlayerContext : ScriptableObject
    {
        [Tooltip("The current squad of the player.")]
        public PlayerSquad PlayerSquad;

        [Header("Inventory")]
        [Tooltip("Runtime player inventory for equipment, spells, and consumable items.")]
        public PlayerInventory Inventory;

        [Header("Tournament")]
        [SerializeField, Tooltip("Persistent tournament progression (completed battles and next unlocked round).")]
        private TournamentProgressState _tournamentProgress = new TournamentProgressState();

        [Header("Resources")]
        [SerializeField, Min(0), Tooltip("Current amount of gold owned by the player.")]
        private int _gold = 1000;
        [SerializeField, Min(0), Tooltip("Current amount of gems owned by the player.")]
        private int _gems = 10;

        public int Gold => Mathf.Max(0, _gold);
        public int Gems => Mathf.Max(0, _gems);
        public TournamentProgressState TournamentProgress => _tournamentProgress ?? (_tournamentProgress = new TournamentProgressState());
        public int CurrentTournamentRoundIndex => TournamentProgress.CurrentRoundIndex;

        public event Action ResourcesChanged;
        public event Action TournamentProgressChanged;

        public void SetResources(int gold, int gems)
        {
            int clampedGold = Mathf.Max(0, gold);
            int clampedGems = Mathf.Max(0, gems);
            if (_gold == clampedGold && _gems == clampedGems)
            {
                return;
            }

            _gold = clampedGold;
            _gems = clampedGems;
            ResourcesChanged?.Invoke();
        }

        public void SetGold(int gold)
        {
            SetResources(gold, _gems);
        }

        public void SetGems(int gems)
        {
            SetResources(_gold, gems);
        }

        public bool IsTournamentBattleCompleted(int roundIndex)
        {
            return TournamentProgress.IsBattleCompleted(roundIndex);
        }

        public void MarkTournamentBattleCompleted(int roundIndex, int totalBattles = TournamentDefinition.BattleCount)
        {
            if (TournamentProgress.MarkBattleCompleted(roundIndex, totalBattles))
            {
                TournamentProgressChanged?.Invoke();
            }
        }

        public void SetTournamentProgress(int currentRoundIndex, bool[] completedBattleFlags, int totalBattles = TournamentDefinition.BattleCount)
        {
            if (TournamentProgress.SetState(currentRoundIndex, completedBattleFlags, totalBattles))
            {
                TournamentProgressChanged?.Invoke();
            }
        }
    }
}
