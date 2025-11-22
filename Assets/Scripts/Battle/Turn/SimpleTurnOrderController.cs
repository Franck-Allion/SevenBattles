using System;
using System.Collections.Generic;
using UnityEngine;
using SevenBattles.Battle.Board;
using SevenBattles.Battle.Units;
using SevenBattles.Core;

namespace SevenBattles.Battle.Turn
{
    // Basic initiative-based turn controller for wizards.
    // Discovers UnitBattleMetadata instances at battle start, sorts by UnitStats.Initiative,
    // and advances turns for player and AI units.
    public class SimpleTurnOrderController : MonoBehaviour, IBattleTurnController
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
        private bool _interactionLocked;
        private int _activeUnitCurrentActionPoints;
        private int _activeUnitMaxActionPoints;
        private bool _activeUnitHasMoved;
        private bool _movementAnimating;
        private bool _hasSelectedMoveTile;
        private Vector2Int _selectedMoveTile;
        private readonly HashSet<Vector2Int> _legalMoveTiles = new HashSet<Vector2Int>();
        private readonly Dictionary<Vector2Int, Vector2Int> _movePrevTile = new Dictionary<Vector2Int, Vector2Int>();

        public bool HasActiveUnit => _hasActiveUnit;

        public bool IsActiveUnitPlayerControlled => IsActiveUnitPlayerControlledInternal();

        public bool IsInteractionLocked => _interactionLocked;

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

        public event Action ActiveUnitChanged;
        public event Action ActiveUnitActionPointsChanged;
        public event Action ActiveUnitStatsChanged;

        public int ActiveUnitCurrentActionPoints => _hasActiveUnit ? _activeUnitCurrentActionPoints : 0;
        public int ActiveUnitMaxActionPoints => _hasActiveUnit ? _activeUnitMaxActionPoints : 0;

        private void Start()
        {
            if (_autoStartOnPlay)
            {
                BeginBattle();
            }
        }

        private void Update()
        {
            if (_interactionLocked) return;
            if (!HasActiveUnit) return;
            if (_movementAnimating) return;

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
            _interactionLocked = locked;
        }

        // Internal rebuild used by StartBattle and tests.
        private void BeginBattle()
        {
            RebuildUnits();
            if (_board != null)
            {
                // During combat, disable hover-driven highlight so only the active unit tile is marked.
                _board.SetHoverEnabled(false);
                if (_activeUnitHighlightMaterial != null)
                {
                    _board.SetHighlightMaterial(_activeUnitHighlightMaterial);
                }
            }
            SelectFirstUnit();
        }

        public void RequestEndTurn()
        {
            if (_interactionLocked) return;
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
                // Stable tie-breaker based on instance id to avoid jitter.
                int ida = a.Metadata != null ? a.Metadata.GetInstanceID() : 0;
                int idb = b.Metadata != null ? b.Metadata.GetInstanceID() : 0;
                return ida.CompareTo(idb);
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
                SetActiveIndex(-1);
                return;
            }

            for (int i = 0; i < _units.Count; i++)
            {
                if (IsUnitValid(_units[i]))
                {
                    SetActiveIndex(i);
                    return;
                }
            }

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

