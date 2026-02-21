using System;
using System.Collections.Generic;
using SevenBattles.Core.Battle;
using SevenBattles.Core.Diagnostics;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenBattles.Preparation
{
    [RequireComponent(typeof(UnitDropZone))]
    public sealed class AllUnitsGridView : MonoBehaviour
    {
        [SerializeField] private ScrollRect _scrollRect;
        [SerializeField] private GridLayoutGroup _gridLayout;
        [SerializeField] private RectTransform _viewport;
        [SerializeField] private UnitPortraitView _portraitPrefab;
        [SerializeField] private TMP_Text _emptyLabel;
        [SerializeField] private RectTransform _dragGhostRoot;
        [Header("Portrait Layout")]
        [SerializeField, Range(0.8f, 2f)] private float _prefabScale = 1.2f;

        private UnitPortraitPool _pool;
        private UnitDropZone _dropZone;
        private Func<UnitSpellLoadout, string> _displayNameProvider;
        private readonly Dictionary<UnitSpellLoadout, UnitPortraitView> _viewByLoadout =
            new Dictionary<UnitSpellLoadout, UnitPortraitView>();
        private int _lastCount;
        private bool _missingPoolLogged;
        private bool _viewportAutoFixed;
        private bool _viewportMaskAutoFixed;
        private bool _diagnosticsLogged;
        private bool _layoutBaselineCaptured;
        private bool _contentOffsetClampLogged;
        private bool _contentRectAutoFixed;
        private Vector2 _baseCellSize;
        private Vector2 _baseSpacing;
        private RectOffset _basePadding;

        public event Action<UnitSpellLoadout> PortraitClicked;

        public void SetDisplayNameProvider(Func<UnitSpellLoadout, string> provider)
        {
            _displayNameProvider = provider;
        }

        private void Awake()
        {
            _dropZone = GetComponent<UnitDropZone>();
            if (_dropZone != null)
            {
                _dropZone.SetZoneType(UnitDropZone.ZoneType.AllUnits);
            }

            if (_dragGhostRoot != null)
            {
                _dragGhostRoot.gameObject.SetActive(false);
            }

            EnsureViewportRectIsUsable();
            EnsureViewportMaskIsUsable();
            EnsureContentRectIsUsable();
            CaptureGridLayoutBaselineIfNeeded();
        }

        private void OnDestroy()
        {
            if (_pool != null)
            {
                _pool.ReturnAll();
            }
        }

        public void Refresh(IReadOnlyList<UnitSpellLoadout> units)
        {
            EnsureViewportRectIsUsable();
            EnsureViewportMaskIsUsable();
            EnsureContentRectIsUsable();
            RectTransform content = ResolveContentRoot();
            EnsurePool(units != null ? units.Count : 0, content);
            if (_pool == null)
            {
                if (!_missingPoolLogged)
                {
                    SBLog.Warn("AllUnitsGridView: Portrait pool was not created. Ensure _portraitPrefab and content references are assigned.", this);
                    _missingPoolLogged = true;
                }
                LogMissingPoolDetails(content);
                _viewByLoadout.Clear();
                return;
            }

            _pool.ReturnAll();
            _viewByLoadout.Clear();

            int count = units != null ? units.Count : 0;
            for (int i = 0; i < count; i++)
            {
                UnitSpellLoadout loadout = units[i];
                if (loadout == null)
                {
                    continue;
                }

                UnitPortraitView view = _pool.Get();
                if (content != null)
                {
                    view.transform.SetParent(content, false);
                }

                view.ApplyGridCellLayout(_prefabScale);
                view.Bind(loadout, ResolveDisplayName(loadout));
                view.Clicked -= HandlePortraitClicked;
                view.Clicked += HandlePortraitClicked;
                _viewByLoadout[loadout] = view;

                UnitDragHandler dragHandler = view.GetComponent<UnitDragHandler>();
                if (dragHandler == null)
                {
                    dragHandler = view.gameObject.AddComponent<UnitDragHandler>();
                }
                dragHandler.SetDragGhostRoot(_dragGhostRoot);
            }

            _lastCount = count;
            if (_emptyLabel != null)
            {
                _emptyLabel.gameObject.SetActive(count == 0);
            }

            RebuildGridLayout();
            LogRefreshDiagnosticsOnce(count, content);
        }

        public bool RefreshPortrait(UnitSpellLoadout loadout)
        {
            if (loadout == null)
            {
                return false;
            }

            if (!_viewByLoadout.TryGetValue(loadout, out UnitPortraitView view) || view == null)
            {
                return false;
            }

            view.Bind(loadout, ResolveDisplayName(loadout));
            return true;
        }

        private void EnsureViewportMaskIsUsable()
        {
            if (_scrollRect == null || _scrollRect.viewport == null)
            {
                return;
            }

            RectTransform viewport = _scrollRect.viewport;
            Mask mask = viewport.GetComponent<Mask>();
            if (mask == null || !mask.enabled)
            {
                return;
            }

            Image image = viewport.GetComponent<Image>();
            bool hasSprite = image != null && image.sprite != null;
            if (hasSprite)
            {
                return;
            }

            // Unity UI Mask relies on the Image's geometry; when the sprite is null, the mask may clip everything.
            // RectMask2D uses the rect transform only and is safer for plain ScrollRect viewports.
            if (viewport.GetComponent<RectMask2D>() == null)
            {
                viewport.gameObject.AddComponent<RectMask2D>();
            }
            mask.enabled = false;

            if (!_viewportMaskAutoFixed)
            {
                SBLog.Warn("AllUnitsGridView: Viewport used Mask with a null Image sprite; disabled Mask and added RectMask2D to prevent clipping all content.", this);
                _viewportMaskAutoFixed = true;
            }
        }

        private void EnsureViewportRectIsUsable()
        {
            if (_scrollRect == null || _scrollRect.viewport == null)
            {
                return;
            }

            RectTransform viewport = _scrollRect.viewport;
            Rect rect = viewport.rect;
            if (rect.width > 1f && rect.height > 1f)
            {
                return;
            }

            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.anchoredPosition = Vector2.zero;
            viewport.sizeDelta = Vector2.zero;
            viewport.pivot = new Vector2(0.5f, 0.5f);
            LayoutRebuilder.ForceRebuildLayoutImmediate(viewport);

            if (!_viewportAutoFixed)
            {
                SBLog.Warn("AllUnitsGridView: Viewport RectTransform was collapsed; auto-stretched to parent ScrollView.", this);
                _viewportAutoFixed = true;
            }
        }

        private void EnsureContentRectIsUsable()
        {
            if (_scrollRect == null || _scrollRect.content == null)
            {
                return;
            }

            RectTransform content = _scrollRect.content;
            Vector2 anchorMin = content.anchorMin;
            Vector2 anchorMax = content.anchorMax;
            Vector2 sizeDelta = content.sizeDelta;
            Vector2 anchoredPosition = content.anchoredPosition;
            Vector2 pivot = content.pivot;

            bool changed = false;

            if (Mathf.Abs(anchorMin.x) > 0.0001f || Mathf.Abs(anchorMax.x - 1f) > 0.0001f)
            {
                anchorMin.x = 0f;
                anchorMax.x = 1f;
                changed = true;
            }

            if (Mathf.Abs(sizeDelta.x) > 0.01f)
            {
                sizeDelta.x = 0f;
                changed = true;
            }

            if (Mathf.Abs(anchoredPosition.x) > 0.01f)
            {
                anchoredPosition.x = 0f;
                changed = true;
            }

            if (Mathf.Abs(pivot.x - 0.5f) > 0.0001f)
            {
                pivot.x = 0.5f;
                changed = true;
            }

            if (!changed)
            {
                return;
            }

            content.anchorMin = anchorMin;
            content.anchorMax = anchorMax;
            content.sizeDelta = sizeDelta;
            content.anchoredPosition = anchoredPosition;
            content.pivot = pivot;
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);

            if (!_contentRectAutoFixed)
            {
                SBLog.Warn(
                    "AllUnitsGridView: Content RectTransform horizontal sizing was invalid; normalized anchors/sizeDelta/position to keep items inside viewport.",
                    this);
                _contentRectAutoFixed = true;
            }
        }

        private void EnsurePool(int initialSize, RectTransform content)
        {
            if (_pool != null || _portraitPrefab == null)
            {
                return;
            }

            if (content == null)
            {
                return;
            }

            _pool = new UnitPortraitPool(_portraitPrefab, content, initialSize);
        }

        private void LogMissingPoolDetails(RectTransform content)
        {
            if (_missingPoolLogged)
            {
                // Emit a single more-detailed diagnostic after the first warning.
                string prefabStatus = _portraitPrefab != null ? "set" : "NULL";
                string scrollStatus = _scrollRect != null ? "set" : "NULL";
                string contentStatus = content != null ? $"{content.name} (activeInHierarchy={content.gameObject.activeInHierarchy})" : "NULL";
                SBLog.Warn($"AllUnitsGridView: Pool missing details: _portraitPrefab={prefabStatus}, _scrollRect={scrollStatus}, content={contentStatus}.", this);
            }
        }

        private void LogRefreshDiagnosticsOnce(int count, RectTransform content)
        {
            if (_diagnosticsLogged || count <= 0)
            {
                return;
            }

            RectTransform viewport = _scrollRect != null ? _scrollRect.viewport : null;
            Vector2 viewportSize = viewport != null ? viewport.rect.size : Vector2.zero;
            Vector2 contentSize = content != null ? content.rect.size : Vector2.zero;
            int childCount = content != null ? content.childCount : -1;

            SBLog.Info(
                $"AllUnitsGridView: Refresh rendered count={count}, contentChildren={childCount}, viewportSize={viewportSize}, contentSize={contentSize}, content='{(content != null ? content.name : "<null>")}'.",
                this);

            _diagnosticsLogged = true;
        }

        private RectTransform ResolveContentRoot()
        {
            if (_scrollRect != null && _scrollRect.content != null)
            {
                return _scrollRect.content;
            }

            if (_gridLayout != null)
            {
                return _gridLayout.transform as RectTransform;
            }

            return null;
        }

        private RectTransform ResolveViewport()
        {
            if (_viewport != null)
            {
                return _viewport;
            }

            if (_scrollRect != null && _scrollRect.viewport != null)
            {
                return _scrollRect.viewport;
            }

            return ResolveContentRoot();
        }

        private void RebuildGridLayout()
        {
            if (_gridLayout == null)
            {
                return;
            }

            ApplyScaledGridLayoutMetrics();

            RectTransform viewport = ResolveViewport();
            float width = viewport != null ? viewport.rect.width : 0f;
            if (width > 0f)
            {
                float padding = _gridLayout.padding.left + _gridLayout.padding.right;
                float usableWidth = Mathf.Max(0f, width - padding);
                float cellWidth = _gridLayout.cellSize.x;
                float spacingWidth = _gridLayout.spacing.x;

                int columns = 1;
                if (cellWidth > 0f)
                {
                    columns = Mathf.Max(1, Mathf.FloorToInt((usableWidth + spacingWidth) / (cellWidth + spacingWidth)));
                }

                columns = Mathf.Min(columns, Mathf.Max(1, _lastCount));
                _gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                _gridLayout.constraintCount = columns;
            }

            RectTransform content = ResolveContentRoot();
            _gridLayout.enabled = true;
            if (content != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            }
            ClampContentOffsetIfFullyVisible(content, viewport);
            _gridLayout.enabled = false;
        }

        private void CaptureGridLayoutBaselineIfNeeded()
        {
            if (_layoutBaselineCaptured || _gridLayout == null)
            {
                return;
            }

            _baseCellSize = _gridLayout.cellSize;
            _baseSpacing = _gridLayout.spacing;
            _basePadding = new RectOffset(
                _gridLayout.padding.left,
                _gridLayout.padding.right,
                _gridLayout.padding.top,
                _gridLayout.padding.bottom);
            _layoutBaselineCaptured = true;
        }

        private void ApplyScaledGridLayoutMetrics()
        {
            if (_gridLayout == null)
            {
                return;
            }

            CaptureGridLayoutBaselineIfNeeded();
            if (!_layoutBaselineCaptured)
            {
                return;
            }

            float scale = Mathf.Clamp(_prefabScale, 0.8f, 2f);
            float spacingX = _baseSpacing.x + (_baseCellSize.x * (scale - 1f));
            float spacingY = _baseSpacing.y + (_baseCellSize.y * (scale - 1f));
            int extraPadX = Mathf.CeilToInt((_baseCellSize.x * (scale - 1f)) * 0.5f);
            int extraPadY = Mathf.CeilToInt((_baseCellSize.y * (scale - 1f)) * 0.5f);

            _gridLayout.cellSize = _baseCellSize;
            _gridLayout.spacing = new Vector2(Mathf.Max(0f, spacingX), Mathf.Max(0f, spacingY));
            _gridLayout.padding.left = _basePadding.left + extraPadX;
            _gridLayout.padding.right = _basePadding.right + extraPadX;
            _gridLayout.padding.top = _basePadding.top + extraPadY;
            _gridLayout.padding.bottom = _basePadding.bottom + extraPadY;
        }

        private void ClampContentOffsetIfFullyVisible(RectTransform content, RectTransform viewport)
        {
            if (content == null || viewport == null)
            {
                return;
            }

            Vector2 anchored = content.anchoredPosition;
            bool changed = false;

            if (content.rect.width <= viewport.rect.width + 1f && Mathf.Abs(anchored.x) > 0.01f)
            {
                anchored.x = 0f;
                changed = true;
            }

            if (content.rect.height <= viewport.rect.height + 1f && Mathf.Abs(anchored.y) > 0.01f)
            {
                anchored.y = 0f;
                changed = true;
            }

            if (!changed)
            {
                return;
            }

            content.anchoredPosition = anchored;
            if (!_contentOffsetClampLogged)
            {
                SBLog.Warn(
                    $"AllUnitsGridView: Content offset was outside viewport despite fitting; clamped to {anchored}.",
                    this);
                _contentOffsetClampLogged = true;
            }
        }

        private void HandlePortraitClicked(UnitPortraitView view)
        {
            UnitSpellLoadout loadout = view != null ? view.Loadout : null;
            if (loadout == null)
            {
                return;
            }

            PortraitClicked?.Invoke(loadout);
        }

        private string ResolveDisplayName(UnitSpellLoadout loadout)
        {
            return _displayNameProvider != null ? _displayNameProvider(loadout) : null;
        }
    }
}
