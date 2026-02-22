using System.Collections.Generic;
using TMPro;
using SevenBattles.Core.Diagnostics;
using UnityEngine;
using UnityEngine.UI;

namespace SevenBattles.Preparation
{
    [DisallowMultipleComponent]
    public sealed class UnitTooltipController : MonoBehaviour
    {
        [SerializeField] private UnitTooltipView _tooltipView;
        [SerializeField] private RectTransform _canvasRect;
        [SerializeField] private Vector2 _cursorOffset = new Vector2(16f, -20f);
        [SerializeField] private Vector2 _edgePadding = new Vector2(8f, 8f);
        [SerializeField, Min(0f)] private float _showDelaySeconds;
        [SerializeField] private bool _hideWhileDragging = true;
        [SerializeField] private bool _enableDiagnostics = true;

        private Canvas _owningCanvas;
        private GraphicRaycaster _graphicRaycaster;
        private Object _currentOwner;
        private string _currentText = string.Empty;
        private bool _loggedMissingCanvas;
        private bool _loggedMissingTooltipView;
        private bool _loggedCanvasRectOverride;
        private bool _loggedReadyState;
        private bool _loggedClippingWarning;

        private static readonly Dictionary<Canvas, UnitTooltipController> ControllerByCanvas =
            new Dictionary<Canvas, UnitTooltipController>();

        public bool IsVisible => _tooltipView != null && _tooltipView.IsVisible;
        public Object CurrentOwner => _currentOwner;
        public float CursorOffsetX
        {
            get => _cursorOffset.x;
            set => _cursorOffset.x = value;
        }

        public float CursorOffsetY
        {
            get => _cursorOffset.y;
            set => _cursorOffset.y = value;
        }
        public float ShowDelaySeconds
        {
            get => _showDelaySeconds;
            set => _showDelaySeconds = Mathf.Max(0f, value);
        }

        public static UnitTooltipController ResolveFor(Transform context)
        {
            Canvas canvas = ResolveRootCanvas(context);
            if (canvas == null)
            {
                return null;
            }

            if (ControllerByCanvas.TryGetValue(canvas, out UnitTooltipController cached) &&
                cached != null &&
                cached.gameObject != null)
            {
                cached.EnsureReady();
                return cached;
            }

            UnitTooltipController existing = canvas.GetComponentInChildren<UnitTooltipController>(true);
            if (existing != null)
            {
                existing.EnsureReady();
                ControllerByCanvas[canvas] = existing;
                return existing;
            }

            var rootObject = new GameObject("UnitTooltipController", typeof(RectTransform), typeof(UnitTooltipController));
            rootObject.layer = canvas.gameObject.layer;

            RectTransform rect = rootObject.GetComponent<RectTransform>();
            rect.SetParent(canvas.transform, false);
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            UnitTooltipController created = rootObject.GetComponent<UnitTooltipController>();
            created._canvasRect = canvas.transform as RectTransform;
            created.EnsureReady();
            ControllerByCanvas[canvas] = created;
            return created;
        }

        public void SetCursorOffset(float offsetX, float offsetY)
        {
            _cursorOffset = new Vector2(offsetX, offsetY);
        }

        public void SetShowDelaySeconds(float delaySeconds)
        {
            ShowDelaySeconds = delaySeconds;
        }

        private void Awake()
        {
            EnsureReady();
            Hide();
            RegisterControllerForCanvas();
        }

        private void OnDestroy()
        {
            if (_owningCanvas != null &&
                ControllerByCanvas.TryGetValue(_owningCanvas, out UnitTooltipController current) &&
                current == this)
            {
                ControllerByCanvas.Remove(_owningCanvas);
            }
        }

        private void LateUpdate()
        {
            if (_currentOwner == null || _tooltipView == null || !_tooltipView.IsVisible)
            {
                return;
            }

            if (_hideWhileDragging && UnitDragHandler.IsDragging)
            {
                if (_enableDiagnostics)
                {
                    SBLog.Info("UnitTooltipController: Hiding tooltip because drag is active.", this);
                }
                Hide();
                return;
            }

            UpdateTooltipPosition();
        }

