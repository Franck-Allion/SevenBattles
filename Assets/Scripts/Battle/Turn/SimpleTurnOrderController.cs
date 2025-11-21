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
            if (IsActiveUnitPlayerControlled) return;
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

            var metas = FindObjectsOfType<UnitBattleMetadata>();
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
            ActiveUnitActionPointsChanged?.Invoke();
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
