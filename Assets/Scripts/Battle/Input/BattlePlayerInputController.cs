using UnityEngine;
using SevenBattles.Battle.Board;
using SevenBattles.Battle.Combat;
using SevenBattles.Battle.Cursors;
using SevenBattles.Battle.Movement;
using SevenBattles.Battle.Spells;
using SevenBattles.Core;
using SevenBattles.Core.Battle;
using SevenBattles.Core.Diagnostics;

namespace SevenBattles.Battle.Input
{
    /// <summary>
    /// Adapter contract exposing turn-controller commands needed by player input handling.
    /// </summary>
    public interface IBattlePlayerInputCommands : ITurnOrderController, ISpellSelectionController, IEnchantmentInspectionController
    {
        bool HasSelectedMoveTile { get; }
        Vector2Int SelectedMoveTile { get; }

        void SetSelectedMoveTileState(bool hasSelection, Vector2Int selectedTile);
        bool CanActiveUnitMoveForInput();
        bool CanActiveUnitAttackForInput();
        bool CanActiveUnitShootForInput();
        bool IsTileLegalMoveDestinationForInput(Vector2Int tile);
        void TryExecuteActiveUnitMoveForInput(Vector2Int destinationTile);
        void TryExecuteAttackForInput(Vector2Int targetTile);
        void TryExecuteShootForInput(Vector2Int targetTile);
        void UpdateBoardHighlightForInput();
        bool TryBuildActiveSpellContextForInput(out SpellCastContext context);
        void TryExecuteSpellEffectForInput(SpellDefinition spell, SpellTargetSelection target);
        bool TryInspectUnitAtTileForInput(Vector2Int tile, bool allowPlayerUnits);
        bool TryToggleEnchantmentInspectionForInput(BattleEnchantmentController.EnchantmentSnapshot snapshot, int quadIndex);
    }

    /// <summary>
    /// Handles player click/hover input during player turns (movement, attack, shoot, spell targeting, inspections).
    /// </summary>
    public sealed class BattlePlayerInputController : MonoBehaviour
    {
        [Header("Turn Controller")]
        [SerializeField, Tooltip("Reference to a MonoBehaviour implementing ITurnOrderController and IBattlePlayerInputCommands.")]
        private MonoBehaviour _turnOrderControllerBehaviour;

        [Header("Dependencies")]
        [SerializeField] private WorldPerspectiveBoard _board;
        [SerializeField] private BattleBoardHighlightController _highlightController;
        [SerializeField] private BattleCursorController _cursorController;
        [SerializeField] private BattleCombatController _combatController;
        [SerializeField] private BattleMovementController _movementController;
        [SerializeField] private BattleSpellController _spellController;
        [SerializeField] private BattleEnchantmentController _enchantmentController;

        [Header("Cursor Textures")]
        [SerializeField] private Texture2D _moveCursorTexture;
        [SerializeField] private Vector2 _moveCursorHotspot = new Vector2(16f, 16f);
        [SerializeField] private Texture2D _selectionCursorTexture;
        [SerializeField] private Vector2 _selectionCursorHotspot = new Vector2(16f, 16f);
        [SerializeField] private Texture2D _attackCursorTexture;
        [SerializeField] private Vector2 _attackCursorHotspot = new Vector2(16f, 16f);
        [SerializeField] private Texture2D _shootCursorTexture;
        [SerializeField] private Vector2 _shootCursorHotspot = new Vector2(16f, 16f);

        private ITurnOrderController _turnOrderController;
        private IBattlePlayerInputCommands _inputCommands;

        private void Awake()
        {
            ResolveDependencies();
        }