            if (_units.Count == 0)
            {
                _activeIndex = -1;
                _hasActiveUnit = false;
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
                else
                {
                    _activeIndex = -1;
                    _hasActiveUnit = false;
                }
            }
        }

        private void AdvanceToNextUnit()
        {
            CompactUnits();

            if (_units.Count == 0)
            {
                SetActiveIndex(-1);
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
                        SetActiveIndex(idx);
                        return;
                    }
                    attempts++;
                }

                // No valid units remain.
                SetActiveIndex(-1);
            }
            finally
            {
                _advancing = false;
            }
        }

        private void SetActiveIndex(int index)
        {
            _activeIndex = index;
            _pendingAiEndTime = -1f;
            _activeUnitHasMoved = false;
            _hasSelectedMoveTile = false;
            _legalMoveTiles.Clear();
            _movePrevTile.Clear();

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
            if (!CanActiveUnitMove())
            {
                if (_board != null)
                {
                    _board.SetSecondaryHighlightVisible(false);
                }
                _hasSelectedMoveTile = false;
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
                return;
            }

            var hoveredTile = new Vector2Int(x, y);

            if (_hasSelectedMoveTile)
            {
                _board.SetSecondaryHighlightVisible(true);
                _board.MoveSecondaryHighlightToTile(_selectedMoveTile.x, _selectedMoveTile.y);
                bool stillValid = IsTileLegalMoveDestination(_selectedMoveTile);
                _board.SetSecondaryHighlightColor(stillValid ? _moveValidColor : _moveInvalidColor);

                if (Input.GetMouseButtonDown(0))
                {
                    if (hoveredTile == _selectedMoveTile)
                    {
                        TryExecuteActiveUnitMove(_selectedMoveTile);
                    }
                    else if (IsTileLegalMoveDestination(hoveredTile))
                    {
                        _selectedMoveTile = hoveredTile;
                    }
                }

                return;
            }

            bool legal = IsTileLegalMoveDestination(hoveredTile);
            _board.SetSecondaryHighlightVisible(true);
            _board.MoveSecondaryHighlightToTile(hoveredTile.x, hoveredTile.y);
            _board.SetSecondaryHighlightColor(legal ? _moveValidColor : _moveInvalidColor);

            if (Input.GetMouseButtonDown(0) && legal)
            {
                _hasSelectedMoveTile = true;
                _selectedMoveTile = hoveredTile;
            }
        }

        private bool CanActiveUnitMove()
        {
            if (!_hasActiveUnit) return false;
            if (!IsActiveUnitPlayerControlledInternal()) return false;
            if (_activeUnitCurrentActionPoints <= 0) return false;
            if (_activeUnitHasMoved) return false;
            if (_board == null) return false;
            return true;
        }

        private bool IsTileLegalMoveDestination(Vector2Int tile)
        {
            if (!CanActiveUnitMove()) return false;
            if (_legalMoveTiles.Count == 0) return false;
            return _legalMoveTiles.Contains(tile);
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
            if (!IsTileLegalMoveDestination(destinationTile))
            {
                return;
            }

            var u = _units[_activeIndex];
            var meta = u.Metadata;
            if (meta == null) return;

            var transform = meta.transform;
            if (transform == null) return;

            var originTile = meta.Tile;
            var path = BuildMovePath(originTile, destinationTile);
            if (path == null || path.Count == 0)
            {
                return;
            }

            _movementAnimating = true;
            _hasSelectedMoveTile = false;
            if (_board != null)
            {
                _board.SetSecondaryHighlightVisible(false);
            }
            StartCoroutine(MoveActiveUnitRoutine(meta, path));
        }

        private System.Collections.IEnumerator MoveActiveUnitRoutine(UnitBattleMetadata meta, List<Vector2Int> path)
        {
            if (_board == null)
            {
                yield break;
            }

            var transform = meta.transform;
            if (transform == null)
            {
                yield break;
            }

            float baseZ = transform.position.z;

            object animationManager = TryGetCharacter4DAnimationManager(meta.gameObject, out var characterStateType);
            if (animationManager != null && characterStateType != null)
            {
                TryInvokeCharacterState(animationManager, characterStateType, "Walk");
            }

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
                    SevenBattles.Battle.Units.UnitVisualUtil.SetDirectionIfCharacter4D(meta.gameObject, direction);
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

            if (animationManager != null && characterStateType != null)
            {
                TryInvokeCharacterState(animationManager, characterStateType, "Idle");
            }

            FaceActiveUnitTowardsNearestEnemy(meta);

            _activeUnitHasMoved = true;
            if (_activeUnitCurrentActionPoints > 0)
            {
                _activeUnitCurrentActionPoints--;
            }

            ActiveUnitActionPointsChanged?.Invoke();
            _movementAnimating = false;
            UpdateBoardHighlight();
            _legalMoveTiles.Clear();
            _movePrevTile.Clear();
        }

        private void RebuildLegalMoveTilesForActiveUnit()
        {
            _legalMoveTiles.Clear();
            _movePrevTile.Clear();
            if (!CanActiveUnitMove()) return;
            int cols = _board.Columns;
            int rows = _board.Rows;
            if (cols <= 0 || rows <= 0) return;
            var u = _units[_activeIndex];
            var meta = u.Metadata;
            var stats = u.Stats;
            if (meta == null || !meta.HasTile || stats == null) return;
            int speed = Mathf.Max(0, stats.Speed);
            if (speed <= 0) return;

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

                TryEnqueueNeighbor(origin, current, new Vector2Int(current.x + 1, current.y), used, cols, rows, visited, queue);
                TryEnqueueNeighbor(origin, current, new Vector2Int(current.x - 1, current.y), used, cols, rows, visited, queue);
                TryEnqueueNeighbor(origin, current, new Vector2Int(current.x, current.y + 1), used, cols, rows, visited, queue);
                TryEnqueueNeighbor(origin, current, new Vector2Int(current.x, current.y - 1), used, cols, rows, visited, queue);
            }
        }

        private void TryEnqueueNeighbor(Vector2Int origin, Vector2Int from, Vector2Int tile, int usedSteps, int cols, int rows, Dictionary<Vector2Int, int> visited, Queue<Vector2Int> queue)
        {
            if (tile.x < 0 || tile.x >= cols || tile.y < 0 || tile.y >= rows) return;
            if (visited.ContainsKey(tile)) return;
            if (tile != origin && IsTileOccupiedByAnyUnit(tile)) return;

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

        private static object TryGetCharacter4DAnimationManager(GameObject instance, out System.Type characterStateType)
        {
            characterStateType = null;
            if (instance == null) return null;

            try
            {
                var components = instance.GetComponentsInChildren<MonoBehaviour>(true);
                for (int i = 0; i < components.Length; i++)
                {
                    var comp = components[i];
                    if (comp == null) continue;
                    var type = comp.GetType();

                    if (type.Name == "AnimationManager" || type.FullName == "Assets.HeroEditor4D.Common.Scripts.CharacterScripts.AnimationManager")
                    {
                        var asm = type.Assembly;
                        characterStateType = asm.GetType("Assets.HeroEditor4D.Common.Scripts.Enums.CharacterState");
                        if (characterStateType == null) return null;
                        return comp;
                    }
                }
            }
            catch
            {
                // Ignore and fall through.
            }

            return null;
        }

        private static void TryInvokeCharacterState(object animationManager, System.Type characterStateType, string stateName)
        {
            if (animationManager == null || characterStateType == null || string.IsNullOrEmpty(stateName)) return;

            try
            {
                var method = animationManager.GetType().GetMethod("SetState", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance, null, new System.Type[] { characterStateType }, null);
                if (method == null) return;
                var state = System.Enum.Parse(characterStateType, stateName);
                method.Invoke(animationManager, new object[] { state });
            }
            catch
            {
                // Ignore reflection failures to keep battle logic robust even if HeroEditor4D is not present.
            }
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
            _board.SetHighlightVisible(true);
            _board.MoveHighlightToTile(tile.x, tile.y);
            _board.SetHighlightColor(meta.IsPlayerControlled ? _playerHighlightColor : _enemyHighlightColor);
        }

        private static bool IsUnitValid(TurnUnit unit)
        {
            return unit.Metadata != null && unit.Metadata.isActiveAndEnabled && unit.Stats != null;
        }

        private bool IsActiveUnitPlayerControlledInternal()
        {
            if (!_hasActiveUnit || _activeIndex < 0 || _activeIndex >= _units.Count) return false;
            var u = _units[_activeIndex];
            return u.Metadata != null && u.Metadata.IsPlayerControlled;
        }
    }
}
