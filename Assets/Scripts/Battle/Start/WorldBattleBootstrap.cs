using UnityEngine;

namespace SevenBattles.Battle.Start
{
    // Ensures enemies are spawned before the player placement phase begins.
    // Uses Awake to run before other Start methods, avoiding race conditions.
    public class WorldBattleBootstrap : MonoBehaviour
    {
        [Header("Sequence")]
        [SerializeField, Tooltip("Spawns enemies in Awake so they are visible before any player placement starts.")]
        private bool _spawnEnemiesOnAwake = true;

        [Header("Controllers")]
        [SerializeField] private WorldEnemySquadStartController _enemy;
        [SerializeField, Tooltip("Optional: reference for clarity; not used directly.")]
        private WorldSquadPlacementController _playerPlacement;

        private void Awake()
        {
            if (_spawnEnemiesOnAwake && _enemy != null)
            {
                _enemy.StartEnemySquad();
            }
        }
    }
}