        public void Bind(
            ITurnOrderController turnOrderController,
            WorldPerspectiveBoard board = null,
            BattleBoardHighlightController highlightController = null,
            BattleCursorController cursorController = null,
            BattleCombatController combatController = null,
            BattleMovementController movementController = null,
            BattleSpellController spellController = null,
            BattleEnchantmentController enchantmentController = null,
            Texture2D moveCursorTexture = null,
            Vector2? moveCursorHotspot = null,
            Texture2D selectionCursorTexture = null,
            Vector2? selectionCursorHotspot = null,
            Texture2D attackCursorTexture = null,
            Vector2? attackCursorHotspot = null,
            Texture2D shootCursorTexture = null,
            Vector2? shootCursorHotspot = null)
        {
            _turnOrderController = turnOrderController;
            _turnOrderControllerBehaviour = turnOrderController as MonoBehaviour;
            _inputCommands = turnOrderController as IBattlePlayerInputCommands;

            if (board != null) _board = board;
            if (highlightController != null) _highlightController = highlightController;
            if (cursorController != null) _cursorController = cursorController;
            if (combatController != null) _combatController = combatController;
            if (movementController != null) _movementController = movementController;
            if (spellController != null) _spellController = spellController;
            if (enchantmentController != null) _enchantmentController = enchantmentController;

            if (moveCursorTexture != null) _moveCursorTexture = moveCursorTexture;
            if (moveCursorHotspot.HasValue) _moveCursorHotspot = moveCursorHotspot.Value;
            if (selectionCursorTexture != null) _selectionCursorTexture = selectionCursorTexture;
            if (selectionCursorHotspot.HasValue) _selectionCursorHotspot = selectionCursorHotspot.Value;
            if (attackCursorTexture != null) _attackCursorTexture = attackCursorTexture;
            if (attackCursorHotspot.HasValue) _attackCursorHotspot = attackCursorHotspot.Value;
            if (shootCursorTexture != null) _shootCursorTexture = shootCursorTexture;
            if (shootCursorHotspot.HasValue) _shootCursorHotspot = shootCursorHotspot.Value;

            ValidateTurnControllerBinding();
        }

