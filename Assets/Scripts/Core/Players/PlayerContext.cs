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
    }
}
