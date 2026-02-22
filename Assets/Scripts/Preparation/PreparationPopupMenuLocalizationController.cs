using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.Localization;
using UnityEngine.Serialization;
using UnityEngine.UI;
using SevenBattles.Core.Diagnostics;

namespace SevenBattles.Preparation
{
    public sealed class PreparationPopupMenuLocalizationController : MonoBehaviour
    {
        private const string UI_COMMON_TABLE = "UI.Common";
        private const string SHOP_DEFAULT_KEY = "Preparation.Popup.Shop";
        private const string SQUAD_DEFAULT_KEY = "Preparation.Popup.Squad";
        private const string INVENTORY_TITLE_DEFAULT_KEY = "Preparation.Inventory.Title";

        [Header("Label Targets")]
        [SerializeField, Tooltip("TMP label shown in the Shop menu button.")]
        private TMP_Text _shopLabelTMP;
        [SerializeField, Tooltip("TMP label shown in the Squad menu button.")]
        private TMP_Text _squadLabelTMP;
        [SerializeField, Tooltip("TMP label shown in the Inventory panel title.")]
        private TMP_Text _inventoryTitleTMP;
        [SerializeField, Tooltip("Child object name used to auto-find the Shop button when _shopLabelTMP is not assigned.")]
        private string _shopButtonObjectName = "ShopButtonMenu";
        [SerializeField, Tooltip("Child object name used to auto-find the Squad button when _squadLabelTMP is not assigned.")]
        private string _squadButtonObjectName = "SquadButtonMenu";
        [SerializeField, Tooltip("Object name used to auto-find the Inventory panel title when _inventoryTitleTMP is not assigned.")]
        private string _inventoryTitleObjectName = "Text_Title";

        [Header("Button Targets")]
        [SerializeField, Tooltip("Optional explicit reference to the Shop menu button. Auto-found when null.")]
        private Button _shopButton;
        [SerializeField, Tooltip("Optional explicit reference to the Squad menu button. Auto-found when null.")]
        private Button _squadButton;
        [SerializeField, Tooltip("Optional explicit reference to the Inventory button inside Squad setup. Auto-found when null.")]
        private Button _inventoryButton;
        [SerializeField, Tooltip("Optional explicit reference to the Back button inside SquadPanel. Auto-found when null.")]
        private Button _squadBackButton;
        [SerializeField, Tooltip("Object name used to auto-find the SquadPanel Back button when _squadBackButton is not assigned.")]
        private string _squadBackButtonObjectName = "Button_Back";
        [SerializeField, Tooltip("Object name used to auto-find the Inventory button when _inventoryButton is not assigned.")]
        private string _inventoryButtonObjectName = "InventoryButton";
        [SerializeField, Tooltip("Optional explicit reference to the Back button inside InventoryView. Auto-found when null.")]
        private Button _inventoryBackButton;
        [SerializeField, Tooltip("Object name used to auto-find the InventoryView Back button when _inventoryBackButton is not assigned.")]
        private string _inventoryBackButtonObjectName = "Button_Back";

        [Header("Squad Panel Transition")]
        [SerializeField, Tooltip("Optional explicit reference to the Squad panel root. Auto-found by name when null.")]
        private GameObject _squadPanel;
        [SerializeField, Tooltip("Object name used to auto-find the Squad panel when _squadPanel is not assigned.")]
        private string _squadPanelObjectName = "SquadPanel";
        [SerializeField, Tooltip("CanvasGroup used to animate the Squad panel. Auto-added when missing.")]
        private CanvasGroup _squadPanelCanvasGroup;
        [SerializeField, Min(0f), Tooltip("Reveal duration in seconds for the Squad panel. Uses unscaled time.")]
        private float _squadPanelFadeDuration = 0.24f;
        [SerializeField, Range(0.8f, 1f), Tooltip("Starting scale multiplier used during Squad panel reveal.")]
        private float _squadPanelStartScale = 0.95f;
        [SerializeField, Tooltip("Easing curve used for Squad panel reveal.")]
        private AnimationCurve _squadPanelRevealCurve = null;

        [Header("Inventory Panel Transition")]
        [SerializeField, Tooltip("Optional explicit reference to the Inventory panel root. Auto-found by name when null.")]
        private GameObject _inventoryPanel;
        [SerializeField, Tooltip("Object name used to auto-find the Inventory panel when _inventoryPanel is not assigned.")]
        private string _inventoryPanelObjectName = "InventoryPanel";
        [SerializeField, Tooltip("CanvasGroup used to animate the Inventory panel. Auto-added when missing.")]
        private CanvasGroup _inventoryPanelCanvasGroup;
        [SerializeField, Min(0f), Tooltip("Reveal duration in seconds for the Inventory panel. Uses unscaled time.")]
        private float _inventoryPanelFadeDuration = 0.24f;
        [SerializeField, Range(0.8f, 1f), Tooltip("Starting scale multiplier used during Inventory panel reveal.")]
        private float _inventoryPanelStartScale = 0.95f;
        [SerializeField, Tooltip("Easing curve used for Inventory panel reveal.")]
        private AnimationCurve _inventoryPanelRevealCurve = null;

        [Header("Localization")]
        [SerializeField, Tooltip("Localized label for the Shop button.")]
        private LocalizedString _shopLabel;
        [SerializeField, Tooltip("Localized label for the Squad button.")]
        private LocalizedString _squadLabel;
        [SerializeField, Tooltip("Localized title for the Inventory panel.")]
        private LocalizedString _inventoryTitleLabel;

