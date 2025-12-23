using System.Collections.Generic;
using UnityEngine;
using SevenBattles.Battle.Movement;
using SevenBattles.Battle.Units;

namespace SevenBattles.Battle.AI
{
    /// <summary>
    /// Encapsulates simple AI turn decisions so the turn controller only orchestrates flow.
    /// </summary>
    public class BattleAiTurnService : MonoBehaviour
    {
        [SerializeField, Tooltip("Movement controller used to retrieve cached legal tiles.")]
        private BattleMovementController _movementController;

        private readonly List<Vector2Int> _legalMoveTiles = new List<Vector2Int>();
        private readonly List<UnitBattleMetadata> _nearestEnemies = new List<UnitBattleMetadata>();

        public enum DecisionType
        {
            Move,
            Skip
        }

        public readonly struct Decision
        {
            public Decision(DecisionType type, Vector2Int destination, string logMessage)
            {
                Type = type;
                Destination = destination;
                LogMessage = logMessage;
            }

            public DecisionType Type { get; }
            public Vector2Int Destination { get; }
            public string LogMessage { get; }
        }

        public struct Context
        {
            public UnitBattleMetadata ActiveUnit;
            public UnitStats ActiveStats;
            public int CurrentActionPoints;
            public bool HasMoved;
            public IReadOnlyList<UnitBattleMetadata> AllUnits;
        }

        private void Awake()
        {
            if (_movementController == null)
            {
                _movementController = GetComponent<BattleMovementController>();
                if (_movementController == null)
                {
                    _movementController = FindObjectOfType<BattleMovementController>();
                }
            }
        }

        public void SetMovementController(BattleMovementController controller)
        {
            if (controller != null)
            {
                _movementController = controller;
            }
        }

        public Decision EvaluateMovement(Context context)
        {
            if (_movementController == null)
            {
                return CreateSkipDecision(context.ActiveUnit, "missing movement controller");
            }

            var meta = context.ActiveUnit;
            if (meta == null || !meta.HasTile || context.ActiveStats == null)
            {
                return CreateSkipDecision(meta, "invalid metadata");
            }

            if (context.HasMoved)
            {
                return CreateSkipDecision(meta, "already moved");
            }

            if (context.CurrentActionPoints <= 0)
            {
                return CreateSkipDecision(meta, "no AP");
            }

            _movementController.CopyLegalMoveTiles(_legalMoveTiles);
            if (_legalMoveTiles.Count == 0)
            {
                return CreateSkipDecision(meta, "no legal tiles");
            }

            if (!TryFindNearestEnemyTile(meta, context.AllUnits, out var targetTile))
            {
                return CreateSkipDecision(meta, "no opposing units");
            }

            var destination = SelectDestination(meta.Tile, targetTile);
            if (!destination.HasValue)
            {
                return CreateSkipDecision(meta, "no closer tile");
            }

            return CreateMoveDecision(meta, destination.Value);
        }

        private bool TryFindNearestEnemyTile(UnitBattleMetadata origin, IReadOnlyList<UnitBattleMetadata> units, out Vector2Int tile)
        {
            tile = default;
            _nearestEnemies.Clear();
            if (origin == null || !origin.HasTile || units == null)
            {
                return false;
            }

            int bestDistance = int.MaxValue;
            for (int i = 0; i < units.Count; i++)
            {
                var candidate = units[i];
                if (candidate == null || !candidate.HasTile) continue;
                if (candidate.IsPlayerControlled == origin.IsPlayerControlled) continue;

                int dist = ManhattanDistance(origin.Tile, candidate.Tile);
                if (dist < bestDistance)
                {
                    bestDistance = dist;
                    _nearestEnemies.Clear();
                    _nearestEnemies.Add(candidate);
                }
                else if (dist == bestDistance)
                {
                    _nearestEnemies.Add(candidate);
                }
            }

            if (_nearestEnemies.Count == 0)
            {
                return false;
            }

            tile = SelectPreferredEnemyTile();
            return true;
        }

        private Vector2Int? SelectDestination(Vector2Int origin, Vector2Int target)
        {
            int currentDistance = ManhattanDistance(origin, target);
            Vector2Int bestTile = origin;
            int bestDistance = currentDistance;
            int bestTravelCost = int.MaxValue;
            bool foundBetter = false;

            for (int i = 0; i < _legalMoveTiles.Count; i++)
            {
                var candidate = _legalMoveTiles[i];
                int distance = ManhattanDistance(candidate, target);

                if (!foundBetter)
                {
                    if (distance >= bestDistance)
                    {
                        continue;
                    }
                }
                else if (distance > bestDistance)
                {
                    continue;
                }

                int travelCost = ManhattanDistance(candidate, origin);
                if (!foundBetter ||
                    distance < bestDistance ||
                    (distance == bestDistance && travelCost < bestTravelCost) ||
                    (distance == bestDistance && travelCost == bestTravelCost && (candidate.x < bestTile.x || (candidate.x == bestTile.x && candidate.y < bestTile.y))))
                {
                    bestTile = candidate;
                    bestDistance = distance;
                    bestTravelCost = travelCost;
                    foundBetter = true;
                }
            }

            if (!foundBetter)
            {
                return null;
            }

            return bestTile;
        }

        private Vector2Int SelectPreferredEnemyTile()
        {
            var best = _nearestEnemies[0].Tile;
            for (int i = 1; i < _nearestEnemies.Count; i++)
            {
                var tile = _nearestEnemies[i].Tile;
                if (tile.y < best.y || (tile.y == best.y && tile.x < best.x))
                {
                    best = tile;
                }
            }

            return best;
        }

        private static Decision CreateSkipDecision(UnitBattleMetadata unit, string reason)
        {
            string name = ResolveUnitName(unit);
            string suffix = string.IsNullOrEmpty(reason) ? "." : $" ({reason}).";
            return new Decision(
                DecisionType.Skip,
                default,
                $"[AI] Unit {name} cannot move this turn{suffix}");
        }

        private static Decision CreateMoveDecision(UnitBattleMetadata unit, Vector2Int destination)
        {
            string name = ResolveUnitName(unit);
            return new Decision(
                DecisionType.Move,
                destination,
                $"[AI] Unit {name} moves to ({destination.x},{destination.y}).");
        }

        private static int ManhattanDistance(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }

        private static string ResolveUnitName(UnitBattleMetadata meta)
        {
            if (meta == null)
            {
                return "Unknown";
            }

            var def = meta.Definition;
            if (def != null && !string.IsNullOrEmpty(def.Id))
            {
                return def.Id;
            }

            return meta.name;
        }
    }
}
