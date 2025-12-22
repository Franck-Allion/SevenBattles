using System;
using System.Collections.Generic;
using UnityEngine;
using SevenBattles.Battle.Board;
using SevenBattles.Battle.Units;
using SevenBattles.Core;
using SevenBattles.Core.Battle;
using SevenBattles.Core.Save;

namespace SevenBattles.Battle.Turn
{
    // Basic initiative-based turn controller for wizards.
    // Discovers UnitBattleMetadata instances at battle start, sorts by UnitStats.Initiative,
    // and advances turns for player and AI units.
    public class SimpleTurnOrderController : MonoBehaviour, IBattleTurnController, ISpellSelectionController
    {
        [Header("Board Highlight (optional)")]
        [SerializeField] private WorldPerspectiveBoard _board;
        [SerializeField] private Color _playerHighlightColor = new Color(0.3f, 1f, 0.3f, 0.4f);
        [SerializeField] private Color _enemyHighlightColor = new Color(1f, 0.3f, 0.3f, 0.4f);
        [SerializeField, Tooltip("Optional material used for the active unit tile highlight during battle (e.g., outline-only). If null, the board's default highlight material is used.")]
        private Material _activeUnitHighlightMaterial;
        [Header("Movement")]
        [SerializeField, Tooltip("Color used to highlight a legal destination tile for the active unit.")]
        private Color _moveValidColor = new Color(0.3f, 1f, 0.3f, 0.5f);
        [SerializeField, Tooltip("Color used to highlight an illegal destination tile for the active unit.")]
        private Color _moveInvalidColor = new Color(1f, 0.3f, 0.3f, 0.5f);
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
        private int _activeIndex = -1;
        private float _pendingAiEndTime = -1f;
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

        // Cursor state is now managed by BattleCursorController
        private SpellDefinition _selectedSpell;
        private bool _battleEnded;
        private BattleOutcome _battleOutcome = BattleOutcome.None;

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
                if (!_hasActiveUnit || _activeIndex < 0 || _activeIndex >= _units.Count)
                {
                    return Array.Empty<SpellDefinition>();
                }

                var meta = _units[_activeIndex].Metadata;
                var spells = meta != null && meta.Definition != null ? meta.Definition.Spells : null;
                return spells ?? Array.Empty<SpellDefinition>();
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

            if (!_hasActiveUnit || spell == null)
            {
                return false;
            }

            if (_activeIndex < 0 || _activeIndex >= _units.Count)
            {
                return false;
            }

            if (spell.PrimaryAmountKind == SpellPrimaryAmountKind.None)
            {
                return false;
            }

            var unit = _units[_activeIndex];
            var stats = unit.Stats;
            if (stats == null)
            {
                return false;
            }

            int baseAmount = Mathf.Max(0, spell.PrimaryBaseAmount);
            float scaling = spell.PrimarySpellStatScaling;
            int scaledAmount = baseAmount;

            if (!Mathf.Approximately(scaling, 0f))
            {
                scaledAmount += Mathf.RoundToInt(stats.Spell * scaling);
            }

            var context = new SpellAmountCalculationContext
            {
                Kind = spell.PrimaryAmountKind,
                Element = spell.PrimaryDamageElement,
                BaseAmount = baseAmount,
                Amount = scaledAmount,
                CasterSpellStat = stats.Spell
            };

            ApplySpellAmountModifiers(stats.gameObject, spell, ref context);

            context.Amount = Mathf.Max(0, context.Amount);

            preview.Kind = context.Kind;
            preview.Element = context.Element;
            preview.BaseAmount = context.BaseAmount;
            preview.ModifiedAmount = context.Amount;
            return true;
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

        private static void ApplySpellAmountModifiers(GameObject unitGameObject, SpellDefinition spell, ref SpellAmountCalculationContext context)
        {
            if (unitGameObject == null)
            {
                return;
            }

            var behaviours = unitGameObject.GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                var provider = behaviours[i] as ISpellAmountModifierProvider;
                if (provider == null)
                {
                    continue;
                }

                try
                {
                    provider.ModifySpellAmount(spell, ref context);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"SimpleTurnOrderController: Spell amount modifier threw an exception and was ignored: {ex.Message}", behaviours[i]);
                }
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

            if (_pendingAiEndTime < 0f) return;
            if (_advancing) return;

