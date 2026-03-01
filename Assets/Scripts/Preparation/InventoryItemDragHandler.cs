using SevenBattles.Core.Items;
using SevenBattles.Core.Diagnostics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SevenBattles.Preparation
{
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class InventoryItemDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private enum DragEndReason
        {
            Auto,
            Success,
            Invalid,
            Cancelled
        }

        [SerializeField] private RectTransform _dragGhostRoot;
        [Header("Cursor Feedback")]
        [SerializeField] private bool _enableCursorFeedback = true;
        [SerializeField] private Texture2D _hoverCursorTexture;
        [SerializeField] private Vector2 _hoverCursorHotspot = new Vector2(16f, 16f);
        [SerializeField] private Texture2D _dragCursorTexture;
        [SerializeField] private Vector2 _dragCursorHotspot = new Vector2(16f, 16f);
        [SerializeField] private Texture2D _defaultCursorTexture;
        [SerializeField] private Vector2 _defaultCursorHotspot = new Vector2(4f, 4f);
        [SerializeField, Range(1f, 1.25f)] private float _dragGhostScale = 1.1f;
        [SerializeField, Tooltip("If enabled, logs drag gating diagnostics for inventory entries.")]
        private bool _enableDiagnostics;
        [Header("Drag Polish")]
        [SerializeField, Range(1f, 1.2f)] private float _dragStartScale = 1.035f;
        [SerializeField, Min(0.1f)] private float _scaleReturnSpeed = 12f;
        [SerializeField, Min(0f)] private float _invalidDropShakeDuration = 0.14f;
        [SerializeField, Min(0f)] private float _invalidDropShakeMagnitude = 7f;
        [Header("Optional Sound Hooks")]
        [SerializeField] private AudioSource _audio;
        [SerializeField] private AudioClip _dragStartClip;
        [SerializeField] private AudioClip _dropSuccessClip;
        [SerializeField] private AudioClip _dropInvalidClip;
        [SerializeField, Range(0f, 1f)] private float _feedbackVolume = 1f;

        private CanvasGroup _canvasGroup;
        private RectTransform _rectTransform;
        private Image _ghostImage;
        private bool _isDraggingThis;
        private bool _previousBlocksRaycasts;
        private float _previousAlpha;
        private InventoryEntry _boundEntry;
        private EquipmentDefinition _boundEquipmentDefinition;
        private ItemDefinition _boundItemDefinition;
        private PreparationInventoryItemEntryView _entryView;
        private bool _dropAcceptedThisDrag;
        private Vector3 _baseLocalScale = Vector3.one;
        private bool _baseAnchoredPositionCaptured;
        private Vector2 _baseAnchoredPosition;
        private bool _isShaking;
        private float _shakeRemaining;
        private Vector3 _dragGhostBaseScale = Vector3.one;
        private static InventoryItemDragHandler _activeDragHandler;
        private static PreparationPopupMenuLocalizationController PopupCursorController;
        private static Texture2D PopupDefaultCursorTexture;
        private static Vector2 PopupDefaultCursorHotspot = new Vector2(4f, 4f);
        private static Texture2D PopupHoverCursorTexture;
        private static Vector2 PopupHoverCursorHotspot = new Vector2(16f, 16f);
        private static Texture2D PopupDragCursorTexture;
        private static Vector2 PopupDragCursorHotspot = new Vector2(16f, 16f);

        public static InventoryEntry DraggingEntry { get; private set; }
        public static EquipmentDefinition DraggingEquipmentDef { get; private set; }
        public static ItemDefinition DraggingItemDef { get; private set; }
        // Backward-compatible alias for existing call sites/tests.
        public static EquipmentDefinition DraggingEquipmentDefinition => DraggingEquipmentDef;
        public static ItemDefinition DraggingItemDefinition => DraggingItemDef;
        public static bool IsDraggingItem { get; private set; }
        public static bool AnyDragActive => IsDraggingItem || EquipmentDropSlotView.IsDraggingEquippedItem || UnitDragHandler.IsDragging;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            DraggingEntry = null;
            DraggingEquipmentDef = null;
            DraggingItemDef = null;
            IsDraggingItem = false;
            _activeDragHandler = null;
            PopupCursorController = null;
            PopupDefaultCursorTexture = null;
            PopupHoverCursorTexture = null;
            PopupDragCursorTexture = null;
        }

        public void ConfigureDragPayload(InventoryEntry entry, EquipmentDefinition equipmentDefinition, ItemDefinition itemDefinition)
        {
            _boundEntry = entry;
            _boundEquipmentDefinition = equipmentDefinition;
            _boundItemDefinition = itemDefinition;
        }

        public void ConfigureDragPayload(InventoryEntry entry, EquipmentDefinition equipmentDefinition)
        {
            ConfigureDragPayload(entry, equipmentDefinition, null);
        }

        public void NotifyDropAccepted()
        {
            if (!_isDraggingThis)
            {
                return;
            }

            _dropAcceptedThisDrag = true;
        }

        public static void CancelActiveDrag()
        {
            if (_activeDragHandler == null)
            {
                return;
            }

            _activeDragHandler.EndDrag(DragEndReason.Cancelled);
        }

        public void SetDragGhostRoot(RectTransform dragGhostRoot)
        {
            _dragGhostRoot = dragGhostRoot;
            _ghostImage = null;
        }

        public void Initialize(Transform dragGhostRoot)
        {
            SetDragGhostRoot(dragGhostRoot as RectTransform);
        }

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            _rectTransform = transform as RectTransform;
            _entryView = GetComponent<PreparationInventoryItemEntryView>();
            _baseLocalScale = transform.localScale;
            CaptureBaseAnchoredPositionIfNeeded();
        }

        private void OnEnable()
        {
            _baseLocalScale = transform.localScale;
            CaptureBaseAnchoredPositionIfNeeded();
        }

        private void Update()
        {
            AnimateScaleBackToBase();
            AnimateInvalidDropShake();
        }

        private void OnDisable()
        {
            bool isThisActiveDrag =
                _isDraggingThis ||
                (IsDraggingItem && ReferenceEquals(_activeDragHandler, this));
            if (isThisActiveDrag)
            {
                EndDrag(DragEndReason.Cancelled);
            }

            if (_isShaking)
            {
                StopInvalidShake();
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_canvasGroup == null || IsDraggingItem || EquipmentDropSlotView.IsDraggingEquippedItem || UnitDragHandler.IsDragging)
            {
                if (_enableDiagnostics)
                {
                    SBLog.Info(
                        $"InventoryItemDragHandler: Drag blocked on '{gameObject.name}' (canvas={(_canvasGroup != null ? "ok" : "null")}, itemDragging={IsDraggingItem}, equipmentDragging={EquipmentDropSlotView.IsDraggingEquippedItem}, unitDragging={UnitDragHandler.IsDragging}).",
                        this);
                }
                return;
            }

            if (_activeDragHandler != null && _activeDragHandler != this)
            {
                if (_enableDiagnostics)
                {
                    SBLog.Info($"InventoryItemDragHandler: Drag ignored on '{gameObject.name}' because another item drag is active.", this);
                }
                return;
            }

            ResolvePayloadFromViewIfNeeded();
            if (!TryResolveDragPayload(out Sprite dragIcon, out EquipmentDefinition dragEquipment, out ItemDefinition dragItem))
            {
                if (_enableDiagnostics)
                {
                    string kind = _boundEntry != null ? _boundEntry.Kind.ToString() : "<null>";
                    string entryDef = _boundEntry != null ? _boundEntry.DefinitionId : "<null>";
                    string equipmentDef = _boundEquipmentDefinition != null ? _boundEquipmentDefinition.Id : "<null>";
                    string itemDef = _boundItemDefinition != null ? _boundItemDefinition.Id : "<null>";
                    bool isConsumable = _boundItemDefinition != null && _boundItemDefinition.IsConsumable;
                    SBLog.Warn(
                        $"InventoryItemDragHandler: Drag payload invalid on '{gameObject.name}' (kind={kind}, entryId={entryDef}, equipmentDef={equipmentDef}, itemDef={itemDef}, itemConsumable={isConsumable}).",
                        this);
                }
                return;
            }

            if (_isShaking)
            {
                StopInvalidShake();
            }

            RefreshBaseAnchoredPositionFromCurrentLayout();

            _dropAcceptedThisDrag = false;
            _previousBlocksRaycasts = _canvasGroup.blocksRaycasts;
            _previousAlpha = _canvasGroup.alpha;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.alpha = 0.4f;

            if (_dragGhostRoot != null)
            {
                _dragGhostBaseScale = _dragGhostRoot.localScale;
                _dragGhostRoot.gameObject.SetActive(true);
                _dragGhostRoot.localScale = Vector3.one * Mathf.Max(1f, _dragGhostScale);
                SetGhostScreenPosition(eventData);

                ResolveGhostImage();
                if (_ghostImage != null)
                {
                    _ghostImage.sprite = dragIcon;
                    _ghostImage.enabled = _ghostImage.sprite != null;
                    if (_enableDiagnostics && dragIcon == null)
                    {
                        string payloadId = dragEquipment != null ? dragEquipment.Id : dragItem != null ? dragItem.Id : "<null>";
                        SBLog.Warn(
                            $"InventoryItemDragHandler: Ghost icon is null for dragged payload '{payloadId}' on '{gameObject.name}'. Ghost will be hidden.",
                            this);
                    }
                }
                else if (_enableDiagnostics)
                {
                    SBLog.Warn(
                        $"InventoryItemDragHandler: Drag ghost image component was not found on ghost root '{_dragGhostRoot.name}'.",
                        this);
                }
            }
            else if (_enableDiagnostics)
            {
                SBLog.Warn($"InventoryItemDragHandler: Drag ghost root is null on '{gameObject.name}'.", this);
            }

            DraggingEntry = _boundEntry;
            DraggingEquipmentDef = dragEquipment;
            DraggingItemDef = dragItem;
            IsDraggingItem = true;
            _isDraggingThis = true;
            _activeDragHandler = this;
            if (_enableDiagnostics)
            {
                string payloadType = dragEquipment != null ? "Equipment" : "Item";
                string payloadId = dragEquipment != null ? dragEquipment.Id : dragItem != null ? dragItem.Id : "<null>";
                SBLog.Info($"InventoryItemDragHandler: Drag started ({payloadType}:{payloadId}) on '{gameObject.name}'.", this);
            }

            ApplyDragStartScale();
            ResolveCursorProfileIfNeeded();
            ApplyDragCursor();
            PlayFeedbackClip(_dragStartClip);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_isDraggingThis || _dragGhostRoot == null || eventData == null)
            {
                return;
            }

            SetGhostScreenPosition(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_isDraggingThis)
            {
                return;
            }

            EndDrag(DragEndReason.Auto);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!_enableCursorFeedback || AnyDragActive)
            {
                return;
            }

            if (!CanStartDragFromCurrentPayload())
            {
                return;
            }

            ResolveCursorProfileIfNeeded();
            ApplyHoverCursor();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!_enableCursorFeedback || AnyDragActive)
            {
                return;
            }

            ResolveCursorProfileIfNeeded();
            ApplyDefaultCursor();
        }

        private void EndDrag(DragEndReason reason)
        {
            DragEndReason resolvedReason = reason;
            if (resolvedReason == DragEndReason.Auto)
            {
                resolvedReason = _dropAcceptedThisDrag ? DragEndReason.Success : DragEndReason.Invalid;
            }

            if (_canvasGroup != null)
            {
                _canvasGroup.blocksRaycasts = _previousBlocksRaycasts;
                _canvasGroup.alpha = _previousAlpha;
            }

            if (_dragGhostRoot != null)
            {
                _dragGhostRoot.localScale = _dragGhostBaseScale;
                _dragGhostRoot.gameObject.SetActive(false);
            }

            DraggingEntry = null;
            DraggingEquipmentDef = null;
            DraggingItemDef = null;
            IsDraggingItem = false;
            _dropAcceptedThisDrag = false;
            _isDraggingThis = false;
            if (ReferenceEquals(_activeDragHandler, this))
            {
                _activeDragHandler = null;
            }

            if (resolvedReason == DragEndReason.Success)
            {
                PlayFeedbackClip(_dropSuccessClip);
            }
            else if (resolvedReason == DragEndReason.Invalid)
            {
                PlayFeedbackClip(_dropInvalidClip);
                StartInvalidShake();
            }
            else if (_isShaking)
            {
                StopInvalidShake();
            }

            if (_enableCursorFeedback)
            {
                ResolveCursorProfileIfNeeded();
                ApplyDefaultCursor();
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

        private void SetGhostScreenPosition(PointerEventData eventData)
        {
            if (_dragGhostRoot == null || eventData == null)
            {
                return;
            }

            RectTransform parentRect = _dragGhostRoot.parent as RectTransform;
            if (parentRect == null)
            {
                _dragGhostRoot.position = eventData.position;
                return;
            }

            Canvas canvas = _dragGhostRoot.GetComponentInParent<Canvas>();
            Camera uiCamera = null;
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                uiCamera = eventData.pressEventCamera != null
                    ? eventData.pressEventCamera
                    : eventData.enterEventCamera != null
                        ? eventData.enterEventCamera
                        : canvas.worldCamera;
            }

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, eventData.position, uiCamera, out Vector2 localPoint))
            {
                _dragGhostRoot.anchoredPosition = localPoint;
                return;
            }

            _dragGhostRoot.position = eventData.position;
        }

        private void ResolvePayloadFromViewIfNeeded()
        {
            if (_entryView == null)
            {
                _entryView = GetComponent<PreparationInventoryItemEntryView>();
            }

            if (_entryView == null)
            {
                if (_enableDiagnostics)
                {
                    SBLog.Warn($"InventoryItemDragHandler: Missing {nameof(PreparationInventoryItemEntryView)} on '{gameObject.name}'.", this);
                }
                return;
            }

            _boundEntry = _entryView.BoundEntry;
            _boundEquipmentDefinition = _entryView.BoundEquipmentDefinition;
            _boundItemDefinition = _entryView.BoundItemDefinition;
        }

        private bool CanStartDragFromCurrentPayload()
        {
            ResolvePayloadFromViewIfNeeded();
            return IsSupportedDragEntry(_boundEntry, _boundEquipmentDefinition, _boundItemDefinition);
        }

        private bool TryResolveDragPayload(out Sprite icon, out EquipmentDefinition equipmentDefinition, out ItemDefinition itemDefinition)
        {
            icon = null;
            equipmentDefinition = null;
            itemDefinition = null;

            if (!IsSupportedDragEntry(_boundEntry, _boundEquipmentDefinition, _boundItemDefinition))
            {
                return false;
            }

            if (_boundEntry.Kind == InventoryEntry.EntryKind.Equipment)
            {
                equipmentDefinition = _boundEquipmentDefinition;
                icon = _boundEquipmentDefinition != null ? _boundEquipmentDefinition.Icon : null;
                return true;
            }

            itemDefinition = _boundItemDefinition;
            icon = _boundItemDefinition != null ? _boundItemDefinition.Icon : null;
            return true;
        }

        private static bool IsSupportedDragEntry(InventoryEntry entry, EquipmentDefinition equipmentDefinition, ItemDefinition itemDefinition)
        {
            if (entry == null)
            {
                return false;
            }

            if (entry.Kind == InventoryEntry.EntryKind.Equipment)
            {
                return equipmentDefinition != null;
            }

            if (entry.Kind == InventoryEntry.EntryKind.Item)
            {
                return itemDefinition != null && itemDefinition.IsConsumable;
            }

            return false;
        }

        private void ApplyDragStartScale()
        {
            transform.localScale = _baseLocalScale * Mathf.Max(1f, _dragStartScale);
        }

        private void AnimateScaleBackToBase()
        {
            float dt = Time.unscaledDeltaTime;
            float lerpT = 1f - Mathf.Exp(-Mathf.Max(0.1f, _scaleReturnSpeed) * dt);
            transform.localScale = Vector3.Lerp(transform.localScale, _baseLocalScale, lerpT);
        }

        private void CaptureBaseAnchoredPositionIfNeeded()
        {
            if (_rectTransform == null)
            {
                return;
            }

            _baseAnchoredPosition = _rectTransform.anchoredPosition;
            _baseAnchoredPositionCaptured = true;
        }

        private void RefreshBaseAnchoredPositionFromCurrentLayout()
        {
            if (_rectTransform == null)
            {
                return;
            }

            ForceParentLayoutRebuildIfNeeded();
            _baseAnchoredPosition = _rectTransform.anchoredPosition;
            _baseAnchoredPositionCaptured = true;

            if (_enableDiagnostics)
            {
                SBLog.Info(
                    $"InventoryItemDragHandler: Captured base anchored position {_baseAnchoredPosition} for '{gameObject.name}'.",
                    this);
            }
        }

        private void ForceParentLayoutRebuildIfNeeded()
        {
            if (_rectTransform == null)
            {
                return;
            }

            RectTransform parentRect = _rectTransform.parent as RectTransform;
            if (parentRect == null)
            {
                return;
            }

            bool parentUsesLayout =
                parentRect.GetComponent<LayoutGroup>() != null ||
                parentRect.GetComponent<ContentSizeFitter>() != null;

            if (!parentUsesLayout)
            {
                return;
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
        }

        private void StartInvalidShake()
        {
            if (_rectTransform == null || _invalidDropShakeDuration <= 0f || _invalidDropShakeMagnitude <= 0f)
            {
                return;
            }

            RefreshBaseAnchoredPositionFromCurrentLayout();

            _shakeRemaining = _invalidDropShakeDuration;
            _isShaking = true;
        }

        private void AnimateInvalidDropShake()
        {
            if (!_isShaking || _rectTransform == null)
            {
                return;
            }

            if (_shakeRemaining <= 0f)
            {
                StopInvalidShake();
                return;
            }

            _shakeRemaining -= Time.unscaledDeltaTime;
            float progress = 1f - Mathf.Clamp01(_shakeRemaining / Mathf.Max(0.0001f, _invalidDropShakeDuration));
            float damper = 1f - progress;
            float oscillation = Mathf.Sin(progress * Mathf.PI * 8f);
            float offsetX = oscillation * _invalidDropShakeMagnitude * damper;
            _rectTransform.anchoredPosition = _baseAnchoredPosition + new Vector2(offsetX, 0f);
        }

        private void StopInvalidShake()
        {
            _isShaking = false;
            _shakeRemaining = 0f;
            if (_rectTransform != null && _baseAnchoredPositionCaptured)
            {
                _rectTransform.anchoredPosition = _baseAnchoredPosition;
                ForceParentLayoutRebuildIfNeeded();
                _baseAnchoredPosition = _rectTransform.anchoredPosition;
                if (_enableDiagnostics)
                {
                    SBLog.Info(
                        $"InventoryItemDragHandler: Restored anchored position to {_baseAnchoredPosition} for '{gameObject.name}'.",
                        this);
                }
            }
        }

        private void ApplyHoverCursor()
        {
            if (_hoverCursorTexture != null)
            {
                Cursor.SetCursor(_hoverCursorTexture, _hoverCursorHotspot, CursorMode.Auto);
                return;
            }

            ApplyDefaultCursor();
        }

        private void ApplyDragCursor()
        {
            if (_dragCursorTexture != null)
            {
                Cursor.SetCursor(_dragCursorTexture, _dragCursorHotspot, CursorMode.Auto);
                return;
            }

            if (_hoverCursorTexture != null)
            {
                Cursor.SetCursor(_hoverCursorTexture, _hoverCursorHotspot, CursorMode.Auto);
                return;
            }

            ApplyDefaultCursor();
        }

        private void ApplyDefaultCursor()
        {
            Cursor.SetCursor(_defaultCursorTexture, _defaultCursorHotspot, CursorMode.Auto);
        }

        private void ResolveCursorProfileIfNeeded()
        {
            ResolvePopupCursorProfileIfNeeded();

            if (_defaultCursorTexture == null)
            {
                _defaultCursorTexture = PopupDefaultCursorTexture;
                _defaultCursorHotspot = PopupDefaultCursorHotspot;
            }

            if (_hoverCursorTexture == null)
            {
                _hoverCursorTexture = PopupHoverCursorTexture;
                _hoverCursorHotspot = PopupHoverCursorHotspot;
            }

            if (_dragCursorTexture == null)
            {
                _dragCursorTexture = PopupDragCursorTexture;
                _dragCursorHotspot = PopupDragCursorHotspot;
            }
        }

        private static void ResolvePopupCursorProfileIfNeeded()
        {
            bool controllerInvalid =
                PopupCursorController == null ||
                !PopupCursorController.gameObject.scene.IsValid();

            if (controllerInvalid)
            {
                PopupCursorController = Object.FindFirstObjectByType<PreparationPopupMenuLocalizationController>();
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

        private void PlayFeedbackClip(AudioClip clip)
        {
            if (clip == null || _audio == null)
            {
                return;
            }

            _audio.PlayOneShot(clip, Mathf.Clamp01(_feedbackVolume));
        }
    }
}
