using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SevenBattles.Battle.Board;
using SevenBattles.Battle.Units;
using SevenBattles.Core.Units;

namespace SevenBattles.Battle.Combat
{
    /// <summary>
    /// Handles melee attack validation, execution, and damage application.
    /// Extracted from SimpleTurnOrderController to follow SRP.
    /// </summary>
    public class BattleCombatController : MonoBehaviour
    {
        [Header("Attack Configuration")]
        [SerializeField, Tooltip("Audio clip played when an attack hits (one-shot).")]
        private AudioClip _attackHitClip;
        
        [SerializeField, Tooltip("Color for secondary highlight when hovering an attackable enemy.")]
        private Color _attackCursorColor = new Color(1f, 0.3f, 0.3f, 0.6f);

        [Header("Death Effects")]
        [SerializeField, Tooltip("Optional VFX prefab instantiated when a unit dies (e.g., puff of smoke or soul effect).")]
        private GameObject _deathVfxPrefab;
        
        [SerializeField, Tooltip("Lifetime in seconds before the death VFX instance is destroyed.")]
        private float _deathVfxLifetimeSeconds = 2f;
        
        [SerializeField, Tooltip("Duration in seconds to wait after playing a death animation before removing the unit GameObject from the board.")]
        private float _deathAnimationDurationSeconds = 1f;

        [Header("Dependencies")]
        [SerializeField, Tooltip("Reference to the battle board for hiding highlights during animations.")]
        private WorldPerspectiveBoard _board;
        
        [SerializeField, Tooltip("Service managing battle visual effects like damage numbers.")]
        private BattleVisualFeedbackService _visualFeedback;

        // Internal state
        private readonly HashSet<Vector2Int> _attackableEnemyTiles = new HashSet<Vector2Int>();

        public Color AttackCursorColor => _attackCursorColor;

        /// <summary>
        /// Checks if the active unit can perform an attack.
        /// </summary>
        public bool CanAttack(UnitStats activeUnitStats, int currentAP, bool isPlayerControlled, bool isMoving, bool isAttacking)
        {
            if (!isPlayerControlled) return false;
            if (currentAP < 1) return false;
            if (isMoving) return false;
            if (isAttacking) return false;
            if (_board == null) return false;
            if (activeUnitStats == null) return false;
            if (activeUnitStats.Attack <= 0) return false;

            return true;
        }

        /// <summary>
        /// Checks if the specified tile contains an attackable enemy.
        /// </summary>
        public bool IsAttackableEnemyTile(Vector2Int tile)
        {
            return _attackableEnemyTiles.Contains(tile);
        }

        /// <summary>
        /// Rebuilds the set of tiles containing attackable enemies (adjacent to active unit).
        /// </summary>
        public void RebuildAttackableEnemyTiles<T>(T activeUnit, List<T> allUnits, Func<T, UnitBattleMetadata> getMetadata, Func<T, UnitStats> getStats) where T : struct
        {
            _attackableEnemyTiles.Clear();

            var activeMeta = getMetadata(activeUnit);
            var activeStats = getStats(activeUnit);
            
            if (activeMeta == null || !activeMeta.HasTile) return;
            if (activeStats == null || activeStats.Attack <= 0) return;

            var activeTile = activeMeta.Tile;

            // Check all four cardinal directions (N, S, E, W)
            Vector2Int[] adjacentOffsets = new Vector2Int[]
            {
                new Vector2Int(0, 1),   // North
                new Vector2Int(0, -1),  // South
                new Vector2Int(1, 0),   // East
                new Vector2Int(-1, 0)   // West
            };

            foreach (var offset in adjacentOffsets)
            {
                var checkTile = activeTile + offset;

                // Find if there's an enemy unit on this tile
                for (int i = 0; i < allUnits.Count; i++)
                {
                    var targetMeta = getMetadata(allUnits[i]);
                    if (targetMeta == null || !targetMeta.HasTile) continue;
                    if (targetMeta.Tile != checkTile) continue;

                    // Check if it's an enemy (different team)
                    if (targetMeta.IsPlayerControlled != activeMeta.IsPlayerControlled)
                    {
                        _attackableEnemyTiles.Add(checkTile);
                        break; // Only one unit per tile
                    }
                }
            }
        }

        /// <summary>
        /// Attempts to execute an attack on the target tile.
        /// </summary>
        public void TryExecuteAttack<T>(
            Vector2Int targetTile,
            T attacker,
            List<T> allUnits,
            Func<T, UnitBattleMetadata> getMetadata,
            Func<T, UnitStats> getStats,
            Action onAttackStarted,
            Action onAPConsumed,
            Action onUnitDied,
            Action onComplete) where T : struct
        {
            if (!IsAttackableEnemyTile(targetTile))
            {
                onComplete?.Invoke();
                return;
            }

            var attackerMeta = getMetadata(attacker);
            var attackerStats = getStats(attacker);

            if (attackerMeta == null || attackerStats == null)
            {
                onComplete?.Invoke();
                return;
            }

            // Find the target unit on the target tile
            T? targetUnit = null;
            for (int i = 0; i < allUnits.Count; i++)
            {
                var targetMeta = getMetadata(allUnits[i]);
                if (targetMeta == null || !targetMeta.HasTile) continue;
                if (targetMeta.Tile == targetTile)
                {
                    targetUnit = allUnits[i];
                    break;
                }
            }

            if (!targetUnit.HasValue)
            {
                onComplete?.Invoke();
                return;
            }

            var target = targetUnit.Value;
            var targetStats = getStats(target);
            if (targetStats == null)
            {
                onComplete?.Invoke();
                return;
            }

            // Calculate damage
            int damage = BattleDamageCalculator.Calculate(attackerStats.Attack, targetStats.Defense);

            // Start the attack sequence coroutine
            onAttackStarted?.Invoke();
            StartCoroutine(ExecuteAttackRoutine(attacker, target, damage, getMetadata, getStats, onAPConsumed, onUnitDied, onComplete));
        }

