using SevenBattles.Core.Battle;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SevenBattles.Preparation
{
    [RequireComponent(typeof(UnitPortraitView))]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class UnitDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private RectTransform _dragGhostRoot;

        private UnitPortraitView _portraitView;
        private CanvasGroup _canvasGroup;
        private Image _ghostImage;
        private bool _isDraggingThis;
        private bool _previousBlocksRaycasts;
        private float _previousAlpha;

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

            EndDrag();
        }

        private void EndDrag()
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
            _hasDragOriginZone = false;
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
    }
}
