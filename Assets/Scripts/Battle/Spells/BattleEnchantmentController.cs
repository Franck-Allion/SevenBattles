using System;
using System.Collections.Generic;
using UnityEngine;
using SevenBattles.Battle.Board;
using SevenBattles.Battle.Units;
using SevenBattles.Core;
using SevenBattles.Core.Battle;

namespace SevenBattles.Battle.Spells
{
    /// <summary>
    /// Manages battlefield enchantment placement, quad highlighting, and applying permanent effects.
    /// </summary>
    public sealed class BattleEnchantmentController : MonoBehaviour
    {
        public struct EnchantmentSnapshot
        {
            public SpellDefinition Spell;
            public int QuadIndex;
            public string CasterInstanceId;
            public string CasterUnitId;
            public bool IsPlayerControlledCaster;
        }

        private sealed class ActiveEnchantment
        {
            public SpellDefinition Spell;
            public int QuadIndex;
            public string CasterInstanceId;
            public string CasterUnitId;
            public bool IsPlayerControlledCaster;
            public GameObject Visual;
            public GameObject Vfx;
        }

        [Header("Dependencies")]
        [SerializeField, Tooltip("World board used to project quad points and visuals.")]
        private WorldPerspectiveBoard _board;

        [SerializeField, Tooltip("Battlefield service (MonoBehaviour implementing IBattlefieldService).")]
        private MonoBehaviour _battlefieldServiceBehaviour;

        [Header("Quad Highlight")]
        [SerializeField] private Material _highlightMaterial;
        [SerializeField] private Color _highlightColor = new Color(0.3f, 1f, 0.3f, 0.4f);
        [SerializeField] private string _highlightSortingLayer = "Default";
        [SerializeField] private int _highlightSortingOrder = 2;

        [Header("Enchantment Visual")]
        [SerializeField] private string _enchantmentSortingLayer = "Default";
        [SerializeField] private int _enchantmentSortingOrder = 2;
        [SerializeField] private Material _enchantmentMaterial;
        [SerializeField] private float _enchantmentZOffset;
        [SerializeField, Tooltip("Optional AudioSource used for enchantment cast SFX (uses PlayOneShot).")]
        private AudioSource _enchantmentSfxSource;

        private readonly Dictionary<int, ActiveEnchantment> _activeEnchantments = new Dictionary<int, ActiveEnchantment>();
        private EnchantmentQuadDefinition[] _quads = Array.Empty<EnchantmentQuadDefinition>();
        private IBattlefieldService _battlefieldService;
        private Camera _camera;
        private Mesh _highlightMesh;
        private MeshRenderer _highlightRenderer;

        public int QuadCount => _quads != null ? _quads.Length : 0;

        public bool HasAvailableQuads => GetAvailableQuadCount() > 0;

        private void Awake()
        {
            if (_board == null)
            {
                _board = FindObjectOfType<WorldPerspectiveBoard>();
            }

            ResolveBattlefieldService();
            ApplyBattlefieldDefinition();
        }

        private void OnEnable()
        {
            if (_camera == null) _camera = Camera.main;
            ResolveBattlefieldService();
            if (_battlefieldService != null)
            {
                _battlefieldService.BattlefieldChanged += HandleBattlefieldChanged;
            }
            ApplyBattlefieldDefinition();
        }

        private void OnDisable()
        {
            if (_battlefieldService != null)
            {
                _battlefieldService.BattlefieldChanged -= HandleBattlefieldChanged;
            }
        }

        public void ResetForBattle()
        {
            ClearActiveEnchantments();
            ClearHoverHighlight();
            ApplyBattlefieldDefinition();
        }