        private IEnumerator ExecuteAttackRoutine<T>(
            T attacker,
            T target,
            int damage,
            Func<T, UnitBattleMetadata> getMetadata,
            Func<T, UnitStats> getStats,
            Action onAPConsumed,
            Action onUnitDied,
            Action onComplete) where T : struct
        {
            if (_board != null) _board.SetSecondaryHighlightVisible(false);

            var attackerMeta = getMetadata(attacker);
            var targetMeta = getMetadata(target);
            var targetStats = getStats(target);

            // 1. Face both combatants towards each other
            if (attackerMeta != null && targetMeta != null)
            {
                // Face attacker towards target
                var dir = ComputeDirection(attackerMeta.Tile, targetMeta.Tile);
                if (dir != Vector2.zero)
                {
                    UnitVisualUtil.SetDirectionIfCharacter4D(attackerMeta.gameObject, dir);
                }

                // Face target towards attacker
                var reverseDir = ComputeDirection(targetMeta.Tile, attackerMeta.Tile);
                if (reverseDir != Vector2.zero)
                {
                    UnitVisualUtil.SetDirectionIfCharacter4D(targetMeta.gameObject, reverseDir);
                }
            }

            // 2. Play "Attack" on attacker
            if (attackerMeta != null)
            {
                UnitVisualUtil.TryPlayAnimation(attackerMeta.gameObject, "Attack");
            }

            // 3. Wait for impact
            yield return new WaitForSeconds(0.25f);

            // 4. Play "Hit" on target
            if (targetMeta != null)
            {
                UnitVisualUtil.TryPlayAnimation(targetMeta.gameObject, "Hit");
            }

            // 5. Apply damage/sound
            bool targetDied = false;
            if (targetStats != null)
            {
                targetStats.TakeDamage(damage);
                targetDied = targetStats.Life <= 0;
            }

            // Show damage number above target
            if (_visualFeedback != null && targetMeta != null)
            {
                Vector3 targetPosition = targetMeta.transform.position;
                _visualFeedback.ShowDamageNumber(targetPosition, damage);
            }

            if (_attackHitClip != null)
            {
                AudioSource.PlayClipAtPoint(_attackHitClip, Vector3.zero, 1f);
            }

            // Debug log
            string attackerName = attackerMeta != null ? (attackerMeta.Definition != null ? attackerMeta.Definition.Id : attackerMeta.gameObject.name) : "Unknown";
            string targetName = targetMeta != null ? (targetMeta.Definition != null ? targetMeta.Definition.Id : targetMeta.gameObject.name) : "Unknown";
            Debug.Log($"[Combat] {attackerName} hit {targetName} for {damage} damage.");

            // 6. Wait for completion
            yield return new WaitForSeconds(0.5f);

            // 7. Reset to "Idle" or play death, spawn VFX, and schedule removal
            if (attackerMeta != null)
            {
                UnitVisualUtil.TryPlayAnimation(attackerMeta.gameObject, "Idle");
            }
            
            if (targetMeta != null)
            {
                if (targetDied)
                {
                    // Play death animation
                    UnitVisualUtil.TryPlayAnimation(targetMeta.gameObject, "Death");

                    // Spawn optional death VFX
                    if (_deathVfxPrefab != null)
                    {
                        var vfxInstance = Instantiate(_deathVfxPrefab, targetMeta.transform.position, Quaternion.identity);
                        if (_deathVfxLifetimeSeconds > 0f)
                        {
                            Destroy(vfxInstance, _deathVfxLifetimeSeconds);
                        }
                    }

                    // Play optional per-unit death SFX
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

                    // Defer cleanup
                    StartCoroutine(HandleUnitDeathCleanup(targetMeta, onUnitDied));
                }
                else
                {
                    UnitVisualUtil.TryPlayAnimation(targetMeta.gameObject, "Idle");
                }
            }

            // Consume AP
            onAPConsumed?.Invoke();

            // Notify completion
            onComplete?.Invoke();
        }

        private IEnumerator HandleUnitDeathCleanup(UnitBattleMetadata targetMeta, Action onUnitDied)
        {
            yield return new WaitForSeconds(_deathAnimationDurationSeconds);

            if (targetMeta != null && targetMeta.gameObject != null)
            {
                Destroy(targetMeta.gameObject);
            }

            // Notify that unit died (for compacting units list)
            onUnitDied?.Invoke();
        }

        private Vector2 ComputeDirection(Vector2Int from, Vector2Int to)
        {
            var delta = to - from;
            if (delta.x == 0 && delta.y == 0) return Vector2.zero;

            // Prioritize horizontal over vertical for 4-directional facing
            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
            {
                return delta.x > 0 ? Vector2.right : Vector2.left;
            }
            else
            {
                return delta.y > 0 ? Vector2.up : Vector2.down;
            }
        }
    }
}