        public void UpdatePlayerTurnInput()
        {
            if (!EnsureReady())
            {
                return;
            }

            if (_inputCommands.SelectedSpell != null)
            {
                UpdateSpellTargetingInput(_inputCommands.SelectedSpell);
                return;
            }

            if (_board == null)
            {
                return;
            }

            if (UnityEngine.Input.GetMouseButtonDown(0) && _inputCommands.HasInspectedEnchantment)
            {
                _inputCommands.ClearInspectedEnchantment();
            }

            if (UnityEngine.Input.GetMouseButtonDown(1))
            {
                if (TryInspectEnchantmentAtScreenPosition(UnityEngine.Input.mousePosition))
                {
                    return;
                }

                if (TryInspectUnitAtScreenPosition(UnityEngine.Input.mousePosition, allowPlayerUnits: true))
                {
                    _inputCommands.ClearInspectedEnchantment();
                    return;
                }

                if (_inputCommands.HasInspectedEnchantment)
                {
                    _inputCommands.ClearInspectedEnchantment();
                }
            }

            bool canMove = _inputCommands.CanActiveUnitMoveForInput();
            bool canAttack = _inputCommands.CanActiveUnitAttackForInput();
            bool canShoot = _inputCommands.CanActiveUnitShootForInput();

            if (!canMove && !canAttack && !canShoot)
            {
                _highlightController?.HideSecondaryHighlight();
                _inputCommands.SetSelectedMoveTileState(false, default);
                SetActionCursorsInactive();
                _inputCommands.UpdateBoardHighlightForInput();
                return;
            }

            if (!_board.TryScreenToTile(UnityEngine.Input.mousePosition, out var x, out var y))
            {
                if (!_inputCommands.HasSelectedMoveTile)
                {
                    _inputCommands.UpdateBoardHighlightForInput();
                }

                SetActionCursorsInactive();
                return;
            }

            var hoveredTile = new Vector2Int(x, y);

            if (canShoot && _combatController != null && _combatController.IsShootableEnemyTile(hoveredTile))
            {
                if (_cursorController != null)
                {
                    _cursorController.SetShootCursor(true, _shootCursorTexture, _shootCursorHotspot);
                    _cursorController.SetAttackCursor(false, _attackCursorTexture, _attackCursorHotspot);
                    _cursorController.SetMoveCursor(false, _moveCursorTexture, _moveCursorHotspot);
                    _cursorController.SetSelectionCursor(false, _selectionCursorTexture, _selectionCursorHotspot);
                }

                _highlightController?.SetSecondaryHighlight(hoveredTile, true);

                if (UnityEngine.Input.GetMouseButtonDown(0))
                {
                    _inputCommands.TryExecuteShootForInput(hoveredTile);
                }

                return;
            }

            if (canAttack && _combatController != null && _combatController.IsAttackableEnemyTile(hoveredTile))
            {
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

                if (UnityEngine.Input.GetMouseButtonDown(0))
                {
                    _inputCommands.TryExecuteAttackForInput(hoveredTile);
                }

                return;
            }

            if (_cursorController != null)
            {
                _cursorController.SetAttackCursor(false, _attackCursorTexture, _attackCursorHotspot);
                _cursorController.SetShootCursor(false, _shootCursorTexture, _shootCursorHotspot);
            }

            if (!canMove)
            {
                _highlightController?.HideSecondaryHighlight();
                _inputCommands.SetSelectedMoveTileState(false, default);
                if (_cursorController != null)
                {
                    _cursorController.SetMoveCursor(false, _moveCursorTexture, _moveCursorHotspot);
                    _cursorController.SetSelectionCursor(false, _selectionCursorTexture, _selectionCursorHotspot);
                }

                _inputCommands.UpdateBoardHighlightForInput();
                return;
            }

            if (_inputCommands.HasSelectedMoveTile)
            {
                var selectedMoveTile = _inputCommands.SelectedMoveTile;
                bool stillValid = _inputCommands.IsTileLegalMoveDestinationForInput(selectedMoveTile);
                _highlightController?.SetSecondaryHighlight(selectedMoveTile, stillValid);

                if (_cursorController != null)
                {
                    _cursorController.SetMoveCursor(false, _moveCursorTexture, _moveCursorHotspot);
                    _cursorController.SetSelectionCursor(true, _selectionCursorTexture, _selectionCursorHotspot);
                }

                if (UnityEngine.Input.GetMouseButtonDown(0))
                {
                    if (hoveredTile == selectedMoveTile)
                    {
                        _inputCommands.TryExecuteActiveUnitMoveForInput(selectedMoveTile);
                    }
                    else
                    {
                        _inputCommands.SetSelectedMoveTileState(false, default);
                        if (_cursorController != null)
                        {
                            _cursorController.SetSelectionCursor(false, _selectionCursorTexture, _selectionCursorHotspot);
                        }
                    }
                }

                return;
            }

            bool legal = _inputCommands.IsTileLegalMoveDestinationForInput(hoveredTile);
            _highlightController?.SetSecondaryHighlight(hoveredTile, legal);

            if (_cursorController != null)
            {
                _cursorController.SetMoveCursor(legal, _moveCursorTexture, _moveCursorHotspot);
            }

            if (UnityEngine.Input.GetMouseButtonDown(0) && legal)
            {
                _inputCommands.SetSelectedMoveTileState(true, hoveredTile);
            }
        }