        [Header("Hover Feedback")]
        [SerializeField, FormerlySerializedAs("_hoverCursorTexture"), Tooltip("Cursor texture used while hovering menu buttons.")]
        private Texture2D _buttonHoverCursorTexture;
        [SerializeField, FormerlySerializedAs("_hoverCursorHotspot"), Tooltip("Hotspot used with the menu button hover cursor texture.")]
        private Vector2 _buttonHoverCursorHotspot = new Vector2(16f, 16f);
        [SerializeField, Tooltip("Cursor texture used while hovering unit portraits in the Squad panel. Falls back to the menu button hover cursor when not assigned.")]
        private Texture2D _portraitHoverCursorTexture;
        [SerializeField, Tooltip("Hotspot used with the portrait hover cursor texture.")]
        private Vector2 _portraitHoverCursorHotspot = new Vector2(16f, 16f);
        [SerializeField, Tooltip("Default cursor texture restored when no menu button is hovered.")]
        private Texture2D _defaultCursorTexture;
        [SerializeField, Tooltip("Hotspot used with the default cursor texture.")]
        private Vector2 _defaultCursorHotspot = new Vector2(4f, 4f);
        [SerializeField, Tooltip("Cursor texture used while dragging unit portraits in the Squad panel.")]
        private Texture2D _portraitDragCursorTexture;
        [SerializeField, Tooltip("Hotspot used with the portrait drag cursor texture.")]
        private Vector2 _portraitDragCursorHotspot = new Vector2(16f, 16f);
        [SerializeField, Tooltip("Optional AudioSource used to play button click SFX.")]
        [FormerlySerializedAs("_hoverAudioSource")]
        private AudioSource _clickAudioSource;
        [SerializeField, Tooltip("Optional SFX clip played when clicking a menu button.")]
        [FormerlySerializedAs("_hoverSfxClip")]
        private AudioClip _clickSfxClip;
        [SerializeField, Range(0f, 1.5f), Tooltip("Volume multiplier for menu button click SFX.")]
        [FormerlySerializedAs("_hoverSfxVolume")]
        private float _clickSfxVolume = 1f;
        [SerializeField, Tooltip("Minimum unscaled seconds between two click SFX plays.")]
        [FormerlySerializedAs("_hoverSfxCooldown")]
        private float _clickSfxCooldown = 0.05f;

        private readonly List<MenuButtonHoverForwarder> _hoverForwarders = new List<MenuButtonHoverForwarder>(2);
        private readonly List<ButtonClickSubscription> _clickSubscriptions = new List<ButtonClickSubscription>(2);
        private readonly List<ButtonClickSubscription> _panelClickSubscriptions = new List<ButtonClickSubscription>(4);
        private readonly List<RaycastResult> _raycastBuffer = new List<RaycastResult>(16);
        private int _hoveredButtonCount;
        private float _lastClickSfxTime = -999f;
        private Coroutine _squadPanelRoutine;
        private Coroutine _inventoryPanelRoutine;
        private Vector3 _squadPanelBaseScale = Vector3.one;
        private Vector3 _inventoryPanelBaseScale = Vector3.one;
        private bool _squadPanelScaleCaptured;
        private bool _inventoryPanelScaleCaptured;
        private bool _squadPanelStartupHiddenApplied;
        private bool _inventoryPanelStartupHiddenApplied;
        private Transform _squadPanelAnimatedRoot;
        private Transform _inventoryPanelAnimatedRoot;

        private void Awake()
        {
            SetupLocalizationDefaults();
            ResolveLabelTargets();
            ResolveButtonTargets();
            ResolveSquadPanel();
            ResolveInventoryTargets();
        }

        private void OnEnable()
        {
            SetupLocalizationDefaults();
            ResolveLabelTargets();
            ResolveButtonTargets();
            ResolveSquadPanel();
            ResolveInventoryTargets();
            BindLabels();
            WireHoverFeedback();
            WireSquadPanelButton();
            RefreshLabels();
        }

        private void OnDisable()
        {
            UnbindLabels();
            UnwireHoverFeedback();
            UnwireSquadPanelButton();
            StopSquadPanelRoutine();
            StopInventoryPanelRoutine();
            RestoreDefaultCursor();
        }

        private void LateUpdate()
        {
            if (IsPointerTopmostButton())
            {
                ApplyHoverCursor();
                return;
            }

            if (_hoveredButtonCount > 0)
            {
                ApplyHoverCursor();
            }
        }

        private void SetupLocalizationDefaults()
        {
            if (!HasLocalizedValue(_shopLabel))
            {
                _shopLabel = new LocalizedString(UI_COMMON_TABLE, SHOP_DEFAULT_KEY);
            }

            if (!HasLocalizedValue(_squadLabel))
            {
                _squadLabel = new LocalizedString(UI_COMMON_TABLE, SQUAD_DEFAULT_KEY);
            }

            if (!HasLocalizedValue(_inventoryTitleLabel))
            {
                _inventoryTitleLabel = new LocalizedString(UI_COMMON_TABLE, INVENTORY_TITLE_DEFAULT_KEY);
            }
        }

        private void ResolveLabelTargets()
        {
            if (_shopLabelTMP == null)
            {
                _shopLabelTMP = FindButtonLabel(_shopButtonObjectName);
            }

            if (_squadLabelTMP == null)
            {
                _squadLabelTMP = FindButtonLabel(_squadButtonObjectName);
            }
        }

        private void ResolveButtonTargets()
        {
            if (_shopButton == null)
            {
                _shopButton = FindButton(_shopButtonObjectName);
            }

            if (_squadButton == null)
            {
                _squadButton = FindButton(_squadButtonObjectName);
            }

            if (_inventoryButton == null)
            {
                _inventoryButton = FindSceneButton(_inventoryButtonObjectName);
            }

        }

