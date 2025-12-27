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

namespace SevenBattles.Battle.Turn
{
    // Basic initiative-based turn controller for wizards.
    // Discovers UnitBattleMetadata instances at battle start, sorts by UnitStats.Initiative,
    // and advances turns for player and AI units.
    public class SimpleTurnOrderController : MonoBehaviour, IBattleTurnController, ISpellSelectionController
    {
        [Header("Board Highlight (delegated)")]
        [SerializeField] private WorldPerspectiveBoard _board; // Kept for other initialization if needed, or remove if unused. Checking usage needed. 
        [SerializeField, Tooltip("Service managing board highlights.")]
        private SevenBattles.Battle.Board.BattleBoardHighlightController _highlightController;
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

        [Header("Combat Management")]
        [SerializeField, Tooltip("Service managing attack validation, execution, and damage application. Should reference a BattleCombatController component.")]
        private SevenBattles.Battle.Combat.BattleCombatController _combatController;

        [Header("Movement Management")]
        [SerializeField, Tooltip("Service managing movement validation, BFS pathfinding, and movement execution. Should reference a BattleMovementController component.")]
        private SevenBattles.Battle.Movement.BattleMovementController _movementController;

        [Header("Spell Management")]
        [SerializeField, Tooltip("Service managing spell targeting, execution, and effects. Should reference a BattleSpellController component.")]
        private BattleSpellController _spellController;

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

        private readonly List<TurnUnit> _units = new List<TurnUnit>();
        private readonly HashSet<UnitStats> _healingSubscriptions = new HashSet<UnitStats>();
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
        private bool _spellAnimating;
        private bool _hasSelectedMoveTile;
        private Vector2Int _selectedMoveTile;
        private bool _aiMovePendingCompletion;
        private bool _aiDecisionInProgress;
        [SerializeField, Tooltip("Service responsible for evaluating AI turns.")]
        private BattleAiTurnService _aiTurnService;
        private readonly List<UnitBattleMetadata> _aiAllUnitsBuffer = new List<UnitBattleMetadata>();

        // Cursor state is now managed by BattleCursorController
        private SpellDefinition _selectedSpell;
        private SpellDefinition[] _activeUnitDrawnSpells = Array.Empty<SpellDefinition>();
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
        }

