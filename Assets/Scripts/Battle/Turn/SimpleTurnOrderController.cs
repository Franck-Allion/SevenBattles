using System;
using System.Collections.Generic;
using UnityEngine;
using SevenBattles.Battle.Board;
using SevenBattles.Battle.AI;
using SevenBattles.Battle.Units;
using SevenBattles.Core;
using SevenBattles.Battle.Spells;
using SevenBattles.Core.Battle;
using SevenBattles.Core.Save;
using SevenBattles.Battle.Tiles;

namespace SevenBattles.Battle.Turn
{
    // Basic initiative-based turn controller for wizards.
    // Discovers UnitBattleMetadata instances at battle start, sorts by UnitStats.Initiative,
    // and advances turns for player and AI units.
    public class SimpleTurnOrderController : MonoBehaviour, IBattleTurnController, ISpellSelectionController, IUnitInspectionController, IEnchantmentInspectionController
    {
        [Header("Board Highlight (delegated)")]
        [SerializeField] private WorldPerspectiveBoard _board; // Kept for other initialization if needed, or remove if unused. Checking usage needed. 
        [SerializeField, Tooltip("Service managing board highlights.")]
        private SevenBattles.Battle.Board.BattleBoardHighlightController _highlightController;
        [Header("Battlefield (optional)")]
        [SerializeField, Tooltip("Optional battlefield service used to resolve tile bonuses. If null, will be auto-found at runtime.")]
        private MonoBehaviour _battlefieldServiceBehaviour;
        [Header("Movement")]
        [Header("Movement")]
        [SerializeField, Tooltip("Duration in seconds for the active unit movement animation between tiles.")]
        private float _moveDurationSeconds = 0.35f;
        [SerializeField, Tooltip("Cursor texture displayed when hovering over a legal movement tile.")]
        private Texture2D _moveCursorTexture;
        [SerializeField, Tooltip("Hotspot offset for the move cursor (typically center of the texture).")]
        private Vector2 _moveCursorHotspot = new Vector2(16f, 16f);
        [SerializeField, Tooltip("Cursor texture displayed when a movement tile has been selected (awaiting confirmation).")]
        private Texture2D _selectionCursorTexture;
        [SerializeField, Tooltip("Hotspot offset for the selection cursor (typically center of the texture).")]
        private Vector2 _selectionCursorHotspot = new Vector2(16f, 16f);

        [SerializeField, Tooltip("Cursor texture displayed when hovering over an attackable enemy.")]
        private Texture2D _attackCursorTexture;
        [SerializeField, Tooltip("Hotspot offset for the attack cursor (typically center of the texture).")]
        private Vector2 _attackCursorHotspot = new Vector2(16f, 16f);

        [SerializeField, Tooltip("Cursor texture displayed when hovering over a shootable enemy.")]
        private Texture2D _shootCursorTexture;
        [SerializeField, Tooltip("Hotspot offset for the shoot cursor (typically center of the texture).")]
        private Vector2 _shootCursorHotspot = new Vector2(16f, 16f);

        [Header("Combat Management")]
        [SerializeField, Tooltip("Service managing attack validation, execution, and damage application. Should reference a BattleCombatController component.")]
        private SevenBattles.Battle.Combat.BattleCombatController _combatController;

        [Header("Movement Management")]
        [SerializeField, Tooltip("Service managing movement validation, BFS pathfinding, and movement execution. Should reference a BattleMovementController component.")]
        private SevenBattles.Battle.Movement.BattleMovementController _movementController;

        [Header("Spell Management")]
        [SerializeField, Tooltip("Service managing spell targeting, execution, and effects. Should reference a BattleSpellController component.")]
        private BattleSpellController _spellController;
        [SerializeField, Tooltip("Service managing enchantment placement and effects. Should reference a BattleEnchantmentController component.")]
        private BattleEnchantmentController _enchantmentController;

        [Header("Cursor Management")]
        [SerializeField, Tooltip("Service managing battle cursor states (move, attack, selection, spell). Should reference a BattleCursorController component.")]
        private SevenBattles.Battle.Cursors.BattleCursorController _cursorController;

        [Header("Visual Feedback")]
        [SerializeField, Tooltip("Service managing battle visual effects like damage numbers. Should reference the BattleVisualFeedbackService on _System GameObject.")]
        private BattleVisualFeedbackService _visualFeedback;
        [SerializeField, Tooltip("Optional VFX prefab instantiated when a unit dies (e.g., puff of smoke or soul effect).")]
        private GameObject _deathVfxPrefab;
        [SerializeField, Tooltip("Lifetime in seconds before the death VFX instance is destroyed.")]
        private float _deathVfxLifetimeSeconds = 2f;
        [SerializeField, Tooltip("Duration in seconds to wait after playing a death animation before removing the unit GameObject from the board.")]
        private float _deathAnimationDurationSeconds = 1f;



        [Header("Flow")]
        [SerializeField, Tooltip("If true, BeginBattle is called automatically on Start. Typically disabled when using placement flow.")]
        private bool _autoStartOnPlay = false;
        [SerializeField, Tooltip("Delay in seconds before AI units automatically end their turn.")]
        private float _aiTurnDelaySeconds = 2f;

        [Header("Debug")]
        [SerializeField] private bool _logTurns;

        private struct TurnUnit
        {
            public UnitBattleMetadata Metadata;
            public UnitStats Stats;
        }

        private sealed class TurnUnitProvider : ISpellUnitProvider
        {
            private List<TurnUnit> _units;

            public void Bind(List<TurnUnit> units)
            {
                _units = units;
            }

            public int Count => _units != null ? _units.Count : 0;

            public UnitBattleMetadata GetMetadata(int index)
            {
                if (_units == null || index < 0 || index >= _units.Count)
                {
                    return null;
                }

                return _units[index].Metadata;
            }

            public UnitStats GetStats(int index)
            {
                if (_units == null || index < 0 || index >= _units.Count)
                {
                    return null;
                }

                return _units[index].Stats;
            }

            public bool IsUnitAtTile(int index, Vector2Int tile)
            {
                if (_units == null || index < 0 || index >= _units.Count)
                {
                    return false;
                }

                var unit = _units[index];
                return IsUnitValid(unit) && unit.Metadata.Tile == tile;
            }
        }

        private readonly List<TurnUnit> _units = new List<TurnUnit>();
        private readonly Dictionary<UnitStats, TileStatBonus> _tileStatBonuses = new Dictionary<UnitStats, TileStatBonus>();
        private IBattlefieldService _battlefieldService;
        private TurnUnit _inspectedUnit;
        private bool _hasInspectedUnit;
        private UnitStats _inspectedStatsSubscription;
        private SpellDefinition _inspectedEnchantmentSpell;
        private string _inspectedEnchantmentCasterInstanceId;
        private string _inspectedEnchantmentCasterUnitId;
        private bool _inspectedEnchantmentIsPlayerControlledCaster;
        private int _inspectedEnchantmentQuadIndex = -1;
        private bool _hasInspectedEnchantment;
        private UnitStats _activeStatsSubscription;
        private int _activeIndex = -1;
        private bool _advancing;
        private bool _hasActiveUnit;
        private int _interactionLockCount;
        private int _turnIndex;
        private bool _interactionLocked;
        private int _activeUnitCurrentActionPoints;
        private int _activeUnitMaxActionPoints;
        private bool _activeUnitHasMoved;
        private bool _movementAnimating;
        private bool _attackAnimating;
        private bool _shootAnimating;
        private bool _spellAnimating;
        private int _cachedActiveSpeed;
        private int _cachedActiveAttack;
        private int _cachedActiveShoot;
        private int _cachedActiveShootRange;
        private bool _hasSelectedMoveTile;
        private Vector2Int _selectedMoveTile;
        private bool _aiMovePendingCompletion;
        private bool _aiDecisionInProgress;
        [SerializeField, Tooltip("Service responsible for evaluating AI turns.")]
        private BattleAiTurnService _aiTurnService;
        private readonly List<UnitBattleMetadata> _aiAllUnitsBuffer = new List<UnitBattleMetadata>();
        private readonly TurnUnitProvider _unitProvider = new TurnUnitProvider();
        
        [SerializeField, Tooltip("Service managing unit lifecycle (discovery, sorting, compaction, healing subscriptions).")]
        private BattleUnitLifecycleService _lifecycleService;

        // Cursor state is now managed by BattleCursorController
        private SpellDefinition _selectedSpell;
        private SpellDefinition[] _activeUnitDrawnSpells = Array.Empty<SpellDefinition>();
        private readonly HashSet<SpellDefinition> _spentActiveUnitSpells = new HashSet<SpellDefinition>();
        private bool _battleEnded;
        private BattleOutcome _battleOutcome = BattleOutcome.None;

        private void Awake()
        {
            if (_movementController == null)
            {
                _movementController = GetComponent<SevenBattles.Battle.Movement.BattleMovementController>();
                if (_movementController == null) Debug.LogError("[SimpleTurnOrderController] BattleMovementController dependency missing!");
            }

            if (_spellController == null)
            {
                _spellController = GetComponent<BattleSpellController>();
                if (_spellController == null) Debug.LogError("[SimpleTurnOrderController] BattleSpellController dependency missing!");
            }

            if (_enchantmentController == null)
            {
                _enchantmentController = GetComponent<BattleEnchantmentController>();
                if (_enchantmentController == null)
                {
                    _enchantmentController = FindObjectOfType<BattleEnchantmentController>();
                }
                if (_enchantmentController == null)
                {
                    _enchantmentController = gameObject.AddComponent<BattleEnchantmentController>();
                }
            }

            if (_combatController == null)
            {
                 _combatController = GetComponent<SevenBattles.Battle.Combat.BattleCombatController>();
                 if (_combatController == null) Debug.LogError("[SimpleTurnOrderController] BattleCombatController dependency missing!");
            }
             
            if (_visualFeedback == null)
            {
                 _visualFeedback = FindObjectOfType<BattleVisualFeedbackService>();
            }
            
            if (_cursorController == null)
            {
                _cursorController = FindObjectOfType<SevenBattles.Battle.Cursors.BattleCursorController>();
            }

            if (_highlightController == null)
            {
                _highlightController = GetComponent<SevenBattles.Battle.Board.BattleBoardHighlightController>();
                // Also try finding on _System if not adjacent, though strict component requirement preferred
                if (_highlightController == null) _highlightController = FindObjectOfType<SevenBattles.Battle.Board.BattleBoardHighlightController>();
            }

            if (_aiTurnService == null)
            {
                _aiTurnService = GetComponent<BattleAiTurnService>();
                if (_aiTurnService == null)
                {
                    _aiTurnService = gameObject.AddComponent<BattleAiTurnService>();
                }
            }

            if (_aiTurnService != null && _movementController != null)
            {
                _aiTurnService.SetMovementController(_movementController);
            }

            if (_lifecycleService == null)
            {
                _lifecycleService = GetComponent<BattleUnitLifecycleService>();
                if (_lifecycleService == null)
                {
                    _lifecycleService = gameObject.AddComponent<BattleUnitLifecycleService>();
                }
            }

            ResolveBattlefieldService();
        }