        private void UpdateSpellTargetingInput(SpellDefinition spell)
        {
            if (_board == null || _spellController == null)
            {
                return;
            }

            var handler = _spellController.GetEffectHandler(spell);
            if (handler == null)
            {
                return;
            }

            if (handler.TargetingMode == SpellTargetingMode.Enchantment)
            {
                UpdateEnchantmentTargetingInput(spell, handler);
                return;
            }

            if (UnityEngine.Input.GetMouseButtonDown(1) || UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                _inputCommands.SetSelectedSpell(null);
                if (_cursorController != null)
                {
                    _cursorController.SetSpellCursor(false, null);
                }

                _board.SetSecondaryHighlightVisible(false);
                _inputCommands.UpdateBoardHighlightForInput();
                return;
            }

            _inputCommands.SetSelectedMoveTileState(false, default);
            if (_cursorController != null)
            {
                _cursorController.SetSelectionCursor(false, _selectionCursorTexture, _selectionCursorHotspot);
                _cursorController.SetMoveCursor(false, _moveCursorTexture, _moveCursorHotspot);
                _cursorController.SetAttackCursor(false, _attackCursorTexture, _attackCursorHotspot);
                _cursorController.SetShootCursor(false, _shootCursorTexture, _shootCursorHotspot);
                _cursorController.SetSpellCursor(true, spell);
            }

            if (!_inputCommands.CanActiveUnitCastSpell(spell))
            {
                _board.SetSecondaryHighlightVisible(false);
                return;
            }

            if (!_inputCommands.TryBuildActiveSpellContextForInput(out var context))
            {
                _board.SetSecondaryHighlightVisible(false);
                return;
            }

            if (!_board.TryScreenToTile(UnityEngine.Input.mousePosition, out var x, out var y))
            {
                _board.SetSecondaryHighlightVisible(false);
                return;
            }

            var hoveredTile = new Vector2Int(x, y);
            var target = SpellTargetSelection.ForTile(hoveredTile);
            bool eligible = handler.IsTargetValid(spell, context, target);

            _highlightController?.SetSecondaryHighlight(hoveredTile, eligible);

            if (UnityEngine.Input.GetMouseButtonDown(0) && eligible)
            {
                _inputCommands.TryExecuteSpellEffectForInput(spell, target);
            }
        }

        private void UpdateEnchantmentTargetingInput(SpellDefinition spell, ISpellEffectHandler handler)
        {
            if (_enchantmentController == null)
            {
                return;
            }

            if (UnityEngine.Input.GetMouseButtonDown(1) || UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                _inputCommands.SetSelectedSpell(null);
                if (_cursorController != null)
                {
                    _cursorController.SetSpellCursor(false, null);
                }

                _enchantmentController.ClearHoverHighlight();
                _inputCommands.UpdateBoardHighlightForInput();
                return;
            }

            _inputCommands.SetSelectedMoveTileState(false, default);
            if (_cursorController != null)
            {
                _cursorController.SetSelectionCursor(false, _selectionCursorTexture, _selectionCursorHotspot);
                _cursorController.SetMoveCursor(false, _moveCursorTexture, _moveCursorHotspot);
                _cursorController.SetAttackCursor(false, _attackCursorTexture, _attackCursorHotspot);
                _cursorController.SetShootCursor(false, _shootCursorTexture, _shootCursorHotspot);
                _cursorController.SetSpellCursor(true, spell);
            }

            _highlightController?.HideSecondaryHighlight();
            _board?.SetSecondaryHighlightVisible(false);

            if (!_inputCommands.CanActiveUnitCastSpell(spell))
            {
                _enchantmentController.ClearHoverHighlight();
                return;
            }

            if (!_inputCommands.TryBuildActiveSpellContextForInput(out var context))
            {
                _enchantmentController.ClearHoverHighlight();
                return;
            }

            int hoveredIndex;
            bool hasTarget = handler.UsesActiveEnchantments
                ? _enchantmentController.TryUpdateActiveEnchantmentHighlight(UnityEngine.Input.mousePosition, out hoveredIndex)
                : _enchantmentController.TryUpdateHoverHighlight(UnityEngine.Input.mousePosition, out hoveredIndex);

            if (!hasTarget)
            {
                return;
            }

            var target = SpellTargetSelection.ForQuad(hoveredIndex);
            if (handler.IsTargetValid(spell, context, target) && UnityEngine.Input.GetMouseButtonDown(0))
            {
                _inputCommands.TryExecuteSpellEffectForInput(spell, target);
            }
        }

        public bool TryInspectEnchantmentAtScreenPosition(Vector2 screenPosition)
        {
            if (_enchantmentController == null || _inputCommands == null)
            {
                return false;
            }

            if (!_enchantmentController.TryGetActiveEnchantmentAtScreenPosition(screenPosition, out var snapshot, out var quadIndex))
            {
                return false;
            }

            return _inputCommands.TryToggleEnchantmentInspectionForInput(snapshot, quadIndex);
        }