        private void ResolveSquadPanel()
        {
            if (_squadPanel == null && _squadPanelCanvasGroup != null)
            {
                _squadPanel = ResolvePanelRootFromCanvasGroup(_squadPanelCanvasGroup);
            }

            if (_squadPanel == null)
            {
                _squadPanel = FindObjectByNameInSceneRoot(_squadPanelObjectName);
            }

            if (_squadPanel == null)
            {
                return;
            }

            if (_squadPanelCanvasGroup != null)
            {
                _squadPanelAnimatedRoot = _squadPanelCanvasGroup.transform;
            }
            else
            {
                _squadPanelAnimatedRoot = ResolveSquadPanelAnimatedRoot();
                if (_squadPanelAnimatedRoot == null)
                {
                    _squadPanelAnimatedRoot = _squadPanel.transform;
                }

                _squadPanelCanvasGroup = _squadPanelAnimatedRoot.GetComponent<CanvasGroup>();
                if (_squadPanelCanvasGroup == null)
                {
                    _squadPanelCanvasGroup = _squadPanelAnimatedRoot.gameObject.AddComponent<CanvasGroup>();
                }
            }

            if (!_squadPanelScaleCaptured)
            {
                Transform animatedRoot = _squadPanelAnimatedRoot != null ? _squadPanelAnimatedRoot : _squadPanel.transform;
                _squadPanelBaseScale = animatedRoot.localScale;
                if (_squadPanelBaseScale.sqrMagnitude < 0.000001f)
                {
                    // Some scenes hide UI panels by scaling to zero in the editor; treat that as "unknown base scale".
                    _squadPanelBaseScale = Vector3.one;
                }
                _squadPanelScaleCaptured = true;
            }

            if (_squadBackButton == null)
            {
                _squadBackButton = FindButtonInRoot(_squadPanel, _squadBackButtonObjectName);
                if (_squadBackButton == null)
                {
                    _squadBackButton = FindSceneButton(_squadBackButtonObjectName);
                }
            }

            EnsureSquadPanelStartupHidden();
        }

        private void ResolveInventoryTargets()
        {
            if (_inventoryButton == null)
            {
                _inventoryButton = FindSceneButton(_inventoryButtonObjectName);
            }

            ResolveInventoryPanel();
            if (_inventoryPanel == null)
            {
                return;
            }

            if (_inventoryBackButton == null)
            {
                _inventoryBackButton = FindButtonInRoot(_inventoryPanel, _inventoryBackButtonObjectName);
            }

            if (_inventoryTitleTMP == null)
            {
                _inventoryTitleTMP = FindTextInRoot(_inventoryPanel, _inventoryTitleObjectName);
            }
        }

        private void ResolveInventoryPanel()
        {
            if (_inventoryPanel == null && _inventoryPanelCanvasGroup != null)
            {
                _inventoryPanel = ResolvePanelRootFromCanvasGroup(_inventoryPanelCanvasGroup);
            }

            if (_inventoryPanel == null)
            {
                _inventoryPanel = FindObjectByNameInSceneRoot(_inventoryPanelObjectName);
            }

            if (_inventoryPanel == null)
            {
                return;
            }

            if (_inventoryPanelCanvasGroup != null)
            {
                _inventoryPanelAnimatedRoot = _inventoryPanelCanvasGroup.transform;
            }
            else
            {
                _inventoryPanelAnimatedRoot = ResolveInventoryPanelAnimatedRoot();
                if (_inventoryPanelAnimatedRoot == null)
                {
                    _inventoryPanelAnimatedRoot = _inventoryPanel.transform;
                }

                _inventoryPanelCanvasGroup = _inventoryPanelAnimatedRoot.GetComponent<CanvasGroup>();
                if (_inventoryPanelCanvasGroup == null)
                {
                    _inventoryPanelCanvasGroup = _inventoryPanelAnimatedRoot.gameObject.AddComponent<CanvasGroup>();
                }
            }

            if (!_inventoryPanelScaleCaptured)
            {
                Transform animatedRoot = _inventoryPanelAnimatedRoot != null ? _inventoryPanelAnimatedRoot : _inventoryPanel.transform;
                _inventoryPanelBaseScale = animatedRoot.localScale;
                if (_inventoryPanelBaseScale.sqrMagnitude < 0.000001f)
                {
                    _inventoryPanelBaseScale = Vector3.one;
                }

                _inventoryPanelScaleCaptured = true;
            }

            EnsureInventoryPanelStartupHidden();
        }

        private Transform ResolveSquadPanelAnimatedRoot()
        {
            if (_squadPanel == null)
            {
                return null;
            }

            // PreparationScene currently nests the visible UI under SquadPanel/Canvas.
            Transform directCanvas = _squadPanel.transform.Find("Canvas");
            if (directCanvas != null && directCanvas.GetComponent<Canvas>() != null)
            {
                return directCanvas;
            }

            Canvas anyCanvas = _squadPanel.GetComponentInChildren<Canvas>(true);
            if (anyCanvas != null)
            {
                return anyCanvas.transform;
            }

            return _squadPanel.transform;
        }

        private Transform ResolveInventoryPanelAnimatedRoot()
        {
            if (_inventoryPanel == null)
            {
                return null;
            }

            Transform directCanvas = _inventoryPanel.transform.Find("Canvas");
            if (directCanvas != null && directCanvas.GetComponent<Canvas>() != null)
            {
                return directCanvas;
            }

            Canvas anyCanvas = _inventoryPanel.GetComponentInChildren<Canvas>(true);
            if (anyCanvas != null)
            {
                return anyCanvas.transform;
            }

            return _inventoryPanel.transform;
        }

