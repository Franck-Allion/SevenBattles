using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using SevenBattles.Battle.Turn;
using SevenBattles.Core;
using SevenBattles.Core.Battle;
using SevenBattles.Core.Players;

namespace SevenBattles.Battle.Start
{
    // Ensures enemies are spawned before the player placement phase begins.
    // Uses Awake to run before other Start methods, avoiding race conditions.
    [DefaultExecutionOrder(100)]
    public class WorldBattleBootstrap : MonoBehaviour
    {
        [Header("Sequence")]
        [SerializeField, Tooltip("Spawns enemies in Awake so they are visible before any player placement starts.")]
        private bool _spawnEnemiesOnAwake = true;
        [SerializeField, Tooltip("When enabled, starts the turn controller once player placement is locked.")]
        private bool _startTurnsOnPlacementLocked = true;

        [Header("Controllers")]
        [SerializeField] private WorldEnemySquadStartController _enemy;
        [SerializeField, Tooltip("Player placement controller (MonoBehaviour implementing ISquadPlacementController). If not assigned, will be auto-found at runtime.")]
        private MonoBehaviour _playerPlacementBehaviour;
        [SerializeField, Tooltip("Turn order controller. If not assigned, will be auto-found at runtime.")]
        private SimpleTurnOrderController _turnControllerBehaviour;

        [Header("Battlefield (legacy fallback)")]
        [SerializeField, Tooltip("Default battlefield used when no session is injected (press Play in BattleScene).")]
        private BattlefieldDefinition _defaultBattlefield;

        [Header("UI Transition")]
        [SerializeField, Tooltip("Optional full-screen CanvasGroup used for fade-out/fade-in between placement and battle.")]
        private CanvasGroup _fadeCanvasGroup;
        [SerializeField, Tooltip("Placement HUD root that is hidden once fade-out completes.")]
        private GameObject _placementHudRoot;
        [SerializeField, Tooltip("Battle HUD root that is shown just before fade-in begins.")]
        private GameObject _battleHudRoot;
        [SerializeField, Tooltip("Fade-out duration in seconds.")]
        private float _fadeOutDuration = 0.5f;
        [SerializeField, Tooltip("Fade-in duration in seconds.")]
        private float _fadeInDuration = 0.5f;

        private ISquadPlacementController _playerPlacement;
        private IBattleTurnController _turnController;
        private Coroutine _transitionRoutine;

        private void Awake()
        {
            // Ensure battle session is initialized before spawning enemies
            EnsureBattleSessionInitialized();

            if (_spawnEnemiesOnAwake && _enemy != null)
            {
                _enemy.StartEnemySquad();
            }

            if (_startTurnsOnPlacementLocked)
            {
                ResolveControllers();
                if (_playerPlacement != null)
                {
                    _playerPlacement.PlacementLocked += HandlePlacementLocked;
                }
            }
        }

        /// <summary>
        /// Ensures the battle session is initialized before any controllers attempt to use it.
        /// If no session exists, creates one from legacy ScriptableObject references as a fallback.
        /// </summary>
        private void EnsureBattleSessionInitialized()
        {
            var sessionService = UnityEngine.Object.FindFirstObjectByType<BattleSessionService>();
            if (sessionService == null)
            {
                Debug.LogWarning("WorldBattleBootstrap: No BattleSessionService found in scene. Battle session will not be available.");
                return;
            }

            if (sessionService.CurrentSession != null)
            {
                // Session already initialized (e.g., from SceneFlow or load system)
                return;
            }

            // Fallback: create session from legacy ScriptableObject references
            var config = BuildLegacyBattleSessionConfig();
            if (config != null)
            {
                sessionService.InitializeSession(config);
                Debug.Log("WorldBattleBootstrap: Initialized battle session from legacy ScriptableObject references.");
            }
        }

