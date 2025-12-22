using System;
using System.Collections.Generic;
using UnityEngine;
using SevenBattles.Battle.Board;
using SevenBattles.Battle.Cursors;
using SevenBattles.Battle.Units;
using SevenBattles.Core.Battle;
using SevenBattles.Core.Units;
using SevenBattles.Battle.Combat;

namespace SevenBattles.Battle.Spells
{
    /// <summary>
    /// Service managing spell targeting, execution, VFX spawning, and effect application (damage/heal).
    /// Extracted from SimpleTurnOrderController to follow SRP.
    /// </summary>
    public class BattleSpellController : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField, Tooltip("Reference to the battle board for tile/world conversions.")]
        private WorldPerspectiveBoard _board;

        [SerializeField, Tooltip("Service for displaying visual feedback (damage numbers).")]
        private BattleVisualFeedbackService _visualFeedback;

        [SerializeField, Tooltip("Service dealing with cursor states (to reset cursors on cast).")]
        private BattleCursorController _cursorController;

        // Auto-wire dependencies on awake
        private void Awake()
        {
            if (_board == null)
            {
                _board = FindObjectOfType<WorldPerspectiveBoard>();
                if (_board == null) Debug.LogError("[BattleSpellController] WorldPerspectiveBoard not found.");
            }
            if (_visualFeedback == null)
            {
                _visualFeedback = FindObjectOfType<BattleVisualFeedbackService>();
            }
            if (_cursorController == null)
            {
                _cursorController = FindObjectOfType<BattleCursorController>();
            }
        }

        #region Targeting Logic

        public bool IsTileLegalSpellTarget<T>(
            SpellDefinition spell, 
            Vector2Int tile, 
            UnitBattleMetadata casterMeta, 
            List<T> allUnits, 
            Func<T, UnitBattleMetadata> getMetadata, 
            Func<Vector2Int, T, bool> isUnitAtTile) where T : struct
        {
            if (spell == null) return false;
            if (casterMeta == null || !casterMeta.HasTile) return false;

            int minRange = Mathf.Max(0, spell.MinCastRange);
            int maxRange = Mathf.Max(0, spell.MaxCastRange);
            if (maxRange < minRange) maxRange = minRange;

            var delta = tile - casterMeta.Tile;
            int distance = Mathf.Abs(delta.x) + Mathf.Abs(delta.y);
            if (distance < minRange || distance > maxRange) return false;

            // Find if there is a unit at the target tile
            bool hasUnit = false;
            UnitBattleMetadata targetUnitMeta = null;

            for (int i = 0; i < allUnits.Count; i++)
            {
                var u = allUnits[i];
                // Use caller-provided predicate to check presence AND validity
                // Caller must handle null checks in the predicate if needed, or we rely on getMetadata(u) for basic existence
                
                // Compatibility: If getMetadata returns null, skip. 
                var m = getMetadata(u);
                if (m == null || !m.HasTile) continue;

                if (isUnitAtTile != null && isUnitAtTile(tile, u))
                {
                    hasUnit = true;
                    targetUnitMeta = m;
                    break;
                }
                // Fallback if predicate is null (shouldn't happen given usage)
                else if (isUnitAtTile == null && m.Tile == tile)
                {
                    hasUnit = true;
                    targetUnitMeta = m;
                    break;
                }
            }

            bool isFriendly = hasUnit && targetUnitMeta != null && targetUnitMeta.IsPlayerControlled == casterMeta.IsPlayerControlled;
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

        #endregion

        #region Amount Calculation

        public bool TryGetSpellAmountPreview(SpellDefinition spell, UnitBattleMetadata casterMeta, UnitStats casterStats, out SpellAmountPreview preview)
        {
            preview = default;

            if (spell == null || casterStats == null)
            {
                return false;
            }

            if (spell.PrimaryAmountKind == SpellPrimaryAmountKind.None)
            {
                return false;
            }

            int baseAmount = Mathf.Max(0, spell.PrimaryBaseAmount);
            float scaling = spell.PrimarySpellStatScaling;
            int scaledAmount = baseAmount;

            if (!Mathf.Approximately(scaling, 0f))
            {
                scaledAmount += Mathf.RoundToInt(casterStats.Spell * scaling);
            }

            var context = new SpellAmountCalculationContext
            {
                Kind = spell.PrimaryAmountKind,
                Element = spell.PrimaryDamageElement,
                BaseAmount = baseAmount,
                Amount = scaledAmount,
                CasterSpellStat = casterStats.Spell
            };

            ApplySpellAmountModifiers(casterStats.gameObject, spell, ref context);

            context.Amount = Mathf.Max(0, context.Amount);

            preview.Kind = context.Kind;
            preview.Element = context.Element;
            preview.BaseAmount = context.BaseAmount;
            preview.ModifiedAmount = context.Amount;
            return true;
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
                    Debug.LogWarning($"BattleSpellController: Spell amount modifier threw an exception and was ignored: {ex.Message}", behaviours[i]);
                }
            }
        }

        #endregion

        #region Execution

        /// <summary>
        /// Executes the spell cast logic.
        /// </summary>
        public void TryExecuteSpellCast<T>(
            SpellDefinition spell, 
            Vector2Int targetTile, 
            T caster,
            List<T> allUnits,
            Func<T, UnitBattleMetadata> getMetadata,
            Func<T, UnitStats> getStats,
            Action onStart,
            Action<int> onAPConsumed,
            Action onStatsChanged,
            Action<UnitBattleMetadata> onUnitDied,
            Action onComplete) where T : struct
        {
            var casterMeta = getMetadata(caster);
            var casterStats = getStats(caster);

            if (casterMeta == null || casterStats == null || !casterMeta.HasTile)
            {
                return;
            }

            onStart?.Invoke();

            if (_cursorController != null)
            {
                // We don't have reference to the specific spell being casted in cursor controller here, 
                // but we can just clear spell cursor via SetSpellCursor(false, null).
                // Assuming caller handles selection state clearing if needed.
            }
            if (_board != null) _board.SetSecondaryHighlightVisible(false);

            try
            {
                // Animation
                UnitVisualUtil.TryPlayAnimation(casterMeta.gameObject, "Cast");

                // Identify target
                T targetUnit = default;
                bool hasTargetUnit = false;
                
                // Manual search for target unit
                for (int i = 0; i < allUnits.Count; i++)
                {
                    var u = allUnits[i];
                    var m = getMetadata(u);
                    if (m != null && m.HasTile && m.Tile == targetTile)
                    {
                        targetUnit = u;
                        hasTargetUnit = true;
                        break;
                    }
                }

                var targetMeta = hasTargetUnit ? getMetadata(targetUnit) : null;
                var targetStats = hasTargetUnit ? getStats(targetUnit) : null;

                Vector3 targetWorld = GetSpellTargetWorldPosition(targetTile, targetMeta);
                if (targetMeta == null && casterMeta != null)
                {
                    // Keep VFX on the same Z plane as units
                    targetWorld.z = casterMeta.transform.position.z;
                }

                SpawnSpellTargetVfx(spell, targetWorld, casterMeta, targetMeta, targetTile);
                PlaySpellCastSfx(spell, casterMeta, targetWorld);

                // Calculate Amount
                int amount = 0;
                SpellPrimaryAmountKind kind = SpellPrimaryAmountKind.None;

                if (spell != null && spell.PrimaryAmountKind != SpellPrimaryAmountKind.None)
                {
                    kind = spell.PrimaryAmountKind;
                    if (TryGetSpellAmountPreview(spell, casterMeta, casterStats, out var preview))
                    {
                        amount = Mathf.Max(0, preview.ModifiedAmount);
                        kind = preview.Kind;
                    }
                    else
                    {
                        // Fallback logic if preview failed
                         int baseAmount = Mathf.Max(0, spell.PrimaryBaseAmount);
                        float scaling = spell.PrimarySpellStatScaling;
                        int scaledAmount = baseAmount;
                        if (!Mathf.Approximately(scaling, 0f))
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

                // Handle Death
                if (targetDied && targetMeta != null)
                {
                    onUnitDied?.Invoke(targetMeta);
                }

                // Consume AP
                int cost = Mathf.Max(0, spell != null ? spell.ActionPointCost : 0);
                onAPConsumed?.Invoke(cost);

                // Notify Stats Changed (e.g. self heal)
                if (targetMeta != null && ReferenceEquals(targetMeta, casterMeta))
                {
                    onStatsChanged?.Invoke();
                }

                onComplete?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogError($"[BattleSpellController] Error executing spell cast: {e}");
                onComplete?.Invoke(); // Ensure cleanup happens
            }
        }

        #endregion

        #region Visual Helpers

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

        private void ConfigureSpellVfxRendering(GameObject instance, SpellDefinition spell, UnitBattleMetadata casterMeta, UnitBattleMetadata targetMeta, Vector2Int targetTile)
        {
            if (instance == null) return;

            string sortingLayerName = "Characters";
            int sortingOrder = 100;

            if (targetMeta != null)
            {
                sortingLayerName = !string.IsNullOrEmpty(targetMeta.SortingLayer) ? targetMeta.SortingLayer : sortingLayerName;
                sortingOrder = GetUnitSortingOrder(targetMeta, sortingOrder);
            }
            else if (casterMeta != null)
            {
                sortingLayerName = !string.IsNullOrEmpty(casterMeta.SortingLayer) ? casterMeta.SortingLayer : sortingLayerName;
                sortingOrder = GetUnitSortingOrder(casterMeta, sortingOrder);
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

        // Extracted helper logic for sorting order
        private static int GetUnitSortingOrder(UnitBattleMetadata meta, int fallbackOrder)
        {
            if (meta == null) return fallbackOrder;

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

        #endregion
    }
}