        public bool TryUpdateHoverHighlight(Vector2 screenPosition, out int quadIndex)
        {
            quadIndex = -1;
            if (!TryGetHoveredQuadIndex(screenPosition, out var hoveredIndex))
            {
                ClearHoverHighlight();
                return false;
            }

            if (!IsQuadAvailable(hoveredIndex))
            {
                ClearHoverHighlight();
                return false;
            }

            if (TryGetQuad(hoveredIndex, out var quad))
            {
                SetHighlightQuad(quad);
                quadIndex = hoveredIndex;
                return true;
            }

            ClearHoverHighlight();
            return false;
        }

        public void ClearHoverHighlight()
        {
            if (_highlightRenderer != null && _highlightRenderer.gameObject.activeSelf)
            {
                _highlightRenderer.gameObject.SetActive(false);
            }
        }

        public bool TryPlaceEnchantment(SpellDefinition spell, int quadIndex, UnitBattleMetadata casterMeta)
        {
            if (!IsPlacementValid(spell, quadIndex))
            {
                return false;
            }

            bool isPlayerControlledCaster = casterMeta != null && casterMeta.IsPlayerControlled;
            string casterInstanceId = casterMeta != null ? casterMeta.SaveInstanceId : null;
            string casterUnitId = casterMeta != null && casterMeta.Definition != null ? casterMeta.Definition.Id : null;

            return TryActivateEnchantment(spell, quadIndex, isPlayerControlledCaster, casterInstanceId, casterUnitId, skipVisual: false, casterMeta);
        }

        public bool TryRestoreEnchantment(
            SpellDefinition spell,
            int quadIndex,
            bool isPlayerControlledCaster,
            string casterInstanceId,
            string casterUnitId,
            bool skipVisual = false)
        {
            if (!IsPlacementValid(spell, quadIndex))
            {
                return false;
            }

            return TryActivateEnchantment(spell, quadIndex, isPlayerControlledCaster, casterInstanceId, casterUnitId, skipVisual, null);
        }

        public void CopyActiveEnchantments(List<EnchantmentSnapshot> buffer)
        {
            if (buffer == null)
            {
                return;
            }

            buffer.Clear();
            foreach (var kvp in _activeEnchantments)
            {
                var entry = kvp.Value;
                if (entry == null || entry.Spell == null)
                {
                    continue;
                }

                buffer.Add(new EnchantmentSnapshot
                {
                    Spell = entry.Spell,
                    QuadIndex = entry.QuadIndex,
                    CasterInstanceId = entry.CasterInstanceId,
                    CasterUnitId = entry.CasterUnitId,
                    IsPlayerControlledCaster = entry.IsPlayerControlledCaster
                });
            }
        }

        public EnchantmentStatBonus GetTotalBonusFor(UnitBattleMetadata meta)
        {
            if (meta == null)
            {
                return default;
            }

            var total = default(EnchantmentStatBonus);
            foreach (var kvp in _activeEnchantments)
            {
                var entry = kvp.Value;
                if (entry == null || entry.Spell == null)
                {
                    continue;
                }

                if (!DoesEnchantmentAffectUnit(entry, meta))
                {
                    continue;
                }

                total = EnchantmentStatBonus.Add(total, entry.Spell.EnchantmentStatBonus);
            }

            return total;
        }

        public int GetAvailableQuadCount()
        {
            if (_quads == null || _quads.Length == 0)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < _quads.Length; i++)
            {
                if (IsQuadAvailable(i))
                {
                    count++;
                }
            }

            return count;
        }

        public bool IsQuadAvailable(int index)
        {
            if (_quads == null || index < 0 || index >= _quads.Length)
            {
                return false;
            }

            return !_activeEnchantments.ContainsKey(index) && IsQuadValid(_quads[index]);
        }

        private bool IsPlacementValid(SpellDefinition spell, int quadIndex)
        {
            if (spell == null || !spell.IsEnchantment)
            {
                return false;
            }

            if (!IsQuadAvailable(quadIndex))
            {
                return false;
            }

            return TryGetQuad(quadIndex, out _);
        }