        /// <summary>
        /// Builds a BattleSessionConfig from legacy ScriptableObject references.
        /// This is a migration path to support existing scenes.
        /// </summary>
        private BattleSessionConfig BuildLegacyBattleSessionConfig()
        {
            // Try to find player and enemy squad controllers to extract their legacy references
            var placementController = _playerPlacementBehaviour as WorldSquadPlacementController;
            if (placementController == null)
            {
                placementController = UnityEngine.Object.FindFirstObjectByType<WorldSquadPlacementController>();
            }

            UnitSpellLoadout[] playerSquad = null;
            UnitSpellLoadout[] enemySquad = null;

            // Extract player squad from placement controller's legacy field
            if (placementController != null)
            {
                var playerSquadField = typeof(WorldSquadPlacementController).GetField("_playerSquad", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (playerSquadField != null)
                {
                    var playerSquadSO = playerSquadField.GetValue(placementController) as PlayerSquad;
                    if (playerSquadSO != null)
                    {
                        playerSquad = playerSquadSO.GetLoadouts();
                    }
                }
            }

            // Extract enemy squad from enemy controller's legacy field
            if (_enemy != null)
            {
                var enemySquadField = typeof(WorldEnemySquadStartController).GetField("_enemySquad",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (enemySquadField != null)
                {
                    var enemySquadSO = enemySquadField.GetValue(_enemy) as PlayerSquad;
                    if (enemySquadSO != null)
                    {
                        enemySquad = enemySquadSO.GetLoadouts();
                    }
                }
            }

            if (playerSquad == null && enemySquad == null)
            {
                Debug.LogWarning("WorldBattleBootstrap: Could not build legacy battle session config - no squads found.");
                return null;
            }

            return new BattleSessionConfig(
                playerSquad ?? System.Array.Empty<UnitSpellLoadout>(),
                enemySquad ?? System.Array.Empty<UnitSpellLoadout>(),
                "legacy",
                0
            )
            {
                Battlefield = _defaultBattlefield,
                BattlefieldId = _defaultBattlefield != null ? _defaultBattlefield.Id : null
            };
        }

        private void OnDestroy()
        {
            if (_playerPlacement != null)
            {
                _playerPlacement.PlacementLocked -= HandlePlacementLocked;
            }

            if (_transitionRoutine != null)
            {
                StopCoroutine(_transitionRoutine);
                _transitionRoutine = null;
            }
        }

        private void HandlePlacementLocked()
        {
            if (!_startTurnsOnPlacementLocked)
            {
                return;
            }

            ResolveControllers();

            if (_playerPlacement == null || _turnController == null)
            {
                return;
            }

            if (_transitionRoutine != null)
            {
                return;
            }

            _transitionRoutine = StartCoroutine(PlacementToBattleRoutine());
        }

        private void ResolveControllers()
        {
            if (_playerPlacement == null)
            {
                if (_playerPlacementBehaviour != null)
                {
                    _playerPlacement = _playerPlacementBehaviour as ISquadPlacementController;
                }

                if (_playerPlacement == null)
                {
                    var behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
                    for (int i = 0; i < behaviours.Length; i++)
                    {
                        var candidate = behaviours[i] as ISquadPlacementController;
                        if (candidate != null)
                        {
                            _playerPlacement = candidate;
                            _playerPlacementBehaviour = behaviours[i];
                            break;
                        }
                    }
                }
            }

            if (_turnController == null)
            {
                if (_turnControllerBehaviour != null)
                {
                    _turnController = _turnControllerBehaviour as IBattleTurnController;
                }

                if (_turnController == null)
                {
                    var controller = UnityEngine.Object.FindFirstObjectByType<SimpleTurnOrderController>();
                    _turnControllerBehaviour = controller;
                    _turnController = controller;
                }
            }
        }

        private IEnumerator PlacementToBattleRoutine()
        {
            if (_turnController != null)
            {
                _turnController.SetInteractionLocked(true);
            }

            if (_fadeCanvasGroup != null)
            {
                _fadeCanvasGroup.gameObject.SetActive(true);
                _fadeCanvasGroup.blocksRaycasts = true;
            }

            float fadeOutDuration = Mathf.Max(0.01f, _fadeOutDuration);
            float t = 0f;

            if (_fadeCanvasGroup != null)
            {
                _fadeCanvasGroup.alpha = 0f;
                while (t < fadeOutDuration)
                {
                    t += Time.unscaledDeltaTime;
                    float p = Mathf.Clamp01(t / fadeOutDuration);
                    float eased = p * p * (3f - 2f * p);
                    _fadeCanvasGroup.alpha = eased;
                    yield return null;
                }
                _fadeCanvasGroup.alpha = 1f;
            }

            if (_placementHudRoot != null)
            {
                _placementHudRoot.SetActive(false);
            }

            if (_turnController != null)
            {
                _turnController.StartBattle();
            }

            if (_battleHudRoot != null)
            {
                _battleHudRoot.SetActive(true);
            }

            float fadeInDuration = Mathf.Max(0.01f, _fadeInDuration);
            t = 0f;

            if (_fadeCanvasGroup != null)
            {
                while (t < fadeInDuration)
                {
                    t += Time.unscaledDeltaTime;
                    float p = Mathf.Clamp01(t / fadeInDuration);
                    float eased = 1f - p * p * (3f - 2f * p);
                    _fadeCanvasGroup.alpha = eased;
                    yield return null;
                }

                _fadeCanvasGroup.alpha = 0f;
                _fadeCanvasGroup.blocksRaycasts = false;
                _fadeCanvasGroup.gameObject.SetActive(false);
            }

            if (_turnController != null)
            {
                _turnController.SetInteractionLocked(false);
            }

            _transitionRoutine = null;
        }
    }
}