        public void Show(string text, Object owner)
        {
            if (owner == null || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            EnsureReady();
            if (_tooltipView == null)
            {
                return;
            }

            if (_hideWhileDragging && UnitDragHandler.IsDragging)
            {
                if (_enableDiagnostics)
                {
                    SBLog.Info($"UnitTooltipController: Show blocked while dragging. text='{text}'.", this);
                }
                Hide(owner);
                return;
            }

            _currentOwner = owner;
            if (!string.Equals(_currentText, text, System.StringComparison.Ordinal))
            {
                _currentText = text;
                _tooltipView.SetText(text);
            }

            if (_enableDiagnostics)
            {
                SBLog.Info($"UnitTooltipController: Show text='{text}' owner='{owner.name}'.", this);
                if (_tooltipView.RootRect != null && _canvasRect != null)
                {
                    SBLog.Info(
                        $"UnitTooltipController: Root='{_tooltipView.RootRect.name}', size={_tooltipView.RootRect.rect.size}, canvasRect='{_canvasRect.name}', canvasSize={_canvasRect.rect.size}.",
                        this);
                }
            }
            if (_tooltipView.RootRect != null)
            {
                _tooltipView.RootRect.SetAsLastSibling();
            }
            _tooltipView.Show();
            UpdateTooltipPosition();
        }

        public void Hide(Object owner = null)
        {
            if (owner != null && owner != _currentOwner)
            {
                return;
            }

            _currentOwner = null;
            _currentText = string.Empty;
            if (_tooltipView != null)
            {
                _tooltipView.HideImmediate();
            }

            if (_enableDiagnostics)
            {
                string ownerName = owner != null ? owner.name : "<any>";
                SBLog.Info($"UnitTooltipController: Hide called (owner filter={ownerName}).", this);
            }
        }

        private void EnsureReady()
        {
            if (_owningCanvas == null)
            {
                _owningCanvas = ResolveRootCanvas(transform);
                if (_owningCanvas == null && !_loggedMissingCanvas)
                {
                    _loggedMissingCanvas = true;
                    SBLog.Warn("UnitTooltipController: No root canvas resolved. Tooltip cannot be positioned.", this);
                }
                else if (_owningCanvas != null)
                {
                    _loggedMissingCanvas = false;
                }
            }

            if (_canvasRect == null && _owningCanvas != null)
            {
                _canvasRect = _owningCanvas.transform as RectTransform;
            }
            else if (_owningCanvas != null)
            {
                RectTransform rootCanvasRect = _owningCanvas.transform as RectTransform;
                if (_canvasRect != rootCanvasRect)
                {
                    if (_enableDiagnostics && !_loggedCanvasRectOverride)
                    {
                        string previous = _canvasRect != null ? _canvasRect.name : "<null>";
                        SBLog.Warn(
                            $"UnitTooltipController: _canvasRect was '{previous}', expected root canvas '{rootCanvasRect.name}'. Auto-corrected to root canvas.",
                            this);
                        _loggedCanvasRectOverride = true;
                    }

                    _canvasRect = rootCanvasRect;
                }
            }

            if (_graphicRaycaster == null && _owningCanvas != null)
            {
                _graphicRaycaster = _owningCanvas.GetComponent<GraphicRaycaster>();
            }

            if (_tooltipView == null)
            {
                _tooltipView = GetComponentInChildren<UnitTooltipView>(true);
            }

            if (_tooltipView == null)
            {
                _tooltipView = CreateRuntimeTooltipView();
                if (_enableDiagnostics && _tooltipView != null)
                {
                    SBLog.Info("UnitTooltipController: Created runtime UnitTooltipView instance.", this);
                }
            }

            if (_tooltipView != null)
            {
                _loggedMissingTooltipView = false;
                _tooltipView.EnsureReferences();
                _tooltipView.ApplyDefaultStyling();
                LogReadyStateOnce();
                WarnIfTooltipMayBeClippedByParentMasks();
            }
            else if (!_loggedMissingTooltipView)
            {
                _loggedMissingTooltipView = true;
                SBLog.Warn("UnitTooltipController: Tooltip view is missing.", this);
            }
        }

        private void RegisterControllerForCanvas()
        {
            if (_owningCanvas == null)
            {
                _owningCanvas = ResolveRootCanvas(transform);
            }

            if (_owningCanvas == null)
            {
                return;
            }

            ControllerByCanvas[_owningCanvas] = this;
        }

        private UnitTooltipView CreateRuntimeTooltipView()
        {
            var tooltipObject = new GameObject(
                "UnitNameTooltip",
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(Image),
                typeof(UnitTooltipView));

            tooltipObject.layer = gameObject.layer;
            RectTransform rootRect = tooltipObject.GetComponent<RectTransform>();
            rootRect.SetParent(transform, false);
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = Vector2.zero;
            rootRect.sizeDelta = new Vector2(1f, 1f);

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.layer = tooltipObject.layer;
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.SetParent(rootRect, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            CanvasGroup canvasGroup = tooltipObject.GetComponent<CanvasGroup>();
            Image backgroundImage = tooltipObject.GetComponent<Image>();
            TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
            UnitTooltipView tooltipView = tooltipObject.GetComponent<UnitTooltipView>();
            tooltipView.SetRuntimeReferences(rootRect, canvasGroup, backgroundImage, label);
            return tooltipView;
        }

        private void UpdateTooltipPosition()
        {
            if (_tooltipView == null || _canvasRect == null || _tooltipView.RootRect == null)
            {
                return;
            }

            Camera eventCamera = ResolveEventCamera();
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _canvasRect,
                    Input.mousePosition,
                    eventCamera,
                    out Vector2 localPoint))
            {
                return;
            }

            RectTransform tooltipRect = _tooltipView.RootRect;
            Vector2 tooltipSize = tooltipRect.rect.size;
            Vector2 tooltipPivot = tooltipRect.pivot;
            Rect canvasBounds = _canvasRect.rect;
            Vector2 desired = localPoint + _cursorOffset;

            float minX = canvasBounds.xMin + (tooltipSize.x * tooltipPivot.x) + _edgePadding.x;
            float maxX = canvasBounds.xMax - (tooltipSize.x * (1f - tooltipPivot.x)) - _edgePadding.x;
            float minY = canvasBounds.yMin + (tooltipSize.y * tooltipPivot.y) + _edgePadding.y;
            float maxY = canvasBounds.yMax - (tooltipSize.y * (1f - tooltipPivot.y)) - _edgePadding.y;

            if (minX > maxX)
            {
                float midX = (canvasBounds.xMin + canvasBounds.xMax) * 0.5f;
                minX = midX;
                maxX = midX;
            }

            if (minY > maxY)
            {
                float midY = (canvasBounds.yMin + canvasBounds.yMax) * 0.5f;
                minY = midY;
                maxY = midY;
            }

            desired.x = Mathf.Clamp(desired.x, minX, maxX);
            desired.y = Mathf.Clamp(desired.y, minY, maxY);

            // Convert from clamped canvas-local coordinates to world space so tooltip placement
            // remains correct even when tooltip parent anchors/pivots differ from the canvas.
            Vector3 worldPoint = _canvasRect.TransformPoint(new Vector3(desired.x, desired.y, 0f));
            tooltipRect.position = worldPoint;
        }