        private static GameObject ResolvePanelRootFromCanvasGroup(CanvasGroup canvasGroup)
        {
            if (canvasGroup == null)
            {
                return null;
            }

            Transform groupTransform = canvasGroup.transform;
            if (groupTransform == null)
            {
                return null;
            }

            return groupTransform.parent != null ? groupTransform.parent.gameObject : groupTransform.gameObject;
        }

        private GameObject FindObjectByNameInSceneRoot(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            Transform searchRoot = transform.root != null ? transform.root : transform;
            var transforms = searchRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform node = transforms[i];
                if (node != null && string.Equals(node.name, objectName, System.StringComparison.Ordinal))
                {
                    return node.gameObject;
                }
            }

            var global = GameObject.Find(objectName);
            return global;
        }

        private TMP_Text FindButtonLabel(string buttonObjectName)
        {
            if (string.IsNullOrWhiteSpace(buttonObjectName))
            {
                return null;
            }

            var buttonTransform = transform.Find(buttonObjectName);
            if (buttonTransform == null)
            {
                return null;
            }

            return buttonTransform.GetComponentInChildren<TMP_Text>(true);
        }

        private Button FindButton(string buttonObjectName)
        {
            if (string.IsNullOrWhiteSpace(buttonObjectName))
            {
                return null;
            }

            var buttonTransform = transform.Find(buttonObjectName);
            if (buttonTransform == null)
            {
                return null;
            }

            var button = buttonTransform.GetComponent<Button>();
            if (button != null)
            {
                return button;
            }

            return buttonTransform.GetComponentInChildren<Button>(true);
        }

        private Button FindSceneButton(string buttonObjectName)
        {
            if (string.IsNullOrWhiteSpace(buttonObjectName))
            {
                return null;
            }

            GameObject buttonObject = FindObjectByNameInSceneRoot(buttonObjectName);
            if (buttonObject == null)
            {
                return null;
            }

            var button = buttonObject.GetComponent<Button>();
            if (button != null)
            {
                return button;
            }

            return buttonObject.GetComponentInChildren<Button>(true);
        }

        private Button FindButtonInRoot(GameObject root, string buttonObjectName)
        {
            if (root == null || string.IsNullOrWhiteSpace(buttonObjectName))
            {
                return null;
            }

            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform node = transforms[i];
                if (node == null || !string.Equals(node.name, buttonObjectName, System.StringComparison.Ordinal))
                {
                    continue;
                }

                Button button = node.GetComponent<Button>();
                if (button != null)
                {
                    return button;
                }

                button = node.GetComponentInChildren<Button>(true);
                if (button != null)
                {
                    return button;
                }
            }

