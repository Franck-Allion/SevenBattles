using System;
using SevenBattles.Core;
using SevenBattles.Core.Items;
using SevenBattles.Core.Players;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SevenBattles.Preparation
{
    /// <summary>
    /// UI drop slot for consumable-item shortcuts.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class ConsumableDropSlotView : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private const string DefaultDragGhostName = "InventoryDragGhost";
        private const string BackgroundChildName = "Bg";

        [SerializeField] private ConsumableSlotType _slotType;
        [SerializeField] private Image _iconImage;
        [SerializeField] private Image _backgroundImage;
        [SerializeField] private RectTransform _dragGhostRoot;
        [SerializeField, Tooltip("Optional rarity palette used to tint equipped background by rarity.")]
        private ItemRarityColorPalette _rarityColorPalette;
        [SerializeField, Tooltip("Tint applied while a consumable icon is equipped. Defaults to white to avoid disabled-looking icons.")]
        private Color _equippedIconColor = Color.white;

        [Header("Drop Zone Highlight")]
        [SerializeField] private bool _enableHighlightEffect = true;
        [SerializeField] private RectTransform _highlightRoot;
        [SerializeField] private Image _highlightImage;
        [SerializeField] private CanvasGroup _highlightCanvasGroup;
        [SerializeField] private Color _availableColor = new Color(0.29f, 0.84f, 1f, 1f);
        [SerializeField] private Color _preferredColor = new Color(0.55f, 0.95f, 1f, 1f);
        [SerializeField] private Color _hoverColor = new Color(1f, 0.88f, 0.56f, 1f);
        [SerializeField, Range(0f, 1f)] private float _availableAlpha = 0.10f;
        [SerializeField, Range(0f, 1f)] private float _preferredAlpha = 0.24f;
        [SerializeField, Range(0f, 1f)] private float _hoverAlpha = 0.42f;
        [SerializeField, Range(0f, 0.35f)] private float _pulseAmplitude = 0.06f;
        [SerializeField, Min(0.1f)] private float _pulseSpeed = 2.6f;
        [SerializeField, Min(0.1f)] private float _fadeSpeed = 10f;
        [SerializeField, Range(1f, 1.2f)] private float _hoverScale = 1.035f;
        [Header("Completion State")]
        [SerializeField] private Color _completionColor = new Color(0.31f, 0.86f, 0.56f, 1f);
        [SerializeField, Range(0f, 1f)] private float _completionAlpha = 0.28f;
        [SerializeField, Range(1f, 1.2f)] private float _completionScale = 1.02f;
        [Header("Cursor Feedback")]
        [SerializeField] private bool _enableCursorFeedback = true;
        [SerializeField] private Texture2D _hoverCursorTexture;
        [SerializeField] private Vector2 _hoverCursorHotspot = new Vector2(16f, 16f);
        [SerializeField] private Texture2D _dragCursorTexture;
        [SerializeField] private Vector2 _dragCursorHotspot = new Vector2(16f, 16f);
        [SerializeField] private Texture2D _defaultCursorTexture;
        [SerializeField] private Vector2 _defaultCursorHotspot = new Vector2(4f, 4f);
        [SerializeField, Tooltip("If enabled, logs consumable slot drag/drop diagnostics.")]
        private bool _enableDiagnostics;

        public ConsumableSlotType SlotType => _slotType;
        public bool IsCompletionVisualActive => _isCompletionVisualActive;
        public bool IsDragPreviewActive => _isDragPreviewActive;
        public bool IsValidDropPreview => _isValidDropPreview;
        public bool HasEquippedItem => !string.IsNullOrWhiteSpace(_equippedDefinitionId);
        public string EquippedDefinitionId => _equippedDefinitionId;
        public OwnedUnitData SelectedUnit => _selectedUnit;
        public IItemEquipService ItemEquipService => _itemEquipService;

        public static bool IsDragging { get; set; }
        public static bool IsDraggingEquippedConsumable { get; private set; }
        public static ConsumableSlotType? DraggingFromConsumableSlot { get; private set; }
        public static string DraggingDefinitionId { get; private set; }
        public static ItemDefinition DraggingItemDefinition { get; private set; }
        public static bool AnyDragActive => IsDragging || IsDraggingEquippedConsumable || InventoryItemDragHandler.IsDraggingItem || UnitDragHandler.IsDragging;

        public event Action<ConsumableDropSlotView, PointerEventData> DropReceived;

        private bool _isPointerInside;
        private bool _isCompletionVisualActive;
        private bool _isDragPreviewActive;
        private bool _isValidDropPreview;
        private float _pulseTime;
        private Vector3 _baseHighlightScale = Vector3.one;
        private bool _highlightReady;
        private string _equippedDefinitionId;
        private ItemDefinition _equippedDefinition;
        private IItemEquipService _itemEquipService;
        private OwnedUnitData _selectedUnit;
        private Image _ghostImage;
        private Image _capturedDefaultIconSource;
        private Image _capturedDefaultBackgroundSource;
        private Sprite _defaultIconSprite;
        private bool _defaultIconEnabled;
        private Color _defaultIconColor = Color.white;
        private bool _defaultBackgroundEnabled;
        private Color _defaultBackgroundColor = Color.white;
        private bool _isDraggingThis;
        private float _previousIconAlpha = 1f;
        private static ConsumableDropSlotView _activeDragHandler;
        private static PreparationPopupMenuLocalizationController s_popupCursorController;
        private static Texture2D s_popupDefaultCursorTexture;
        private static Vector2 s_popupDefaultCursorHotspot = new Vector2(4f, 4f);
        private static Texture2D s_popupHoverCursorTexture;
        private static Vector2 s_popupHoverCursorHotspot = new Vector2(16f, 16f);
        private static Texture2D s_popupDragCursorTexture;
        private static Vector2 s_popupDragCursorHotspot = new Vector2(16f, 16f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            IsDragging = false;
            IsDraggingEquippedConsumable = false;
            DraggingFromConsumableSlot = null;
            DraggingDefinitionId = null;
            DraggingItemDefinition = null;
            _activeDragHandler = null;
            s_popupCursorController = null;
            s_popupDefaultCursorTexture = null;
            s_popupHoverCursorTexture = null;
            s_popupDragCursorTexture = null;
        }

        private void Awake()
        {
            EnsureRootRaycastTarget();
            EnsureHighlightOverlay();
            EnsureIconImage();
            EnsureBackgroundImage();
            RefreshEquippedIcon();
            ApplyHighlightImmediate(0f, _availableColor, 1f);
        }

        private void Update()
        {
            if (!_highlightReady || !_enableHighlightEffect)
            {
                return;
            }

            if (InventoryItemDragHandler.IsDraggingItem)
            {
                AnimateInventoryDragHighlight();
                return;
            }

            bool dragActive = IsDragging || _isDragPreviewActive;
            if (!dragActive)
            {
                _pulseTime = 0f;
                AnimateHighlight(_availableAlpha, _availableColor, 1f);
                return;
            }

            float baseAlpha = _isValidDropPreview ? _preferredAlpha : _availableAlpha;
            Color targetColor = _isValidDropPreview ? _preferredColor : _availableColor;
            float targetScale = 1f;

            if (_isPointerInside)
            {
                targetColor = _hoverColor;
                baseAlpha = Mathf.Max(baseAlpha, _hoverAlpha);
                targetScale = _hoverScale;
            }

            _pulseTime += Time.unscaledDeltaTime;
            float pulse = Mathf.Sin(_pulseTime * _pulseSpeed * Mathf.PI * 2f) * _pulseAmplitude;
            float targetAlpha = Mathf.Clamp01(baseAlpha + pulse);
            AnimateHighlight(targetAlpha, targetColor, targetScale);
        }

        public void SetItemEquipService(IItemEquipService itemEquipService)
        {
            _itemEquipService = itemEquipService;
        }

        public void SetSelectedUnit(OwnedUnitData unitData)
        {
            _selectedUnit = unitData;
        }

        public void SetSlotType(ConsumableSlotType slotType)
        {
            _slotType = slotType;
        }

        public void SetDragGhostRoot(RectTransform dragGhostRoot)
        {
            _dragGhostRoot = dragGhostRoot;
            _ghostImage = null;
        }

        public void SetIconImage(Image iconImage)
        {
            _iconImage = iconImage;
            CaptureDefaultIconStateIfNeeded();
            RefreshEquippedIcon();
        }

        public void SetBackgroundImage(Image backgroundImage)
        {
            _backgroundImage = backgroundImage;
            CaptureDefaultBackgroundStateIfNeeded();
            RefreshEquippedIcon();
        }

        public void SetRarityColorPalette(ItemRarityColorPalette rarityColorPalette)
        {
            _rarityColorPalette = rarityColorPalette;
            RefreshBackgroundVisual();
        }

        public void SetEquippedItem(string definitionId, ItemDefinition definition)
        {
            _equippedDefinitionId = string.IsNullOrWhiteSpace(definitionId) ? null : definitionId;
            _equippedDefinition = definition;
            RefreshEquippedIcon();
        }

        public void NotifyDropAccepted()
        {
            // Reserved for future feedback wiring.
        }

        public static void CancelActiveDrag()
        {
            if (_activeDragHandler != null)
            {
                _activeDragHandler.EndEquippedDrag();
                return;
            }

            IsDraggingEquippedConsumable = false;
            DraggingFromConsumableSlot = null;
            DraggingDefinitionId = null;
            DraggingItemDefinition = null;
        }

        public void SetDropPreviewState(bool isDragActive, bool isValidDropTarget)
        {
            _isDragPreviewActive = isDragActive;
            _isValidDropPreview = isDragActive && isValidDropTarget;
            if (!_isDragPreviewActive)
            {
                _pulseTime = 0f;
            }
        }

        public void SetCompletionVisual(bool isCompletionVisualActive)
        {
            _isCompletionVisualActive = isCompletionVisualActive;
            if (!_highlightReady || IsDragging || _isDragPreviewActive)
            {
                return;
            }

            if (_isCompletionVisualActive)
            {
                ApplyHighlightImmediate(_completionAlpha, _completionColor, _completionScale);
            }
            else
            {
                ApplyHighlightImmediate(0f, _availableColor, 1f);
            }
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (!InventoryItemDragHandler.IsDraggingItem)
            {
                if (_enableDiagnostics)
                {
                    Core.Diagnostics.SBLog.Info($"ConsumableDropSlotView[{_slotType}]: OnDrop ignored because no inventory item drag is active.", this);
                }
                return;
            }

            ItemDefinition draggedItemDef = InventoryItemDragHandler.DraggingItemDef;
            InventoryEntry draggedEntry = InventoryItemDragHandler.DraggingEntry;
            if (CanEquipDraggedDefinition(draggedItemDef) &&
                _itemEquipService != null &&
                _selectedUnit != null &&
                _itemEquipService.TryEquip(_selectedUnit, draggedItemDef, _slotType, draggedEntry))
            {
                SetEquippedItem(draggedItemDef.Id, draggedItemDef);
                SetCompletionVisual(true);
                NotifyInventoryDragAccepted(eventData);
                if (_enableDiagnostics)
                {
                    Core.Diagnostics.SBLog.Info(
                        $"ConsumableDropSlotView[{_slotType}]: Equipped '{draggedItemDef.Id}' on unit '{(_selectedUnit != null ? _selectedUnit.OwnedUnitId : "<null>")}' using dragged entry {(draggedEntry != null ? draggedEntry.EntryKey : "<null>")}.",
                        this);
                }
                return;
            }

            if (_enableDiagnostics)
            {
                string itemId = draggedItemDef != null ? draggedItemDef.Id : "<null>";
                bool itemConsumable = draggedItemDef != null && draggedItemDef.IsConsumable;
                bool hasSlot = TryGetSlotIndex(_selectedUnit, _slotType, out _);
                Core.Diagnostics.SBLog.Warn(
                    $"ConsumableDropSlotView[{_slotType}]: Drop rejected (item={itemId}, consumable={itemConsumable}, draggedEntry={(draggedEntry != null ? draggedEntry.EntryKey : "<null>")}, service={(_itemEquipService != null ? "yes" : "no")}, unit={(_selectedUnit != null ? _selectedUnit.OwnedUnitId : "<null>")}, slotExists={hasSlot}).",
                    this);
            }

            DropReceived?.Invoke(this, eventData);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isPointerInside = true;
            if (!_enableCursorFeedback || AnyDragActive || !HasEquippedItem)
            {
                return;
            }

            ResolveCursorProfileIfNeeded();
            ApplyHoverCursor();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isPointerInside = false;
            if (!_enableCursorFeedback || AnyDragActive)
            {
                return;
            }

            ResolveCursorProfileIfNeeded();
            ApplyDefaultCursor();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (InventoryItemDragHandler.IsDraggingItem || UnitDragHandler.IsDragging || EquipmentDropSlotView.IsDraggingEquippedItem)
            {
                if (_enableDiagnostics)
                {
                    Core.Diagnostics.SBLog.Info(
                        $"ConsumableDropSlotView[{_slotType}]: OnBeginDrag ignored (inventoryDragging={InventoryItemDragHandler.IsDraggingItem}, unitDragging={UnitDragHandler.IsDragging}, equipmentDragging={EquipmentDropSlotView.IsDraggingEquippedItem}).",
                        this);
                }
                return;
            }

            if (_activeDragHandler != null && _activeDragHandler != this)
            {
                if (_enableDiagnostics)
                {
                    Core.Diagnostics.SBLog.Info(
                        $"ConsumableDropSlotView[{_slotType}]: OnBeginDrag ignored because another consumable slot drag is active.",
                        this);
                }
                return;
            }

            if (string.IsNullOrWhiteSpace(_equippedDefinitionId))
            {
                if (_enableDiagnostics)
                {
                    Core.Diagnostics.SBLog.Info($"ConsumableDropSlotView[{_slotType}]: OnBeginDrag ignored because slot has no equipped definition.", this);
                }
                return;
            }

            EnsureIconImage();
            if (_iconImage != null)
            {
                _previousIconAlpha = _iconImage.color.a;
                SetIconAlpha(0.4f);
            }

            EnsureDragGhostRoot();
            if (_dragGhostRoot != null)
            {
                _dragGhostRoot.gameObject.SetActive(true);
                SetGhostScreenPosition(eventData);

                ResolveGhostImage();
                if (_ghostImage != null)
                {
                    _ghostImage.sprite = _equippedDefinition != null ? _equippedDefinition.Icon : _iconImage != null ? _iconImage.sprite : null;
                    _ghostImage.enabled = _ghostImage.sprite != null;
                    if (_enableDiagnostics && _ghostImage.sprite == null)
                    {
                        Core.Diagnostics.SBLog.Warn(
                            $"ConsumableDropSlotView[{_slotType}]: Drag ghost sprite is null for definition '{_equippedDefinitionId ?? "<null>"}'.",
                            this);
                    }
                }
            }

            IsDraggingEquippedConsumable = true;
            DraggingFromConsumableSlot = _slotType;
            DraggingDefinitionId = _equippedDefinitionId;
            DraggingItemDefinition = _equippedDefinition;
            _isDraggingThis = true;
            _activeDragHandler = this;

            if (_enableCursorFeedback)
            {
                ResolveCursorProfileIfNeeded();
                ApplyDragCursor();
            }

            if (_enableDiagnostics)
            {
                Core.Diagnostics.SBLog.Info(
                    $"ConsumableDropSlotView[{_slotType}]: Drag started for definition '{_equippedDefinitionId}' (ghost={(_dragGhostRoot != null ? _dragGhostRoot.name : "<null>")}).",
                    this);
            }
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

            EndEquippedDrag();
        }

        private void OnDisable()
        {
            bool thisOwnsActiveDrag =
                _isDraggingThis ||
                (IsDraggingEquippedConsumable &&
                 DraggingFromConsumableSlot.HasValue &&
                 DraggingFromConsumableSlot.Value == _slotType &&
                 ReferenceEquals(_activeDragHandler, this));
            if (thisOwnsActiveDrag)
            {
                EndEquippedDrag();
            }
        }

        private void EnsureIconImage()
        {
            if (_iconImage != null)
            {
                _iconImage.raycastTarget = false;
                CaptureDefaultIconStateIfNeeded();
                return;
            }

            Transform existing = transform.Find("Icon");
            if (existing == null)
            {
                existing = transform.Find("EquippedIcon");
            }

            if (existing == null)
            {
                existing = FindChildImageTransform(transform);
            }

            if (existing is RectTransform existingRect)
            {
                _iconImage = existingRect.GetComponent<Image>();
                if (_iconImage == null)
                {
                    _iconImage = existingRect.gameObject.AddComponent<Image>();
                }
            }
            else
            {
                var iconObject = new GameObject("EquippedIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                iconObject.transform.SetParent(transform, false);
                RectTransform iconRect = iconObject.GetComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(0.15f, 0.15f);
                iconRect.anchorMax = new Vector2(0.85f, 0.85f);
                iconRect.offsetMin = Vector2.zero;
                iconRect.offsetMax = Vector2.zero;
                _iconImage = iconObject.GetComponent<Image>();
                _iconImage.sprite = null;
                _iconImage.enabled = false;
            }

            _iconImage.raycastTarget = false;
            CaptureDefaultIconStateIfNeeded();
        }

        private void EnsureBackgroundImage()
        {
            if (_backgroundImage != null)
            {
                CaptureDefaultBackgroundStateIfNeeded();
                return;
            }

            Transform backgroundTransform = transform.Find(BackgroundChildName);
            if (backgroundTransform == null)
            {
                backgroundTransform = FindByName(transform, BackgroundChildName);
            }

            if (backgroundTransform != null)
            {
                _backgroundImage = backgroundTransform.GetComponent<Image>();
                if (_backgroundImage == null)
                {
                    _backgroundImage = backgroundTransform.GetComponentInChildren<Image>(true);
                }
            }

            CaptureDefaultBackgroundStateIfNeeded();
        }

        private static Transform FindChildImageTransform(Transform root)
        {
            if (root == null)
            {
                return null;
            }

            Image[] childImages = root.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < childImages.Length; i++)
            {
                Image image = childImages[i];
                if (image == null || image.transform == root)
                {
                    continue;
                }

                return image.transform;
            }

            return null;
        }

        private static Transform FindByName(Transform root, string objectName)
        {
            if (root == null || string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            Transform[] nodes = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < nodes.Length; i++)
            {
                Transform node = nodes[i];
                if (node != null && string.Equals(node.name, objectName, StringComparison.Ordinal))
                {
                    return node;
                }
            }

            return null;
        }

        private void EnsureRootRaycastTarget()
        {
            Image slotImage = GetComponent<Image>();
            if (slotImage == null)
            {
                slotImage = gameObject.AddComponent<Image>();
                Color color = slotImage.color;
                color.a = 0f;
                slotImage.color = color;
            }

            slotImage.raycastTarget = true;
        }

        private void RefreshEquippedIcon()
        {
            EnsureIconImage();
            EnsureBackgroundImage();
            RefreshBackgroundVisual();
            if (_iconImage == null)
            {
                return;
            }

            CaptureDefaultIconStateIfNeeded();
            Sprite equippedIcon = _equippedDefinition != null ? _equippedDefinition.Icon : null;
            if (equippedIcon != null)
            {
                _iconImage.sprite = equippedIcon;
                _iconImage.enabled = true;
                Color equippedColor = _equippedIconColor;
                equippedColor.a = 1f;
                _iconImage.color = equippedColor;
                return;
            }

            _iconImage.sprite = _defaultIconSprite;
            _iconImage.enabled = _defaultIconEnabled || _defaultIconSprite != null;
            _iconImage.color = _defaultIconColor;
        }

        private void CaptureDefaultIconStateIfNeeded()
        {
            if (_iconImage == null || ReferenceEquals(_capturedDefaultIconSource, _iconImage))
            {
                return;
            }

            _defaultIconSprite = _iconImage.sprite;
            _defaultIconEnabled = _iconImage.enabled;
            _defaultIconColor = _iconImage.color;
            _capturedDefaultIconSource = _iconImage;
        }

        private void CaptureDefaultBackgroundStateIfNeeded()
        {
            if (_backgroundImage == null || ReferenceEquals(_capturedDefaultBackgroundSource, _backgroundImage))
            {
                return;
            }

            _defaultBackgroundEnabled = _backgroundImage.enabled;
            _defaultBackgroundColor = _backgroundImage.color;
            _capturedDefaultBackgroundSource = _backgroundImage;
        }

        private void RefreshBackgroundVisual()
        {
            if (_backgroundImage == null)
            {
                return;
            }

            CaptureDefaultBackgroundStateIfNeeded();
            if (_equippedDefinition != null)
            {
                Color rarityColor = ItemRarityColorUtility.GetInventoryBackgroundColor(_equippedDefinition.Rarity, _rarityColorPalette);
                rarityColor.a = _defaultBackgroundColor.a > 0f ? _defaultBackgroundColor.a : rarityColor.a;
                _backgroundImage.color = rarityColor;
                _backgroundImage.enabled = true;
                return;
            }

            _backgroundImage.enabled = _defaultBackgroundEnabled;
            _backgroundImage.color = _defaultBackgroundColor;
        }

        private void EnsureHighlightOverlay()
        {
            if (!_enableHighlightEffect)
            {
                return;
            }

            RectTransform selfRect = transform as RectTransform;
            if (selfRect == null)
            {
                return;
            }

            if (_highlightRoot == null)
            {
                var go = new GameObject("ConsumableDropHighlight", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
                go.transform.SetParent(selfRect, false);
                _highlightRoot = go.GetComponent<RectTransform>();
                _highlightRoot.anchorMin = Vector2.zero;
                _highlightRoot.anchorMax = Vector2.one;
                _highlightRoot.anchoredPosition = Vector2.zero;
                _highlightRoot.sizeDelta = Vector2.zero;
                _highlightRoot.localScale = Vector3.one;
                _highlightRoot.SetAsFirstSibling();
            }

            if (_highlightImage == null)
            {
                _highlightImage = _highlightRoot.GetComponent<Image>();
                if (_highlightImage == null)
                {
                    _highlightImage = _highlightRoot.gameObject.AddComponent<Image>();
                }
            }

            if (_highlightCanvasGroup == null)
            {
                _highlightCanvasGroup = _highlightRoot.GetComponent<CanvasGroup>();
                if (_highlightCanvasGroup == null)
                {
                    _highlightCanvasGroup = _highlightRoot.gameObject.AddComponent<CanvasGroup>();
                }
            }

            Image slotImage = GetComponent<Image>();
            if (slotImage != null)
            {
                _highlightImage.sprite = slotImage.sprite;
                _highlightImage.type = slotImage.type;
                _highlightImage.preserveAspect = slotImage.preserveAspect;
            }
            else
            {
                _highlightImage.type = Image.Type.Sliced;
            }

            _highlightImage.raycastTarget = false;
            _baseHighlightScale = _highlightRoot.localScale;
            _highlightReady = true;
        }

        private void ApplyHighlightImmediate(float alpha, Color color, float scaleMultiplier)
        {
            if (!_highlightReady)
            {
                return;
            }

            _highlightCanvasGroup.alpha = Mathf.Clamp01(alpha);
            _highlightImage.color = color;
            _highlightRoot.localScale = _baseHighlightScale * Mathf.Max(1f, scaleMultiplier);
        }

        private void AnimateHighlight(float targetAlpha, Color targetColor, float targetScaleMultiplier)
        {
            if (!_highlightReady)
            {
                return;
            }

            float lerpT = Mathf.Clamp01(Time.unscaledDeltaTime * Mathf.Max(0.1f, _fadeSpeed));
            _highlightCanvasGroup.alpha = Mathf.Lerp(_highlightCanvasGroup.alpha, Mathf.Clamp01(targetAlpha), lerpT);
            _highlightImage.color = Color.Lerp(_highlightImage.color, targetColor, lerpT);
            Vector3 targetScale = _baseHighlightScale * Mathf.Max(1f, targetScaleMultiplier);
            _highlightRoot.localScale = Vector3.Lerp(_highlightRoot.localScale, targetScale, lerpT);
        }

        private void AnimateInventoryDragHighlight()
        {
            bool canEquipDragged = CanEquipDraggedDefinition(InventoryItemDragHandler.DraggingItemDef);
            if (!canEquipDragged)
            {
                _pulseTime = 0f;
                AnimateHighlight(0f, _availableColor, 1f);
                return;
            }

            float baseAlpha = _preferredAlpha;
            Color targetColor = _preferredColor;
            float targetScale = 1f;

            if (_isPointerInside)
            {
                targetColor = _hoverColor;
                baseAlpha = Mathf.Max(baseAlpha, _hoverAlpha);
                targetScale = _hoverScale;
            }

            _pulseTime += Time.unscaledDeltaTime;
            float pulse = Mathf.Sin(_pulseTime * _pulseSpeed * Mathf.PI * 2f) * _pulseAmplitude;
            float targetAlpha = Mathf.Clamp01(baseAlpha + pulse);
            AnimateHighlight(targetAlpha, targetColor, targetScale);
        }

        private bool CanEquipDraggedDefinition(ItemDefinition draggedDefinition)
        {
            if (draggedDefinition == null || !draggedDefinition.IsConsumable)
            {
                return false;
            }

            if (_itemEquipService == null || _selectedUnit == null)
            {
                return false;
            }

            return TryGetSlotIndex(_selectedUnit, _slotType, out _);
        }

        private static void NotifyInventoryDragAccepted(PointerEventData eventData)
        {
            if (eventData == null || eventData.pointerDrag == null)
            {
                return;
            }

            InventoryItemDragHandler dragHandler = eventData.pointerDrag.GetComponentInParent<InventoryItemDragHandler>();
            if (dragHandler != null)
            {
                dragHandler.NotifyDropAccepted();
            }
        }

        private void EndEquippedDrag()
        {
            SetIconAlpha(_previousIconAlpha);
            if (_dragGhostRoot != null)
            {
                _dragGhostRoot.gameObject.SetActive(false);
            }

            _isDraggingThis = false;
            if (ReferenceEquals(_activeDragHandler, this))
            {
                _activeDragHandler = null;
            }

            IsDraggingEquippedConsumable = false;
            DraggingFromConsumableSlot = null;
            DraggingDefinitionId = null;
            DraggingItemDefinition = null;

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

        private void EnsureDragGhostRoot()
        {
            if (_dragGhostRoot != null)
            {
                if (IsParentChainActive(_dragGhostRoot))
                {
                    return;
                }

                if (_enableDiagnostics)
                {
                    Core.Diagnostics.SBLog.Warn(
                        $"ConsumableDropSlotView[{_slotType}]: Assigned drag ghost root '{_dragGhostRoot.name}' is under an inactive parent chain. Re-resolving ghost root.",
                        this);
                }

                _dragGhostRoot = null;
            }

            if (_dragGhostRoot != null)
            {
                return;
            }

            Canvas currentCanvas = GetComponentInParent<Canvas>();
            Canvas rootCanvas = currentCanvas != null
                ? (currentCanvas.isRootCanvas ? currentCanvas : currentCanvas.rootCanvas)
                : null;
            if (rootCanvas == null)
            {
                return;
            }

            Transform existing = rootCanvas.transform.Find(DefaultDragGhostName);
            if (existing is RectTransform existingRect)
            {
                _dragGhostRoot = existingRect;
                return;
            }

            var ghostObject = new GameObject(DefaultDragGhostName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            ghostObject.transform.SetParent(rootCanvas.transform, false);
            _dragGhostRoot = ghostObject.GetComponent<RectTransform>();
            _dragGhostRoot.anchorMin = new Vector2(0.5f, 0.5f);
            _dragGhostRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _dragGhostRoot.pivot = new Vector2(0.5f, 0.5f);
            _dragGhostRoot.sizeDelta = new Vector2(80f, 80f);
            _dragGhostRoot.SetAsLastSibling();

            Image image = ghostObject.GetComponent<Image>();
            image.raycastTarget = false;
            image.enabled = false;
            ghostObject.SetActive(false);

            if (_enableDiagnostics)
            {
                Core.Diagnostics.SBLog.Info(
                    $"ConsumableDropSlotView[{_slotType}]: Created runtime drag ghost root '{_dragGhostRoot.name}' under root canvas '{rootCanvas.name}'.",
                    this);
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
            if (_enableDiagnostics)
            {
                Core.Diagnostics.SBLog.Warn(
                    $"ConsumableDropSlotView[{_slotType}]: Failed to convert ghost screen position. Falling back to world position.",
                    this);
            }
        }

        private static bool IsParentChainActive(Transform transform)
        {
            if (transform == null)
            {
                return false;
            }

            Transform parent = transform.parent;
            while (parent != null)
            {
                if (!parent.gameObject.activeSelf)
                {
                    return false;
                }

                parent = parent.parent;
            }

            return true;
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
                _defaultCursorTexture = s_popupDefaultCursorTexture;
                _defaultCursorHotspot = s_popupDefaultCursorHotspot;
            }

            if (_hoverCursorTexture == null)
            {
                _hoverCursorTexture = s_popupHoverCursorTexture;
                _hoverCursorHotspot = s_popupHoverCursorHotspot;
            }

            if (_dragCursorTexture == null)
            {
                _dragCursorTexture = s_popupDragCursorTexture;
                _dragCursorHotspot = s_popupDragCursorHotspot;
            }
        }

        private static void ResolvePopupCursorProfileIfNeeded()
        {
            bool controllerInvalid =
                s_popupCursorController == null ||
                !s_popupCursorController.gameObject.scene.IsValid();

            if (controllerInvalid)
            {
                s_popupCursorController = UnityEngine.Object.FindFirstObjectByType<PreparationPopupMenuLocalizationController>();
            }

            if (s_popupCursorController == null)
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

                    s_popupCursorController = popup;
                    break;
                }
            }

            if (s_popupCursorController == null)
            {
                return;
            }

            if (!s_popupCursorController.TryGetSquadPortraitCursorProfile(
                    out Texture2D defaultTexture,
                    out Vector2 defaultHotspot,
                    out Texture2D hoverTexture,
                    out Vector2 hoverHotspot,
                    out Texture2D dragTexture,
                    out Vector2 dragHotspot))
            {
                return;
            }

            s_popupDefaultCursorTexture = defaultTexture;
            s_popupDefaultCursorHotspot = defaultHotspot;
            s_popupHoverCursorTexture = hoverTexture;
            s_popupHoverCursorHotspot = hoverHotspot;
            s_popupDragCursorTexture = dragTexture;
            s_popupDragCursorHotspot = dragHotspot;
        }

        private void SetIconAlpha(float alpha)
        {
            if (_iconImage == null)
            {
                return;
            }

            Color color = _iconImage.color;
            color.a = Mathf.Clamp01(alpha);
            _iconImage.color = color;
        }

        private static bool TryGetSlotIndex(OwnedUnitData unit, ConsumableSlotType slotType, out int slotIndex)
        {
            slotIndex = -1;
            if (unit == null || unit.EquippedConsumables == null)
            {
                return false;
            }

            for (int i = 0; i < unit.EquippedConsumables.Length; i++)
            {
                if (unit.EquippedConsumables[i].SlotType == slotType)
                {
                    slotIndex = i;
                    return true;
                }
            }

            return false;
        }
    }
}
