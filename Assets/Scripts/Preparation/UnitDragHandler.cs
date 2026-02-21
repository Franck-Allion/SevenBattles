using System.Collections.Generic;
using SevenBattles.Core.Battle;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SevenBattles.Preparation
{
    [RequireComponent(typeof(UnitPortraitView))]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class UnitDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private RectTransform _dragGhostRoot;
        [Header("Cursor Feedback")]
        [SerializeField] private bool _enableCursorFeedback = true;
        [SerializeField] private Texture2D _portraitHoverCursorTexture;
        [SerializeField] private Vector2 _portraitHoverCursorHotspot = new Vector2(16f, 16f);
        [SerializeField] private Texture2D _portraitDragCursorTexture;
        [SerializeField] private Vector2 _portraitDragCursorHotspot = new Vector2(16f, 16f);
        [SerializeField] private Texture2D _defaultCursorTexture;
        [SerializeField] private Vector2 _defaultCursorHotspot = new Vector2(4f, 4f);

        private UnitPortraitView _portraitView;
        private CanvasGroup _canvasGroup;
        private Image _ghostImage;
        private bool _isDraggingThis;
        private bool _isPointerPressedThis;
        private bool _previousBlocksRaycasts;
        private float _previousAlpha;
        private static readonly List<RaycastResult> RaycastBuffer = new List<RaycastResult>(16);
        private static PreparationPopupMenuLocalizationController PopupCursorController;
        private static Texture2D PopupDefaultCursorTexture;
        private static Vector2 PopupDefaultCursorHotspot = new Vector2(4f, 4f);
        private static Texture2D PopupHoverCursorTexture;
        private static Vector2 PopupHoverCursorHotspot = new Vector2(16f, 16f);
        private static Texture2D PopupDragCursorTexture;
        private static Vector2 PopupDragCursorHotspot = new Vector2(16f, 16f);

        public static UnitSpellLoadout DraggingLoadout { get; private set; }
        public static bool IsDragging { get; private set; }
        private static bool _hasDragOriginZone;
        private static UnitDropZone.ZoneType _dragOriginZone;

        public static bool TryGetDragOriginZone(out UnitDropZone.ZoneType zoneType)
        {
            zoneType = _dragOriginZone;
            return _hasDragOriginZone;
        }

        public void SetDragGhostRoot(RectTransform dragGhostRoot)
        {
            _dragGhostRoot = dragGhostRoot;
            _ghostImage = null;
        }

        private void Awake()
        {
            _portraitView = GetComponent<UnitPortraitView>();
            _canvasGroup = GetComponent<CanvasGroup>();
        }

        private void OnDisable()
        {
            if (_isDraggingThis)
            {
                EndDrag();
            }

            _isPointerPressedThis = false;
            if (_enableCursorFeedback && !IsPointerTopmostPortrait(null))
            {
                ResolveCursorProfileIfNeeded();
                ApplyDefaultCursor();
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            UnitSpellLoadout loadout = _portraitView != null ? _portraitView.Loadout : null;
            if (loadout == null || _canvasGroup == null)
            {
                return;
            }

            UnitDropZone originZone = GetComponentInParent<UnitDropZone>();
            if (originZone != null)
            {
                _dragOriginZone = originZone.Type;
                _hasDragOriginZone = true;
            }
            else
            {
                _hasDragOriginZone = false;
            }

            _previousBlocksRaycasts = _canvasGroup.blocksRaycasts;
            _previousAlpha = _canvasGroup.alpha;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.alpha = 0.4f;

            if (_dragGhostRoot != null)
            {
                _dragGhostRoot.gameObject.SetActive(true);
                _dragGhostRoot.position = eventData.position;

                ResolveGhostImage();
                if (_ghostImage != null)
                {
                    _ghostImage.sprite = loadout.Definition != null ? loadout.Definition.Portrait : null;
                    _ghostImage.enabled = _ghostImage.sprite != null;
                }
            }

            DraggingLoadout = loadout;
            IsDragging = true;
            _isDraggingThis = true;

            if (ShouldApplyPortraitCursorFeedback())
            {
                ResolveCursorProfileIfNeeded();
                ApplyDragCursor();
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_isDraggingThis || _dragGhostRoot == null)
            {
                return;
            }

            _dragGhostRoot.position = eventData.position;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_isDraggingThis)
            {
                return;
            }

            EndDrag(eventData);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (IsDragging || !ShouldApplyPortraitCursorFeedback())
            {
                return;
            }

            ResolveCursorProfileIfNeeded();
            ApplyHoverCursor();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (IsDragging || !_enableCursorFeedback)
            {
                return;
            }

            ResolveCursorProfileIfNeeded();
            if (IsPointerTopmostButton(eventData))
            {
                return;
            }

            if (IsSquadPanelVisibleForCursorFeedback() && IsPointerTopmostPortrait(eventData))
            {
                ApplyHoverCursor();
            }
            else
            {
                ApplyDefaultCursor();
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            _isPointerPressedThis = true;
            if (!ShouldApplyPortraitCursorFeedback())
            {
                return;
            }

            ResolveCursorProfileIfNeeded();
            ApplyDragCursor();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            if (!_isPointerPressedThis)
            {
                return;
            }

            _isPointerPressedThis = false;
            if (IsDragging || !_enableCursorFeedback)
            {
                return;
            }

            ResolveCursorProfileIfNeeded();
            if (IsPointerTopmostButton(eventData))
            {
                return;
            }

            if (IsSquadPanelVisibleForCursorFeedback() && IsPointerTopmostPortrait(eventData))
            {
                ApplyHoverCursor();
            }
            else
            {
                ApplyDefaultCursor();
            }
        }

        private void EndDrag(PointerEventData eventData = null)
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.blocksRaycasts = _previousBlocksRaycasts;
                _canvasGroup.alpha = _previousAlpha;
            }

            if (_dragGhostRoot != null)
            {
                _dragGhostRoot.gameObject.SetActive(false);
            }

            DraggingLoadout = null;
            IsDragging = false;
            _isDraggingThis = false;
            _isPointerPressedThis = false;
            _hasDragOriginZone = false;

            if (_enableCursorFeedback)
            {
                ResolveCursorProfileIfNeeded();
                if (IsPointerTopmostButton(eventData))
                {
                    return;
                }

                if (IsSquadPanelVisibleForCursorFeedback() && IsPointerTopmostPortrait(eventData))
                {
                    ApplyHoverCursor();
                }
                else
                {
                    ApplyDefaultCursor();
                }
            }
        }

        private void ResolveGhostImage()
        {
            if (_ghostImage != null || _dragGhostRoot == null)
            {
                return;
            }

            _ghostImage = _dragGhostRoot.GetComponent<Image>();
            if (_ghostImage == null)
            {
                _ghostImage = _dragGhostRoot.GetComponentInChildren<Image>(true);
            }
        }

        private void ResolveCursorProfileIfNeeded()
        {
            ResolvePopupCursorProfileIfNeeded();

            if (_defaultCursorTexture == null)
            {
                _defaultCursorTexture = PopupDefaultCursorTexture;
                _defaultCursorHotspot = PopupDefaultCursorHotspot;
            }

            if (_portraitHoverCursorTexture == null)
            {
                _portraitHoverCursorTexture = PopupHoverCursorTexture;
                _portraitHoverCursorHotspot = PopupHoverCursorHotspot;
            }

            if (_portraitDragCursorTexture == null)
            {
                _portraitDragCursorTexture = PopupDragCursorTexture;
                _portraitDragCursorHotspot = PopupDragCursorHotspot;
            }
        }

        private static void ResolvePopupCursorProfileIfNeeded()
        {
            bool controllerInvalid =
                PopupCursorController == null ||
                !PopupCursorController.gameObject.scene.IsValid();

            if (controllerInvalid)
            {
                PopupCursorController = UnityEngine.Object.FindFirstObjectByType<PreparationPopupMenuLocalizationController>();
            }

            if (PopupCursorController == null)
            {
                PreparationPopupMenuLocalizationController[] popupControllers =
                    Resources.FindObjectsOfTypeAll<PreparationPopupMenuLocalizationController>();

                for (int i = 0; i < popupControllers.Length; i++)
                {
                    PreparationPopupMenuLocalizationController popup = popupControllers[i];
                    if (popup == null || !popup.gameObject.scene.IsValid())
                    {
                        continue;
                    }

                    PopupCursorController = popup;
                    break;
                }
            }

            if (PopupCursorController == null)
            {
                return;
            }

            if (!PopupCursorController.TryGetSquadPortraitCursorProfile(
                    out Texture2D defaultTexture,
                    out Vector2 defaultHotspot,
                    out Texture2D hoverTexture,
                    out Vector2 hoverHotspot,
                    out Texture2D dragTexture,
                    out Vector2 dragHotspot))
            {
                return;
            }

            PopupDefaultCursorTexture = defaultTexture;
            PopupDefaultCursorHotspot = defaultHotspot;
            PopupHoverCursorTexture = hoverTexture;
            PopupHoverCursorHotspot = hoverHotspot;
            PopupDragCursorTexture = dragTexture;
            PopupDragCursorHotspot = dragHotspot;
        }

        private bool ShouldApplyPortraitCursorFeedback()
        {
            if (!_enableCursorFeedback || !isActiveAndEnabled || _portraitView == null || _portraitView.Loadout == null)
            {
                return false;
            }

            return IsSquadPanelVisibleForCursorFeedback();
        }

        private bool IsSquadPanelVisibleForCursorFeedback()
        {
            ResolvePopupCursorProfileIfNeeded();
            if (PopupCursorController != null)
            {
                return PopupCursorController.IsSquadPanelVisible();
            }

            Transform node = transform;
            while (node != null)
            {
                if (string.Equals(node.name, "SquadPanel", System.StringComparison.Ordinal))
                {
                    return node.gameObject.activeInHierarchy;
                }

                node = node.parent;
            }

            return false;
        }

        private void ApplyHoverCursor()
        {
            if (_portraitHoverCursorTexture != null)
            {
                Cursor.SetCursor(_portraitHoverCursorTexture, _portraitHoverCursorHotspot, CursorMode.Auto);
                return;
            }

            ApplyDefaultCursor();
        }

        private void ApplyDragCursor()
        {
            if (_portraitDragCursorTexture != null)
            {
                Cursor.SetCursor(_portraitDragCursorTexture, _portraitDragCursorHotspot, CursorMode.Auto);
                return;
            }

            if (_portraitHoverCursorTexture != null)
            {
                Cursor.SetCursor(_portraitHoverCursorTexture, _portraitHoverCursorHotspot, CursorMode.Auto);
                return;
            }

            ApplyDefaultCursor();
        }

        private void ApplyDefaultCursor()
        {
            Cursor.SetCursor(_defaultCursorTexture, _defaultCursorHotspot, CursorMode.Auto);
        }

        private static bool IsPointerTopmostPortrait(PointerEventData eventData)
        {
            GameObject topTarget = GetTopmostRaycastTarget(eventData);
            return topTarget != null && topTarget.GetComponentInParent<UnitDragHandler>() != null;
        }

        private static bool IsPointerTopmostButton(PointerEventData eventData)
        {
            GameObject topTarget = GetTopmostRaycastTarget(eventData);
            return topTarget != null && topTarget.GetComponentInParent<Button>() != null;
        }

        private static GameObject GetTopmostRaycastTarget(PointerEventData eventData)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return null;
            }

            var probe = new PointerEventData(eventSystem)
            {
                position = eventData != null ? eventData.position : (Vector2)Input.mousePosition
            };

            RaycastBuffer.Clear();
            eventSystem.RaycastAll(probe, RaycastBuffer);
            for (int i = 0; i < RaycastBuffer.Count; i++)
            {
                GameObject target = RaycastBuffer[i].gameObject;
                if (target == null)
                {
                    continue;
                }

                if (!target.activeInHierarchy)
                {
                    continue;
                }

                return target;
            }

            return null;
        }
    }
}