        private bool TryActivateEnchantment(
            SpellDefinition spell,
            int quadIndex,
            bool isPlayerControlledCaster,
            string casterInstanceId,
            string casterUnitId,
            bool skipVisual,
            UnitBattleMetadata casterMeta)
        {
            if (!TryGetQuad(quadIndex, out var quad))
            {
                return false;
            }

            var entry = new ActiveEnchantment
            {
                Spell = spell,
                QuadIndex = quadIndex,
                CasterInstanceId = casterInstanceId,
                CasterUnitId = casterUnitId,
                IsPlayerControlledCaster = isPlayerControlledCaster
            };

            if (!skipVisual)
            {
                entry.Visual = SpawnEnchantmentVisual(spell, quad);
                entry.Vfx = SpawnEnchantmentVfx(spell, quad);
                PlaySpellCastSfx(spell, casterMeta, quad);
            }

            _activeEnchantments[quadIndex] = entry;
            ApplyEnchantmentEffect(spell, isPlayerControlledCaster);
            return true;
        }

        private void ApplyEnchantmentEffect(SpellDefinition spell, bool isPlayerControlledCaster)
        {
            if (spell == null || spell.EnchantmentStatBonus.IsZero)
            {
                return;
            }

            var metas = UnityEngine.Object.FindObjectsByType<UnitBattleMetadata>(FindObjectsSortMode.None);
            for (int i = 0; i < metas.Length; i++)
            {
                var meta = metas[i];
                if (meta == null || !meta.isActiveAndEnabled)
                {
                    continue;
                }

                if (!DoesEnchantmentAffectUnit(spell.EnchantmentTargetScope, isPlayerControlledCaster, meta))
                {
                    continue;
                }

                var stats = meta.GetComponent<UnitStats>();
                if (stats == null)
                {
                    continue;
                }

                stats.ApplyStatDelta(spell.EnchantmentStatBonus);
            }
        }

        private static bool DoesEnchantmentAffectUnit(ActiveEnchantment enchantment, UnitBattleMetadata meta)
        {
            return DoesEnchantmentAffectUnit(enchantment.Spell.EnchantmentTargetScope, enchantment.IsPlayerControlledCaster, meta);
        }

        private static bool DoesEnchantmentAffectUnit(EnchantmentTargetScope scope, bool isPlayerControlledCaster, UnitBattleMetadata meta)
        {
            if (meta == null)
            {
                return false;
            }

            switch (scope)
            {
                case EnchantmentTargetScope.FriendlyUnits:
                    return meta.IsPlayerControlled == isPlayerControlledCaster;
                case EnchantmentTargetScope.EnemyUnits:
                    return meta.IsPlayerControlled != isPlayerControlledCaster;
                default:
                    return true;
            }
        }

        private GameObject SpawnEnchantmentVisual(SpellDefinition spell, EnchantmentQuadDefinition quad)
        {
            if (_board == null || spell == null)
            {
                return null;
            }

            var sprite = spell.EnchantmentBoardSprite != null ? spell.EnchantmentBoardSprite : spell.Icon;
            if (sprite == null)
            {
                return null;
            }

            var parent = _board.transform;
            var go = new GameObject($"Enchantment_{spell.name}");
            go.transform.SetParent(parent, false);

            var center = quad.Center;
            var offset = quad.Offset;
            go.transform.localPosition = new Vector3(center.x + offset.x, center.y + offset.y, _enchantmentZOffset);
            float scale = quad.Scale > 0f ? quad.Scale : 1f;
            go.transform.localScale = new Vector3(scale, scale, 1f);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingLayerName = _enchantmentSortingLayer;
            renderer.sortingOrder = _enchantmentSortingOrder;
            if (_enchantmentMaterial != null)
            {
                renderer.sharedMaterial = _enchantmentMaterial;
            }

            PrepareFadeIn(renderer, spell);

            return go;
        }

