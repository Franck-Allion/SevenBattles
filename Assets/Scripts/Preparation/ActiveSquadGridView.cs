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
    public sealed class ActiveSquadGridView : MonoBehaviour
    {
        [SerializeField] private ScrollRect _scrollRect;
        [SerializeField] private GridLayoutGroup _gridLayout;
        [SerializeField] private RectTransform _viewport;
        [SerializeField] private UnitPortraitView _portraitPrefab;
        [SerializeField] private TMP_Text _emptyLabel;
        [SerializeField] private TMP_Text _fullLabel;
        [SerializeField] private RectTransform _dragGhostRoot;
        [Header("Portrait Layout")]
        [SerializeField, Range(0.8f, 2f)] private float _prefabScale = 1.2f;

        private UnitPortraitPool _pool;
        private UnitDropZone _dropZone;
        private int _lastCount;
        private bool _isFull;
        private bool _missingPoolLogged;
        private bool _viewportAutoFixed;
        private bool _viewportMaskAutoFixed;
        private bool _diagnosticsLogged;
        private bool _layoutBaselineCaptured;
        private bool _contentOffsetClampLogged;
        private bool _childOutsideViewportLogged;
        private bool _contentRectAutoFixed;
        private Vector2 _baseCellSize;
        private Vector2 _baseSpacing;
        private RectOffset _basePadding;

        public event Action<UnitSpellLoadout> PortraitClicked;

        private void Awake()
        {
            _dropZone = GetComponent<UnitDropZone>();
            if (_dropZone != null)
            {
                _dropZone.SetZoneType(UnitDropZone.ZoneType.ActiveSquad);
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
                    SBLog.Warn("ActiveSquadGridView: Portrait pool was not created. Ensure _portraitPrefab and content references are assigned.", this);
                    _missingPoolLogged = true;
                }
                LogMissingPoolDetails(content);
                return;
            }

            _pool.ReturnAll();

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
                view.Bind(loadout);
                view.Clicked -= HandlePortraitClicked;
                view.Clicked += HandlePortraitClicked;

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

            if (_fullLabel != null)
            {
                _fullLabel.gameObject.SetActive(_isFull);
            }

            RebuildGridLayout();
            LogRefreshDiagnosticsOnce(count, content);
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

            if (viewport.GetComponent<RectMask2D>() == null)
            {
                viewport.gameObject.AddComponent<RectMask2D>();
            }
            mask.enabled = false;

            if (!_viewportMaskAutoFixed)
            {
                SBLog.Warn("ActiveSquadGridView: Viewport used Mask with a null Image sprite; disabled Mask and added RectMask2D to prevent clipping all content.", this);
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
                SBLog.Warn("ActiveSquadGridView: Viewport RectTransform was collapsed; auto-stretched to parent ScrollView.", this);
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
                    "ActiveSquadGridView: Content RectTransform horizontal sizing was invalid; normalized anchors/sizeDelta/position to keep items inside viewport.",
                    this);
                _contentRectAutoFixed = true;
            }
        }

        public void SetIsFull(bool isFull)
        {
            _isFull = isFull;
            if (_fullLabel != null)
            {
                _fullLabel.gameObject.SetActive(_isFull);
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
                string prefabStatus = _portraitPrefab != null ? "set" : "NULL";
                string scrollStatus = _scrollRect != null ? "set" : "NULL";
                string contentStatus = content != null ? $"{content.name} (activeInHierarchy={content.gameObject.activeInHierarchy})" : "NULL";
                SBLog.Warn($"ActiveSquadGridView: Pool missing details: _portraitPrefab={prefabStatus}, _scrollRect={scrollStatus}, content={contentStatus}.", this);
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
                $"ActiveSquadGridView: Refresh rendered count={count}, contentChildren={childCount}, viewportSize={viewportSize}, contentSize={contentSize}, content='{(content != null ? content.name : "<null>")}'.",
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
            WarnIfFirstChildOutsideViewport(content, viewport);
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
                    $"ActiveSquadGridView: Content offset was outside viewport despite fitting; clamped to {anchored}.",
                    this);
                _contentOffsetClampLogged = true;
            }
        }

        private void WarnIfFirstChildOutsideViewport(RectTransform content, RectTransform viewport)
        {
            if (_childOutsideViewportLogged || content == null || viewport == null || content.childCount == 0)
            {
                return;
            }

            RectTransform firstChild = content.GetChild(0) as RectTransform;
            if (firstChild == null)
            {
                return;
            }

            Vector3[] viewportCorners = new Vector3[4];
            Vector3[] childCorners = new Vector3[4];
            viewport.GetWorldCorners(viewportCorners);
            firstChild.GetWorldCorners(childCorners);

            float viewportMinX = viewportCorners[0].x;
            float viewportMinY = viewportCorners[0].y;
            float viewportMaxX = viewportCorners[2].x;
            float viewportMaxY = viewportCorners[2].y;

            float childMinX = childCorners[0].x;
            float childMinY = childCorners[0].y;
            float childMaxX = childCorners[2].x;
            float childMaxY = childCorners[2].y;

            bool overlaps =
                childMaxX >= viewportMinX &&
                childMinX <= viewportMaxX &&
                childMaxY >= viewportMinY &&
                childMinY <= viewportMaxY;

            if (overlaps)
            {
                return;
            }

            SBLog.Warn(
                $"ActiveSquadGridView: First child is outside viewport after layout. childPos={firstChild.anchoredPosition}, childSize={firstChild.rect.size}, contentPos={content.anchoredPosition}, contentSize={content.rect.size}, viewportSize={viewport.rect.size}.",
                this);
            _childOutsideViewportLogged = true;
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
    }
}
