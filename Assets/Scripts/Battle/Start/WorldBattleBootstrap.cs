using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using SevenBattles.Battle.Turn;
using SevenBattles.Core;
using SevenBattles.Core.Battle;
using SevenBattles.Core.Players;
using SevenBattles.Core.Preload;
using System;
using System.Threading.Tasks;

using SevenBattles.Core.Diagnostics;
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
        [SerializeField, Tooltip("Optional preload manifest executed after fade-out and before battle HUD swap.")]
        private ScenePreloadManifest _preloadManifest;

        private ISquadPlacementController _playerPlacement;
        private IBattleTurnController _turnController;
        private bool _enemiesSpawned;
        private bool _startupPreloadCompletedSuccessfully;
        private Coroutine _enemySpawnStartupRoutine;
        private Coroutine _transitionRoutine;

        private void Awake()
        {
            // Ensure diagnostics starts from a clean state even when no preload manifest is assigned.
            AssetCacheDiagnostics.Reset();
            RegisterManifestAssetsForDiagnostics(_preloadManifest);

            // Ensure battle session is initialized before spawning enemies
            EnsureBattleSessionInitialized();

            if (_enemy == null)
            {
                _enemy = UnityEngine.Object.FindFirstObjectByType<WorldEnemySquadStartController>();
            }

            if (_spawnEnemiesOnAwake && _enemy != null)
            {
                _enemy.SetAutoStartSuppressed(true);
                if (_preloadManifest != null)
                {
                    _enemySpawnStartupRoutine = StartCoroutine(PreloadThenSpawnEnemiesRoutine());
                }
                else
                {
                    SpawnEnemiesIfNeeded();
                }
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
                SBLog.Warn("WorldBattleBootstrap: No BattleSessionService found in scene. Battle session will not be available.");
                return;
            }

            if (sessionService.CurrentSession != null)
            {
                // Session already initialized (e.g., from SceneFlow or load system)
                return;
            }

            if (BattleSessionConfigTransfer.TryConsume(out var pendingConfig))
            {
                sessionService.InitializeSession(pendingConfig);
                SBLog.Info("WorldBattleBootstrap: Initialized battle session from pending config.");
                return;
            }

            // Fallback: create session from legacy ScriptableObject references
            var config = BuildLegacyBattleSessionConfig();
            if (config != null)
            {
                sessionService.InitializeSession(config);
                SBLog.Info("WorldBattleBootstrap: Initialized battle session from legacy ScriptableObject references.");
            }
        }

        /// <summary>
        /// Builds a BattleSessionConfig from legacy ScriptableObject references.
        /// This is a migration path to support existing scenes.
        /// </summary>
        private BattleSessionConfig BuildLegacyBattleSessionConfig()
        {
            UnitSpellLoadout[] playerSquad = null;
            UnitSpellLoadout[] enemySquad = null;

            if (PlayerContext.HasRuntimeInstance && PlayerContext.RuntimeInstance != null)
            {
                playerSquad = CloneLoadouts(PlayerContext.RuntimeInstance.GetActiveSquadLoadoutsNonAlloc());
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
                SBLog.Warn("WorldBattleBootstrap: Could not build legacy battle session config - no squads found.");
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

        private static UnitSpellLoadout[] CloneLoadouts(System.Collections.Generic.IReadOnlyList<UnitSpellLoadout> source)
        {
            if (source == null || source.Count == 0)
            {
                return null;
            }

            var clone = new System.Collections.Generic.List<UnitSpellLoadout>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                UnitSpellLoadout loadout = source[i];
                if (loadout == null || loadout.Definition == null)
                {
                    continue;
                }

                clone.Add(new UnitSpellLoadout
                {
                    Definition = loadout.Definition,
                    Level = loadout.EffectiveLevel,
                    Xp = loadout.EffectiveXp,
                    Spells = loadout.Spells != null ? (SpellDefinition[])loadout.Spells.Clone() : Array.Empty<SpellDefinition>()
                });
            }

            return clone.Count > 0 ? clone.ToArray() : null;
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

            if (_enemySpawnStartupRoutine != null)
            {
                StopCoroutine(_enemySpawnStartupRoutine);
                _enemySpawnStartupRoutine = null;
            }
        }

        public bool IsManagingEnemySpawn => _spawnEnemiesOnAwake;

        public bool TryRequestEnemySpawn(WorldEnemySquadStartController enemy)
        {
            if (enemy == null)
            {
                return false;
            }

            if (_enemy == null)
            {
                _enemy = enemy;
            }

            _enemy.SetAutoStartSuppressed(true);

            if (_enemiesSpawned)
            {
                return true;
            }

            if (_enemySpawnStartupRoutine != null)
            {
                return true;
            }

            if (_preloadManifest != null)
            {
                _enemySpawnStartupRoutine = StartCoroutine(PreloadThenSpawnEnemiesRoutine());
            }
            else
            {
                SpawnEnemiesIfNeeded();
            }

            return true;
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

        /// <summary>
        /// Applies the "battle phase" UI state (battle HUD visible, placement HUD hidden) after a save/load restore.
        /// This intentionally does not call StartBattle() because the turn controller may have already restored its
        /// active unit/turn state from the save file.
        /// </summary>
        public void ApplyLoadedBattleUiState()
        {
            if (_transitionRoutine != null)
            {
                StopCoroutine(_transitionRoutine);
                _transitionRoutine = null;
            }

            if (_fadeCanvasGroup != null)
            {
                _fadeCanvasGroup.alpha = 0f;
                _fadeCanvasGroup.blocksRaycasts = false;
                _fadeCanvasGroup.gameObject.SetActive(false);
            }

            if (_placementHudRoot != null)
            {
                _placementHudRoot.SetActive(false);
            }

            if (_battleHudRoot != null)
            {
                _battleHudRoot.SetActive(true);
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

            if (_preloadManifest != null && !_startupPreloadCompletedSuccessfully)
            {
                SBLog.Info($"[Preload] Starting manifest '{_preloadManifest.name}' for scene transition.", this);
                var preloader = new ScenePreloader();
                Task<PreloadResult[]> task = preloader.RunAllAsync(_preloadManifest, destroyCancellationToken);
                while (!task.IsCompleted)
                {
                    yield return null;
                }

                if (task.Status == TaskStatus.RanToCompletion)
                {
                    PreloadResult[] results = task.Result ?? Array.Empty<PreloadResult>();
                    int completedTaskCount = results.Length;
                    int failedTaskCount = CountFailedTasks(results);

                    if (completedTaskCount <= 0)
                    {
                        SBLog.Warn("[Preload] Manifest executed but produced zero tasks. Check manifest entries.", this);
                    }
                    else if (failedTaskCount > 0)
                    {
                        SBLog.Warn($"[Preload] Completed {completedTaskCount} task(s) with {failedTaskCount} failure(s).", this);
                    }
                    else
                    {
                        SBLog.Info($"[Preload] Completed {completedTaskCount} task(s) successfully.", this);
                    }
                }
                else if (task.IsCanceled || task.Status == TaskStatus.Canceled)
                {
                    SBLog.Warn("[Preload] Manifest execution was canceled.", this);
                }
                else if (task.IsFaulted || task.Status == TaskStatus.Faulted)
                {
                    string errorMessage = task.Exception != null && task.Exception.GetBaseException() != null
                        ? task.Exception.GetBaseException().Message
                        : "Unknown preload error.";
                    SBLog.Error($"[Preload] Manifest execution faulted: {errorMessage}", this);
                }
            }
            else
            {
                if (_preloadManifest == null)
                {
                    SBLog.Warn("[Preload] No ScenePreloadManifest assigned on WorldBattleBootstrap. Preload skipped.", this);
                }
                else
                {
                    SBLog.Info("[Preload] Skipping transition preload because startup preload already completed successfully.", this);
                }
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

        private static int CountFailedTasks(PreloadResult[] results)
        {
            if (results == null || results.Length == 0)
            {
                return 0;
            }

            int failed = 0;
            for (int i = 0; i < results.Length; i++)
            {
                if (!results[i].Success)
                {
                    failed++;
                }
            }

            return failed;
        }

        private IEnumerator PreloadThenSpawnEnemiesRoutine()
        {
            if (_preloadManifest == null)
            {
                SpawnEnemiesIfNeeded();
                _enemySpawnStartupRoutine = null;
                yield break;
            }

            SBLog.Info($"[Preload] Starting manifest '{_preloadManifest.name}' before enemy spawn.", this);
            var preloader = new ScenePreloader();
            Task<PreloadResult[]> task = preloader.RunAllAsync(_preloadManifest, destroyCancellationToken);
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.Status == TaskStatus.RanToCompletion)
            {
                PreloadResult[] results = task.Result ?? Array.Empty<PreloadResult>();
                int completedTaskCount = results.Length;
                int failedTaskCount = CountFailedTasks(results);

                if (completedTaskCount <= 0)
                {
                    SBLog.Warn("[Preload] Manifest executed but produced zero tasks before enemy spawn. Check manifest entries.", this);
                }
                else if (failedTaskCount > 0)
                {
                    SBLog.Warn($"[Preload] Pre-enemy spawn preload completed {completedTaskCount} task(s) with {failedTaskCount} failure(s).", this);
                }
                else
                {
                    SBLog.Info($"[Preload] Pre-enemy spawn preload completed {completedTaskCount} task(s) successfully.", this);
                    _startupPreloadCompletedSuccessfully = true;
                }
            }
            else if (task.IsCanceled || task.Status == TaskStatus.Canceled)
            {
                SBLog.Warn("[Preload] Pre-enemy spawn manifest execution was canceled.", this);
            }
            else if (task.IsFaulted || task.Status == TaskStatus.Faulted)
            {
                string errorMessage = task.Exception != null && task.Exception.GetBaseException() != null
                    ? task.Exception.GetBaseException().Message
                    : "Unknown preload error.";
                SBLog.Error($"[Preload] Pre-enemy spawn manifest execution faulted: {errorMessage}", this);
            }

            SpawnEnemiesIfNeeded();
            _enemySpawnStartupRoutine = null;
        }

        private void SpawnEnemiesIfNeeded()
        {
            if (_enemiesSpawned || _enemy == null)
            {
                return;
            }

            _enemy.StartEnemySquad();
            _enemiesSpawned = true;
        }

        private static void RegisterManifestAssetsForDiagnostics(ScenePreloadManifest manifest)
        {
            if (manifest == null)
            {
                return;
            }

            AssetCacheDiagnostics.RegisterManifestAssets(manifest.PrefabsToWarm);
            AssetCacheDiagnostics.RegisterManifestAssets(manifest.AudioClips);
            AssetCacheDiagnostics.RegisterManifestAssets(manifest.Sprites);
            AssetCacheDiagnostics.RegisterManifestAssets(manifest.Textures);
        }
    }
}