        public bool TryInspectEnemyAtScreenPosition(Vector2 screenPosition)
        {
            return TryInspectUnitAtScreenPosition(screenPosition, allowPlayerUnits: false);
        }

        public bool TryInspectEnemyAtTile(Vector2Int tile)
        {
            if (!EnsureReady())
            {
                return false;
            }

            return _inputCommands.TryInspectUnitAtTileForInput(tile, allowPlayerUnits: false);
        }

        public bool TryInspectUnitAtScreenPosition(Vector2 screenPosition, bool allowPlayerUnits)
        {
            if (!EnsureReady() || _board == null)
            {
                return false;
            }

            if (!_board.TryScreenToTile(screenPosition, out var x, out var y))
            {
                return false;
            }

            return _inputCommands.TryInspectUnitAtTileForInput(new Vector2Int(x, y), allowPlayerUnits);
        }

        private bool EnsureReady()
        {
            if (_inputCommands != null)
            {
                return true;
            }

            ResolveDependencies();
            return _inputCommands != null;
        }

        private void ResolveDependencies()
        {
            if (_board == null)
            {
                _board = FindObjectOfType<WorldPerspectiveBoard>();
            }

            if (_highlightController == null)
            {
                _highlightController = GetComponent<BattleBoardHighlightController>();
                if (_highlightController == null)
                {
                    _highlightController = FindObjectOfType<BattleBoardHighlightController>();
                }
            }

            if (_cursorController == null)
            {
                _cursorController = FindObjectOfType<BattleCursorController>();
            }

            if (_combatController == null)
            {
                _combatController = GetComponent<BattleCombatController>();
                if (_combatController == null)
                {
                    _combatController = FindObjectOfType<BattleCombatController>();
                }
            }

            if (_movementController == null)
            {
                _movementController = GetComponent<BattleMovementController>();
                if (_movementController == null)
                {
                    _movementController = FindObjectOfType<BattleMovementController>();
                }
            }

            if (_spellController == null)
            {
                _spellController = GetComponent<BattleSpellController>();
                if (_spellController == null)
                {
                    _spellController = FindObjectOfType<BattleSpellController>();
                }
            }

            if (_enchantmentController == null)
            {
                _enchantmentController = GetComponent<BattleEnchantmentController>();
                if (_enchantmentController == null)
                {
                    _enchantmentController = FindObjectOfType<BattleEnchantmentController>();
                }
            }

            if (_turnOrderController == null)
            {
                _turnOrderController = _turnOrderControllerBehaviour as ITurnOrderController;
            }

            if (_turnOrderController == null)
            {
                var behaviours = GetComponents<MonoBehaviour>();
                for (int i = 0; i < behaviours.Length; i++)
                {
                    if (behaviours[i] is ITurnOrderController candidate)
                    {
                        _turnOrderControllerBehaviour = behaviours[i];
                        _turnOrderController = candidate;
                        break;
                    }
                }
            }

            _inputCommands = _turnOrderController as IBattlePlayerInputCommands;
            ValidateTurnControllerBinding();
        }

        private void ValidateTurnControllerBinding()
        {
            if (_turnOrderController != null && _inputCommands == null)
            {
                SBLog.Error("[BattlePlayerInputController] Assigned turn controller must implement IBattlePlayerInputCommands.", this);
            }
        }

        private void SetActionCursorsInactive()
        {
            if (_cursorController == null)
            {
                return;
            }

            _cursorController.SetAttackCursor(false, _attackCursorTexture, _attackCursorHotspot);
            _cursorController.SetShootCursor(false, _shootCursorTexture, _shootCursorHotspot);
            _cursorController.SetMoveCursor(false, _moveCursorTexture, _moveCursorHotspot);
            _cursorController.SetSelectionCursor(false, _selectionCursorTexture, _selectionCursorHotspot);
        }
    }
}