        private GameObject SpawnEnchantmentVfx(SpellDefinition spell, EnchantmentQuadDefinition quad)
        {
            if (_board == null || spell == null || spell.EnchantmentVfxPrefab == null)
            {
                return null;
            }

            var center = quad.Center;
            var offset = quad.Offset;
            var vfxOffset = spell.EnchantmentVfxOffset;
            var position = _board.transform.TransformPoint(new Vector3(center.x + offset.x + vfxOffset.x, center.y + offset.y + vfxOffset.y, 0f));
            var instance = InstantiatePrefabAsGameObject(spell.EnchantmentVfxPrefab, position, Quaternion.identity);
            if (instance == null)
            {
                Debug.LogWarning($"[BattleEnchantmentController] EnchantmentVfxPrefab is not a GameObject prefab: '{spell.EnchantmentVfxPrefab?.name}'.", this);
                return null;
            }

            if (!Mathf.Approximately(spell.EnchantmentVfxScaleMultiplier, 1f) && spell.EnchantmentVfxScaleMultiplier > 0f)
            {
                instance.transform.localScale = instance.transform.localScale * spell.EnchantmentVfxScaleMultiplier;
            }

            ApplyVfxSorting(instance, _enchantmentSortingLayer, _enchantmentSortingOrder + 1);

            float lifetime = Mathf.Max(0f, spell.EnchantmentVfxLifetimeSeconds);
            float fadeDuration = Mathf.Max(0f, spell.EnchantmentVfxFadeOutDurationSeconds);
            StartVfxFadeOut(instance, lifetime, fadeDuration);
            if (lifetime > 0f)
            {
                Destroy(instance, Mathf.Max(lifetime, fadeDuration));
            }

            return instance;
        }

        private void PrepareFadeIn(SpriteRenderer renderer, SpellDefinition spell)
        {
            if (renderer == null || spell == null)
            {
                return;
            }

            float duration = Mathf.Max(0f, spell.EnchantmentAppearDurationSeconds);
            float delay = Mathf.Max(0f, spell.EnchantmentAppearDelaySeconds);

            if (duration <= 0f)
            {
                return;
            }

            var color = renderer.color;
            renderer.color = new Color(color.r, color.g, color.b, 0f);
            StartCoroutine(FadeInSprite(renderer, delay, duration));
        }

        private System.Collections.IEnumerator FadeInSprite(SpriteRenderer renderer, float delay, float duration)
        {
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            if (renderer == null)
            {
                yield break;
            }

            float elapsed = 0f;
            var color = renderer.color;
            while (elapsed < duration)
            {
                if (renderer == null)
                {
                    yield break;
                }

                elapsed += Time.deltaTime;
                float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
                renderer.color = new Color(color.r, color.g, color.b, t);
                yield return null;
            }

            if (renderer != null)
            {
                renderer.color = new Color(color.r, color.g, color.b, 1f);
            }
        }

        private void SetHighlightQuad(EnchantmentQuadDefinition quad)
        {
            EnsureHighlightObjects();
            if (_highlightRenderer == null || _highlightMesh == null)
            {
                return;
            }

            UpdateHighlightMesh(quad.TopLeft, quad.TopRight, quad.BottomRight, quad.BottomLeft);
            _highlightRenderer.gameObject.SetActive(true);
        }

