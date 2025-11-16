using UnityEngine;
using SevenBattles.Battle.Turn;

namespace SevenBattles.Battle.Start
{
    // Ensures enemies are spawned before the player placement phase begins.
    // Uses Awake to run before other Start methods, avoiding race conditions.
    public class WorldBattleBootstrap : MonoBehaviour
    {
        [Header("Sequence")]
        [SerializeField, Tooltip("Spawns enemies in Awake so they are visible before any player placement starts.")]
        private bool _spawnEnemiesOnAwake = true;
        [SerializeField, Tooltip("When enabled, starts the turn controller once player placement is locked.")]
        private bool _startTurnsOnPlacementLocked = true;

        [Header("Controllers")]
        [SerializeField] private WorldEnemySquadStartController _enemy;
        [SerializeField, Tooltip("Player placement controller. If not assigned, will be auto-found at runtime.")]
        private WorldSquadPlacementController _playerPlacement;
        [SerializeField, Tooltip("Turn order controller. If not assigned, will be auto-found at runtime.")]
        private SimpleTurnOrderController _turnController;

        private void Awake()
        {
            if (_spawnEnemiesOnAwake && _enemy != null)
            {
                _enemy.StartEnemySquad();
            }

            if (_startTurnsOnPlacementLocked)
            {
                if (_playerPlacement == null)
                {
                    _playerPlacement = FindObjectOfType<WorldSquadPlacementController>();
                }
                if (_turnController == null)
                {
                    _turnController = FindObjectOfType<SimpleTurnOrderController>();
                }

                if (_playerPlacement != null && _turnController != null)
                {
                    _playerPlacement.PlacementLocked += HandlePlacementLocked;
                }
            }
        }

        private void OnDestroy()
        {
            if (_playerPlacement != null)
            {
                _playerPlacement.PlacementLocked -= HandlePlacementLocked;
            }
        }

        private void HandlePlacementLocked()
        {
            if (_turnController != null)
            {
                _turnController.StartBattle();
            }
        }
    }
}