            return null;
        }

        private TMP_Text FindTextInRoot(GameObject root, string textObjectName)
        {
            if (root == null || string.IsNullOrWhiteSpace(textObjectName))
            {
                return null;
            }

            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform node = transforms[i];
                if (node == null || !string.Equals(node.name, textObjectName, System.StringComparison.Ordinal))
                {
                    continue;
                }

                TMP_Text text = node.GetComponent<TMP_Text>();
                if (text != null)
                {
                    return text;
                }

                text = node.GetComponentInChildren<TMP_Text>(true);
                if (text != null)
                {
                    return text;
                }
            }

            return null;
        }

        private void BindLabels()
        {
            if (_shopLabel != null)
            {
                _shopLabel.StringChanged += HandleShopLabelChanged;
            }

            if (_squadLabel != null)
            {
                _squadLabel.StringChanged += HandleSquadLabelChanged;
            }

            if (_inventoryTitleLabel != null)
            {
                _inventoryTitleLabel.StringChanged += HandleInventoryTitleChanged;
            }
        }

        private void UnbindLabels()
        {
            if (_shopLabel != null)
            {
                _shopLabel.StringChanged -= HandleShopLabelChanged;
            }

            if (_squadLabel != null)
            {
                _squadLabel.StringChanged -= HandleSquadLabelChanged;
            }

            if (_inventoryTitleLabel != null)
            {
                _inventoryTitleLabel.StringChanged -= HandleInventoryTitleChanged;
            }
        }

        private void RefreshLabels()
        {
            _shopLabel?.RefreshString();
            _squadLabel?.RefreshString();
            _inventoryTitleLabel?.RefreshString();
        }

        private void WireHoverFeedback()
        {
            UnwireHoverFeedback();
            RegisterButtonHover(_shopButton);
            RegisterButtonHover(_squadButton);
        }

        private void WireSquadPanelButton()
        {
            UnwireSquadPanelButton();
            if (_squadButton != null)
            {
                UnityAction openAction = HandleSquadButtonClicked;
                _squadButton.onClick.AddListener(openAction);
                _panelClickSubscriptions.Add(new ButtonClickSubscription(_squadButton, openAction));
            }

            if (_squadBackButton != null)
            {
                UnityAction closeAction = HandleSquadBackButtonClicked;
                _squadBackButton.onClick.AddListener(closeAction);
                _panelClickSubscriptions.Add(new ButtonClickSubscription(_squadBackButton, closeAction));
            }

            if (_inventoryButton != null)
            {
                UnityAction inventoryOpenAction = HandleInventoryButtonClicked;
                _inventoryButton.onClick.AddListener(inventoryOpenAction);
                _panelClickSubscriptions.Add(new ButtonClickSubscription(_inventoryButton, inventoryOpenAction));
            }

            if (_inventoryBackButton != null)
            {
                UnityAction inventoryCloseAction = HandleInventoryBackButtonClicked;
                _inventoryBackButton.onClick.AddListener(inventoryCloseAction);
                _panelClickSubscriptions.Add(new ButtonClickSubscription(_inventoryBackButton, inventoryCloseAction));
            }

            if (_panelClickSubscriptions.Count == 0)
            {
                return;
            }
        }

        private void UnwireHoverFeedback()
        {
            for (int i = 0; i < _hoverForwarders.Count; i++)
            {
                var forwarder = _hoverForwarders[i];
                if (forwarder != null)
                {
                    forwarder.SetOwner(null);
                }
            }

            _hoverForwarders.Clear();

            for (int i = 0; i < _clickSubscriptions.Count; i++)
            {
                var sub = _clickSubscriptions[i];
                if (sub.Button != null && sub.ClickAction != null)
                {
                    sub.Button.onClick.RemoveListener(sub.ClickAction);
                }
            }

            _clickSubscriptions.Clear();
            _hoveredButtonCount = 0;
        }

        private void UnwireSquadPanelButton()
        {
            for (int i = 0; i < _panelClickSubscriptions.Count; i++)
            {
                var sub = _panelClickSubscriptions[i];
                if (sub.Button != null && sub.ClickAction != null)
                {
                    sub.Button.onClick.RemoveListener(sub.ClickAction);
                }
            }

            _panelClickSubscriptions.Clear();
        }

        private void RegisterButtonHover(Button button)
        {
            if (button == null)
            {
                return;
            }

            if (ContainsSubscription(button))
            {
                return;
            }

            var forwarder = button.GetComponent<MenuButtonHoverForwarder>();
            if (forwarder == null)
            {
                forwarder = button.gameObject.AddComponent<MenuButtonHoverForwarder>();
            }

            UnityAction clickAction = HandleMenuButtonClicked;
            button.onClick.AddListener(clickAction);
            _clickSubscriptions.Add(new ButtonClickSubscription(button, clickAction));

            forwarder.SetOwner(this);
            _hoverForwarders.Add(forwarder);
        }

        private void HandleMenuButtonPointerEnter()
        {
            _hoveredButtonCount = Mathf.Max(0, _hoveredButtonCount + 1);
            ApplyHoverCursor();
        }

        private void HandleMenuButtonPointerExit()
        {
            _hoveredButtonCount = Mathf.Max(0, _hoveredButtonCount - 1);
            if (_hoveredButtonCount == 0)
            {
                RestoreDefaultCursor();
            }
        }

        private void ApplyHoverCursor()
        {
            if (_buttonHoverCursorTexture == null)
            {
                return;
            }

            Cursor.SetCursor(_buttonHoverCursorTexture, _buttonHoverCursorHotspot, CursorMode.Auto);
        }

        private void RestoreDefaultCursor()
        {
            Cursor.SetCursor(_defaultCursorTexture, _defaultCursorHotspot, CursorMode.Auto);
        }

        private bool IsPointerTopmostButton()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return false;
            }

            var probe = new PointerEventData(eventSystem)
            {
                position = Input.mousePosition
            };

            _raycastBuffer.Clear();
            eventSystem.RaycastAll(probe, _raycastBuffer);
            for (int i = 0; i < _raycastBuffer.Count; i++)
            {
                GameObject target = _raycastBuffer[i].gameObject;
                if (target == null || !target.activeInHierarchy)
                {
                    continue;
                }

                return target.GetComponentInParent<Button>() != null;
            }

            return false;
        }

        private void HandleMenuButtonClicked()
        {
            PlayClickSfx();
        }

        public bool TryGetSquadPortraitCursorProfile(
            out Texture2D defaultCursorTexture,
            out Vector2 defaultCursorHotspot,
            out Texture2D hoverCursorTexture,
            out Vector2 hoverCursorHotspot,
            out Texture2D dragCursorTexture,
            out Vector2 dragCursorHotspot)
        {
            Texture2D portraitHover = _portraitHoverCursorTexture != null ? _portraitHoverCursorTexture : _buttonHoverCursorTexture;
            Vector2 portraitHoverHotspot = _portraitHoverCursorTexture != null ? _portraitHoverCursorHotspot : _buttonHoverCursorHotspot;

            defaultCursorTexture = _defaultCursorTexture;
            defaultCursorHotspot = _defaultCursorHotspot;
            hoverCursorTexture = portraitHover;
            hoverCursorHotspot = portraitHoverHotspot;
            dragCursorTexture = _portraitDragCursorTexture != null ? _portraitDragCursorTexture : portraitHover;
            dragCursorHotspot = _portraitDragCursorTexture != null ? _portraitDragCursorHotspot : portraitHoverHotspot;

            return defaultCursorTexture != null || hoverCursorTexture != null || dragCursorTexture != null;
        }

        public bool IsSquadPanelVisible()
        {
            ResolveSquadPanel();
            if (_squadPanel == null || !_squadPanel.activeInHierarchy)
            {
                return false;
            }

            if (_squadPanelCanvasGroup == null)
            {
                return true;
            }

            return _squadPanelCanvasGroup.alpha > 0.001f && _squadPanelCanvasGroup.blocksRaycasts;
        }

        private void HandleSquadButtonClicked()
        {
            ResolveSquadPanel();
            ShowSquadPanel();
        }

        private void HandleSquadBackButtonClicked()
        {
            ResolveSquadPanel();
            PlayClickSfx();
            HideSquadPanel();
        }

        private void HandleInventoryButtonClicked()
        {
            ResolveSquadPanel();
            ResolveInventoryTargets();
            PlayClickSfx();
            HideSquadPanel();
            ShowInventoryPanel();
        }

        private void HandleInventoryBackButtonClicked()
        {
            ResolveSquadPanel();
            ResolveInventoryTargets();
            PlayClickSfx();
            HideInventoryPanel();
            ShowSquadPanel();
        }

        private void ShowSquadPanel()
        {
            if (_squadPanel == null || _squadPanelCanvasGroup == null)
            {
                return;
            }

            StopSquadPanelRoutine();

            if (!_squadPanel.activeSelf)
            {
                _squadPanel.SetActive(true);
            }

            float duration = Mathf.Max(0f, _squadPanelFadeDuration);
            if (duration <= 0.0001f)
            {
                SetSquadPanelVisibleImmediate();
                return;
            }

            _squadPanelRoutine = StartCoroutine(ShowSquadPanelRoutine(duration));
        }

        private void HideSquadPanel()
        {
            if (_squadPanel == null || _squadPanelCanvasGroup == null)
            {
                return;
            }

            StopSquadPanelRoutine();

            if (!_squadPanel.activeSelf || _squadPanelCanvasGroup.alpha <= 0.001f)
            {
                SetSquadPanelHiddenAndDisabledImmediate();
                return;
            }

            float duration = Mathf.Max(0f, _squadPanelFadeDuration);
            if (duration <= 0.0001f)
            {
                SetSquadPanelHiddenAndDisabledImmediate();
                return;
            }

            _squadPanelRoutine = StartCoroutine(HideSquadPanelRoutine(duration));
        }

        private System.Collections.IEnumerator ShowSquadPanelRoutine(float duration)
        {
            if (_squadPanel == null || _squadPanelCanvasGroup == null)
            {
                yield break;
            }

            Transform animatedRoot = _squadPanelAnimatedRoot != null ? _squadPanelAnimatedRoot : _squadPanel.transform;
            float fromAlpha = Mathf.Clamp01(_squadPanelCanvasGroup.alpha);
            Vector3 toScale = _squadPanelBaseScale;
            float startScaleFactor = Mathf.Clamp(_squadPanelStartScale, 0.8f, 1f);
            Vector3 fromScale = fromAlpha > 0.001f
                ? animatedRoot.localScale
                : new Vector3(toScale.x * startScaleFactor, toScale.y * startScaleFactor, toScale.z);

            _squadPanelCanvasGroup.alpha = fromAlpha;
            _squadPanelCanvasGroup.interactable = false;
            _squadPanelCanvasGroup.blocksRaycasts = true;
            animatedRoot.localScale = fromScale;

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float normalized = Mathf.Clamp01(t / duration);
                float eased = EvaluateRevealCurve(_squadPanelRevealCurve, normalized);
                _squadPanelCanvasGroup.alpha = Mathf.LerpUnclamped(fromAlpha, 1f, eased);
                animatedRoot.localScale = Vector3.LerpUnclamped(fromScale, toScale, eased);
                yield return null;
            }

            SetSquadPanelVisibleImmediate();
            _squadPanelRoutine = null;
        }

        private System.Collections.IEnumerator HideSquadPanelRoutine(float duration)
        {
            if (_squadPanel == null || _squadPanelCanvasGroup == null)
            {
                yield break;
            }

            if (!_squadPanel.activeSelf)
            {
                _squadPanel.SetActive(true);
            }

            Transform animatedRoot = _squadPanelAnimatedRoot != null ? _squadPanelAnimatedRoot : _squadPanel.transform;
            float fromAlpha = Mathf.Clamp01(_squadPanelCanvasGroup.alpha);
            Vector3 fromScale = animatedRoot.localScale;
            float startScaleFactor = Mathf.Clamp(_squadPanelStartScale, 0.8f, 1f);
            Vector3 toScale = new Vector3(
                _squadPanelBaseScale.x * startScaleFactor,
                _squadPanelBaseScale.y * startScaleFactor,
                _squadPanelBaseScale.z);

            _squadPanelCanvasGroup.alpha = fromAlpha;
            _squadPanelCanvasGroup.interactable = false;
            _squadPanelCanvasGroup.blocksRaycasts = true;

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float normalized = Mathf.Clamp01(t / duration);
                float eased = EvaluateRevealCurve(_squadPanelRevealCurve, normalized);
                _squadPanelCanvasGroup.alpha = Mathf.LerpUnclamped(fromAlpha, 0f, eased);
                animatedRoot.localScale = Vector3.LerpUnclamped(fromScale, toScale, eased);
                yield return null;
            }

            SetSquadPanelHiddenAndDisabledImmediate();
            _squadPanelRoutine = null;
        }

        private float EvaluateRevealCurve(AnimationCurve curve, float value)
        {
            float clamped = Mathf.Clamp01(value);
            if (curve == null || curve.length == 0)
            {
                // SmoothStep fallback keeps the reveal polished even when no custom curve is assigned.
                return clamped * clamped * (3f - (2f * clamped));
            }

            return curve.Evaluate(clamped);
        }

        private void StopSquadPanelRoutine()
        {
            if (_squadPanelRoutine != null)
            {
                StopCoroutine(_squadPanelRoutine);
                _squadPanelRoutine = null;
            }
        }

        private void SetSquadPanelVisibleImmediate()
        {
            if (_squadPanel == null || _squadPanelCanvasGroup == null)
            {
                return;
            }

            if (!_squadPanel.activeSelf)
            {
                _squadPanel.SetActive(true);
            }

            _squadPanelCanvasGroup.alpha = 1f;
            _squadPanelCanvasGroup.interactable = true;
            _squadPanelCanvasGroup.blocksRaycasts = true;
            Transform animatedRoot = _squadPanelAnimatedRoot != null ? _squadPanelAnimatedRoot : _squadPanel.transform;
            animatedRoot.localScale = _squadPanelBaseScale;
        }

        private void SetSquadPanelHiddenImmediate()
        {
            if (_squadPanelCanvasGroup == null)
            {
                return;
            }

            _squadPanelCanvasGroup.alpha = 0f;
            _squadPanelCanvasGroup.interactable = false;
            _squadPanelCanvasGroup.blocksRaycasts = false;
        }

        private void SetSquadPanelHiddenAndDisabledImmediate()
        {
            SetSquadPanelHiddenImmediate();
            if (_squadPanel != null && _squadPanel.activeSelf)
            {
                _squadPanel.SetActive(false);
            }

            Transform animatedRoot = _squadPanelAnimatedRoot != null ? _squadPanelAnimatedRoot : _squadPanel != null ? _squadPanel.transform : null;
            if (animatedRoot != null)
            {
                animatedRoot.localScale = _squadPanelBaseScale;
            }
        }

        private void EnsureSquadPanelStartupHidden()
        {
            if (_squadPanelStartupHiddenApplied)
            {
                return;
            }

            StopSquadPanelRoutine();
            SetSquadPanelHiddenImmediate();
            if (_squadPanel != null && _squadPanel.activeSelf)
            {
                _squadPanel.SetActive(false);
            }

            _squadPanelStartupHiddenApplied = true;
        }

        private void ShowInventoryPanel()
        {
            if (_inventoryPanel == null || _inventoryPanelCanvasGroup == null)
            {
                return;
            }

            StopInventoryPanelRoutine();

            if (!_inventoryPanel.activeSelf)
            {
                _inventoryPanel.SetActive(true);
            }

            float duration = Mathf.Max(0f, _inventoryPanelFadeDuration);
            if (duration <= 0.0001f)
            {
                SetInventoryPanelVisibleImmediate();
                return;
            }

            _inventoryPanelRoutine = StartCoroutine(ShowInventoryPanelRoutine(duration));
        }

        private void HideInventoryPanel()
        {
            if (_inventoryPanel == null || _inventoryPanelCanvasGroup == null)
            {
                return;
            }

            StopInventoryPanelRoutine();

            if (!_inventoryPanel.activeSelf || _inventoryPanelCanvasGroup.alpha <= 0.001f)
            {
                SetInventoryPanelHiddenAndDisabledImmediate();
                return;
            }

            float duration = Mathf.Max(0f, _inventoryPanelFadeDuration);
            if (duration <= 0.0001f)
            {
                SetInventoryPanelHiddenAndDisabledImmediate();
                return;
            }

            _inventoryPanelRoutine = StartCoroutine(HideInventoryPanelRoutine(duration));
        }

        private System.Collections.IEnumerator ShowInventoryPanelRoutine(float duration)
        {
            if (_inventoryPanel == null || _inventoryPanelCanvasGroup == null)
            {
                yield break;
            }

            Transform animatedRoot = _inventoryPanelAnimatedRoot != null ? _inventoryPanelAnimatedRoot : _inventoryPanel.transform;
            float fromAlpha = Mathf.Clamp01(_inventoryPanelCanvasGroup.alpha);
            Vector3 toScale = _inventoryPanelBaseScale;
            float startScaleFactor = Mathf.Clamp(_inventoryPanelStartScale, 0.8f, 1f);
            Vector3 fromScale = fromAlpha > 0.001f
                ? animatedRoot.localScale
                : new Vector3(toScale.x * startScaleFactor, toScale.y * startScaleFactor, toScale.z);

            _inventoryPanelCanvasGroup.alpha = fromAlpha;
            _inventoryPanelCanvasGroup.interactable = false;
            _inventoryPanelCanvasGroup.blocksRaycasts = true;
            animatedRoot.localScale = fromScale;

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float normalized = Mathf.Clamp01(t / duration);
                float eased = EvaluateRevealCurve(_inventoryPanelRevealCurve, normalized);
                _inventoryPanelCanvasGroup.alpha = Mathf.LerpUnclamped(fromAlpha, 1f, eased);
                animatedRoot.localScale = Vector3.LerpUnclamped(fromScale, toScale, eased);
                yield return null;
            }

            SetInventoryPanelVisibleImmediate();
            _inventoryPanelRoutine = null;
        }

        private System.Collections.IEnumerator HideInventoryPanelRoutine(float duration)
        {
            if (_inventoryPanel == null || _inventoryPanelCanvasGroup == null)
            {
                yield break;
            }

            if (!_inventoryPanel.activeSelf)
            {
                _inventoryPanel.SetActive(true);
            }

            Transform animatedRoot = _inventoryPanelAnimatedRoot != null ? _inventoryPanelAnimatedRoot : _inventoryPanel.transform;
            float fromAlpha = Mathf.Clamp01(_inventoryPanelCanvasGroup.alpha);
            Vector3 fromScale = animatedRoot.localScale;
            float startScaleFactor = Mathf.Clamp(_inventoryPanelStartScale, 0.8f, 1f);
            Vector3 toScale = new Vector3(
                _inventoryPanelBaseScale.x * startScaleFactor,
                _inventoryPanelBaseScale.y * startScaleFactor,
                _inventoryPanelBaseScale.z);

            _inventoryPanelCanvasGroup.alpha = fromAlpha;
            _inventoryPanelCanvasGroup.interactable = false;
            _inventoryPanelCanvasGroup.blocksRaycasts = true;

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float normalized = Mathf.Clamp01(t / duration);
                float eased = EvaluateRevealCurve(_inventoryPanelRevealCurve, normalized);
                _inventoryPanelCanvasGroup.alpha = Mathf.LerpUnclamped(fromAlpha, 0f, eased);
                animatedRoot.localScale = Vector3.LerpUnclamped(fromScale, toScale, eased);
                yield return null;
            }

            SetInventoryPanelHiddenAndDisabledImmediate();
            _inventoryPanelRoutine = null;
        }

        private void StopInventoryPanelRoutine()
        {
            if (_inventoryPanelRoutine != null)
            {
                StopCoroutine(_inventoryPanelRoutine);
                _inventoryPanelRoutine = null;
            }
        }

        private void SetInventoryPanelVisibleImmediate()
        {
            if (_inventoryPanel == null || _inventoryPanelCanvasGroup == null)
            {
                return;
            }

            if (!_inventoryPanel.activeSelf)
            {
                _inventoryPanel.SetActive(true);
            }

            _inventoryPanelCanvasGroup.alpha = 1f;
            _inventoryPanelCanvasGroup.interactable = true;
            _inventoryPanelCanvasGroup.blocksRaycasts = true;
            Transform animatedRoot = _inventoryPanelAnimatedRoot != null ? _inventoryPanelAnimatedRoot : _inventoryPanel.transform;
            animatedRoot.localScale = _inventoryPanelBaseScale;
        }

        private void SetInventoryPanelHiddenImmediate()
        {
            if (_inventoryPanelCanvasGroup == null)
            {
                return;
            }

            _inventoryPanelCanvasGroup.alpha = 0f;
            _inventoryPanelCanvasGroup.interactable = false;
            _inventoryPanelCanvasGroup.blocksRaycasts = false;
        }

        private void SetInventoryPanelHiddenAndDisabledImmediate()
        {
            SetInventoryPanelHiddenImmediate();
            if (_inventoryPanel != null && _inventoryPanel.activeSelf)
            {
                _inventoryPanel.SetActive(false);
            }

            Transform animatedRoot = _inventoryPanelAnimatedRoot != null ? _inventoryPanelAnimatedRoot : _inventoryPanel != null ? _inventoryPanel.transform : null;
            if (animatedRoot != null)
            {
                animatedRoot.localScale = _inventoryPanelBaseScale;
            }
        }

        private void EnsureInventoryPanelStartupHidden()
        {
            if (_inventoryPanelStartupHiddenApplied)
            {
                return;
            }

            StopInventoryPanelRoutine();
            SetInventoryPanelHiddenImmediate();
            if (_inventoryPanel != null && _inventoryPanel.activeSelf)
            {
                _inventoryPanel.SetActive(false);
            }

            _inventoryPanelStartupHiddenApplied = true;
        }

        private void PlayClickSfx()
        {
            if (_clickSfxClip == null)
            {
                return;
            }

            if (Time.unscaledTime - _lastClickSfxTime < Mathf.Max(0f, _clickSfxCooldown))
            {
                return;
            }

            float volume = Mathf.Clamp(_clickSfxVolume, 0f, 1.5f);
            if (_clickAudioSource != null)
            {
                _clickAudioSource.PlayOneShot(_clickSfxClip, volume);
            }
            else
            {
                AudioSource.PlayClipAtPoint(_clickSfxClip, Vector3.zero, volume);
            }

            _lastClickSfxTime = Time.unscaledTime;
        }

        private bool ContainsSubscription(Button button)
        {
            for (int i = 0; i < _clickSubscriptions.Count; i++)
            {
                if (_clickSubscriptions[i].Button == button)
                {
                    return true;
                }
            }

            return false;
        }

        private void HandleShopLabelChanged(string localizedValue)
        {
            LocalizationCacheDiagnostics.LogDisplay(_shopLabel, "PreparationPopupMenu.ShopLabel", this);
            if (_shopLabelTMP != null && !string.IsNullOrWhiteSpace(localizedValue))
            {
                _shopLabelTMP.text = localizedValue;
            }
        }

        private void HandleSquadLabelChanged(string localizedValue)
        {
            LocalizationCacheDiagnostics.LogDisplay(_squadLabel, "PreparationPopupMenu.SquadLabel", this);
            if (_squadLabelTMP != null && !string.IsNullOrWhiteSpace(localizedValue))
            {
                _squadLabelTMP.text = localizedValue;
            }
        }

        private void HandleInventoryTitleChanged(string localizedValue)
        {
            LocalizationCacheDiagnostics.LogDisplay(_inventoryTitleLabel, "PreparationPopupMenu.InventoryTitle", this);
            if (_inventoryTitleTMP != null && !string.IsNullOrWhiteSpace(localizedValue))
            {
                _inventoryTitleTMP.text = localizedValue;
            }
        }

        private static bool HasLocalizedValue(LocalizedString localized)
        {
            return localized != null && !localized.IsEmpty;
        }

        private sealed class MenuButtonHoverForwarder : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            private PreparationPopupMenuLocalizationController _owner;

            public void SetOwner(PreparationPopupMenuLocalizationController owner)
            {
                _owner = owner;
            }

            public void OnPointerEnter(PointerEventData eventData)
            {
                _owner?.HandleMenuButtonPointerEnter();
            }

            public void OnPointerExit(PointerEventData eventData)
            {
                _owner?.HandleMenuButtonPointerExit();
            }

            private void OnDisable()
            {
                _owner?.HandleMenuButtonPointerExit();
            }
        }

        private readonly struct ButtonClickSubscription
        {
            public readonly Button Button;
            public readonly UnityAction ClickAction;

            public ButtonClickSubscription(Button button, UnityAction clickAction)
            {
                Button = button;
                ClickAction = clickAction;
            }
        }
    }
}