        private void OnDisable()
        {
            ClearHealingSubscriptions();
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

            // Map runtime stats to the UI view model using the full unit stat surface.
            stats.Life = u.Stats.Life;
            stats.MaxLife = u.Stats.MaxLife;
            stats.Force = u.Stats.Attack;
            stats.Shoot = u.Stats.Shoot;
            stats.Spell = u.Stats.Spell;
            stats.Speed = u.Stats.Speed;
            stats.Luck = u.Stats.Luck;
            stats.Defense = u.Stats.Defense;
            stats.Protection = u.Stats.Protection;
            stats.Initiative = u.Stats.Initiative;
            stats.Morale = u.Stats.Morale;

            return true;
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

        public event Action ActiveUnitChanged;
        public event Action ActiveUnitActionPointsChanged;
        public event Action ActiveUnitStatsChanged;
        public event Action<BattleOutcome> BattleEnded;
        public event Action SelectedSpellChanged;

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

        private void Update()
        {
            if (_battleEnded) return;
            if (_interactionLocked) return;
            if (!HasActiveUnit) return;
            if (_movementAnimating) return;
            if (_attackAnimating) return;
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
            ResetSpellDecksForBattle();
            _turnIndex = 0;
            ConfigureBoardForBattle();
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
            if (spell != null && !IsSpellAvailableToActiveUnit(spell))
            {
                spell = null;
            }

            if (ReferenceEquals(_selectedSpell, spell))
            {
                return;
            }

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

        private void RebuildUnits()
        {
            ClearHealingSubscriptions();
            _units.Clear();
            _activeIndex = -1;

            var metas = UnityEngine.Object.FindObjectsByType<UnitBattleMetadata>(FindObjectsSortMode.None);
            for (int i = 0; i < metas.Length; i++)
            {
                var meta = metas[i];
                if (meta == null || !meta.isActiveAndEnabled) continue;
                var stats = meta.GetComponent<UnitStats>();
                if (stats == null) continue;
                _units.Add(new TurnUnit { Metadata = meta, Stats = stats });
                SubscribeToHealing(stats);
            }

            _units.Sort((a, b) =>
            {
                int ia = a.Stats != null ? a.Stats.Initiative : 0;
                int ib = b.Stats != null ? b.Stats.Initiative : 0;
                // Higher initiative acts first.
                int cmp = ib.CompareTo(ia);
                if (cmp != 0) return cmp;
                // Stable tie-breaker based on persisted SaveInstanceId so ordering
                // is deterministic across save/load and between runs.
                string ida = a.Metadata != null ? a.Metadata.SaveInstanceId : null;
                string idb = b.Metadata != null ? b.Metadata.SaveInstanceId : null;
                if (ida == null && idb == null) return 0;
                if (ida == null) return -1;
                if (idb == null) return 1;
                return string.CompareOrdinal(ida, idb);
            });

            if (_logTurns)
            {
                Debug.Log($"SimpleTurnOrderController: Rebuilt units list, count={_units.Count}.", this);
            }
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
            // Remove units that are no longer valid (e.g., destroyed) to keep the list in sync.
            for (int i = _units.Count - 1; i >= 0; i--)
            {
                if (!IsUnitValid(_units[i]))
                {
                    UnsubscribeFromHealing(_units[i].Stats);
                    _units.RemoveAt(i);
                    if (i <= _activeIndex)
                    {
                        _activeIndex--;
                    }
                }
            }

            // After compaction, always evaluate battle outcome in case an entire squad
            // has been wiped out even if some units remain (e.g., enemy-only or player-only).
            EvaluateBattleOutcomeAndStop();
            if (_battleEnded)
            {
                return;
            }

            if (_units.Count == 0)
            {
                return;
            }

            if (_activeIndex < 0 || _activeIndex >= _units.Count || !IsUnitValid(_units[_activeIndex]))
            {
                // Clamp to first valid unit if any remain; otherwise mark as no active unit.
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
                // If firstValid < 0 here, _units would be empty and handled above.
            }
        }

        private void ClearHealingSubscriptions()
        {
            foreach (var stats in _healingSubscriptions)
            {
                if (stats != null)
                {
                    stats.Healed -= HandleUnitHealed;
                }
            }

            _healingSubscriptions.Clear();
        }

        private void SubscribeToHealing(UnitStats stats)
        {
            if (stats == null)
            {
                return;
            }

            if (_healingSubscriptions.Add(stats))
            {
                stats.Healed += HandleUnitHealed;
            }
        }

        private void UnsubscribeFromHealing(UnitStats stats)
        {
            if (stats == null)
            {
                return;
            }

            if (_healingSubscriptions.Remove(stats))
            {
                stats.Healed -= HandleUnitHealed;
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
            _activeIndex = index;
            _activeUnitHasMoved = false;
            _hasSelectedMoveTile = false;
            _aiMovePendingCompletion = false;
            _aiDecisionInProgress = false;
            _activeUnitDrawnSpells = Array.Empty<SpellDefinition>();

            // Reset cursors when active unit changes
            if (_cursorController != null)
            {
                _cursorController.SetAttackCursor(false, _attackCursorTexture, _attackCursorHotspot);
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
            }
            else
            {
                _hasActiveUnit = true;

                var u = _units[_activeIndex];
                int baseAp = u.Stats != null ? u.Stats.ActionPoints : 0;
                baseAp = Mathf.Max(0, baseAp);
                _activeUnitMaxActionPoints = baseAp;
                _activeUnitCurrentActionPoints = baseAp;
            }

            UpdateBoardHighlight();
            RebuildLegalMoveTilesForActiveUnit();
            if (_combatController != null && _hasActiveUnit && _activeIndex >= 0 && _activeIndex < _units.Count)
            {
                _combatController.RebuildAttackableEnemyTiles(_units[_activeIndex], _units, u => u.Metadata, u => u.Stats);
            }

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
            if (_movementAnimating || _attackAnimating || _spellAnimating) return;

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
            if (_movementAnimating || _attackAnimating || _spellAnimating) return;
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

            // First check if we can do anything at all
            bool canMove = CanActiveUnitMove();
            bool canAttack = _combatController != null && _hasActiveUnit && _activeIndex >= 0 && _activeIndex < _units.Count
                && _combatController.CanAttack(_units[_activeIndex].Stats, _activeUnitCurrentActionPoints, IsActiveUnitPlayerControlledInternal(), _movementAnimating, _attackAnimating);

            if (!canMove && !canAttack)
            {
                if (_highlightController != null)
                {
                    _highlightController.HideSecondaryHighlight();
                }
                _hasSelectedMoveTile = false;
                if (_cursorController != null)
                {
                    _cursorController.SetAttackCursor(false, _attackCursorTexture, _attackCursorHotspot);
                    _cursorController.SetMoveCursor(false, _moveCursorTexture, _moveCursorHotspot);
                    _cursorController.SetSelectionCursor(false, _selectionCursorTexture, _selectionCursorHotspot);
                }
                UpdateBoardHighlight();
                return;
            }

            if (_board == null) return;

            if (!_board.TryScreenToTile(Input.mousePosition, out var x, out var y))
            {
                if (!_hasSelectedMoveTile)
                {
                    UpdateBoardHighlight();
                }
                if (_cursorController != null)
                {
                    _cursorController.SetAttackCursor(false, _attackCursorTexture, _attackCursorHotspot);
                    _cursorController.SetMoveCursor(false, _moveCursorTexture, _moveCursorHotspot);
                    _cursorController.SetSelectionCursor(false, _selectionCursorTexture, _selectionCursorHotspot);
                }
                return;
            }

            var hoveredTile = new Vector2Int(x, y);

            // PRIORITY 1: Attack input handling (takes priority over movement)
            if (canAttack && _combatController != null && _combatController.IsAttackableEnemyTile(hoveredTile))
            {
                // Show attack cursor and highlight
                if (_cursorController != null)
                {
                    _cursorController.SetAttackCursor(true, _attackCursorTexture, _attackCursorHotspot);
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
                                _combatController.RebuildAttackableEnemyTiles(_units[_activeIndex], _units, u => u.Metadata, u => u.Stats); 
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

            // PRIORITY 2: Movement input handling (fallback if not attacking)
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

        private void UpdateSpellTargetingInput(SpellDefinition spell)
        {
            if (_board == null)
            {
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
                _cursorController.SetSpellCursor(true, _selectedSpell);
            }

            if (!_board.TryScreenToTile(Input.mousePosition, out var x, out var y))
            {
                _board.SetSecondaryHighlightVisible(false);
                return;
            }

            var hoveredTile = new Vector2Int(x, y);
            bool eligible = IsTileLegalSpellTarget(spell, hoveredTile);

            if (_highlightController != null)
            {
                _highlightController.SetSecondaryHighlight(hoveredTile, eligible);
            }

            if (Input.GetMouseButtonDown(0) && eligible)
            {
                TryExecuteSpellCast(spell, hoveredTile);
            }
        }

        private void TryExecuteSpellCast(SpellDefinition spell, Vector2Int targetTile)
        {
            if (_spellController == null) return;
            if (_activeIndex < 0 || _activeIndex >= _units.Count) return;

            var caster = _units[_activeIndex];

            _spellController.TryExecuteSpellCast(
                spell, targetTile, caster, _units,
                u => u.Metadata, u => u.Stats,
                onStart: () =>
                {
                    _spellAnimating = true;
                    if (_cursorController != null) _cursorController.SetSpellCursor(false, _selectedSpell);
                    if (_board != null) _board.SetSecondaryHighlightVisible(false);
                },
                onAPConsumed: cost => ConsumeActiveUnitActionPoints(cost),
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
                    SetSelectedSpell(null);
                    _spellAnimating = false;
                }
            );
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

        private bool CanActiveUnitCastSpell(SpellDefinition spell)
        {
            if (spell == null) return false;
            if (!_hasActiveUnit) return false;
            if (!IsActiveUnitPlayerControlledInternal()) return false;
            if (_movementAnimating) return false;
            if (_attackAnimating) return false;
            if (_board == null) return false;

            int apCost = Mathf.Max(0, spell.ActionPointCost);
            return _activeUnitCurrentActionPoints >= apCost;
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
                    UpdateBoardHighlight();
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

        // Helper to delegate to combat controller
        private void RebuildAttackableEnemyTiles()
        {
            if (_combatController != null && _hasActiveUnit && _activeIndex >= 0 && _activeIndex < _units.Count)
            {
                _combatController.RebuildAttackableEnemyTiles(_units[_activeIndex], _units, u => u.Metadata, u => u.Stats);
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
