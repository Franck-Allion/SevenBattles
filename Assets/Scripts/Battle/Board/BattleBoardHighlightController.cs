using UnityEngine;
using SevenBattles.Battle.Units;

namespace SevenBattles.Battle.Board
{
    /// <summary>
    /// Service responsible for managing the visual highlighting of tiles on the battle board.
    /// Handles primary (active unit) and secondary (cursor) highlights, including sorting order logic.
    /// </summary>
    public class BattleBoardHighlightController : MonoBehaviour
    {
        [Header("Board Reference")]
        [SerializeField] private WorldPerspectiveBoard _board;

        [Header("Highlight Colors")]
        [SerializeField] private Color _playerHighlightColor = new Color(0.3f, 1f, 0.3f, 0.4f);
        [SerializeField] private Color _enemyHighlightColor = new Color(1f, 0.3f, 0.3f, 0.4f);
        [SerializeField] private bool _overrideActiveUnitColor;
        [SerializeField, Tooltip("If checked, this color will be used for the active unit highlight regardless of team.")]
        private Color _activeUnitColor = Color.white;
        
        [Header("Movement Colors")]
        [SerializeField, Tooltip("Color used to highlight a legal destination tile for the active unit.")]
        private Color _moveValidColor = new Color(0.3f, 1f, 0.3f, 0.5f);
        [SerializeField, Tooltip("Color used to highlight an illegal destination tile for the active unit.")]
        private Color _moveInvalidColor = new Color(1f, 0.3f, 0.3f, 0.5f);

        [Header("Materials")]
        [SerializeField, Tooltip("Optional material used for the active unit tile highlight during battle (e.g., outline-only). If null, the board's default highlight material is used.")]
        private Material _activeUnitHighlightMaterial;

        private void Awake()
        {
            if (_board == null)
            {
                _board = FindObjectOfType<WorldPerspectiveBoard>();
                if (_board == null)
                {
                    Debug.LogError("[BattleBoardHighlightController] WorldPerspectiveBoard dependency missing!");
                }
            }
        }

        public void InitializeForBattle()
        {
            if (_activeUnitHighlightMaterial != null && _board != null)
            {
                _board.SetHighlightMaterial(_activeUnitHighlightMaterial);
            }
        }

        public void HideAll()
        {
            if (_board == null) return;
            _board.SetHighlightVisible(false);
            _board.SetSecondaryHighlightVisible(false);
        }

        public void UpdateActiveUnitHighlight(UnitBattleMetadata meta)
        {
            if (_board == null) return;

            if (meta == null || !meta.HasTile)
            {
                _board.SetHighlightVisible(false);
                return;
            }

            var tile = meta.Tile;
            
            // Validate BaseSortingOrder - should never be 0 or negative
            if (meta.BaseSortingOrder <= 0)
            {
                Debug.LogWarning($"[BattleBoardHighlightController] Active unit has invalid BaseSortingOrder={meta.BaseSortingOrder}. Setting to default 100.", this);
                meta.BaseSortingOrder = 100;
            }
            
            // Get actual sorting order from the unit's renderers
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
            }
            
            // Set highlight to render behind the unit
            int highlightSortingOrder = unitSortingOrder - 1;
            const int MinHighlightSortingOrder = 1;
            if (highlightSortingOrder < MinHighlightSortingOrder)
            {
                highlightSortingOrder = MinHighlightSortingOrder;
            }
            
            _board.SetHighlightSortingOrder(highlightSortingOrder);
            _board.SetHighlightVisible(true);
            _board.MoveHighlightToTile(tile.x, tile.y);
            if (_overrideActiveUnitColor)
            {
                _board.SetHighlightColor(_activeUnitColor);
            }
            else
            {
                _board.SetHighlightColor(meta.IsPlayerControlled ? _playerHighlightColor : _enemyHighlightColor);
            }
            
            // Note: _activeUnitHighlightMaterial is available if needed but WorldPerspectiveBoard might not expose material setter easily 
            // or it might use valid/invalid materials.
            // SimpleTurnOrderController had field _activeUnitHighlightMaterial but seemingly didn't use it in UpdateBoardHighlight in the viewed snippet?
            // Checking snippet: Step 732 didn't show usage. Step 745 showed definition.
            // If it was unused, we keep it for potential future use or if I missed the usage.
            // Assuming it might be used elsewhere or planned.
        }

        public void SetSecondaryHighlight(Vector2Int tile, bool isValid)
        {
            if (_board == null) return;
            SetSecondaryHighlight(tile, isValid ? _moveValidColor : _moveInvalidColor);
        }

        public void SetSecondaryHighlight(Vector2Int tile, Color color)
        {
            if (_board == null) return;
            _board.SetSecondaryHighlightVisible(true);
            _board.MoveSecondaryHighlightToTile(tile.x, tile.y);
            _board.SetSecondaryHighlightColor(color);
        }

        public void HideSecondaryHighlight()
        {
             if (_board != null) _board.SetSecondaryHighlightVisible(false);
        }
    }
}