            if (Time.time >= _pendingAiEndTime)
            {
                if (_logTurns)
                {
                    Debug.Log("SimpleTurnOrderController: AI turn timeout reached, advancing.", this);
                }
                AdvanceToNextUnit();
            }
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
            _board.SetHoverEnabled(false);
            if (_activeUnitHighlightMaterial != null)
            {
                _board.SetHighlightMaterial(_activeUnitHighlightMaterial);
            }
        }

        private void RebuildUnits()
        {
            _units.Clear();
            _activeIndex = -1;
            _pendingAiEndTime = -1f;

            var metas = UnityEngine.Object.FindObjectsByType<UnitBattleMetadata>(FindObjectsSortMode.None);
            for (int i = 0; i < metas.Length; i++)
            {
                var meta = metas[i];
                if (meta == null || !meta.isActiveAndEnabled) continue;
                var stats = meta.GetComponent<UnitStats>();
                if (stats == null) continue;
                _units.Add(new TurnUnit { Metadata = meta, Stats = stats });
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
            _pendingAiEndTime = -1f;
            _activeUnitHasMoved = false;
            _hasSelectedMoveTile = false;

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

            if (_hasActiveUnit && !IsActiveUnitPlayerControlledInternal())
            {
                float delay = Mathf.Max(0f, _aiTurnDelaySeconds);
                _pendingAiEndTime = Time.time + delay;
            }

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
                if (_board != null)
                {
                    _board.SetSecondaryHighlightVisible(false);
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
                if (!_hasSelectedMoveTile && _board != null)
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
                _board.SetSecondaryHighlightVisible(true);
                _board.MoveSecondaryHighlightToTile(hoveredTile.x, hoveredTile.y);
                _board.SetSecondaryHighlightColor(_combatController.AttackCursorColor);

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
                if (_board != null)
                {
                    _board.SetSecondaryHighlightVisible(false);
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
                _board.SetSecondaryHighlightVisible(true);
                _board.MoveSecondaryHighlightToTile(_selectedMoveTile.x, _selectedMoveTile.y);
                bool stillValid = IsTileLegalMoveDestination(_selectedMoveTile);
                _board.SetSecondaryHighlightColor(stillValid ? _moveValidColor : _moveInvalidColor);
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
            _board.SetSecondaryHighlightVisible(true);
            _board.MoveSecondaryHighlightToTile(hoveredTile.x, hoveredTile.y);
            _board.SetSecondaryHighlightColor(legal ? _moveValidColor : _moveInvalidColor);

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

            _board.SetSecondaryHighlightVisible(true);
            _board.MoveSecondaryHighlightToTile(hoveredTile.x, hoveredTile.y);
            _board.SetSecondaryHighlightColor(eligible ? _moveValidColor : _moveInvalidColor);

            if (Input.GetMouseButtonDown(0) && eligible)
            {
                TryExecuteSpellCast(spell, hoveredTile);
            }
        }

        private void TryExecuteSpellCast(SpellDefinition spell, Vector2Int targetTile)
        {
            if (!IsTileLegalSpellTarget(spell, targetTile))
            {
                return;
            }

            if (_activeIndex < 0 || _activeIndex >= _units.Count)
            {
                return;
            }

            var caster = _units[_activeIndex];
            var casterMeta = caster.Metadata;
            var casterStats = caster.Stats;
            if (casterMeta == null || casterStats == null || !casterMeta.HasTile)
            {
                return;
            }

            _spellAnimating = true;
            if (_cursorController != null)
            {
                _cursorController.SetSpellCursor(false, _selectedSpell);
            }
            if (_board != null) _board.SetSecondaryHighlightVisible(false);

            try
            {
                // Optional casting animation (no-op if missing).
                UnitVisualUtil.TryPlayAnimation(casterMeta.gameObject, "Cast");

                bool hasTargetUnit = TryGetValidUnitAtTile(targetTile, out var targetUnit);
                var targetMeta = hasTargetUnit ? targetUnit.Metadata : null;
                var targetStats = hasTargetUnit ? targetUnit.Stats : null;

                Vector3 targetWorld = GetSpellTargetWorldPosition(targetTile, targetMeta);
                if (targetMeta == null && casterMeta != null)
                {
                    // Keep VFX on the same Z plane as units (helps with 2.5D camera setups / transparency sorting).
                    targetWorld.z = casterMeta.transform.position.z;
                }

                SpawnSpellTargetVfx(spell, targetWorld, casterMeta, targetMeta, targetTile);
                PlaySpellCastSfx(spell, casterMeta, targetWorld);

                // Apply primary effect (if configured).
                int amount = 0;
                SpellPrimaryAmountKind kind = SpellPrimaryAmountKind.None;
                if (spell != null && spell.PrimaryAmountKind != SpellPrimaryAmountKind.None)
                {
                    kind = spell.PrimaryAmountKind;

                    if (TryGetActiveUnitSpellAmountPreview(spell, out var preview))
                    {
                        amount = Mathf.Max(0, preview.ModifiedAmount);
                        kind = preview.Kind;
                    }
                    else
                    {
                        int baseAmount = Mathf.Max(0, spell.PrimaryBaseAmount);
                        float scaling = spell.PrimarySpellStatScaling;
                        int scaledAmount = baseAmount;

                        if (!Mathf.Approximately(scaling, 0f) && casterStats != null)
                        {
                            scaledAmount += Mathf.RoundToInt(casterStats.Spell * scaling);
                        }

                        amount = Mathf.Max(0, scaledAmount);
                    }
                }

                bool targetDied = false;

                if (kind == SpellPrimaryAmountKind.Damage && targetStats != null)
                {
                    if (targetMeta != null)
                    {
                        UnitVisualUtil.TryPlayAnimation(targetMeta.gameObject, "Hit");
                    }
                    targetStats.TakeDamage(amount);
                    targetDied = targetStats.Life <= 0;

                    if (_visualFeedback != null)
                    {
                        _visualFeedback.ShowDamageNumber(targetWorld, amount);
                    }
                }
                else if (kind == SpellPrimaryAmountKind.Heal && targetStats != null)
                {
                    targetStats.Heal(amount);

                    if (_visualFeedback != null)
                    {
                        _visualFeedback.ShowHealNumber(targetWorld, amount);
                    }
                }

                // Death handling mirrors melee attack flow.
                if (targetDied && targetMeta != null)
                {
                    HandleUnitDeathVfxAndCleanup(targetMeta);
                    CompactUnits();
                }

                // Consume AP cost.
                ConsumeActiveUnitActionPoints(Mathf.Max(0, spell != null ? spell.ActionPointCost : 0));

                // If we modified the active unit's combat stats directly (e.g., self-heal), notify UI.
                if (targetMeta != null && ReferenceEquals(targetMeta, casterMeta))
                {
                    ActiveUnitStatsChanged?.Invoke();
                }

                RebuildAttackableEnemyTiles();
                UpdateBoardHighlight();

                // Default AAA flow: return to base mode after a successful cast.
                SetSelectedSpell(null);
            }
            finally
            {
                _spellAnimating = false;
            }
        }

        private static void PlaySpellCastSfx(SpellDefinition spell, UnitBattleMetadata casterMeta, Vector3 targetWorld)
        {
            if (spell == null || spell.CastSfxClip == null)
            {
                return;
            }

            float volume = Mathf.Clamp(spell.CastSfxVolume, 0f, 1.5f);
            Vector3 pos = spell.CastSfxAtTarget ? targetWorld : (casterMeta != null ? casterMeta.transform.position : targetWorld);
            AudioSource.PlayClipAtPoint(spell.CastSfxClip, pos, volume);
        }

        private Vector3 GetSpellTargetWorldPosition(Vector2Int tile, UnitBattleMetadata targetMeta)
        {
            if (targetMeta != null)
            {
                return targetMeta.transform.position;
            }

            if (_board != null)
            {
                return _board.TileCenterWorld(tile.x, tile.y);
            }

            return Vector3.zero;
        }

        private void SpawnSpellTargetVfx(SpellDefinition spell, Vector3 worldPosition, UnitBattleMetadata casterMeta, UnitBattleMetadata targetMeta, Vector2Int targetTile)
        {
            if (spell == null || spell.TargetVfxPrefab == null)
            {
                return;
            }

            var instance = Instantiate(spell.TargetVfxPrefab, worldPosition, Quaternion.identity);
            ConfigureSpellVfxRendering(instance, spell, casterMeta, targetMeta, targetTile);

            float scaleMultiplier = Mathf.Max(0f, spell.TargetVfxScaleMultiplier);
            if (!Mathf.Approximately(scaleMultiplier, 1f) && scaleMultiplier > 0f)
            {
                instance.transform.localScale = instance.transform.localScale * scaleMultiplier;
            }

            // Some VFX prefabs are authored with Play On Awake; others are not. Ensure playback.
            var systems = instance.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < systems.Length; i++)
            {
                if (systems[i] != null)
                {
                    systems[i].Play(true);
                }
            }

            float lifetime = Mathf.Max(0f, spell.TargetVfxLifetimeSeconds);
            if (lifetime > 0f)
            {
                Destroy(instance, lifetime);
            }
        }

        private static void ConfigureSpellVfxRendering(GameObject instance, SpellDefinition spell, UnitBattleMetadata casterMeta, UnitBattleMetadata targetMeta, Vector2Int targetTile)
        {
            if (instance == null)
            {
                return;
            }

            string sortingLayerName = "Characters";
            int sortingOrder = 100;

            if (targetMeta != null)
            {
                sortingLayerName = !string.IsNullOrEmpty(targetMeta.SortingLayer) ? targetMeta.SortingLayer : sortingLayerName;
                sortingOrder = GetUnitSortingOrder(targetMeta, fallbackOrder: sortingOrder);
            }
            else if (casterMeta != null)
            {
                sortingLayerName = !string.IsNullOrEmpty(casterMeta.SortingLayer) ? casterMeta.SortingLayer : sortingLayerName;
                sortingOrder = GetUnitSortingOrder(casterMeta, fallbackOrder: sortingOrder);
            }

            if (spell != null && !string.IsNullOrEmpty(spell.TargetVfxSortingLayerOverride))
            {
                sortingLayerName = spell.TargetVfxSortingLayerOverride;
            }

            int orderOffset = spell != null ? spell.TargetVfxSortingOrderOffset : 0;
            sortingOrder += orderOffset;

            var group = instance.GetComponentInChildren<UnityEngine.Rendering.SortingGroup>(true);
            if (group != null)
            {
                group.sortingLayerName = sortingLayerName;
                group.sortingOrder = sortingOrder;
            }

            var spriteRenderers = instance.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                var sr = spriteRenderers[i];
                if (sr == null) continue;
                sr.sortingLayerName = sortingLayerName;
                sr.sortingOrder = sortingOrder;
            }

            var particleRenderers = instance.GetComponentsInChildren<ParticleSystemRenderer>(true);
            for (int i = 0; i < particleRenderers.Length; i++)
            {
                var pr = particleRenderers[i];
                if (pr == null) continue;
                pr.sortingLayerName = sortingLayerName;
                pr.sortingOrder = sortingOrder;
            }
        }

        private static int GetUnitSortingOrder(UnitBattleMetadata meta, int fallbackOrder)
        {
            if (meta == null)
            {
                return fallbackOrder;
            }

            var group = meta.gameObject.GetComponentInChildren<UnityEngine.Rendering.SortingGroup>(true);
            if (group != null)
            {
                return group.sortingOrder;
            }

            var renderer = meta.gameObject.GetComponentInChildren<SpriteRenderer>(true);
            if (renderer != null)
            {
                return renderer.sortingOrder;
            }

            return fallbackOrder;
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

            var meta = _units[_activeIndex].Metadata;
            var spells = meta != null && meta.Definition != null ? meta.Definition.Spells : null;
            if (spells == null) return false;

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
            if (!CanActiveUnitCastSpell(spell)) return false;
            if (_activeIndex < 0 || _activeIndex >= _units.Count) return false;

            var caster = _units[_activeIndex];
            var casterMeta = caster.Metadata;
            if (casterMeta == null || !casterMeta.HasTile) return false;

            int minRange = Mathf.Max(0, spell.MinCastRange);
            int maxRange = Mathf.Max(0, spell.MaxCastRange);
            if (maxRange < minRange) maxRange = minRange;

            var delta = tile - casterMeta.Tile;
            int distance = Mathf.Abs(delta.x) + Mathf.Abs(delta.y);
            if (distance < minRange || distance > maxRange) return false;

            bool hasUnit = TryGetValidUnitAtTile(tile, out var targetUnit);
            bool isFriendly = hasUnit && targetUnit.Metadata != null && targetUnit.Metadata.IsPlayerControlled == casterMeta.IsPlayerControlled;
            bool isEnemy = hasUnit && !isFriendly;

            switch (spell.TargetFilter)
            {
                case SpellTargetFilter.EnemyUnit:
                    return hasUnit && isEnemy;
                case SpellTargetFilter.FriendlyUnit:
                    return hasUnit && isFriendly;
                case SpellTargetFilter.AnyUnit:
                    return hasUnit;
                case SpellTargetFilter.EmptyTile:
                    return !hasUnit;
                case SpellTargetFilter.AnyTile:
                    return true;
                default:
                    return false;
            }
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
            if (_movementController == null) return false;
            // Provide data needed for validation, but do not pass full state if not needed
            // But CanMove needs stats, AP, etc.
            if (!_hasActiveUnit || _activeIndex < 0 || _activeIndex >= _units.Count) return false;
            var u = _units[_activeIndex];
            // We use the controller's validation but we also check local state references if needed
            return _movementController.CanMove(u.Stats, _activeUnitCurrentActionPoints, _activeUnitHasMoved, IsActiveUnitPlayerControlledInternal());
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
                    if (_board != null)
                    {
                        _board.SetHighlightVisible(false);
                        _board.SetSecondaryHighlightVisible(false);
                    }
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
                }
            );
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

