using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SevenBattles.Battle.Board;
using SevenBattles.Battle.Units;
using SevenBattles.Core.Units;

namespace SevenBattles.Battle.Movement
{
    /// <summary>
    /// Handles unit movement validation, BFS pathfinding, and movement execution.
    /// Extracted from SimpleTurnOrderController to follow SRP.
    /// </summary>
    public class BattleMovementController : MonoBehaviour
    {
        [Header("Movement Configuration")]
        [SerializeField, Tooltip("Duration in seconds for each tile movement step.")]
        private float _moveDurationSeconds = 0.3f;

        [Header("Dependencies")]
        [SerializeField, Tooltip("Reference to the battle board for tile/world conversions.")]
        private WorldPerspectiveBoard _board;

        // Internal state - BFS pathfinding cache
        private readonly HashSet<Vector2Int> _legalMoveTiles = new HashSet<Vector2Int>();
        private readonly Dictionary<Vector2Int, Vector2Int> _movePrevTile = new Dictionary<Vector2Int, Vector2Int>();

        /// <summary>
        /// Checks if the unit can perform movement.
        /// </summary>
        public bool CanMove(UnitStats stats, int currentAP, bool hasAlreadyMoved, bool isPlayerControlled)
        {
            if (!isPlayerControlled) return false;
            if (currentAP <= 0) return false;
            if (hasAlreadyMoved) return false;
            if (_board == null) return false;
            if (stats == null) return false;
            return true;
        }

        /// <summary>
        /// Checks if the specified tile is a legal move destination.
        /// </summary>
        public bool IsTileLegalMoveDestination(Vector2Int tile)
        {
            if (_legalMoveTiles.Count == 0) return false;
            return _legalMoveTiles.Contains(tile);
        }

        private void Awake()
        {
            if (_board == null)
            {
                _board = FindObjectOfType<WorldPerspectiveBoard>();
                if (_board == null)
                {
                    Debug.LogError("[BattleMovementController] WorldPerspectiveBoard dependency is missing and could not be found in the scene.");
                }
            }
        }

        /// <summary>
        /// Rebuilds the set of legal move tiles using BFS pathfinding.
        /// </summary>
        public void RebuildLegalMoveTiles<T>(
            T activeUnit,
            List<T> allUnits,
            Func<T, UnitBattleMetadata> getMetadata,
            Func<T, UnitStats> getStats,
            Func<Vector2Int, bool> isTileOccupied) where T : struct
        {
            _legalMoveTiles.Clear();
            _movePrevTile.Clear();

            if (_board == null)
            {
                Debug.LogError("[BattleMovementController] Cannot rebuild legal move tiles: Board is null.");
                return;
            }

            var meta = getMetadata(activeUnit);
            var stats = getStats(activeUnit);

            if (meta == null || !meta.HasTile || stats == null)
            {
                return;
            }

            int speed = Mathf.Max(0, stats.Speed);
            if (speed <= 0)
            {
                return;
            }

            int cols = _board.Columns;
            int rows = _board.Rows;
            if (cols <= 0 || rows <= 0) return;

            var origin = meta.Tile;
            var visited = new Dictionary<Vector2Int, int>();
            var queue = new Queue<Vector2Int>();
            visited[origin] = 0;
            queue.Enqueue(origin);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                int used = visited[current];
                if (used >= speed) continue;

                TryEnqueueNeighbor(origin, current, new Vector2Int(current.x + 1, current.y), used, cols, rows, visited, queue, isTileOccupied);
                TryEnqueueNeighbor(origin, current, new Vector2Int(current.x - 1, current.y), used, cols, rows, visited, queue, isTileOccupied);
                TryEnqueueNeighbor(origin, current, new Vector2Int(current.x, current.y + 1), used, cols, rows, visited, queue, isTileOccupied);
                TryEnqueueNeighbor(origin, current, new Vector2Int(current.x, current.y - 1), used, cols, rows, visited, queue, isTileOccupied);
            }
        }

        /// <summary>
        /// Attempts to execute a movement to the destination tile.
        /// </summary>
        public void TryExecuteMove(
            Vector2Int destinationTile,
            UnitBattleMetadata activeUnitMeta,
            Action onMoveStarted,
            Action onAPConsumed,
            Action<UnitBattleMetadata> onFaceNearestEnemy,
            Action onMoveCompleted)
        {
            if (!IsTileLegalMoveDestination(destinationTile))
            {
                onMoveCompleted?.Invoke();
                return;
            }

            if (activeUnitMeta == null || activeUnitMeta.transform == null)
            {
                onMoveCompleted?.Invoke();
                return;
            }

            var originTile = activeUnitMeta.Tile;
            var path = BuildMovePath(originTile, destinationTile);
            if (path == null || path.Count == 0)
            {
                onMoveCompleted?.Invoke();
                return;
            }

            onMoveStarted?.Invoke();
            StartCoroutine(MoveUnitRoutine(activeUnitMeta, path, onAPConsumed, onFaceNearestEnemy, onMoveCompleted));
        }

