using System.Collections.Generic;
using UnityEngine;

namespace SevenBattles.Battle.Start
{
    // Pure logic model for squad placement validation and state tracking.
    // Independent of Unity scene objects to enable unit testing.
    public class SquadPlacementModel
    {
        private readonly int _columns;
        private readonly int _rows;
        private readonly int _allowedPlayerRows; // number of rows starting at y=0 that are valid (e.g., 2)
        private readonly int _squadSize;

        // Maps tile -> wizard index
        private readonly Dictionary<Vector2Int, int> _occupancy = new Dictionary<Vector2Int, int>();
        // Maps wizard index -> tile (if placed)
        private readonly Dictionary<int, Vector2Int> _placedByWizard = new Dictionary<int, Vector2Int>();

        public SquadPlacementModel(int columns, int rows, int allowedPlayerRows, int squadSize)
        {
            _columns = Mathf.Max(1, columns);
            _rows = Mathf.Max(1, rows);
            _allowedPlayerRows = Mathf.Clamp(allowedPlayerRows, 1, _rows);
            _squadSize = Mathf.Max(1, squadSize);
        }

        public bool IsInsideGrid(Vector2Int tile)
        {
            return tile.x >= 0 && tile.x < _columns && tile.y >= 0 && tile.y < _rows;
        }

        public bool IsInPlayerPlacementArea(Vector2Int tile)
        {
            // Player area: first N rows from y=0 inclusive
            return IsInsideGrid(tile) && tile.y >= 0 && tile.y < _allowedPlayerRows;
        }

        public bool IsOccupied(Vector2Int tile)
        {
            return _occupancy.ContainsKey(tile);
        }

        public bool CanPlace(Vector2Int tile)
        {
            if (!IsInPlayerPlacementArea(tile)) return false;
            if (IsOccupied(tile)) return false;
            return true;
        }

        public bool TryPlace(int wizardIndex, Vector2Int tile)
        {
            if (wizardIndex < 0 || wizardIndex >= _squadSize) return false;
            if (!CanPlace(tile)) return false;

            // If this wizard is already placed, remove previous first
            if (_placedByWizard.TryGetValue(wizardIndex, out var prev))
            {
                _occupancy.Remove(prev);
                _placedByWizard.Remove(wizardIndex);
            }

            _occupancy[tile] = wizardIndex;
            _placedByWizard[wizardIndex] = tile;
            return true;
        }

        public bool TryRemoveAt(Vector2Int tile, out int wizardIndex)
        {
            if (_occupancy.TryGetValue(tile, out wizardIndex))
            {
                _occupancy.Remove(tile);
                _placedByWizard.Remove(wizardIndex);
                return true;
            }
            wizardIndex = -1;
            return false;
        }

        public bool TryRemoveWizard(int wizardIndex)
        {
            if (_placedByWizard.TryGetValue(wizardIndex, out var tile))
            {
                _placedByWizard.Remove(wizardIndex);
                _occupancy.Remove(tile);
                return true;
            }
            return false;
        }

        public bool IsComplete()
        {
            return _placedByWizard.Count >= _squadSize;
        }

        public bool TryGetWizardAt(Vector2Int tile, out int wizardIndex)
        {
            return _occupancy.TryGetValue(tile, out wizardIndex);
        }

        public bool TryGetTileOfWizard(int wizardIndex, out Vector2Int tile)
        {
            return _placedByWizard.TryGetValue(wizardIndex, out tile);
        }

        public IReadOnlyDictionary<Vector2Int, int> Occupancy => _occupancy;
    }
}