        private void OnEnable()
        {
            ResolveBattlefieldService();
            if (_battlefieldService != null)
            {
                _battlefieldService.BattlefieldChanged += HandleBattlefieldChanged;
            }
        }

        private void OnDisable()
        {
            ClearHealingSubscriptions();
            ClearActiveStatsSubscription();
            ClearInspectedEnemyInternal(false);
            ClearInspectedEnchantmentInternal(false);
            if (_battlefieldService != null)
            {
                _battlefieldService.BattlefieldChanged -= HandleBattlefieldChanged;
            }
        }

        public bool HasActiveUnit => _hasActiveUnit;

        public bool IsActiveUnitPlayerControlled => IsActiveUnitPlayerControlledInternal();

        public bool IsInteractionLocked => _interactionLocked;

        public int TurnIndex => _turnIndex;

        public bool HasBattleEnded => _battleEnded;

        public BattleOutcome Outcome => _battleOutcome;

        public SpellDefinition[] ActiveUnitSpells
        {
            get
            {
                return _hasActiveUnit ? _activeUnitDrawnSpells ?? Array.Empty<SpellDefinition>() : Array.Empty<SpellDefinition>();
            }
        }

        public SpellDefinition SelectedSpell => _selectedSpell;

        public Sprite ActiveUnitPortrait
        {
            get
            {
                if (!_hasActiveUnit || _activeIndex < 0 || _activeIndex >= _units.Count)
                {
                    return null;
                }
                var u = _units[_activeIndex];
                return u.Metadata != null ? u.Metadata.Portrait : null;
            }
        }

        public bool TryGetActiveUnitStats(out UnitStatsViewData stats)
        {
            stats = default;

            if (!_hasActiveUnit || _activeIndex < 0 || _activeIndex >= _units.Count)
            {
                return false;
            }

            var u = _units[_activeIndex];
            if (u.Stats == null)
            {
                return false;
            }

            stats = BuildStatsViewData(u.Stats, GetTileStatBonus(u.Stats));
            return true;
        }

        public bool HasInspectedEnemy => _hasInspectedUnit;

        public bool HasInspectedEnchantment => _hasInspectedEnchantment;

        public SpellDefinition InspectedEnchantmentSpell => _hasInspectedEnchantment ? _inspectedEnchantmentSpell : null;

        public bool InspectedEnchantmentIsPlayerControlledCaster => _hasInspectedEnchantment && _inspectedEnchantmentIsPlayerControlledCaster;

        public Sprite InspectedEnemyPortrait
        {
            get
            {
                if (!_hasInspectedUnit || _inspectedUnit.Metadata == null)
                {
                    return null;
                }

                return _inspectedUnit.Metadata.Portrait;
            }
        }

        public bool TryGetInspectedEnemyStats(out UnitStatsViewData stats)
        {
            stats = default;
            if (!_hasInspectedUnit || _inspectedUnit.Stats == null)
            {
                return false;
            }

            stats = BuildStatsViewData(_inspectedUnit.Stats, GetTileStatBonus(_inspectedUnit.Stats));
            return true;
        }

        public void ClearInspectedEnemy()
        {
            ClearInspectedEnemyInternal(true);
        }

        public bool TryGetInspectedEnchantmentSpellAmountPreview(out SpellAmountPreview preview)
        {
            preview = default;

            if (!_hasInspectedEnchantment || _inspectedEnchantmentSpell == null || _spellController == null)
            {
                return false;
            }

            if (!TryGetInspectedEnchantmentCaster(out var meta, out var stats))
            {
                return false;
            }

            return _spellController.TryGetSpellAmountPreview(_inspectedEnchantmentSpell, meta, stats, out preview);
        }

        public void ClearInspectedEnchantment()
        {
            ClearInspectedEnchantmentInternal(true);
        }

        private UnitStatsViewData BuildStatsViewData(UnitStats stats, TileStatBonus bonus)
        {
            return new UnitStatsViewData
            {
                Life = stats.Life,
                MaxLife = stats.MaxLife,
                Level = stats.Level,
                Force = stats.Attack,
                Shoot = stats.Shoot,
                Spell = stats.Spell,
                Speed = stats.Speed,
                Luck = stats.Luck,
                Defense = stats.Defense,
                Protection = stats.Protection,
                Initiative = stats.Initiative,
                Morale = stats.Morale,
                BonusLife = bonus.Life,
                BonusForce = bonus.Attack,
                BonusShoot = bonus.Shoot,
                BonusSpell = bonus.Spell,
                BonusSpeed = bonus.Speed,
                BonusLuck = bonus.Luck,
                BonusDefense = bonus.Defense,
                BonusProtection = bonus.Protection,
                BonusInitiative = bonus.Initiative,
                BonusMorale = bonus.Morale
            };
        }

        public bool TryGetActiveUnitSpellAmountPreview(SpellDefinition spell, out SpellAmountPreview preview)
        {
            preview = default;

            if (!_hasActiveUnit || _spellController == null || spell == null)
            {
                return false;
            }

            if (_activeIndex < 0 || _activeIndex >= _units.Count)
            {
                return false;
            }

            var unit = _units[_activeIndex];
            return _spellController.TryGetSpellAmountPreview(spell, unit.Metadata, unit.Stats, out preview);
        }

        public bool IsActiveUnitSpellSpentThisTurn(SpellDefinition spell)
        {
            if (!_hasActiveUnit || spell == null)
            {
                return false;
            }

            return _spentActiveUnitSpells.Contains(spell);
        }

        public event Action ActiveUnitChanged;
        public event Action ActiveUnitActionPointsChanged;
        public event Action ActiveUnitStatsChanged;
        public event Action<BattleOutcome> BattleEnded;
        public event Action SelectedSpellChanged;
        public event Action InspectedUnitChanged;
        public event Action InspectedUnitStatsChanged;
        public event Action InspectedEnchantmentChanged;

        public int ActiveUnitCurrentActionPoints => _hasActiveUnit ? _activeUnitCurrentActionPoints : 0;
        public int ActiveUnitMaxActionPoints => _hasActiveUnit ? _activeUnitMaxActionPoints : 0;
        public bool ActiveUnitHasMoved => _hasActiveUnit && _activeUnitHasMoved;

        public UnitBattleMetadata ActiveUnitMetadata
        {
            get
            {
                if (!_hasActiveUnit || _activeIndex < 0 || _activeIndex >= _units.Count)
                {
                    return null;
                }

                return _units[_activeIndex].Metadata;
            }
        }



        private void Start()
        {
            if (_autoStartOnPlay)
            {
                BeginBattle();
            }
        }

        private void LateUpdate()
        {
            if (_battleEnded) return;
            if (_interactionLocked) return;
            if (!HasActiveUnit) return;
            if (_movementAnimating) return;
            if (_attackAnimating) return;
            if (_shootAnimating) return;
            if (_spellAnimating) return;

            if (IsActiveUnitPlayerControlled)
            {
                UpdatePlayerTurnInput();
                return;
            }

            BeginAiTurnForActiveUnit();
        }

        // Entry point for scene/placement flow when combat actually begins.
        public void StartBattle()
        {
            BeginBattle();
        }

        public void SetInteractionLocked(bool locked)
        {
            if (locked)
            {
                _interactionLockCount++;
            }
            else
            {
                _interactionLockCount = Mathf.Max(0, _interactionLockCount - 1);
            }

            _interactionLocked = _interactionLockCount > 0;
        }

        // Internal rebuild used by StartBattle and tests.
        private void BeginBattle()
        {
            _battleEnded = false;
            _battleOutcome = BattleOutcome.None;
            RebuildUnits();
            ApplyTileStatBonusesForAllUnits();
            ResetSpellDecksForBattle();
            if (_enchantmentController != null)
            {
                _enchantmentController.ResetForBattle();
            }
            _turnIndex = 0;
            ConfigureBoardForBattle();
            if (_cursorController != null)
            {
                _cursorController.ApplyDefaultCursor();
            }
            SelectFirstUnit();
        }

        public void RequestEndTurn()
        {
            if (_battleEnded) return;
            // Treat any positive lock count as authoritative for blocking interaction,
            // even if the cached bool is temporarily out of sync.
            if (_interactionLockCount > 0 || _interactionLocked) return;
            if (!HasActiveUnit) return;
            if (!IsActiveUnitPlayerControlled)
            {
                if (_logTurns)
                {
                    Debug.Log("SimpleTurnOrderController: Ignoring player end-turn request during AI turn.", this);
                }
                return;
            }
            AdvanceToNextUnit();
        }

        public void SetSelectedSpell(SpellDefinition spell)
        {
            if (spell != null && (!IsSpellAvailableToActiveUnit(spell) || IsActiveUnitSpellSpentThisTurn(spell)))
            {
                spell = null;
            }

            ISpellEffectHandler newHandler = null;
            if (spell != null)
            {
                newHandler = _spellController != null ? _spellController.GetEffectHandler(spell) : null;
                if (newHandler == null || !TryBuildActiveSpellContext(out var context) ||
                    !newHandler.HasAvailableTargets(spell, context))
                {
                    spell = null;
                    newHandler = null;
                }
            }

            if (ReferenceEquals(_selectedSpell, spell))
            {
                return;
            }

            ClearInspectedEnchantmentInternal(true);

            var previousHandler = _selectedSpell != null && _spellController != null
                ? _spellController.GetEffectHandler(_selectedSpell)
                : null;
            bool wasEnchantmentTarget = previousHandler != null && previousHandler.TargetingMode == SpellTargetingMode.Enchantment;
            _selectedSpell = spell;

            // Spell targeting overrides any pending movement selection.
            _hasSelectedMoveTile = false;

            if (_board != null)
            {
                _board.SetSecondaryHighlightVisible(false);
            }

            if (_cursorController != null)
            {
                _cursorController.SetSpellCursor(_selectedSpell != null, _selectedSpell);
            }

            if (wasEnchantmentTarget && (newHandler == null || newHandler.TargetingMode != SpellTargetingMode.Enchantment))
            {
                _enchantmentController?.ClearHoverHighlight();
            }

            SelectedSpellChanged?.Invoke();
        }