        private void TryEnqueueNeighbor(
            Vector2Int origin,
            Vector2Int from,
            Vector2Int tile,
            int usedSteps,
            int cols,
            int rows,
            Dictionary<Vector2Int, int> visited,
            Queue<Vector2Int> queue,
            Func<Vector2Int, bool> isTileOccupied)
        {
            if (tile.x < 0 || tile.x >= cols || tile.y < 0 || tile.y >= rows) return;
            if (visited.ContainsKey(tile)) return;
            if (tile != origin && isTileOccupied(tile)) return;

            int nextUsed = usedSteps + 1;
            visited[tile] = nextUsed;
            _movePrevTile[tile] = from;
            if (tile != origin)
            {
                _legalMoveTiles.Add(tile);
            }
            queue.Enqueue(tile);
        }

        private List<Vector2Int> BuildMovePath(Vector2Int origin, Vector2Int destination)
        {
            if (!_movePrevTile.ContainsKey(destination))
            {
                return null;
            }

            var path = new List<Vector2Int>();
            var current = destination;
            while (current != origin)
            {
                path.Add(current);
                if (!_movePrevTile.TryGetValue(current, out current))
                {
                    return null;
                }
            }
            path.Reverse();
            return path;
        }

        private IEnumerator MoveUnitRoutine(
            UnitBattleMetadata meta,
            List<Vector2Int> path,
            Action onAPConsumed,
            Action<UnitBattleMetadata> onFaceNearestEnemy,
            Action onMoveCompleted)
        {
            if (_board == null)
            {
                onMoveCompleted?.Invoke();
                yield break;
            }

            var transform = meta.transform;
            if (transform == null)
            {
                onMoveCompleted?.Invoke();
                yield break;
            }

            float baseZ = transform.position.z;
            UnitVisualUtil.TrySetState(meta.gameObject, "Walk");

            var currentTile = meta.Tile;

            for (int i = 0; i < path.Count; i++)
            {
                var nextTile = path[i];
                Vector3 start = transform.position;
                Vector3 target = _board.TileCenterWorld(nextTile.x, nextTile.y);
                target.z = baseZ;

                Vector2 direction = ComputeDirection(currentTile, nextTile);
                if (direction != Vector2.zero)
                {
                    UnitVisualUtil.SetDirectionIfCharacter4D(meta.gameObject, direction);
                }

                float t = 0f;
                float duration = Mathf.Max(0.01f, _moveDurationSeconds);

                while (t < duration)
                {
                    t += Time.deltaTime;
                    float p = Mathf.Clamp01(t / duration);
                    float eased = p * p * (3f - 2f * p);
                    transform.position = Vector3.LerpUnclamped(start, target, eased);
                    yield return null;
                }

                transform.position = target;
                currentTile = nextTile;
            }

            meta.Tile = currentTile;

            // Update sorting order based on new tile position
            if (_board != null)
            {
                int newSortingOrder = _board.ComputeSortingOrder(
                    currentTile.x,
                    currentTile.y,
                    meta.BaseSortingOrder,
                    rowStride: 10,
                    intraRowOffset: 0);

                var sortingGroup = transform.GetComponentInChildren<UnityEngine.Rendering.SortingGroup>(true);
                if (sortingGroup != null)
                {
                    sortingGroup.sortingLayerName = meta.SortingLayer;
                    sortingGroup.sortingOrder = newSortingOrder;
                }
                else
                {
                    var renderers = transform.GetComponentsInChildren<SpriteRenderer>(true);
                    for (int i = 0; i < renderers.Length; i++)
                    {
                        renderers[i].sortingLayerName = meta.SortingLayer;
                        renderers[i].sortingOrder = newSortingOrder;
                    }
                }
            }

            UnitVisualUtil.TrySetState(meta.gameObject, "Idle");

            // Face nearest enemy
            onFaceNearestEnemy?.Invoke(meta);

            // Consume AP
            onAPConsumed?.Invoke();

            // Clear caches
            _legalMoveTiles.Clear();
            _movePrevTile.Clear();

            // Notify completion
            onMoveCompleted?.Invoke();
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
    }
}