            int bestDistance = int.MaxValue;
            System.Collections.Generic.List<UnitBattleMetadata> nearest = null;

            for (int i = 0; i < _units.Count; i++)
            {
                var unit = _units[i];
                if (!IsUnitValid(unit)) continue;
                var otherMeta = unit.Metadata;
                if (otherMeta == null || !otherMeta.HasTile) continue;
                if (otherMeta.IsPlayerControlled == isPlayer) continue;

                var tile = otherMeta.Tile;
                int dist = Mathf.Abs(tile.x - origin.x) + Mathf.Abs(tile.y - origin.y);
                if (dist < bestDistance)
                {
                    bestDistance = dist;
                    if (nearest == null) nearest = new System.Collections.Generic.List<UnitBattleMetadata>();
                    else nearest.Clear();
                    nearest.Add(otherMeta);
                }
                else if (dist == bestDistance && dist != int.MaxValue)
                {
                    if (nearest == null) nearest = new System.Collections.Generic.List<UnitBattleMetadata>();
                    nearest.Add(otherMeta);
                }
            }

            if (nearest == null || nearest.Count == 0) return;

            var target = nearest[nearest.Count == 1 ? 0 : UnityEngine.Random.Range(0, nearest.Count)];
            var direction = ComputeDirection(origin, target.Tile);
            if (direction == Vector2.zero) return;