        private void ConfigureBoardForBattle()
        {
            if (_board == null)
            {
                return;
            }

            // During combat, disable hover-driven highlight so only the active unit tile is marked,
            // and optionally switch to the dedicated active-unit highlight material.
            // Highlighting material is now handled by BattleBoardHighlightController.
            _board.SetHoverEnabled(false);

            if (_highlightController != null)
            {
                _highlightController.InitializeForBattle();
            }
        }

        private void ResolveBattlefieldService()
        {
            if (_battlefieldService != null)
            {
                return;
            }

            if (_battlefieldServiceBehaviour != null)
            {
                _battlefieldService = _battlefieldServiceBehaviour as IBattlefieldService;
            }

            if (_battlefieldService == null)
            {
                var behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
                for (int i = 0; i < behaviours.Length; i++)
                {
                    if (behaviours[i] is IBattlefieldService service)
                    {
                        _battlefieldService = service;
                        _battlefieldServiceBehaviour = behaviours[i];
                        break;
                    }
                }
            }
        }

        private void HandleBattlefieldChanged(BattlefieldDefinition battlefield)
        {
            ApplyTileStatBonusesForAllUnits();
        }

        private void RebuildUnits()
        {
            if (_lifecycleService == null)
            {
                Debug.LogError("[SimpleTurnOrderController] Lifecycle service not initialized!");
                return;
            }

            ClearHealingSubscriptions();
            _units.Clear();
            _activeIndex = -1;
            _tileStatBonuses.Clear();

            // Delegate discovery and sorting to lifecycle service
            var entries = _lifecycleService.DiscoverAndSortUnits();
            for (int i = 0; i < entries.Count; i++)
            {
                _units.Add(new TurnUnit { Metadata = entries[i].Metadata, Stats = entries[i].Stats });
            }

            // Subscribe to healing events via lifecycle service
            _lifecycleService.SubscribeToHealingEvents(entries, _visualFeedback);

            if (_logTurns)
            {
                Debug.Log($"SimpleTurnOrderController: Rebuilt units list, count={_units.Count}.", this);
            }
        }

        private void ApplyTileStatBonusesForAllUnits()
        {
            ResolveBattlefieldService();
            bool activeChanged = false;
            for (int i = 0; i < _units.Count; i++)
            {
                if (ApplyTileStatBonusForUnit(_units[i]))
                {
                    activeChanged = true;
                }
            }

            if (activeChanged)
            {
                RebuildLegalMoveTilesForActiveUnit();
                RebuildAttackableEnemyTiles();
            }
        }

        private bool ApplyTileStatBonusForUnit(TurnUnit unit)
        {
            ResolveBattlefieldService();
            var stats = unit.Stats;
            if (stats == null)
            {
                return false;
            }

            _tileStatBonuses.TryGetValue(stats, out var previousBonus);

            var currentBonus = default(TileStatBonus);
            if (BattleTileEffectRules.TryGetTileColor(_battlefieldService, unit.Metadata, out var color))
            {
                currentBonus = BattleTileEffectRules.GetStatBonus(color);
            }

            var delta = TileStatBonus.Subtract(currentBonus, previousBonus);
            bool changed = !delta.IsZero;
            if (!delta.IsZero)
            {
                stats.ApplyStatDelta(delta);
            }

            _tileStatBonuses[stats] = currentBonus;

            if (changed && IsActiveUnitStats(stats))
            {
                ActiveUnitStatsChanged?.Invoke();
                return true;
            }

            return false;
        }

        private TileStatBonus GetTileStatBonus(UnitStats stats)
        {
            if (stats == null)
            {
                return default;
            }

            if (_tileStatBonuses.TryGetValue(stats, out var bonus))
            {
                return bonus;
            }

            return default;
        }

        private bool IsActiveUnitStats(UnitStats stats)
        {
            if (stats == null || !_hasActiveUnit || _activeIndex < 0 || _activeIndex >= _units.Count)
            {
                return false;
            }

            return _units[_activeIndex].Stats == stats;
        }

        private void SelectFirstUnit()
        {
            if (_units.Count == 0)
            {
                _turnIndex = 0;
                SetActiveIndex(-1);
                return;
            }

            for (int i = 0; i < _units.Count; i++)
            {
                if (IsUnitValid(_units[i]))
                {
                    _turnIndex = 1;
                    SetActiveIndex(i);
                    return;
                }
            }

            _turnIndex = 0;
            SetActiveIndex(-1);
        }

        private void CompactUnits()
        {
            if (_lifecycleService == null)
            {
                Debug.LogError("[SimpleTurnOrderController] Lifecycle service not initialized!");
                return;
            }

            // Convert units to entries for lifecycle service
            var entries = new List<BattleUnitLifecycleService.UnitEntry>();
            for (int i = 0; i < _units.Count; i++)
            {
                entries.Add(new BattleUnitLifecycleService.UnitEntry 
                { 
                    Metadata = _units[i].Metadata, 
                    Stats = _units[i].Stats 
                });
            }

            // Delegate compaction to lifecycle service
            int oldActiveIndex = _activeIndex;
            _activeIndex = _lifecycleService.CompactDeadUnits(entries, _activeIndex);

            // Update local units list and clean up tile bonuses for removed units
            for (int i = _units.Count - 1; i >= 0; i--)
            {
                if (i >= entries.Count || !_lifecycleService.IsAlive(entries[i]))
                {
                    _tileStatBonuses.Remove(_units[i].Stats);
                }
            }

            _units.Clear();
            for (int i = 0; i < entries.Count; i++)
            {
                _units.Add(new TurnUnit { Metadata = entries[i].Metadata, Stats = entries[i].Stats });
            }

            // After compaction, always evaluate battle outcome
            EvaluateBattleOutcomeAndStop();
            if (_battleEnded)
            {
                return;
            }

            if (_units.Count == 0)
            {
                return;
            }

            // Ensure active index points to a valid unit
            if (_activeIndex < 0 || _activeIndex >= _units.Count || !IsUnitValid(_units[_activeIndex]))
            {
                int firstValid = -1;
                for (int i = 0; i < _units.Count; i++)
                {
                    if (IsUnitValid(_units[i]))
                    {
                        firstValid = i;
                        break;
                    }
                }

                if (firstValid >= 0)
                {
                    _activeIndex = firstValid;
                    _hasActiveUnit = true;
                }
            }
        }

        private void ClearHealingSubscriptions()
        {
            if (_lifecycleService != null)
            {
                _lifecycleService.UnsubscribeFromHealingEvents();
            }
        }

        private void SubscribeToHealing(UnitStats stats)
        {
            // Now handled by lifecycle service
        }

        private void UnsubscribeFromHealing(UnitStats stats)
        {
            // Now handled by lifecycle service during CompactUnits
        }

        private void SetInspectedEnemy(TurnUnit unit)
        {
            SetInspectedUnit(unit, allowPlayerUnits: false);
        }

        private void SetInspectedUnit(TurnUnit unit, bool allowPlayerUnits)
        {
            if (unit.Metadata == null)
            {
                return;
            }

            if (!allowPlayerUnits && unit.Metadata.IsPlayerControlled)
            {
                return;
            }

            if (_hasInspectedUnit && _inspectedUnit.Metadata == unit.Metadata)
            {
                return;
            }

            ClearInspectedEnemyInternal(false);
            _inspectedUnit = unit;
            _hasInspectedUnit = true;
            SubscribeToInspectedStats(unit.Stats);
            InspectedUnitChanged?.Invoke();
        }

        private void SetInspectedEnchantment(BattleEnchantmentController.EnchantmentSnapshot snapshot)
        {
            if (snapshot.Spell == null)
            {
                return;
            }

            ClearInspectedEnchantmentInternal(false);
            _hasInspectedEnchantment = true;
            _inspectedEnchantmentSpell = snapshot.Spell;
            _inspectedEnchantmentCasterInstanceId = snapshot.CasterInstanceId;
            _inspectedEnchantmentCasterUnitId = snapshot.CasterUnitId;
            _inspectedEnchantmentIsPlayerControlledCaster = snapshot.IsPlayerControlledCaster;
            _inspectedEnchantmentQuadIndex = snapshot.QuadIndex;
            InspectedEnchantmentChanged?.Invoke();
        }

        private void SubscribeToInspectedStats(UnitStats stats)
        {
            if (stats == null)
            {
                return;
            }

            _inspectedStatsSubscription = stats;
            stats.Changed += HandleInspectedUnitStatsChanged;
        }

        private void ClearInspectedEnemyInternal(bool notify)
        {
            if (_inspectedStatsSubscription != null)
            {
                _inspectedStatsSubscription.Changed -= HandleInspectedUnitStatsChanged;
                _inspectedStatsSubscription = null;
            }

            bool wasInspecting = _hasInspectedUnit;
            _hasInspectedUnit = false;
            _inspectedUnit = default;

            if (notify && wasInspecting)
            {
                InspectedUnitChanged?.Invoke();
            }
        }

        private void ClearInspectedEnchantmentInternal(bool notify)
        {
            bool wasInspecting = _hasInspectedEnchantment;
            _hasInspectedEnchantment = false;
            _inspectedEnchantmentSpell = null;
            _inspectedEnchantmentCasterInstanceId = null;
            _inspectedEnchantmentCasterUnitId = null;
            _inspectedEnchantmentIsPlayerControlledCaster = false;
            _inspectedEnchantmentQuadIndex = -1;

            if (notify && wasInspecting)
            {
                InspectedEnchantmentChanged?.Invoke();
            }
        }

