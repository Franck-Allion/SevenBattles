using System;
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

        [Header("Resources")]
        [SerializeField, Min(0), Tooltip("Current amount of gold owned by the player.")]
        private int _gold = 1000;
        [SerializeField, Min(0), Tooltip("Current amount of gems owned by the player.")]
        private int _gems = 10;

        public int Gold => Mathf.Max(0, _gold);
        public int Gems => Mathf.Max(0, _gems);

        public event Action ResourcesChanged;

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
    }
}
