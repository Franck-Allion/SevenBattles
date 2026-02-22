using UnityEngine;
using UnityEngine.EventSystems;
using SevenBattles.Core.Diagnostics;

namespace SevenBattles.Preparation
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnitPortraitView))]
    public sealed class UnitPortraitTooltipHandler : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IBeginDragHandler,
        IEndDragHandler
    {
        [SerializeField] private UnitTooltipController _tooltipController;
        [SerializeField] private bool _enableDiagnostics = true;

        private UnitPortraitView _portraitView;
        private bool _isPointerInside;
        private bool _isDraggingThis;
        private bool _hasPendingShow;
        private float _pendingShowTime;
        private bool _loggedMissingController;

        private void Awake()
        {
            _portraitView = GetComponent<UnitPortraitView>();
        }

        private void OnEnable()
        {
            ResolveTooltipControllerIfNeeded();
        }

        private void OnDisable()
        {
            CancelPendingShow();
            _isPointerInside = false;
            _isDraggingThis = false;
            HideOwnedTooltip();
        }

        private void Update()
        {
            if (!_hasPendingShow)
            {
                return;
            }

            if (!_isPointerInside || _isDraggingThis || UnitDragHandler.IsDragging)
            {
                CancelPendingShow();
                return;
            }

            if (Time.unscaledTime < _pendingShowTime)
            {
                return;
            }

            _hasPendingShow = false;
            TryShowTooltip();
        }

        public void SetTooltipController(UnitTooltipController tooltipController)
        {
            _tooltipController = tooltipController;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isPointerInside = true;
            if (_enableDiagnostics)
            {
                string loadoutId = _portraitView != null && _portraitView.Loadout != null && _portraitView.Loadout.Definition != null
                    ? _portraitView.Loadout.Definition.Id
                    : "<null>";
                SBLog.Info(
                    $"UnitPortraitTooltipHandler: PointerEnter on '{gameObject.name}', loadout='{loadoutId}', displayName='{(_portraitView != null ? _portraitView.DisplayName : "<null-view>")}', draggingGlobal={UnitDragHandler.IsDragging}.",
                    this);
            }
            StartShowFlow();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isPointerInside = false;
            if (_enableDiagnostics)
            {
                SBLog.Info($"UnitPortraitTooltipHandler: PointerExit on '{gameObject.name}'.", this);
            }
            CancelPendingShow();
            HideOwnedTooltip();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _isDraggingThis = true;
            if (_enableDiagnostics)
            {
                SBLog.Info($"UnitPortraitTooltipHandler: BeginDrag on '{gameObject.name}'.", this);
            }
            CancelPendingShow();
            HideOwnedTooltip();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _isDraggingThis = false;
            if (_enableDiagnostics)
            {
                SBLog.Info($"UnitPortraitTooltipHandler: EndDrag on '{gameObject.name}', pointerInside={_isPointerInside}.", this);
            }

            if (_isPointerInside)
            {
                StartShowFlow();
            }
        }

        private void StartShowFlow()
        {
            ResolveTooltipControllerIfNeeded();
            float delay = _tooltipController != null ? _tooltipController.ShowDelaySeconds : 0f;
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
                    $"UnitPortraitTooltipHandler: Scheduled tooltip for '{gameObject.name}' in {delay:0.###}s.",
                    this);
            }
        }

        private void TryShowTooltip()
        {
            if (_portraitView == null || _portraitView.Loadout == null)
            {
                if (_enableDiagnostics)
                {
                    SBLog.Warn($"UnitPortraitTooltipHandler: Show aborted on '{gameObject.name}' because portrait/loadout is missing.", this);
                }
                return;
            }

            if (_isDraggingThis || UnitDragHandler.IsDragging)
            {
                if (_enableDiagnostics)
                {
                    SBLog.Info($"UnitPortraitTooltipHandler: Show aborted on '{gameObject.name}' while dragging.", this);
                }
                return;
            }

            string displayName = _portraitView.DisplayName;
            if (string.IsNullOrWhiteSpace(displayName))
            {
                if (_enableDiagnostics)
                {
                    SBLog.Warn($"UnitPortraitTooltipHandler: Show aborted on '{gameObject.name}' because DisplayName is empty.", this);
                }
                return;
            }

            ResolveTooltipControllerIfNeeded();
            if (_tooltipController == null)
            {
                if (_enableDiagnostics)
                {
                    SBLog.Warn($"UnitPortraitTooltipHandler: Show aborted on '{gameObject.name}' because no UnitTooltipController was resolved.", this);
                }
                return;
            }

            if (_enableDiagnostics)
            {
                SBLog.Info($"UnitPortraitTooltipHandler: Show tooltip '{displayName}' for '{gameObject.name}'.", this);
            }
            _tooltipController.Show(displayName, this);
        }

        private void HideOwnedTooltip()
        {
            if (_tooltipController == null)
            {
                return;
            }

            if (_enableDiagnostics)
            {
                SBLog.Info($"UnitPortraitTooltipHandler: Hide tooltip request from '{gameObject.name}'.", this);
            }
            _tooltipController.Hide(this);
        }

        private void CancelPendingShow()
        {
            _hasPendingShow = false;
        }

        private void ResolveTooltipControllerIfNeeded()
        {
            bool controllerInvalid =
                _tooltipController == null ||
                _tooltipController.gameObject == null ||
                !_tooltipController.gameObject.scene.IsValid();

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
                    SBLog.Info($"UnitPortraitTooltipHandler: Resolved UnitTooltipController '{_tooltipController.name}' for '{gameObject.name}'.", this);
                }
            }
            else if (!_loggedMissingController)
            {
                _loggedMissingController = true;
                SBLog.Warn($"UnitPortraitTooltipHandler: Failed to resolve UnitTooltipController for '{gameObject.name}'.", this);
            }
        }
    }
}