            SevenBattles.Battle.Units.UnitVisualUtil.SetDirectionIfCharacter4D(meta.gameObject, direction);
        }



        private void UpdateBoardHighlight()
        {
            if (_board == null) return;

            if (!_hasActiveUnit || _activeIndex < 0 || _activeIndex >= _units.Count)
            {
                _board.SetHighlightVisible(false);
                return;
            }

            var u = _units[_activeIndex];
            var meta = u.Metadata;
            if (meta == null || !meta.HasTile)
            {
                _board.SetHighlightVisible(false);
                return;
            }

            var tile = meta.Tile;
            
            // Validate BaseSortingOrder - should never be 0 or negative
            if (meta.BaseSortingOrder <= 0)
            {
                Debug.LogWarning($"[SimpleTurnOrderController] Active unit has invalid BaseSortingOrder={meta.BaseSortingOrder}. " +
                                 $"This will cause rendering issues. Setting to default 100.", this);
                meta.BaseSortingOrder = 100;
            }
            
            // Get actual sorting order from the unit's renderers
            // We must use the actual value, not computed, because the unit may have been placed with a different sorting order
            int unitSortingOrder = -1;
            var group = meta.gameObject.GetComponentInChildren<UnityEngine.Rendering.SortingGroup>(true);
            if (group != null)
            {
                unitSortingOrder = group.sortingOrder;
            }
            else
            {
                var renderer = meta.gameObject.GetComponentInChildren<SpriteRenderer>(true);
                if (renderer != null)
                {
                    unitSortingOrder = renderer.sortingOrder;
                }
            }
            
            // Fallback to computed value if we couldn't get actual sorting order
            if (unitSortingOrder < 0)
            {
                unitSortingOrder = _board.ComputeSortingOrder(tile.x, tile.y, meta.BaseSortingOrder, rowStride: 10, intraRowOffset: 0);
                Debug.LogWarning($"[SimpleTurnOrderController] Could not get actual unit sorting order, using computed value: {unitSortingOrder}", this);
            }
            
            // Set highlight to render behind the unit
            int highlightSortingOrder = unitSortingOrder - 1;
            
            // Safety: ensure highlight never goes below minimum threshold (board background)
            const int MinHighlightSortingOrder = 1;
            if (highlightSortingOrder < MinHighlightSortingOrder)
            {
                highlightSortingOrder = MinHighlightSortingOrder;
            }
            
            _board.SetHighlightSortingOrder(highlightSortingOrder);
            
            _board.SetHighlightVisible(true);
            _board.MoveHighlightToTile(tile.x, tile.y);
            _board.SetHighlightColor(meta.IsPlayerControlled ? _playerHighlightColor : _enemyHighlightColor);
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