        private bool TryGetInspectedEnchantmentCaster(out UnitBattleMetadata meta, out UnitStats stats)
        {
            meta = null;
            stats = null;

            if (!_hasInspectedEnchantment || _units == null || _units.Count == 0)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(_inspectedEnchantmentCasterInstanceId))
            {
                for (int i = 0; i < _units.Count; i++)
                {
                    var unit = _units[i];
                    if (!IsUnitValid(unit)) continue;
                    var candidateMeta = unit.Metadata;
                    if (candidateMeta == null) continue;
                    if (string.Equals(candidateMeta.SaveInstanceId, _inspectedEnchantmentCasterInstanceId, StringComparison.Ordinal))
                    {
                        meta = candidateMeta;
                        stats = unit.Stats;
                        return true;
                    }
                }
            }

            if (!string.IsNullOrEmpty(_inspectedEnchantmentCasterUnitId))
            {
                for (int i = 0; i < _units.Count; i++)
                {
                    var unit = _units[i];
                    if (!IsUnitValid(unit)) continue;
                    var candidateMeta = unit.Metadata;
                    if (candidateMeta == null) continue;
                    var def = candidateMeta.Definition;
                    if (def == null) continue;
                    if (!string.Equals(def.Id, _inspectedEnchantmentCasterUnitId, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (candidateMeta.IsPlayerControlled != _inspectedEnchantmentIsPlayerControlledCaster)
                    {
                        continue;
                    }

                    meta = candidateMeta;
                    stats = unit.Stats;
                    return true;
                }
            }

            return false;
        }

        private void SubscribeToActiveStats(UnitStats stats)
        {
            if (_activeStatsSubscription == stats)
            {
                return;
            }

            if (_activeStatsSubscription != null)
            {
                _activeStatsSubscription.Changed -= HandleActiveStatsChanged;
            }

            _activeStatsSubscription = stats;
            if (_activeStatsSubscription != null)
            {
                _activeStatsSubscription.Changed += HandleActiveStatsChanged;
            }
        }

        private void ClearActiveStatsSubscription()
        {
            if (_activeStatsSubscription != null)
            {
                _activeStatsSubscription.Changed -= HandleActiveStatsChanged;
                _activeStatsSubscription = null;
            }
        }

        private void HandleActiveStatsChanged()
        {
            if (_hasActiveUnit)
            {
                ActiveUnitStatsChanged?.Invoke();
                if (TryUpdateActiveUnitCachedStats())
                {
                    RebuildLegalMoveTilesForActiveUnit();
                    RebuildAttackableEnemyTiles();
                }
            }
        }

        private void HandleInspectedUnitStatsChanged()
        {
            if (_hasInspectedUnit)
            {
                InspectedUnitStatsChanged?.Invoke();
            }
        }

        private void HandleUnitHealed(UnitStats stats, int effectiveHealAmount)
        {
            if (_visualFeedback == null || stats == null)
            {
                return;
            }

            _visualFeedback.ShowHealNumber(stats.transform.position, effectiveHealAmount);
        }

        private void EvaluateBattleOutcomeAndStop()
        {
            if (_battleEnded)
            {
                return;
            }

            int playerAlive = 0;
            int enemyAlive = 0;

            for (int i = 0; i < _units.Count; i++)
            {
                var unit = _units[i];
                if (!IsUnitValid(unit))
                {
                    continue;
                }

                if (unit.Metadata != null && unit.Metadata.IsPlayerControlled)
                {
                    playerAlive++;
                }
                else
                {
                    enemyAlive++;
                }
            }

            // Determine outcome. If both sides reach zero simultaneously, treat as defeat.
            BattleOutcome outcome;
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
            else
            {
                // Both zero or still mixed; for MVP treat simultaneous zero as defeat for consistency.
                if (playerAlive <= 0 && enemyAlive <= 0)
                {
                    outcome = BattleOutcome.PlayerDefeat;
                    Debug.Log("[Battle] Player defeated (simultaneous wipe). Both squads have no units remaining.", this);
                }
                else
                {
                    // No terminal condition reached; keep battle running.
                    return;
                }
            }

            _battleEnded = true;
            _battleOutcome = outcome;

            _turnIndex = 0;
            SetActiveIndex(-1);

            var handler = BattleEnded;
            if (handler != null)
            {
                handler(outcome);
            }
        }

        private void AdvanceToNextUnit()
        {
            CompactUnits();

            if (_units.Count == 0)
            {
                // Battle outcome has already been evaluated in CompactUnits.
                return;
            }

            _advancing = true;
            try
            {
                int startIndex = _activeIndex;
                int count = _units.Count;
                if (startIndex < 0 || startIndex >= count)
                {
                    startIndex = -1;
                }

                int attempts = 0;
                int idx = startIndex;
                while (attempts < count)
                {
                    idx = (idx + 1) % count;
                    if (IsUnitValid(_units[idx]))
                    {
                        if (startIndex >= 0 && idx <= startIndex)
                        {
                            _turnIndex = Mathf.Max(1, _turnIndex + 1);
                        }
                        SetActiveIndex(idx);
                        return;
                    }
                    attempts++;
                }

                // No valid units remain.
                _turnIndex = 0;
                SetActiveIndex(-1);
            }
            finally
            {
                _advancing = false;
            }
        }

        public void RestoreFromSave(BattleTurnSaveData battleTurn)
        {
            if (battleTurn == null)
            {
                return;
            }

            RebuildUnits();
            ApplyTileStatBonusesForAllUnits();
            _turnIndex = Mathf.Max(0, battleTurn.TurnIndex);

            if (_units.Count == 0)
            {
                SetActiveIndex(-1);
                return;
            }

            int targetIndex = -1;

            if (!string.IsNullOrEmpty(battleTurn.ActiveUnitInstanceId))
            {
                for (int i = 0; i < _units.Count; i++)
                {
                    var meta = _units[i].Metadata;
                    if (meta != null && string.Equals(meta.SaveInstanceId, battleTurn.ActiveUnitInstanceId, StringComparison.Ordinal))
                    {
                        targetIndex = i;
                        break;
                    }
                }
            }

            if (targetIndex < 0 && !string.IsNullOrEmpty(battleTurn.ActiveUnitId))
            {
                for (int i = 0; i < _units.Count; i++)
                {
                    var meta = _units[i].Metadata;
                    if (meta == null) continue;
                    var def = meta.Definition;
                    string id = def != null ? def.Id : null;
                    bool isPlayer = meta.IsPlayerControlled;
                    string team = isPlayer ? "player" : "enemy";

                    if (string.Equals(id, battleTurn.ActiveUnitId, StringComparison.Ordinal) &&
                        (string.IsNullOrEmpty(battleTurn.ActiveUnitTeam) ||
                         string.Equals(battleTurn.ActiveUnitTeam, team, StringComparison.OrdinalIgnoreCase)))
                    {
                        targetIndex = i;
                        break;
                    }
                }
            }

            if (targetIndex < 0)
            {
                _turnIndex = 0;
                SetActiveIndex(-1);
                return;
            }

            ConfigureBoardForBattle();
            SetActiveIndex(targetIndex);

            if (_hasActiveUnit)
            {
                _activeUnitMaxActionPoints = Mathf.Max(0, battleTurn.ActiveUnitMaxActionPoints);
                _activeUnitCurrentActionPoints = Mathf.Clamp(battleTurn.ActiveUnitCurrentActionPoints, 0, _activeUnitMaxActionPoints);
                _activeUnitHasMoved = battleTurn.ActiveUnitHasMoved;

                // Rebuild movement grid now that AP and movement flags have been restored,
                // so legal move tiles match the loaded state instead of the initial stats.
                RebuildLegalMoveTilesForActiveUnit();
            }

            ActiveUnitChanged?.Invoke();
            ActiveUnitStatsChanged?.Invoke();
            ActiveUnitActionPointsChanged?.Invoke();
        }

        private void SetActiveIndex(int index)
        {
            ClearInspectedEnemyInternal(true);
            _activeIndex = index;
            _activeUnitHasMoved = false;
            _hasSelectedMoveTile = false;
            _aiMovePendingCompletion = false;
            _aiDecisionInProgress = false;
            _activeUnitDrawnSpells = Array.Empty<SpellDefinition>();
            _spentActiveUnitSpells.Clear();

            // Reset cursors when active unit changes
            if (_cursorController != null)
            {
                _cursorController.SetAttackCursor(false, _attackCursorTexture, _attackCursorHotspot);
                _cursorController.SetShootCursor(false, _shootCursorTexture, _shootCursorHotspot);
                _cursorController.SetMoveCursor(false, _moveCursorTexture, _moveCursorHotspot);
                _cursorController.SetSelectionCursor(false, _selectionCursorTexture, _selectionCursorHotspot);
            }
            SetSelectedSpell(null);

            if (_activeIndex < 0 || _activeIndex >= _units.Count || !IsUnitValid(_units[_activeIndex]))
            {
                _activeIndex = -1;
                _hasActiveUnit = false;
                _activeUnitCurrentActionPoints = 0;
                _activeUnitMaxActionPoints = 0;
                SubscribeToActiveStats(null);
            }
            else
            {
                _hasActiveUnit = true;

                var u = _units[_activeIndex];
                int baseAp = u.Stats != null ? u.Stats.ActionPoints : 0;
                baseAp = Mathf.Max(0, baseAp);
                _activeUnitMaxActionPoints = baseAp;
                _activeUnitCurrentActionPoints = baseAp;
                SubscribeToActiveStats(u.Stats);
                ApplyTileStatBonusForUnit(u);
                CacheActiveUnitStats(u.Stats);
            }

            UpdateBoardHighlight();
            RebuildLegalMoveTilesForActiveUnit();
            RebuildAttackableEnemyTiles();

            DrawActiveUnitSpells();

            if (_logTurns)
            {
                if (_hasActiveUnit)
                {
                    var u = _units[_activeIndex];
                    string side = u.Metadata != null && u.Metadata.IsPlayerControlled ? "Player" : "AI";
                    int initiative = u.Stats != null ? u.Stats.Initiative : 0;
                    Debug.Log($"SimpleTurnOrderController: Active -> {side} unit (initiative={initiative}, index={_activeIndex})", this);
                }
                else
                {
                    Debug.Log("SimpleTurnOrderController: No active unit (battle likely ended).", this);
                }
            }

            ActiveUnitChanged?.Invoke();
            ActiveUnitStatsChanged?.Invoke();
            ActiveUnitActionPointsChanged?.Invoke();

            if (_hasActiveUnit && !IsActiveUnitPlayerControlledInternal())
            {
                BeginAiTurnForActiveUnit();
            }
        }

        private void ResetSpellDecksForBattle()
        {
            for (int i = 0; i < _units.Count; i++)
            {
                var meta = _units[i].Metadata;
                if (meta == null) continue;
                var deck = meta.GetComponent<UnitSpellDeck>();
                if (deck != null)
                {
                    deck.ResetDeckForBattle();
                }
            }
        }

        private void DrawActiveUnitSpells()
        {
            if (!_hasActiveUnit || _activeIndex < 0 || _activeIndex >= _units.Count)
            {
                _activeUnitDrawnSpells = Array.Empty<SpellDefinition>();
                return;
            }

            var unit = _units[_activeIndex];
            var meta = unit.Metadata;
            if (meta == null)
            {
                _activeUnitDrawnSpells = Array.Empty<SpellDefinition>();
                return;
            }

            var deck = meta.GetComponent<UnitSpellDeck>();
            if (deck == null)
            {
                _activeUnitDrawnSpells = Array.Empty<SpellDefinition>();
                return;
            }

            _activeUnitDrawnSpells = deck.DrawForTurn();
        }

        private void BeginAiTurnForActiveUnit()
        {
            if (_battleEnded) return;
            if (_aiMovePendingCompletion || _aiDecisionInProgress) return;
            if (_advancing) return;
            if (!_hasActiveUnit || _activeIndex < 0 || _activeIndex >= _units.Count) return;
            if (IsActiveUnitPlayerControlledInternal()) return;
            if (_movementAnimating || _attackAnimating || _shootAnimating || _spellAnimating) return;

            if (TryExecuteAiAttack())
            {
                return;
            }

            if (TryExecuteAiShoot())
            {
                return;
            }

            if (_aiTurnService == null)
            {
                CompleteAiTurnAfterDecision();
                return;
            }

            _aiDecisionInProgress = true;
            RebuildLegalMoveTilesForActiveUnit();
            var decision = _aiTurnService.EvaluateMovement(BuildAiContext());
            _aiDecisionInProgress = false;

            if (!string.IsNullOrEmpty(decision.LogMessage))
            {
                Debug.Log(decision.LogMessage, this);
            }

            if (decision.Type == BattleAiTurnService.DecisionType.Move)
            {
                _aiMovePendingCompletion = true;
                TryExecuteActiveUnitMove(decision.Destination);
            }
            else
            {
                CompleteAiTurnAfterDecision();
            }
        }

        private bool TryExecuteAiAttack()
        {
            if (_combatController == null) return false;
            if (!_hasActiveUnit || _activeIndex < 0 || _activeIndex >= _units.Count) return false;
            if (_movementAnimating || _attackAnimating || _shootAnimating || _spellAnimating) return false;
            if (_activeUnitCurrentActionPoints <= 0) return false;

            var activeUnit = _units[_activeIndex];
            var stats = activeUnit.Stats;
            if (stats == null) return false;

            RebuildAttackableEnemyTiles();
            if (!TryGetAttackableEnemyTile(activeUnit, out var targetTile)) return false;

            _combatController.TryExecuteAttack(
                targetTile,
                activeUnit,
                _units,
                u => u.Metadata,
                u => u.Stats,
                () => { _attackAnimating = true; },
                () => { ConsumeActiveUnitActionPoint(); },
                () =>
                {
                    CompactUnits();
                    if (!_battleEnded && _hasActiveUnit && _activeIndex >= 0 && _activeIndex < _units.Count)
                    {
                        RebuildAttackableEnemyTiles();
                    }
                },
                () =>
                {
                    _attackAnimating = false;
                    UpdateBoardHighlight();
                    CompleteAiTurnAfterDecision();
                }
            );

            return true;
        }

        private bool TryExecuteAiShoot()
        {
            if (_combatController == null) return false;
            if (!_hasActiveUnit || _activeIndex < 0 || _activeIndex >= _units.Count) return false;
            if (_movementAnimating || _attackAnimating || _shootAnimating || _spellAnimating) return false;
            if (_activeUnitCurrentActionPoints <= 0) return false;

            var activeUnit = _units[_activeIndex];
            var stats = activeUnit.Stats;
            if (stats == null || stats.Shoot <= 0 || stats.ShootRange <= 0) return false;

            RebuildAttackableEnemyTiles();
            if (!TryGetShootableEnemyTile(activeUnit, out var targetTile)) return false;

            _combatController.TryExecuteShoot(
                targetTile,
                activeUnit,
                _units,
                u => u.Metadata,
                u => u.Stats,
                () => { _shootAnimating = true; },
                () => { ConsumeActiveUnitActionPoint(); },
                () =>
                {
                    CompactUnits();
                    if (!_battleEnded && _hasActiveUnit && _activeIndex >= 0 && _activeIndex < _units.Count)
                    {
                        RebuildAttackableEnemyTiles();
                    }
                },
                () =>
                {
                    _shootAnimating = false;
                    UpdateBoardHighlight();
                    CompleteAiTurnAfterDecision();
                }
            );

            return true;
        }

        private bool TryGetAttackableEnemyTile(TurnUnit activeUnit, out Vector2Int targetTile)
        {
            targetTile = default;

            if (_combatController == null) return false;

            var meta = activeUnit.Metadata;
            if (meta == null || !meta.HasTile) return false;

            var origin = meta.Tile;
            Vector2Int[] offsets =
            {
                new Vector2Int(0, 1),
                new Vector2Int(0, -1),
                new Vector2Int(1, 0),
                new Vector2Int(-1, 0)
            };

            for (int i = 0; i < offsets.Length; i++)
            {
                var candidate = origin + offsets[i];
                if (_combatController.IsAttackableEnemyTile(candidate))
                {
                    targetTile = candidate;
                    return true;
                }
            }

            return false;
        }

        private bool TryGetShootableEnemyTile(TurnUnit activeUnit, out Vector2Int targetTile)
        {
            targetTile = default;

            if (_combatController == null) return false;

            var meta = activeUnit.Metadata;
            var stats = activeUnit.Stats;
            if (meta == null || stats == null || !meta.HasTile) return false;

            int range = Mathf.Max(0, stats.ShootRange);
            if (range <= 0) return false;

            var origin = meta.Tile;
            int maxStep = range + 1;
            Vector2Int[] directions =
            {
                new Vector2Int(0, 1),
                new Vector2Int(0, -1),
                new Vector2Int(1, 0),
                new Vector2Int(-1, 0)
            };

            for (int d = 0; d < directions.Length; d++)
            {
                var dir = directions[d];
                for (int step = 1; step <= maxStep; step++)
                {
                    var candidate = new Vector2Int(origin.x + (dir.x * step), origin.y + (dir.y * step));
                    if (_combatController.IsShootableEnemyTile(candidate))
                    {
                        targetTile = candidate;
                        return true;
                    }
                }
            }

            return false;
        }

        private BattleAiTurnService.Context BuildAiContext()
        {
            _aiAllUnitsBuffer.Clear();
            for (int i = 0; i < _units.Count; i++)
            {
                if (!IsUnitValid(_units[i])) continue;
                if (_units[i].Metadata == null) continue;
                _aiAllUnitsBuffer.Add(_units[i].Metadata);
            }

            var activeUnit = _units[_activeIndex];
            return new BattleAiTurnService.Context
            {
                ActiveUnit = activeUnit.Metadata,
                ActiveStats = activeUnit.Stats,
                CurrentActionPoints = _activeUnitCurrentActionPoints,
                HasMoved = _activeUnitHasMoved,
                AllUnits = _aiAllUnitsBuffer
            };
        }

        private void CompleteAiTurnAfterDecision()
        {
            _aiMovePendingCompletion = false;
            _aiDecisionInProgress = false;

            if (_battleEnded) return;
            if (!_hasActiveUnit || IsActiveUnitPlayerControlledInternal()) return;
            if (_movementAnimating || _attackAnimating || _shootAnimating || _spellAnimating) return;
            if (_advancing) return;

            AdvanceToNextUnit();
        }


        private void UpdatePlayerTurnInput()
        {
            if (_selectedSpell != null)
            {
                UpdateSpellTargetingInput(_selectedSpell);
                return;
            }

            if (_board == null) return;

            if (Input.GetMouseButtonDown(0) && _hasInspectedEnchantment)
            {
                ClearInspectedEnchantmentInternal(true);
            }

            if (Input.GetMouseButtonDown(1))
            {
                if (TryInspectEnchantmentAtScreenPosition(Input.mousePosition))
                {
                    return;
                }

                if (TryInspectUnitAtScreenPosition(Input.mousePosition, allowPlayerUnits: true))
                {
                    ClearInspectedEnchantmentInternal(true);
                    return;
                }

                if (_hasInspectedEnchantment)
                {
                    ClearInspectedEnchantmentInternal(true);
                }
            }

            // First check if we can do anything at all
            bool canMove = CanActiveUnitMove();
            bool canAttack = _combatController != null && _hasActiveUnit && _activeIndex >= 0 && _activeIndex < _units.Count
                && !_shootAnimating
                && _combatController.CanAttack(_units[_activeIndex].Stats, _activeUnitCurrentActionPoints, IsActiveUnitPlayerControlledInternal(), _movementAnimating, _attackAnimating);
            bool canShoot = _combatController != null && _hasActiveUnit && _activeIndex >= 0 && _activeIndex < _units.Count
                && _combatController.CanShoot(_units[_activeIndex].Stats, _activeUnitCurrentActionPoints, IsActiveUnitPlayerControlledInternal(), _movementAnimating, _attackAnimating, _shootAnimating);

            if (!canMove && !canAttack && !canShoot)
            {
                if (_highlightController != null)
                {
                    _highlightController.HideSecondaryHighlight();
                }
                _hasSelectedMoveTile = false;
                if (_cursorController != null)
                {
                    _cursorController.SetAttackCursor(false, _attackCursorTexture, _attackCursorHotspot);
                    _cursorController.SetShootCursor(false, _shootCursorTexture, _shootCursorHotspot);
                    _cursorController.SetMoveCursor(false, _moveCursorTexture, _moveCursorHotspot);
                    _cursorController.SetSelectionCursor(false, _selectionCursorTexture, _selectionCursorHotspot);
                }
                UpdateBoardHighlight();
                return;
            }

            if (!_board.TryScreenToTile(Input.mousePosition, out var x, out var y))
            {
                if (!_hasSelectedMoveTile)
                {
                    UpdateBoardHighlight();
                }
                if (_cursorController != null)
                {
                    _cursorController.SetAttackCursor(false, _attackCursorTexture, _attackCursorHotspot);
                    _cursorController.SetShootCursor(false, _shootCursorTexture, _shootCursorHotspot);
                    _cursorController.SetMoveCursor(false, _moveCursorTexture, _moveCursorHotspot);
                    _cursorController.SetSelectionCursor(false, _selectionCursorTexture, _selectionCursorHotspot);
                }
                return;
            }

            var hoveredTile = new Vector2Int(x, y);

            // PRIORITY 1: Shoot input handling (takes priority over movement)
            if (canShoot && _combatController != null && _combatController.IsShootableEnemyTile(hoveredTile))
            {
                if (_cursorController != null)
                {
                    _cursorController.SetShootCursor(true, _shootCursorTexture, _shootCursorHotspot);
                    _cursorController.SetAttackCursor(false, _attackCursorTexture, _attackCursorHotspot);
                    _cursorController.SetMoveCursor(false, _moveCursorTexture, _moveCursorHotspot);
                    _cursorController.SetSelectionCursor(false, _selectionCursorTexture, _selectionCursorHotspot);
                }

                if (_highlightController != null)
                {
                    _highlightController.SetSecondaryHighlight(hoveredTile, true);
                }

                if (Input.GetMouseButtonDown(0) && _hasActiveUnit && _activeIndex >= 0 && _activeIndex < _units.Count)
                {
                    _combatController.TryExecuteShoot(
                        hoveredTile,
                        _units[_activeIndex],
                        _units,
                        u => u.Metadata,
                        u => u.Stats,
                        () => { _shootAnimating = true; },
                        () => { ConsumeActiveUnitActionPoint(); },
                        () =>
                        {
                            CompactUnits();
                            if (!_battleEnded && _hasActiveUnit && _activeIndex >= 0 && _activeIndex < _units.Count)
                            {
                                RebuildAttackableEnemyTiles();
                            }
                        },
                        () => { _shootAnimating = false; UpdateBoardHighlight(); }
                    );
                }

                return;
            }

            // PRIORITY 2: Attack input handling
            if (canAttack && _combatController != null && _combatController.IsAttackableEnemyTile(hoveredTile))
            {
                // Show attack cursor and highlight
                if (_cursorController != null)
                {
                    _cursorController.SetAttackCursor(true, _attackCursorTexture, _attackCursorHotspot);
                    _cursorController.SetShootCursor(false, _shootCursorTexture, _shootCursorHotspot);
                    _cursorController.SetMoveCursor(false, _moveCursorTexture, _moveCursorHotspot);
                    _cursorController.SetSelectionCursor(false, _selectionCursorTexture, _selectionCursorHotspot);
                }

                if (_highlightController != null)
                {
                    _highlightController.SetSecondaryHighlight(hoveredTile, _combatController.AttackCursorColor);
                }

                if (Input.GetMouseButtonDown(0) && _hasActiveUnit && _activeIndex >= 0 && _activeIndex < _units.Count)
                {
                    _combatController.TryExecuteAttack(
                        hoveredTile,
                        _units[_activeIndex],
                        _units,
                        u => u.Metadata,
                        u => u.Stats,
                        () => { _attackAnimating = true; },
                        () => { ConsumeActiveUnitActionPoint(); },
                        () => { 
                            CompactUnits(); 
                            if (!_battleEnded && _hasActiveUnit && _activeIndex >= 0 && _activeIndex < _units.Count)
                            {
                                RebuildAttackableEnemyTiles();
                            }
                        },
                        () => { _attackAnimating = false; UpdateBoardHighlight(); }
                    );
                }

                return;
            }

            // Reset attack cursor if not hovering attackable enemy
            if (_cursorController != null)
            {
                _cursorController.SetAttackCursor(false, _attackCursorTexture, _attackCursorHotspot);
            }

            // Reset shoot cursor if not hovering shootable enemy
            if (_cursorController != null)
            {
                _cursorController.SetShootCursor(false, _shootCursorTexture, _shootCursorHotspot);
            }

            // PRIORITY 3: Movement input handling (fallback if not attacking or shooting)
            if (!canMove)
            {
                if (_highlightController != null)
                {
                    _highlightController.HideSecondaryHighlight();
                }
                _hasSelectedMoveTile = false;
                if (_cursorController != null)
                {
                    _cursorController.SetMoveCursor(false, _moveCursorTexture, _moveCursorHotspot);
                    _cursorController.SetSelectionCursor(false, _selectionCursorTexture, _selectionCursorHotspot);
                }
                UpdateBoardHighlight();
                return;
            }

            if (_hasSelectedMoveTile)
            {
                bool stillValid = IsTileLegalMoveDestination(_selectedMoveTile);
                if (_highlightController != null)
                {
                    _highlightController.SetSecondaryHighlight(_selectedMoveTile, stillValid);
                }
                
                if (_cursorController != null)
                {
                    _cursorController.SetMoveCursor(false, _moveCursorTexture, _moveCursorHotspot);
                    _cursorController.SetSelectionCursor(true, _selectionCursorTexture, _selectionCursorHotspot);
                }

                if (Input.GetMouseButtonDown(0))
                {
                    if (hoveredTile == _selectedMoveTile)
                    {
                        TryExecuteActiveUnitMove(_selectedMoveTile);
                    }
                    else
                    {
                        // Reset selection when clicking any other tile
                        _hasSelectedMoveTile = false;
                        if (_cursorController != null)
                        {
                            _cursorController.SetSelectionCursor(false, _selectionCursorTexture, _selectionCursorHotspot);
                        }
                    }
                }

                return;
            }

            bool legal = IsTileLegalMoveDestination(hoveredTile);
            if (_highlightController != null)
            {
                _highlightController.SetSecondaryHighlight(hoveredTile, legal);
            }

            // Show move cursor only when hovering a legal movement tile
            if (_cursorController != null)
            {
                _cursorController.SetMoveCursor(legal, _moveCursorTexture, _moveCursorHotspot);
            }

            if (Input.GetMouseButtonDown(0) && legal)
            {
                _hasSelectedMoveTile = true;
                _selectedMoveTile = hoveredTile;
            }
        }

        private bool TryInspectEnchantmentAtScreenPosition(Vector2 screenPosition)
        {
            if (_enchantmentController == null)
            {
                return false;
            }

            if (!_enchantmentController.TryGetActiveEnchantmentAtScreenPosition(screenPosition, out var snapshot, out var quadIndex))
            {
                return false;
            }

            if (_hasInspectedEnchantment && _inspectedEnchantmentQuadIndex == quadIndex)
            {
                ClearInspectedEnchantmentInternal(true);
                return true;
            }

            SetInspectedEnchantment(snapshot);
            return true;
        }

        private bool TryInspectEnemyAtScreenPosition(Vector2 screenPosition)
        {
            return TryInspectUnitAtScreenPosition(screenPosition, allowPlayerUnits: false);
        }

        private bool TryInspectEnemyAtTile(Vector2Int tile)
        {
            return TryInspectUnitAtTile(tile, allowPlayerUnits: false);
        }

        private bool TryInspectUnitAtScreenPosition(Vector2 screenPosition, bool allowPlayerUnits)
        {
            if (_board == null)
            {
                return false;
            }

            if (!_board.TryScreenToTile(screenPosition, out var x, out var y))
            {
                return false;
            }

            return TryInspectUnitAtTile(new Vector2Int(x, y), allowPlayerUnits);
        }

        private bool TryInspectUnitAtTile(Vector2Int tile, bool allowPlayerUnits)
        {
            if (!TryGetValidUnitAtTile(tile, out var unit))
            {
                return false;
            }

            if (unit.Metadata == null)
            {
                return false;
            }

            if (!allowPlayerUnits && unit.Metadata.IsPlayerControlled)
            {
                return false;
            }

            SetInspectedUnit(unit, allowPlayerUnits);
            return true;
        }

        private void UpdateSpellTargetingInput(SpellDefinition spell)
        {
            if (_board == null)
            {
                return;
            }

            var handler = _spellController != null ? _spellController.GetEffectHandler(spell) : null;
            if (handler == null)
            {
                return;
            }

            if (handler.TargetingMode == SpellTargetingMode.Enchantment)
            {
                UpdateEnchantmentTargetingInput(spell, handler);
                return;
            }

            // Cancel spell targeting (AAA-style): RMB or ESC clears selection.
            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
            {
                SetSelectedSpell(null);
                if (_cursorController != null)
                {
                    _cursorController.SetSpellCursor(false, _selectedSpell);
                }
                _board.SetSecondaryHighlightVisible(false);
                UpdateBoardHighlight();
                return;
            }

            // Spell targeting uses its own cursor and always clears move-selection visuals.
            _hasSelectedMoveTile = false;
            if (_cursorController != null)
            {
                _cursorController.SetSelectionCursor(false, _selectionCursorTexture, _selectionCursorHotspot);
                _cursorController.SetMoveCursor(false, _moveCursorTexture, _moveCursorHotspot);
                _cursorController.SetAttackCursor(false, _attackCursorTexture, _attackCursorHotspot);
                _cursorController.SetShootCursor(false, _shootCursorTexture, _shootCursorHotspot);
                _cursorController.SetSpellCursor(true, _selectedSpell);
            }

            if (!CanActiveUnitCastSpell(spell))
            {
                _board.SetSecondaryHighlightVisible(false);
                return;
            }

            if (!TryBuildActiveSpellContext(out var context))
            {
                _board.SetSecondaryHighlightVisible(false);
                return;
            }

            if (!_board.TryScreenToTile(Input.mousePosition, out var x, out var y))
            {
                _board.SetSecondaryHighlightVisible(false);
                return;
            }

            var hoveredTile = new Vector2Int(x, y);
            var target = SpellTargetSelection.ForTile(hoveredTile);
            bool eligible = handler.IsTargetValid(spell, context, target);

            if (_highlightController != null)
            {
                _highlightController.SetSecondaryHighlight(hoveredTile, eligible);
            }

            if (Input.GetMouseButtonDown(0) && eligible)
            {
                TryExecuteSpellEffect(spell, target);
            }
        }

        private void UpdateEnchantmentTargetingInput(SpellDefinition spell, ISpellEffectHandler handler)
        {
            if (_enchantmentController == null)
            {
                return;
            }

            // Cancel enchantment targeting: RMB or ESC clears selection.
            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
            {
                SetSelectedSpell(null);
                if (_cursorController != null)
                {
                    _cursorController.SetSpellCursor(false, _selectedSpell);
                }
                _enchantmentController.ClearHoverHighlight();
                UpdateBoardHighlight();
                return;
            }

            _hasSelectedMoveTile = false;
            if (_cursorController != null)
            {
                _cursorController.SetSelectionCursor(false, _selectionCursorTexture, _selectionCursorHotspot);
                _cursorController.SetMoveCursor(false, _moveCursorTexture, _moveCursorHotspot);
                _cursorController.SetAttackCursor(false, _attackCursorTexture, _attackCursorHotspot);
                _cursorController.SetShootCursor(false, _shootCursorTexture, _shootCursorHotspot);
                _cursorController.SetSpellCursor(true, _selectedSpell);
            }

            if (_highlightController != null)
            {
                _highlightController.HideSecondaryHighlight();
            }
            if (_board != null)
            {
                _board.SetSecondaryHighlightVisible(false);
            }

            if (!CanActiveUnitCastSpell(spell))
            {
                _enchantmentController.ClearHoverHighlight();
                return;
            }

            if (!TryBuildActiveSpellContext(out var context))
            {
                _enchantmentController.ClearHoverHighlight();
                return;
            }

            int hoveredIndex;
            bool hasTarget = handler.UsesActiveEnchantments
                ? _enchantmentController.TryUpdateActiveEnchantmentHighlight(Input.mousePosition, out hoveredIndex)
                : _enchantmentController.TryUpdateHoverHighlight(Input.mousePosition, out hoveredIndex);

            if (hasTarget)
            {
                var target = SpellTargetSelection.ForQuad(hoveredIndex);
                if (handler.IsTargetValid(spell, context, target) && Input.GetMouseButtonDown(0))
                {
                    TryExecuteSpellEffect(spell, target);
                }
            }
        }

        private void TryExecuteSpellEffect(SpellDefinition spell, SpellTargetSelection target)
        {
            if (_spellController == null) return;
            if (spell == null) return;
            if (_activeIndex < 0 || _activeIndex >= _units.Count) return;
            if (IsActiveUnitSpellSpentThisTurn(spell)) return;
            if (!CanActiveUnitCastSpell(spell)) return;
            if (!TryBuildActiveSpellContext(out var context)) return;

            var handler = _spellController.GetEffectHandler(spell);
            if (handler == null)
            {
                return;
            }

            if (!handler.IsTargetValid(spell, context, target))
            {
                return;
            }

            bool applied = false;
            var callbacks = new SpellCastCallbacks(
                onStart: () =>
                {
                    _spellAnimating = true;
                    applied = true;
                    MarkSpellSpentThisTurn(spell);
                    if (_cursorController != null) _cursorController.SetSpellCursor(false, _selectedSpell);
                    if (_board != null) _board.SetSecondaryHighlightVisible(false);
                },
                onApConsumed: cost => ConsumeActiveUnitActionPoints(cost),
                onStatsChanged: () => ActiveUnitStatsChanged?.Invoke(),
                onUnitDied: meta =>
                {
                    HandleUnitDeathVfxAndCleanup(meta);
                    CompactUnits();
                },
                onComplete: () =>
                {
                    RebuildAttackableEnemyTiles();
                    UpdateBoardHighlight();
                    if (handler.TargetingMode == SpellTargetingMode.Enchantment)
                    {
                        _enchantmentController?.ClearHoverHighlight();
                    }
                    if (ShouldRemoveSpellFromDeck(handler, spell, applied))
                    {
                        RemoveSpellFromActiveUnitDeck(spell);
                    }
                    SetSelectedSpell(null);
                    _spellAnimating = false;
                }
            );

            _spellController.TryExecuteSpellEffect(spell, context, target, callbacks);
        }

        private void TryExecuteSpellCast(SpellDefinition spell, Vector2Int targetTile)
        {
            TryExecuteSpellEffect(spell, SpellTargetSelection.ForTile(targetTile));
        }

        private void HandleUnitDeathVfxAndCleanup(UnitBattleMetadata targetMeta)
        {
            if (targetMeta == null)
            {
                return;
            }

            UnitVisualUtil.TryPlayAnimation(targetMeta.gameObject, "Death");

            if (_deathVfxPrefab != null)
            {
                var vfxInstance = Instantiate(_deathVfxPrefab, targetMeta.transform.position, Quaternion.identity);
                if (_deathVfxLifetimeSeconds > 0f)
                {
                    Destroy(vfxInstance, _deathVfxLifetimeSeconds);
                }
            }

            var def = targetMeta.Definition;
            if (def != null && def.DeathSfx != null)
            {
                float volume = 1f;
                try
                {
                    volume = Mathf.Clamp(def.DeathSfxVolume, 0f, 1.5f);
                }
                catch
                {
                    volume = 1f;
                }

                AudioSource.PlayClipAtPoint(def.DeathSfx, targetMeta.transform.position, volume);
            }

            StartCoroutine(HandleUnitDeathCleanup(targetMeta));
        }

        private bool TryBuildActiveSpellContext(out SpellCastContext context)
        {
            context = default;

            if (_spellController == null)
            {
                return false;
            }

            if (!_hasActiveUnit || _activeIndex < 0 || _activeIndex >= _units.Count)
            {
                return false;
            }

            var caster = _units[_activeIndex];
            if (!IsUnitValid(caster))
            {
                return false;
            }

            _unitProvider.Bind(_units);
            context = new SpellCastContext(_spellController, _enchantmentController, _unitProvider, caster.Metadata, caster.Stats);
            return true;
        }

        private bool IsSpellAvailableToActiveUnit(SpellDefinition spell)
        {
            if (spell == null) return false;
            if (!_hasActiveUnit || _activeIndex < 0 || _activeIndex >= _units.Count) return false;

            var spells = _activeUnitDrawnSpells;
            if (spells == null || spells.Length == 0) return false;

            for (int i = 0; i < spells.Length; i++)
            {
                if (ReferenceEquals(spells[i], spell))
                {
                    return true;
                }
            }

            return false;
        }

        private void MarkSpellSpentThisTurn(SpellDefinition spell)
        {
            if (spell == null)
            {
                return;
            }

            _spentActiveUnitSpells.Add(spell);
        }

        private static bool ShouldRemoveSpellFromDeck(ISpellEffectHandler handler, SpellDefinition spell, bool applied)
        {
            if (!applied || handler == null || spell == null)
            {
                return false;
            }

            switch (handler.RemovalPolicy)
            {
                case SpellRemovalPolicy.Always:
                    return true;
                case SpellRemovalPolicy.EphemeralOnly:
                    return spell.IsEphemeral;
                default:
                    return false;
            }
        }

        private void RemoveSpellFromActiveUnitDeck(SpellDefinition spell)
        {
            if (spell == null || !_hasActiveUnit || _activeIndex < 0 || _activeIndex >= _units.Count)
            {
                return;
            }

            var unit = _units[_activeIndex];
            var meta = unit.Metadata;
            if (meta == null)
            {
                return;
            }

            var deck = meta.GetComponent<UnitSpellDeck>();
            if (deck != null)
            {
                deck.RemoveSpellForBattle(spell);
            }

            RemoveSpellFromActiveUnitDrawn(spell);
        }

        private void RemoveSpellFromActiveUnitDrawn(SpellDefinition spell)
        {
            if (_activeUnitDrawnSpells == null || _activeUnitDrawnSpells.Length == 0 || spell == null)
            {
                return;
            }

            int count = 0;
            for (int i = 0; i < _activeUnitDrawnSpells.Length; i++)
            {
                if (!ReferenceEquals(_activeUnitDrawnSpells[i], spell))
                {
                    count++;
                }
            }

            if (count == _activeUnitDrawnSpells.Length)
            {
                return;
            }

            if (count == 0)
            {
                _activeUnitDrawnSpells = Array.Empty<SpellDefinition>();
                return;
            }

            var next = new SpellDefinition[count];
            int index = 0;
            for (int i = 0; i < _activeUnitDrawnSpells.Length; i++)
            {
                var item = _activeUnitDrawnSpells[i];
                if (!ReferenceEquals(item, spell))
                {
                    next[index++] = item;
                }
            }

            _activeUnitDrawnSpells = next;
        }

        public bool CanActiveUnitCastSpell(SpellDefinition spell)
        {
            if (spell == null) return false;
            if (!_hasActiveUnit) return false;
            if (IsActiveUnitSpellSpentThisTurn(spell)) return false;
            if (!IsActiveUnitPlayerControlledInternal()) return false;
            if (_movementAnimating) return false;
            if (_attackAnimating) return false;
            if (_shootAnimating) return false;
            if (_board == null) return false;

            int apCost = Mathf.Max(0, spell.ActionPointCost);
            if (_activeUnitCurrentActionPoints < apCost)
            {
                return false;
            }

            var handler = _spellController != null ? _spellController.GetEffectHandler(spell) : null;
            if (handler == null)
            {
                return false;
            }

            if (!TryBuildActiveSpellContext(out var context) || !handler.HasAvailableTargets(spell, context))
            {
                return false;
            }

            return true;
        }

        private bool IsTileLegalSpellTarget(SpellDefinition spell, Vector2Int tile)
        {
            if (_spellController == null) return false;
            if (!CanActiveUnitCastSpell(spell)) return false;
            if (_activeIndex < 0 || _activeIndex >= _units.Count) return false;

            var caster = _units[_activeIndex];
            return _spellController.IsTileLegalSpellTarget(spell, tile, caster.Metadata, _units, 
                u => u.Metadata, 
                (pos, u) => IsUnitValid(u) && u.Metadata.Tile == pos);
        }

        private bool CanActiveUnitAttack()
        {
            if (_combatController == null) return false;
            if (!_hasActiveUnit || _activeIndex < 0 || _activeIndex >= _units.Count) return false;
            if (_shootAnimating) return false;
            return _combatController.CanAttack(_units[_activeIndex].Stats, _activeUnitCurrentActionPoints, IsActiveUnitPlayerControlledInternal(), _movementAnimating, _attackAnimating);
        }

        private bool IsAttackableEnemyTile(Vector2Int tile)
        {
            return _combatController != null && _combatController.IsAttackableEnemyTile(tile);
        }

        private void TryExecuteAttack(Vector2Int targetTile)
        {
            if (_combatController == null) return;
            if (!_hasActiveUnit || _activeIndex < 0 || _activeIndex >= _units.Count) return;

            _combatController.TryExecuteAttack(
                targetTile,
                _units[_activeIndex],
                _units,
                u => u.Metadata,
                u => u.Stats,
                () => { _attackAnimating = true; },
                () => { ConsumeActiveUnitActionPoint(); },
                () =>
                {
                    CompactUnits();
                    if (!_battleEnded && _hasActiveUnit && _activeIndex >= 0 && _activeIndex < _units.Count)
                    {
                        RebuildAttackableEnemyTiles();
                    }
                },
                () => { _attackAnimating = false; UpdateBoardHighlight(); }
            );
        }

        private bool TryGetValidUnitAtTile(Vector2Int tile, out TurnUnit unit)
        {
            for (int i = 0; i < _units.Count; i++)
            {
                var candidate = _units[i];
                if (!IsUnitValid(candidate)) continue;
                if (candidate.Metadata.Tile != tile) continue;
                unit = candidate;
                return true;
            }

            unit = default;
            return false;
        }

        private bool CanActiveUnitMove()
        {
            if (!_hasActiveUnit || _activeIndex < 0 || _activeIndex >= _units.Count) return false;
            if (!IsActiveUnitPlayerControlledInternal()) return false;
            if (_movementController == null) return false;
            var u = _units[_activeIndex];
            // We use the controller's validation but we also check local state references if needed
            return _movementController.CanMove(u.Stats, _activeUnitCurrentActionPoints, _activeUnitHasMoved);
        }

        private bool IsTileLegalMoveDestination(Vector2Int tile)
        {
            if (_movementController == null) return false;
            return _movementController.IsTileLegalMoveDestination(tile);
        }

        private bool IsTileOccupiedByAnyUnit(Vector2Int tile)
        {
            for (int i = 0; i < _units.Count; i++)
            {
                var meta = _units[i].Metadata;
                if (meta == null || !meta.HasTile) continue;
                if (meta.Tile == tile) return true;
            }

            return false;
        }

        private void TryExecuteActiveUnitMove(Vector2Int destinationTile)
        {
            if (_movementController == null) return;
            if (!_hasActiveUnit || _activeIndex < 0 || _activeIndex >= _units.Count) return;
            var u = _units[_activeIndex];
            var meta = u.Metadata;
            if (meta == null) return;

            _movementController.TryExecuteMove(
                destinationTile, 
                meta,
                () => { // On Start
                    _movementAnimating = true;
                    _hasSelectedMoveTile = false;
                    if (_highlightController != null)
                    {
                        _highlightController.HideAll();
                    }
                    /* _board directly usage removed */
                },
                () => { // On AP Consumed
                    if (_activeUnitCurrentActionPoints > 0)
                    {
                        _activeUnitCurrentActionPoints--;
                    }
                    ActiveUnitActionPointsChanged?.Invoke();
                },
                (m) => { // On Face Enemy
                     FaceActiveUnitTowardsNearestEnemy(m);
                },
                  () => { // On Complete
                      _activeUnitHasMoved = true;
                      _movementAnimating = false;
                      ActiveUnitActionPointsChanged?.Invoke();
                      UpdateBoardHighlight();
                      ApplyTileStatBonusForUnit(u);
                      RebuildAttackableEnemyTiles();
                      HandleAiMoveCompleted();
                  }
            );
        }

        private void HandleAiMoveCompleted()
        {
            if (!_aiMovePendingCompletion)
            {
                return;
            }

            _aiMovePendingCompletion = false;

            if (TryExecuteAiAttack())
            {
                return;
            }

            if (TryExecuteAiShoot())
            {
                return;
            }

            CompleteAiTurnAfterDecision();
        }

        private void RebuildLegalMoveTilesForActiveUnit()
        {
            if (_movementController != null && _hasActiveUnit && _activeIndex >= 0 && _activeIndex < _units.Count)
            {
                _movementController.RebuildLegalMoveTiles(_units[_activeIndex], _units, u => u.Metadata, u => u.Stats, IsTileOccupiedByAnyUnit);
            }
        }

        private static Vector2 ComputeDirection(Vector2Int from, Vector2Int to)
        {
            int dx = to.x - from.x;
            int dy = to.y - from.y;
            if (Mathf.Abs(dx) >= Mathf.Abs(dy))
            {
                if (dx > 0) return Vector2.right;
                if (dx < 0) return Vector2.left;
            }
            else
            {
                if (dy > 0) return Vector2.up;
                if (dy < 0) return Vector2.down;
            }
            return Vector2.zero;
        }

        private void FaceActiveUnitTowardsNearestEnemy(UnitBattleMetadata meta)
        {
            if (meta == null || !meta.HasTile) return;

            var origin = meta.Tile;
            bool isPlayer = meta.IsPlayerControlled;
            UnitBattleMetadata target = null;
            int bestDistance = int.MaxValue;

            for (int i = 0; i < _units.Count; i++)
            {
                var candidate = _units[i];
                if (!IsUnitValid(candidate)) continue;
                var otherMeta = candidate.Metadata;
                if (otherMeta == null || !otherMeta.HasTile) continue;
                if (otherMeta.IsPlayerControlled == isPlayer) continue;

                int dist = Mathf.Abs(otherMeta.Tile.x - origin.x) + Mathf.Abs(otherMeta.Tile.y - origin.y);
                if (dist < bestDistance ||
                    (dist == bestDistance && (otherMeta.Tile.y < target.Tile.y ||
                                              (otherMeta.Tile.y == target.Tile.y && otherMeta.Tile.x < target.Tile.x))))
                {
                    target = otherMeta;
                    bestDistance = dist;
                }
            }

            if (target == null) return;

            var direction = ComputeDirection(origin, target.Tile);
            if (direction == Vector2.zero) return;

            SevenBattles.Battle.Units.UnitVisualUtil.SetDirectionIfCharacter4D(meta.gameObject, direction);
        }



        private void UpdateBoardHighlight()
        {
            if (_highlightController == null) return;
            // Also explicitly clearing old board highlighting to act as safety during transition if needed,
            // but controller handles it.

            if (!_hasActiveUnit || _activeIndex < 0 || _activeIndex >= _units.Count)
            {
                _highlightController.HideAll();
                return;
            }

            var u = _units[_activeIndex];
            _highlightController.UpdateActiveUnitHighlight(u.Metadata);
        }

        private static bool IsUnitValid(TurnUnit unit)
        {
            if (unit.Metadata == null || !unit.Metadata.isActiveAndEnabled) return false;
            if (unit.Stats == null) return false;
            if (unit.Stats.Life <= 0) return false;
            if (!unit.Metadata.HasTile) return false;
            return true;
        }

        private bool IsActiveUnitPlayerControlledInternal()
        {
            if (!_hasActiveUnit || _activeIndex < 0 || _activeIndex >= _units.Count) return false;
            var u = _units[_activeIndex];
            return u.Metadata != null && u.Metadata.IsPlayerControlled;
        }

        private void CacheActiveUnitStats(UnitStats stats)
        {
            _cachedActiveSpeed = stats != null ? stats.Speed : 0;
            _cachedActiveAttack = stats != null ? stats.Attack : 0;
            _cachedActiveShoot = stats != null ? stats.Shoot : 0;
            _cachedActiveShootRange = stats != null ? stats.ShootRange : 0;
        }

        private bool TryUpdateActiveUnitCachedStats()
        {
            var stats = _activeStatsSubscription;
            if (stats == null)
            {
                return false;
            }

            bool changed = false;
            if (stats.Speed != _cachedActiveSpeed) changed = true;
            if (stats.Attack != _cachedActiveAttack) changed = true;
            if (stats.Shoot != _cachedActiveShoot) changed = true;
            if (stats.ShootRange != _cachedActiveShootRange) changed = true;

            if (changed)
            {
                CacheActiveUnitStats(stats);
            }

            return changed;
        }

        // Helper to delegate to combat controller
        private void RebuildAttackableEnemyTiles()
        {
            if (_combatController != null && _hasActiveUnit && _activeIndex >= 0 && _activeIndex < _units.Count)
            {
                _combatController.RebuildAttackableEnemyTiles(_units[_activeIndex], _units, u => u.Metadata, u => u.Stats);
                _combatController.RebuildShootableEnemyTiles(_units[_activeIndex], _units, u => u.Metadata, u => u.Stats);
            }
        }

        private void ConsumeActiveUnitActionPoint()
        {
            ConsumeActiveUnitActionPoints(1);
        }

        private void ConsumeActiveUnitActionPoints(int amount)
        {
            amount = Mathf.Max(0, amount);
            if (amount <= 0)
            {
                return;
            }

            if (_activeUnitCurrentActionPoints <= 0)
            {
                return;
            }

            _activeUnitCurrentActionPoints = Mathf.Max(0, _activeUnitCurrentActionPoints - amount);
            ActiveUnitActionPointsChanged?.Invoke();
        }

        private System.Collections.IEnumerator HandleUnitDeathCleanup(UnitBattleMetadata targetMeta)
        {
            if (targetMeta == null)
            {
                yield break;
            }

            var targetGo = targetMeta.gameObject;
            float waitSeconds = Mathf.Max(0f, _deathAnimationDurationSeconds);
            if (waitSeconds > 0f)
            {
                yield return new WaitForSeconds(waitSeconds);
            }

            if (targetMeta != null)
            {
                targetMeta.ClearTile();
            }

            if (targetGo != null)
            {
                Destroy(targetGo);
            }
        }

        private int CalculateDamage(int attack, int defense)
        {
            return SevenBattles.Battle.Combat.BattleDamageCalculator.Calculate(attack, defense);
        }

    }
}