        private void LogReadyStateOnce()
        {
            if (!_enableDiagnostics || _loggedReadyState)
            {
                return;
            }

            string canvasName = _owningCanvas != null ? _owningCanvas.name : "<null>";
            string canvasRectName = _canvasRect != null ? _canvasRect.name : "<null>";
            string tooltipName = _tooltipView != null ? _tooltipView.name : "<null>";
            string tooltipParent = _tooltipView != null && _tooltipView.transform.parent != null
                ? _tooltipView.transform.parent.name
                : "<null>";
            SBLog.Info(
                $"UnitTooltipController: Ready. canvas='{canvasName}', canvasRect='{canvasRectName}', tooltip='{tooltipName}', tooltipParent='{tooltipParent}'.",
                this);
            _loggedReadyState = true;
        }

        private void WarnIfTooltipMayBeClippedByParentMasks()
        {
            if (_loggedClippingWarning || _tooltipView == null || _tooltipView.RootRect == null)
            {
                return;
            }

            Transform node = _tooltipView.RootRect.parent;
            while (node != null)
            {
                Mask mask = node.GetComponent<Mask>();
                RectMask2D rectMask = node.GetComponent<RectMask2D>();
                if ((mask != null && mask.enabled) || rectMask != null)
                {
                    SBLog.Warn(
                        $"UnitTooltipController: Tooltip parent '{node.name}' has a mask component and may clip the tooltip.",
                        this);
                    _loggedClippingWarning = true;
                    return;
                }

                if (_canvasRect != null && node == _canvasRect.transform)
                {
                    break;
                }

                node = node.parent;
            }
        }

        private Camera ResolveEventCamera()
        {
            if (_owningCanvas == null)
            {
                _owningCanvas = ResolveRootCanvas(transform);
            }

            if (_owningCanvas == null || _owningCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return null;
            }

            if (_owningCanvas.worldCamera != null)
            {
                return _owningCanvas.worldCamera;
            }

            if (_graphicRaycaster == null)
            {
                _graphicRaycaster = _owningCanvas.GetComponent<GraphicRaycaster>();
            }

            if (_graphicRaycaster != null && _graphicRaycaster.eventCamera != null)
            {
                return _graphicRaycaster.eventCamera;
            }

            return Camera.main;
        }

        private static Canvas ResolveRootCanvas(Transform context)
        {
            Canvas canvas = context != null ? context.GetComponentInParent<Canvas>() : null;
            if (canvas == null)
            {
                canvas = Object.FindFirstObjectByType<Canvas>();
            }

            if (canvas == null)
            {
                return null;
            }

            return canvas.isRootCanvas ? canvas : canvas.rootCanvas;
        }
    }
}