        private void EnsureHighlightObjects()
        {
            if (_highlightMaterial == null)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    _highlightMaterial = new Material(shader);
                }
            }

            if (_highlightMaterial == null || _highlightRenderer != null || _board == null)
            {
                return;
            }

            var go = new GameObject("EnchantmentQuadHighlight");
            go.transform.SetParent(_board.transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            var mf = go.AddComponent<MeshFilter>();
            _highlightRenderer = go.AddComponent<MeshRenderer>();
            _highlightRenderer.material = new Material(_highlightMaterial);
            _highlightRenderer.sortingLayerName = _highlightSortingLayer;
            _highlightRenderer.sortingOrder = _highlightSortingOrder;

            _highlightMesh = new Mesh { name = "EnchantmentQuadHighlightMesh" };
            _highlightMesh.MarkDynamic();
            mf.sharedMesh = _highlightMesh;

            if (_highlightRenderer.material != null && _highlightRenderer.material.HasProperty("_Color"))
            {
                _highlightRenderer.material.color = _highlightColor;
            }

            UpdateHighlightMesh(Vector2.zero, Vector2.right * 0.1f, new Vector2(0.1f, -0.1f), Vector2.down * 0.1f);
            _highlightRenderer.gameObject.SetActive(false);
        }

        private void UpdateHighlightMesh(Vector2 tl, Vector2 tr, Vector2 br, Vector2 bl)
        {
            if (_highlightMesh == null)
            {
                return;
            }

            var v = new Vector3[4]
            {
                new Vector3(tl.x, tl.y, 0f),
                new Vector3(tr.x, tr.y, 0f),
                new Vector3(br.x, br.y, 0f),
                new Vector3(bl.x, bl.y, 0f)
            };
            var uv = new Vector2[4] { Vector2.up, Vector2.one, Vector2.right, Vector2.zero };
            var t = new int[6] { 0, 1, 2, 2, 3, 0 };
            _highlightMesh.Clear();
            _highlightMesh.vertices = v;
            _highlightMesh.uv = uv;
            _highlightMesh.triangles = t;
            _highlightMesh.RecalculateBounds();
        }

        private bool TryGetHoveredQuadIndex(Vector2 screenPosition, out int quadIndex)
        {
            quadIndex = -1;
            if (_board == null)
            {
                return false;
            }

            if (!TryGetLocalPoint(screenPosition, out var local))
            {
                return false;
            }

            if (_quads == null || _quads.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < _quads.Length; i++)
            {
                var quad = _quads[i];
                if (PointInQuad(local, quad.TopLeft, quad.TopRight, quad.BottomRight, quad.BottomLeft))
                {
                    quadIndex = i;
                    return true;
                }
            }

            return false;
        }

        private bool TryGetLocalPoint(Vector2 screenPosition, out Vector2 local)
        {
            local = default;
            if (_board == null)
            {
                return false;
            }

            if (_camera == null)
            {
                _camera = Camera.main;
            }

            if (_camera == null)
            {
                return false;
            }

            var ray = _camera.ScreenPointToRay(screenPosition);
            var plane = new Plane(_board.transform.forward, _board.transform.position);
            if (!plane.Raycast(ray, out var dist))
            {
                return false;
            }

            var world = ray.origin + ray.direction * dist;
            var local3 = _board.transform.InverseTransformPoint(world);
            local = new Vector2(local3.x, local3.y);
            return true;
        }

        private bool TryGetQuad(int index, out EnchantmentQuadDefinition quad)
        {
            if (_quads == null || index < 0 || index >= _quads.Length)
            {
                quad = default;
                return false;
            }

            quad = _quads[index];
            return IsQuadValid(quad);
        }

        private static bool IsQuadValid(EnchantmentQuadDefinition quad)
        {
            return AllFinite(quad.TopLeft) &&
                   AllFinite(quad.TopRight) &&
                   AllFinite(quad.BottomRight) &&
                   AllFinite(quad.BottomLeft);
        }

        private static bool AllFinite(Vector2 v)
        {
            return !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsInfinity(v.x) && !float.IsInfinity(v.y);
        }

        private static bool PointInQuad(Vector2 p, Vector2 tl, Vector2 tr, Vector2 br, Vector2 bl)
        {
            return PointInTriangle(p, tl, tr, br) || PointInTriangle(p, br, bl, tl);
        }

        private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Sign(p, a, b);
            float d2 = Sign(p, b, c);
            float d3 = Sign(p, c, a);
            bool hasNeg = (d1 < 0f) || (d2 < 0f) || (d3 < 0f);
            bool hasPos = (d1 > 0f) || (d2 > 0f) || (d3 > 0f);
            return !(hasNeg && hasPos);
        }

        private static float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
        {
            return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
        }

        private void HandleBattlefieldChanged(BattlefieldDefinition battlefield)
        {
            ClearActiveEnchantments();
            ClearHoverHighlight();
            ApplyBattlefieldDefinition();
        }

        private void ApplyBattlefieldDefinition()
        {
            var battlefield = _battlefieldService != null ? _battlefieldService.Current : null;
            _quads = battlefield != null ? battlefield.EnchantmentQuads : Array.Empty<EnchantmentQuadDefinition>();
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

        private void ClearActiveEnchantments()
        {
            foreach (var kvp in _activeEnchantments)
            {
                var entry = kvp.Value;
                if (entry != null && entry.Visual != null)
                {
                    Destroy(entry.Visual);
                }

                if (entry != null && entry.Vfx != null)
                {
                    Destroy(entry.Vfx);
                }
            }

            _activeEnchantments.Clear();
        }

        private static void ApplyVfxSorting(GameObject instance, string sortingLayerName, int sortingOrder)
        {
            if (instance == null)
            {
                return;
            }

            var group = instance.GetComponentInChildren<UnityEngine.Rendering.SortingGroup>(true);
            if (group != null)
            {
                group.sortingLayerName = sortingLayerName;
                group.sortingOrder = sortingOrder;
            }

            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                renderers[i].sortingLayerName = sortingLayerName;
                renderers[i].sortingOrder = sortingOrder;
            }
        }

        private static GameObject InstantiatePrefabAsGameObject(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null) return null;

            var obj = UnityEngine.Object.Instantiate((UnityEngine.Object)prefab, position, rotation);
            return obj as GameObject;
        }

        private void PlaySpellCastSfx(SpellDefinition spell, UnitBattleMetadata casterMeta, EnchantmentQuadDefinition quad)
        {
            if (spell == null || spell.CastSfxClip == null)
            {
                return;
            }

            float volume = Mathf.Clamp(spell.CastSfxVolume, 0f, 1.5f);
            Vector3 targetWorld;
            if (_board != null)
            {
                var center = quad.Center;
                var offset = quad.Offset;
                targetWorld = _board.transform.TransformPoint(new Vector3(center.x + offset.x, center.y + offset.y, 0f));
            }
            else if (casterMeta != null)
            {
                targetWorld = casterMeta.transform.position;
            }
            else
            {
                targetWorld = Vector3.zero;
            }

            if (casterMeta != null)
            {
                targetWorld.z = casterMeta.transform.position.z;
            }

            Vector3 pos = spell.CastSfxAtTarget ? targetWorld : (casterMeta != null ? casterMeta.transform.position : targetWorld);
            var source = EnsureEnchantmentSfxSource();
            if (source != null)
            {
                source.transform.position = pos;
                source.PlayOneShot(spell.CastSfxClip, volume);
                return;
            }

            AudioSource.PlayClipAtPoint(spell.CastSfxClip, pos, volume);
        }

        private AudioSource EnsureEnchantmentSfxSource()
        {
            if (_enchantmentSfxSource != null)
            {
                return _enchantmentSfxSource;
            }

            var source = GetComponent<AudioSource>();
            if (source == null)
            {
                source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
            }

            _enchantmentSfxSource = source;
            return source;
        }

        private void StartVfxFadeOut(GameObject instance, float lifetime, float fadeDuration)
        {
            if (instance == null || lifetime <= 0f || fadeDuration <= 0f)
            {
                return;
            }

            float delay = Mathf.Max(0f, lifetime - fadeDuration);
            StartCoroutine(FadeOutVfx(instance, delay, fadeDuration));
        }

        private System.Collections.IEnumerator FadeOutVfx(GameObject instance, float delay, float duration)
        {
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            if (instance == null)
            {
                yield break;
            }

            var spriteRenderers = instance.GetComponentsInChildren<SpriteRenderer>(true);
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            var rendererData = BuildVfxRendererData(renderers);
            var spriteData = BuildVfxSpriteData(spriteRenderers);
            var propertyBlock = new MaterialPropertyBlock();

            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (instance == null)
                {
                    yield break;
                }

                elapsed += Time.deltaTime;
                float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
                float eased = t * t * (3f - 2f * t);
                float alpha = 1f - eased;

                ApplyVfxSpriteAlpha(spriteData, alpha);
                ApplyVfxRendererAlpha(rendererData, alpha, propertyBlock);

                yield return null;
            }

            ApplyVfxSpriteAlpha(spriteData, 0f);
            ApplyVfxRendererAlpha(rendererData, 0f, propertyBlock);
        }


        private static List<VfxSpriteFadeData> BuildVfxSpriteData(SpriteRenderer[] renderers)
        {
            var data = new List<VfxSpriteFadeData>();
            if (renderers == null)
            {
                return data;
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                data.Add(new VfxSpriteFadeData
                {
                    Renderer = renderer,
                    BaseColor = renderer.color
                });
            }

            return data;
        }

        private static List<VfxRendererFadeData> BuildVfxRendererData(Renderer[] renderers)
        {
            var data = new List<VfxRendererFadeData>();
            if (renderers == null)
            {
                return data;
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || renderer is SpriteRenderer)
                {
                    continue;
                }

                if (!TryGetRendererFadeColor(renderer, out var color, out var propertyId))
                {
                    continue;
                }

                data.Add(new VfxRendererFadeData
                {
                    Renderer = renderer,
                    BaseColor = color,
                    ColorPropertyId = propertyId
                });
            }

            return data;
        }

        private static bool TryGetRendererFadeColor(Renderer renderer, out Color color, out int propertyId)
        {
            color = default;
            propertyId = 0;
            if (renderer == null)
            {
                return false;
            }

            var material = renderer.sharedMaterial;
            if (material == null)
            {
                return false;
            }

            return TryGetColorProperty(material, "_Color", out color, out propertyId) ||
                   TryGetColorProperty(material, "_BaseColor", out color, out propertyId) ||
                   TryGetColorProperty(material, "_TintColor", out color, out propertyId) ||
                   TryGetColorProperty(material, "_Tint", out color, out propertyId);
        }

        private static bool TryGetColorProperty(Material material, string propertyName, out Color color, out int propertyId)
        {
            color = default;
            propertyId = 0;
            if (material == null || string.IsNullOrEmpty(propertyName) || !material.HasProperty(propertyName))
            {
                return false;
            }

            propertyId = Shader.PropertyToID(propertyName);
            color = material.GetColor(propertyId);
            return true;
        }

        private static void ApplyVfxSpriteAlpha(List<VfxSpriteFadeData> sprites, float alpha)
        {
            if (sprites == null)
            {
                return;
            }

            for (int i = 0; i < sprites.Count; i++)
            {
                var entry = sprites[i];
                if (entry.Renderer == null)
                {
                    continue;
                }

                var baseColor = entry.BaseColor;
                entry.Renderer.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * alpha);
            }
        }

        private static void ApplyVfxRendererAlpha(List<VfxRendererFadeData> renderers, float alpha, MaterialPropertyBlock block)
        {
            if (renderers == null || block == null)
            {
                return;
            }

            for (int i = 0; i < renderers.Count; i++)
            {
                var entry = renderers[i];
                if (entry.Renderer == null)
                {
                    continue;
                }

                var baseColor = entry.BaseColor;
                entry.Renderer.GetPropertyBlock(block);
                block.SetColor(entry.ColorPropertyId, new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * alpha));
                entry.Renderer.SetPropertyBlock(block);
            }
        }

        private struct VfxSpriteFadeData
        {
            public SpriteRenderer Renderer;
            public Color BaseColor;
        }

        private struct VfxRendererFadeData
        {
            public Renderer Renderer;
            public Color BaseColor;
            public int ColorPropertyId;
        }
    }
}
