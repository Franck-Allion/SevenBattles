using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using SevenBattles.Battle.Board;
using SevenBattles.Battle.Cursors;
using SevenBattles.Battle.Units;
using SevenBattles.Battle.Tiles;
using SevenBattles.Core;
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
        
        [Header("Battlefield (optional)")]
        [SerializeField, Tooltip("Optional battlefield service used to resolve tile bonuses. If null, will be auto-found at runtime.")]
        private MonoBehaviour _battlefieldServiceBehaviour;
        private IBattlefieldService _battlefieldService;

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

            ResolveBattlefieldService();
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

            if (spell.RequiresSameRowOrColumn)
            {
                bool orthogonalOrSelf = (delta.x == 0) || (delta.y == 0);
                if (!orthogonalOrSelf) return false;

                if (spell.RequiresClearLineOfSight && delta != Vector2Int.zero)
                {
                    if (HasBlockingUnitBetween(casterMeta.Tile, tile, allUnits, getMetadata, isUnitAtTile))
                    {
                        return false;
                    }
                }
            }

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

        private static bool HasBlockingUnitBetween<T>(
            Vector2Int from,
            Vector2Int to,
            List<T> allUnits,
            Func<T, UnitBattleMetadata> getMetadata,
            Func<Vector2Int, T, bool> isUnitAtTile) where T : struct
        {
            var delta = to - from;
            if (delta == Vector2Int.zero) return false;

            Vector2Int step;
            if (delta.x != 0 && delta.y != 0)
            {
                return false;
            }

            step = delta.x != 0 ? new Vector2Int(Math.Sign(delta.x), 0) : new Vector2Int(0, Math.Sign(delta.y));
            var cursor = from + step;

            while (cursor != to)
            {
                for (int i = 0; i < allUnits.Count; i++)
                {
                    var unit = allUnits[i];
                    var meta = getMetadata(unit);
                    if (meta == null || !meta.HasTile) continue;

                    if (isUnitAtTile != null)
                    {
                        if (isUnitAtTile(cursor, unit))
                        {
                            return true;
                        }
                    }
                    else
                    {
                        if (meta.Tile == cursor)
                        {
                            return true;
                        }
                    }
                }

                cursor += step;
            }

            return false;
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

            ApplyTileSpellAmountBonus(casterMeta, ref context);
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

        private void ApplyTileSpellAmountBonus(UnitBattleMetadata casterMeta, ref SpellAmountCalculationContext context)
        {
            int bonus = GetTileSpellAmountBonus(casterMeta, context.Element);
            if (bonus != 0)
            {
                context.Amount += bonus;
            }
        }

        private int GetTileSpellAmountBonus(UnitBattleMetadata casterMeta, DamageElement element)
        {
            ResolveBattlefieldService();
            if (!BattleTileEffectRules.TryGetTileColor(_battlefieldService, casterMeta, out var color))
            {
                return 0;
            }

            return BattleTileEffectRules.GetSpellAmountBonus(color, element);
        }

        private void ResolveBattlefieldService()
        {
            if (_battlefieldService != null)
            {
                return;
            }

            if (_battlefieldServiceBehaviour != null)
            {
                _battlefieldService = _battlefieldServiceBehaviour as IBattlefieldService;
            }

            if (_battlefieldService == null)
            {
                var behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
                for (int i = 0; i < behaviours.Length; i++)
                {
                    if (behaviours[i] is IBattlefieldService service)
                    {
                        _battlefieldService = service;
                        _battlefieldServiceBehaviour = behaviours[i];
                        break;
                    }
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
                        int tileBonus = GetTileSpellAmountBonus(casterMeta, spell.PrimaryDamageElement);
                        amount = Mathf.Max(0, scaledAmount + tileBonus);
                    }
                }

                bool usesProjectile = spell != null && spell.ProjectilePrefab != null;
                if (usesProjectile)
                {
                    if (targetMeta == null || targetStats == null)
                    {
                        onComplete?.Invoke();
                        return;
                    }

                    var casterWorld = casterMeta.transform.position;
                    var travel = targetWorld - casterWorld;
                    travel.z = 0f;
                    if (travel.sqrMagnitude < 0.0001f)
                    {
                        travel = Vector3.right;
                    }

                    var direction = travel.normalized;
                    float spawnOffset = Mathf.Max(0f, spell.ProjectileSpawnOffset);
                    float casterPadding = GetApproxUnitRadius(casterMeta);
                    var spawnPos = casterWorld + direction * (spawnOffset + casterPadding);
                    spawnPos.z = casterWorld.z;

                    Quaternion rotation;
                    if (spell.ProjectileOrientation == ProjectileOrientation.FaceCamera2D)
                    {
                        rotation = Compute2DRotation(direction);
                    }
                    else
                    {
                        rotation = ComputeProjectileRotation(direction);
                    }
                    var projectileInstance = InstantiatePrefabAsGameObject(spell.ProjectilePrefab, spawnPos, rotation);
                    if (projectileInstance == null)
                    {
                        Debug.LogError($"[BattleSpellController] ProjectilePrefab is not a GameObject prefab: '{spell.ProjectilePrefab?.name}'.", this);
                        onComplete?.Invoke();
                        return;
                    }

                    TryOverrideHovlProjectileSpeed(projectileInstance, spell.ProjectileSpeedOverride);
                    TryIgnoreCollisionWithCaster(projectileInstance, casterMeta.gameObject);
                    ConfigureProjectileRendering(projectileInstance, casterMeta, targetMeta, targetTile);

                    PlaySpellCastSfx(spell, casterMeta, targetWorld);

                    int cost = Mathf.Max(0, spell.ActionPointCost);
                    onAPConsumed?.Invoke(cost);

                    var relay = projectileInstance.GetComponent<SpellProjectileImpactRelay>();
                    if (relay == null)
                    {
                        relay = projectileInstance.AddComponent<SpellProjectileImpactRelay>();
                    }

                    relay.Initialize(
                        expectedTargetRoot: targetMeta.gameObject,
                        onImpact: (validHit, impactPosition) =>
                        {
                            if (!validHit) return;

                            bool targetDied = false;

                            Vector3 impactWorld = targetMeta != null ? targetMeta.transform.position : targetWorld;
                            impactWorld.z = casterWorld.z;

                            SpawnSpellTargetVfx(spell, impactWorld, casterMeta, targetMeta, targetTile);
                            PlaySpellImpactSfx(spell, impactWorld);

                            if (kind == SpellPrimaryAmountKind.Damage && targetStats != null)
                            {
                                UnitVisualUtil.TryPlayAnimation(targetMeta.gameObject, "Hit");
                                targetStats.TakeDamage(amount);
                                targetDied = targetStats.Life <= 0;

                                if (_visualFeedback != null)
                                {
                                    _visualFeedback.ShowDamageNumber(impactWorld, amount);
                                }
                            }
                            else if (kind == SpellPrimaryAmountKind.Heal && targetStats != null)
                            {
                                targetStats.Heal(amount);
                            }

                            if (targetDied && targetMeta != null)
                            {
                                onUnitDied?.Invoke(targetMeta);
                            }

                            if (targetMeta != null && ReferenceEquals(targetMeta, casterMeta))
                            {
                                onStatsChanged?.Invoke();
                            }
                        },
                        onComplete: () =>
                        {
                            onComplete?.Invoke();
                        }
                    );

                    return;
                }

                SpawnSpellTargetVfx(spell, targetWorld, casterMeta, targetMeta, targetTile);
                PlaySpellCastSfx(spell, casterMeta, targetWorld);

                bool diedImmediate = false;

                if (kind == SpellPrimaryAmountKind.Damage && targetStats != null)
                {
                    if (targetMeta != null)
                    {
                        UnitVisualUtil.TryPlayAnimation(targetMeta.gameObject, "Hit");
                    }
                    targetStats.TakeDamage(amount);
                    diedImmediate = targetStats.Life <= 0;

                    if (_visualFeedback != null)
                    {
                        _visualFeedback.ShowDamageNumber(targetWorld, amount);
                    }
                }
                else if (kind == SpellPrimaryAmountKind.Heal && targetStats != null)
                {
                    targetStats.Heal(amount);
                }

                if (diedImmediate && targetMeta != null)
                {
                    onUnitDied?.Invoke(targetMeta);
                }

                int immediateCost = Mathf.Max(0, spell != null ? spell.ActionPointCost : 0);
                onAPConsumed?.Invoke(immediateCost);

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

            var instance = InstantiatePrefabAsGameObject(spell.TargetVfxPrefab, worldPosition, Quaternion.identity);
            if (instance == null)
            {
                Debug.LogError($"[BattleSpellController] TargetVfxPrefab is not a GameObject prefab: '{spell.TargetVfxPrefab?.name}'.", this);
                return;
            }
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

        private static void PlaySpellImpactSfx(SpellDefinition spell, Vector3 impactWorld)
        {
            if (spell == null || spell.ImpactSfxClip == null)
            {
                return;
            }

            float volume = Mathf.Clamp(spell.ImpactSfxVolume, 0f, 1.5f);
            AudioSource.PlayClipAtPoint(spell.ImpactSfxClip, impactWorld, volume);
        }

        private static void TryOverrideHovlProjectileSpeed(GameObject projectileInstance, float speed)
        {
            if (projectileInstance == null) return;
            if (speed <= 0f) return;

            Component mover = projectileInstance.GetComponent("HS_ProjectileMover2D");
            if (mover == null)
            {
                var components = projectileInstance.GetComponentsInChildren<Component>(true);
                for (int i = 0; i < components.Length; i++)
                {
                    var c = components[i];
                    if (c == null) continue;
                    if (c.GetType().Name != "HS_ProjectileMover2D") continue;
                    mover = c;
                    break;
                }
            }

            if (mover == null) return;

            try
            {
                var field = mover.GetType().GetField("speed", BindingFlags.Instance | BindingFlags.Public);
                if (field != null && field.FieldType == typeof(float))
                {
                    field.SetValue(mover, speed);
                }
            }
            catch
            {
                // Ignore third-party reflection issues.
            }
        }

        private static void TryIgnoreCollisionWithCaster(GameObject projectileInstance, GameObject casterRoot)
        {
            if (projectileInstance == null || casterRoot == null) return;

            var projectileColliders = projectileInstance.GetComponentsInChildren<Collider2D>(true);
            var casterColliders = casterRoot.GetComponentsInChildren<Collider2D>(true);
            if (projectileColliders == null || casterColliders == null) return;

            for (int i = 0; i < projectileColliders.Length; i++)
            {
                var pc = projectileColliders[i];
                if (pc == null) continue;

                for (int j = 0; j < casterColliders.Length; j++)
                {
                    var cc = casterColliders[j];
                    if (cc == null) continue;
                    Physics2D.IgnoreCollision(pc, cc, true);
                }
            }
        }

        private static Quaternion ComputeProjectileRotation(Vector3 direction)
        {
            direction.z = 0f;
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = Vector3.right;
            }

            var forward = direction.normalized;
            var up = Vector3.Cross(Vector3.forward, forward);
            if (up.sqrMagnitude < 0.0001f)
            {
                up = Vector3.up;
            }

            return Quaternion.LookRotation(forward, up);
        }

        private static Quaternion Compute2DRotation(Vector3 direction)
        {
            direction.z = 0f;
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = Vector3.right;
            }

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            return Quaternion.Euler(0f, 0f, angle);
        }

        private static GameObject InstantiatePrefabAsGameObject(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null) return null;

            // Avoid Unity's generic Instantiate<T> cast exceptions when a mismatched object reference is assigned.
            var obj = UnityEngine.Object.Instantiate((UnityEngine.Object)prefab, position, rotation);
            return obj as GameObject;
        }

        private static float GetApproxUnitRadius(UnitBattleMetadata meta)
        {
            if (meta == null) return 0f;

            float best = 0f;

            var sprite = meta.GetComponentInChildren<SpriteRenderer>();
            if (sprite != null)
            {
                var extents = sprite.bounds.extents;
                best = Mathf.Max(best, Mathf.Max(extents.x, extents.y));
            }

            var colliders = meta.GetComponentsInChildren<Collider2D>();
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] == null) continue;
                var extents = colliders[i].bounds.extents;
                best = Mathf.Max(best, Mathf.Max(extents.x, extents.y));
            }

            return best;
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

        private void ConfigureProjectileRendering(GameObject instance, UnitBattleMetadata casterMeta, UnitBattleMetadata targetMeta, Vector2Int targetTile)
        {
            if (instance == null) return;

            string sortingLayerName = "Characters";
            int sortingOrder = 100;

            if (casterMeta != null)
            {
                sortingLayerName = !string.IsNullOrEmpty(casterMeta.SortingLayer) ? casterMeta.SortingLayer : sortingLayerName;
                sortingOrder = GetUnitSortingOrder(casterMeta, sortingOrder);
            }

            if (targetMeta != null)
            {
                sortingLayerName = !string.IsNullOrEmpty(targetMeta.SortingLayer) ? targetMeta.SortingLayer : sortingLayerName;
                sortingOrder = GetUnitSortingOrder(targetMeta, sortingOrder);
            }

            sortingOrder += 5;

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
