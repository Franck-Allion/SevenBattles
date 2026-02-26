using TMPro;
using SevenBattles.Core.Diagnostics;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SevenBattles.Preparation
{
    public sealed class PreparationInventoryItemEntryView : MonoBehaviour
    {
        [SerializeField, Tooltip("Root background image (Item).")]
        private Image _backgroundImage;
        [SerializeField, Tooltip("Icon image displayed for the inventory entry.")]
        private Image _itemIconImage;
        [SerializeField, Tooltip("Quantity label shown on the inventory tile.")]
        private TMP_Text _quantityText;
        [SerializeField, Tooltip("Optional hover tooltip handler used to display item names.")]
        private PreparationInventoryItemTooltipHandler _tooltipHandler;
        private bool _raycastTargetsConfigured;

        public void Bind(Sprite icon, Color backgroundColor, int quantity, Sprite fallbackIcon, Color fallbackColor)
        {
            Bind(icon, backgroundColor, quantity, fallbackIcon, fallbackColor, string.Empty);
        }

        public void Bind(
            Sprite icon,
            Color backgroundColor,
            int quantity,
            Sprite fallbackIcon,
            Color fallbackColor,
            string tooltipName)
        {
            EnsureReferences();

            if (_backgroundImage != null)
            {
                _backgroundImage.color = backgroundColor.a <= 0f ? fallbackColor : backgroundColor;
            }

            if (_itemIconImage != null)
            {
                Sprite resolvedIcon = icon != null ? icon : fallbackIcon;
                _itemIconImage.sprite = resolvedIcon;
                _itemIconImage.enabled = resolvedIcon != null;
            }

            if (_quantityText != null)
            {
                _quantityText.text = Mathf.Max(1, quantity).ToString();
            }

            if (_tooltipHandler != null)
            {
                _tooltipHandler.SetTooltipText(tooltipName);
            }
        }

        public void ConfigureTooltipCursorOffset(bool overrideOffset, Vector2 offset)
        {
            EnsureReferences();
            if (_tooltipHandler == null)
            {
                return;
            }

            _tooltipHandler.SetTooltipCursorOffsetOverride(overrideOffset, offset);
        }

        private void Awake()
        {
            EnsureReferences();
        }

        private void EnsureReferences()
        {
            if (_backgroundImage == null)
            {
                _backgroundImage = GetComponent<Image>();
            }

            if (_itemIconImage == null)
            {
                _itemIconImage = FindImageByName("ItemIcon");
            }

            if (_quantityText == null)
            {
                _quantityText = GetComponentInChildren<TMP_Text>(true);
            }

            if (_tooltipHandler == null)
            {
                _tooltipHandler = GetComponent<PreparationInventoryItemTooltipHandler>();
            }

            if (_tooltipHandler == null)
            {
                _tooltipHandler = gameObject.AddComponent<PreparationInventoryItemTooltipHandler>();
            }

            ConfigureRaycastTargets();
        }

        private Image FindImageByName(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            Image[] images = GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                Image image = images[i];
                if (image == null)
                {
                    continue;
                }

                if (string.Equals(image.gameObject.name, objectName, System.StringComparison.Ordinal))
                {
                    return image;
                }
            }

            return null;
        }

        private void ConfigureRaycastTargets()
        {
            if (_raycastTargetsConfigured)
            {
                return;
            }

            Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                Graphic graphic = graphics[i];
                if (graphic == null)
                {
                    continue;
                }

                // Keep pointer targeting on the root tile only.
                bool isRootBackground = _backgroundImage != null && ReferenceEquals(graphic, _backgroundImage);
                graphic.raycastTarget = isRootBackground;
            }

            _raycastTargetsConfigured = true;
        }
    }

    [DisallowMultipleComponent]
    public sealed class PreparationInventoryItemTooltipHandler : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        [SerializeField] private UnitTooltipController _tooltipController;
        [SerializeField, Min(0f)] private float _hoverDelaySeconds = 1f;
        [SerializeField, Tooltip("If enabled, overrides tooltip cursor offset while this inventory item tooltip is shown.")]
        private bool _overrideTooltipCursorOffset = true;
        [SerializeField, Tooltip("Tooltip offset from mouse position in canvas-space UI units (resolution-independent with CanvasScaler).")]
        private Vector2 _tooltipCursorOffset = new Vector2(36f, -30f);
        [SerializeField] private bool _enableDiagnostics = true;

        private bool _isPointerInside;
        private bool _hasPendingShow;
        private float _pendingShowTime;
        private string _tooltipText = string.Empty;
        private bool _loggedMissingController;
        private bool _loggedMissingTooltipText;
        private bool _hasStoredPreviousCursorOffset;
        private Vector2 _previousCursorOffset;
        private static readonly List<RaycastResult> RaycastBuffer = new List<RaycastResult>(16);

        public string TooltipText => _tooltipText;

        private void OnEnable()
        {
            ResolveTooltipControllerIfNeeded();
            if (_enableDiagnostics)
            {
                SBLog.Info(
                    $"PreparationInventoryItemTooltipHandler: Enabled on '{gameObject.name}' (delay={_hoverDelaySeconds:0.###}s).",
                    this);
            }
        }

        private void OnDisable()
        {
            _isPointerInside = false;
            CancelPendingShow();
            HideOwnedTooltip();
            RestoreTooltipCursorOffsetIfNeeded();
            if (_enableDiagnostics)
            {
                SBLog.Info($"PreparationInventoryItemTooltipHandler: Disabled on '{gameObject.name}'.", this);
            }
        }

        private void Update()
        {
            if (!_hasPendingShow)
            {
                return;
            }

            if (!_isPointerInside)
            {
                CancelPendingShow();
                return;
            }

            if (Time.unscaledTime < _pendingShowTime)
            {
                return;
            }

            _hasPendingShow = false;
            if (_enableDiagnostics)
            {
                SBLog.Info($"PreparationInventoryItemTooltipHandler: Delay elapsed on '{gameObject.name}', trying to show tooltip.", this);
            }
            TryShowTooltip();
        }

        public void SetTooltipController(UnitTooltipController tooltipController)
        {
            _tooltipController = tooltipController;
        }

        public void SetHoverDelaySeconds(float delaySeconds)
        {
            _hoverDelaySeconds = Mathf.Max(0f, delaySeconds);
        }

        public void SetTooltipCursorOffsetOverride(bool overrideOffset, Vector2 offset)
        {
            _overrideTooltipCursorOffset = overrideOffset;
            _tooltipCursorOffset = offset;
        }

        public void SetTooltipText(string tooltipText)
        {
            string normalized = tooltipText ?? string.Empty;
            bool changed = !string.Equals(_tooltipText, normalized, System.StringComparison.Ordinal);
            _tooltipText = normalized;
            if (!string.IsNullOrWhiteSpace(_tooltipText))
            {
                _loggedMissingTooltipText = false;
            }

            if (_enableDiagnostics && changed)
            {
                SBLog.Info(
                    $"PreparationInventoryItemTooltipHandler: Tooltip text set on '{gameObject.name}' -> '{_tooltipText}'.",
                    this);
            }
            if (string.IsNullOrWhiteSpace(_tooltipText))
            {
                CancelPendingShow();
                HideOwnedTooltip();
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_isPointerInside)
            {
                return;
            }

            _isPointerInside = true;
            if (_enableDiagnostics)
            {
                SBLog.Info(
                    $"PreparationInventoryItemTooltipHandler: PointerEnter on '{gameObject.name}', text='{_tooltipText}'.",
                    this);
            }
            StartShowFlow();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (IsPointerStillOverThisItem(eventData))
            {
                if (_enableDiagnostics)
                {
                    SBLog.Info(
                        $"PreparationInventoryItemTooltipHandler: Ignored PointerExit on '{gameObject.name}' because pointer is still over item hierarchy.",
                        this);
                }
                return;
            }

            _isPointerInside = false;
            if (_enableDiagnostics)
            {
                SBLog.Info($"PreparationInventoryItemTooltipHandler: PointerExit on '{gameObject.name}'.", this);
            }
            CancelPendingShow();
            HideOwnedTooltip();
        }

        private void StartShowFlow()
        {
            if (string.IsNullOrWhiteSpace(_tooltipText))
            {
                if (_enableDiagnostics && !_loggedMissingTooltipText)
                {
                    _loggedMissingTooltipText = true;
                    SBLog.Warn(
                        $"PreparationInventoryItemTooltipHandler: Hover ignored on '{gameObject.name}' because tooltip text is empty.",
                        this);
                }
                return;
            }

            float delay = Mathf.Max(0f, _hoverDelaySeconds);
            if (delay <= 0f)
            {
                TryShowTooltip();
                return;
            }

            _pendingShowTime = Time.unscaledTime + delay;
            _hasPendingShow = true;
            if (_enableDiagnostics)
            {
                SBLog.Info(
                    $"PreparationInventoryItemTooltipHandler: Scheduled tooltip on '{gameObject.name}' in {delay:0.###}s.",
                    this);
            }
        }

        private void TryShowTooltip()
        {
            if (!_isPointerInside || string.IsNullOrWhiteSpace(_tooltipText))
            {
                return;
            }

            ResolveTooltipControllerIfNeeded();
            if (_tooltipController == null)
            {
                if (_enableDiagnostics)
                {
                    SBLog.Warn($"PreparationInventoryItemTooltipHandler: No tooltip controller for '{gameObject.name}'.", this);
                }
                return;
            }

            ApplyTooltipCursorOffsetIfNeeded();
            _tooltipController.Show(_tooltipText, this);
            if (_enableDiagnostics)
            {
                SBLog.Info(
                    $"PreparationInventoryItemTooltipHandler: Show tooltip '{_tooltipText}' for '{gameObject.name}'.",
                    this);
            }
        }

        private void HideOwnedTooltip()
        {
            if (_tooltipController == null)
            {
                RestoreTooltipCursorOffsetIfNeeded();
                return;
            }

            if (_enableDiagnostics)
            {
                SBLog.Info($"PreparationInventoryItemTooltipHandler: Hide tooltip for '{gameObject.name}'.", this);
            }
            RestoreTooltipCursorOffsetIfNeeded();
            _tooltipController.Hide(this);
        }

        private void CancelPendingShow()
        {
            _hasPendingShow = false;
        }

        private void ResolveTooltipControllerIfNeeded()
        {
            Canvas expectedCanvas = ResolveExpectedRootCanvas();
            bool controllerInvalid =
                _tooltipController == null ||
                _tooltipController.gameObject == null ||
                !_tooltipController.gameObject.scene.IsValid() ||
                !IsControllerOnExpectedCanvas(_tooltipController, expectedCanvas);

            if (!controllerInvalid)
            {
                return;
            }

            _tooltipController = UnitTooltipController.ResolveFor(transform);
            if (_tooltipController != null)
            {
                _loggedMissingController = false;
                if (_enableDiagnostics)
                {
                    SBLog.Info(
                        $"PreparationInventoryItemTooltipHandler: Resolved UnitTooltipController '{_tooltipController.name}' for '{gameObject.name}'.",
                        this);
                }
                return;
            }

            if (_loggedMissingController || !_enableDiagnostics)
            {
                return;
            }

            _loggedMissingController = true;
            SBLog.Warn(
                $"PreparationInventoryItemTooltipHandler: Failed to resolve UnitTooltipController for '{gameObject.name}'.",
                this);
        }

        private bool IsPointerStillOverThisItem(PointerEventData eventData)
        {
            GameObject target = eventData != null ? eventData.pointerCurrentRaycast.gameObject : null;
            if (target != null)
            {
                Transform targetTransform = target.transform;
                if (targetTransform == transform || targetTransform.IsChildOf(transform))
                {
                    return true;
                }
            }

            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return false;
            }

            var probe = new PointerEventData(eventSystem)
            {
                position = Input.mousePosition
            };

            RaycastBuffer.Clear();
            eventSystem.RaycastAll(probe, RaycastBuffer);
            for (int i = 0; i < RaycastBuffer.Count; i++)
            {
                GameObject hit = RaycastBuffer[i].gameObject;
                if (hit == null)
                {
                    continue;
                }

                Transform hitTransform = hit.transform;
                if (hitTransform == transform || hitTransform.IsChildOf(transform))
                {
                    return true;
                }
            }

            return false;
        }

        private Canvas ResolveExpectedRootCanvas()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                return null;
            }

            return canvas.isRootCanvas ? canvas : canvas.rootCanvas;
        }

        private static bool IsControllerOnExpectedCanvas(UnitTooltipController controller, Canvas expectedCanvas)
        {
            if (controller == null)
            {
                return false;
            }

            if (expectedCanvas == null)
            {
                return true;
            }

            Canvas controllerCanvas = controller.GetComponentInParent<Canvas>();
            if (controllerCanvas == null)
            {
                return false;
            }

            Canvas controllerRoot = controllerCanvas.isRootCanvas ? controllerCanvas : controllerCanvas.rootCanvas;
            return controllerRoot == expectedCanvas;
        }

        private void ApplyTooltipCursorOffsetIfNeeded()
        {
            if (_tooltipController == null || !_overrideTooltipCursorOffset)
            {
                return;
            }

            if (!_hasStoredPreviousCursorOffset)
            {
                _previousCursorOffset = new Vector2(_tooltipController.CursorOffsetX, _tooltipController.CursorOffsetY);
                _hasStoredPreviousCursorOffset = true;
            }

            _tooltipController.SetCursorOffset(_tooltipCursorOffset.x, _tooltipCursorOffset.y);
        }

        private void RestoreTooltipCursorOffsetIfNeeded()
        {
            if (!_hasStoredPreviousCursorOffset || _tooltipController == null)
            {
                return;
            }

            if (_tooltipController.CurrentOwner == this)
            {
                _tooltipController.SetCursorOffset(_previousCursorOffset.x, _previousCursorOffset.y);
            }

            _hasStoredPreviousCursorOffset = false;
        }
    }
}
