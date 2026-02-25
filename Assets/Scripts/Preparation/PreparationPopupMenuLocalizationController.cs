using System.Collections.Generic;
using SevenBattles.Core;
using SevenBattles.Core.Battle;
using SevenBattles.Core.Items;
using SevenBattles.Core.Players;
using SevenBattles.Core.Units;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;
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
        private const string PANEL_SWITCH_OVERLAY_RUNTIME_NAME = "PreparationPanelSwitchOverlay_Runtime";
        private const string TOURNAMENT_PATH_PREVIEW_OBJECT_NAME = "TournamentPathPreview";
        private const string INVENTORY_ITEMS_CONTENT_PATH = "Canvas/InventoryView/Right_Panel/ScrollRect/Viewport/Content";

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
        [SerializeField, Tooltip("If enabled, forces InventoryPanel canvas to Screen Space - Camera for world sprite preview support.")]
        private bool _inventoryPanelForceCameraRenderMode = true;
        [SerializeField, Min(0.31f), Tooltip("Plane distance used when InventoryPanel canvas is in Screen Space - Camera.")]
        private float _inventoryPanelCameraPlaneDistance = 1f;
        [SerializeField, Tooltip("Optional explicit camera for InventoryPanel canvas. Falls back to Main Camera when empty.")]
        private Camera _inventoryPanelRenderCamera;
        [SerializeField, Min(0f), Tooltip("Reveal duration in seconds for the Inventory panel. Uses unscaled time.")]
        private float _inventoryPanelFadeDuration = 0.24f;
        [SerializeField, Range(0.8f, 1f), Tooltip("Starting scale multiplier used during Inventory panel reveal.")]
        private float _inventoryPanelStartScale = 0.95f;
        [SerializeField, Tooltip("Easing curve used for Inventory panel reveal.")]
        private AnimationCurve _inventoryPanelRevealCurve = null;

        [Header("Inventory Items List")]
        [SerializeField, Tooltip("Presenter that populates InventoryView item tiles. Auto-added when missing.")]
        private PreparationInventoryListPresenter _inventoryListPresenter;
        [SerializeField, Tooltip("Optional explicit Content root for inventory item tiles. Auto-found under InventoryPanel when null.")]
        private RectTransform _inventoryItemsContentRoot;
        [SerializeField, Tooltip("Optional Item prefab used when pool growth is needed.")]
        private GameObject _inventoryItemPrefab;
        [SerializeField, Tooltip("Optional ItemEmpty prefab used for empty inventory slots.")]
        private GameObject _inventoryItemEmptyPrefab;
        [SerializeField, Tooltip("Optional explicit page-buttons root under InventoryView used for pagination buttons. Auto-found when null.")]
        private RectTransform _inventoryPageButtonsRoot;
        [SerializeField, Tooltip("Object name used to auto-find the page-buttons root under InventoryPanel.")]
        private string _inventoryPageButtonsRootObjectName = "Pages";
        [SerializeField, Tooltip("Optional Page button prefab used to dynamically build page buttons.")]
        private GameObject _inventoryPageButtonPrefab;
        [SerializeField, Tooltip("Optional registry for equipment icon/background lookup.")]
        private EquipmentDefinitionRegistry _equipmentDefinitionRegistry;
        [SerializeField, Tooltip("Optional registry for item icon/background lookup.")]
        private ItemDefinitionRegistry _itemDefinitionRegistry;

        [Header("Panel Switch FX")]
        [SerializeField, Tooltip("Optional overlay CanvasGroup used during Squad <-> Inventory transitions. Runtime-created when null.")]
        private CanvasGroup _panelSwitchOverlayCanvasGroup;
        [SerializeField, Tooltip("Optional Image paired with the overlay CanvasGroup. Runtime-created when null.")]
        private Image _panelSwitchOverlayImage;
        [SerializeField, Tooltip("Overlay tint used for the cinematic panel switch veil.")]
        private Color _panelSwitchOverlayColor = Color.black;
        [SerializeField, Range(0f, 1f), Tooltip("Peak alpha reached by the panel switch veil.")]
        private float _panelSwitchOverlayPeakAlpha = 0.78f;
        [SerializeField, Min(0f), Tooltip("Duration in seconds for each half of the panel switch transition. Uses unscaled time.")]
        private float _panelSwitchHalfDuration = 0.1f;
        [SerializeField, Range(1f, 1.2f), Tooltip("Slight overshoot scale applied to the incoming panel for a premium reveal.")]
        private float _panelSwitchIncomingOvershootScale = 1.02f;
        [SerializeField, Tooltip("Easing curve used for the panel switch veil and incoming reveal.")]
        private AnimationCurve _panelSwitchCurve = null;
        [SerializeField, Min(0.2f), Tooltip("Multiplier applied to slide distance based on panel width.")]
        private float _panelSwitchSlideDistanceMultiplier = 1.05f;
        [SerializeField, Range(0f, 1f), Tooltip("How far the outgoing panel drifts to the left relative to full slide distance.")]
        private float _panelSwitchOutgoingSlideRatio = 0.2f;

        [Header("Inventory Unit Preview")]
        [SerializeField, Tooltip("Optional explicit reference to the squad setup controller used to track the currently selected unit.")]
        private SquadSetupController _squadSetupController;
        [SerializeField, Tooltip("Optional explicit transform where the selected unit prefab is instantiated in InventoryView.")]
        private RectTransform _inventoryUnitPreviewAnchor;
        [SerializeField, Tooltip("Object name used to auto-find the selected unit preview anchor under InventoryPanel.")]
        private string _inventoryUnitPreviewAnchorObjectName = "CharacterBgBottom";
        [SerializeField, Tooltip("Optional TMP label under InventoryView/Character used to display the currently selected unit name.")]
        private TMP_Text _inventorySelectedUnitNameTMP;
        [SerializeField, Tooltip("Object name used to auto-find the selected unit name label under InventoryPanel.")]
        private string _inventorySelectedUnitNameObjectName = "UnitName";
        [SerializeField, Tooltip("Fallback object name used when the primary selected unit name label cannot be found.")]
        private string _inventorySelectedUnitNameFallbackObjectName = "NameText";
        [SerializeField, Tooltip("Optional root Transform containing Inventory selected-unit stat rows (Life/Attack/...).")]
        private Transform _inventorySelectedUnitStatsRoot;
        [SerializeField, Tooltip("Object name used to auto-find the Inventory selected-unit stats root under InventoryPanel.")]
        private string _inventorySelectedUnitStatsRootObjectName = "Stats";
        [SerializeField, Tooltip("Fallback object name used when the primary Inventory stats root cannot be found.")]
        private string _inventorySelectedUnitStatsRootFallbackObjectName = "Stats2";
        [SerializeField, Tooltip("Child object name under each stat row used to resolve the numeric TMP label.")]
        private string _inventorySelectedUnitStatValueObjectName = "Value";
        [SerializeField, Tooltip("Child object name under each stat row used to resolve the label TMP (Life, Attack, ...).")]
        private string _inventorySelectedUnitStatLabelObjectName = "Label";
        [SerializeField, Tooltip("Optional TMP label under InventoryView used to display the selected unit level number.")]
        private TMP_Text _inventorySelectedUnitLevelTMP;
        [SerializeField, Tooltip("Object name used to auto-find the selected unit level label under InventoryPanel.")]
        private string _inventorySelectedUnitLevelObjectName = "TextLevelNum";
        [SerializeField, Tooltip("Optional Slider under InventoryView used to display selected unit XP progress to next level.")]
        private Slider _inventorySelectedUnitXpSlider;
        [SerializeField, Tooltip("Object name used to auto-find the selected unit XP Slider under InventoryPanel.")]
        private string _inventorySelectedUnitXpSliderObjectName = "Slider";
        [SerializeField, Tooltip("Optional TMP label under InventoryView used to display selected unit XP text (current/next).")]
        private TMP_Text _inventorySelectedUnitXpTextTMP;
        [SerializeField, Tooltip("Object name used to auto-find the selected unit XP text label under InventoryPanel.")]
        private string _inventorySelectedUnitXpTextObjectName = "TextExp";
        [SerializeField, Tooltip("Local position offset applied to the spawned selected unit preview prefab.")]
        private Vector3 _inventoryUnitPreviewLocalPosition = Vector3.zero;
        [SerializeField, Tooltip("Local scale applied to the spawned selected unit preview prefab.")]
        private Vector3 _inventoryUnitPreviewLocalScale = Vector3.one;
        [SerializeField, Tooltip("If enabled, auto-fits preview scale to occupy a readable portion of CharacterBgBottom on screen.")]
        private bool _inventoryUnitPreviewAutoFitScale = true;
        [SerializeField, Range(0.2f, 1.2f), Tooltip("Target vertical occupancy of CharacterBgBottom used by auto-fit scale.")]
        private float _inventoryUnitPreviewAutoFitFill = 0.78f;
        [SerializeField, Min(0.01f), Tooltip("Minimum multiplier allowed when auto-fitting preview scale.")]
        private float _inventoryUnitPreviewAutoFitMinScaleMultiplier = 0.1f;
        [SerializeField, Min(0.1f), Tooltip("Maximum multiplier allowed when auto-fitting preview scale.")]
        private float _inventoryUnitPreviewAutoFitMaxScaleMultiplier = 500f;
        [SerializeField, Tooltip("If enabled, places the unit preview as the first child under CharacterBgBottom.")]
        private bool _inventoryUnitPreviewAsFirstSibling = false;
        [SerializeField, Tooltip("Sorting layer applied to the inventory unit preview sprites.")]
        private string _inventoryUnitPreviewSortingLayer = "Default";
        [SerializeField, Tooltip("Sorting order applied to the inventory unit preview sprites.")]
        private int _inventoryUnitPreviewSortingOrder = 2000;
        [SerializeField, Tooltip("Optional explicit world transform parent used to host the inventory unit preview instance.")]
        private Transform _inventoryUnitPreviewWorldRoot;
        [SerializeField, Tooltip("Additional world-space offset applied after anchoring the unit preview to CharacterBgBottom.")]
        private Vector3 _inventoryUnitPreviewWorldOffset = Vector3.zero;
        [SerializeField, Tooltip("Depth offset applied from the inventory canvas plane when converting the anchor to world-space. Negative values bring the unit closer to camera.")]
        private float _inventoryUnitPreviewPlaneDepthOffset = -0.06f;
        [SerializeField, Tooltip("If enabled, logs detailed diagnostics for inventory preview spawn and placement.")]
        private bool _inventoryUnitPreviewDiagnostics = false;

        [Header("Inventory Backdrop")]
        [SerializeField, Tooltip("If enabled, hides TournamentPathPreview while Inventory panel is visible to avoid map bleed-through.")]
        private bool _hideTournamentPathWhileInventoryVisible = true;
        [SerializeField, Tooltip("Optional explicit reference to TournamentPathPreview root. Auto-found when empty.")]
        private GameObject _tournamentPathPreviewRoot;

        [Header("Menu HUD Visibility")]
        [SerializeField, Tooltip("If enabled, hides the Shop/Squad popup HUD while Inventory is visible.")]
        private bool _hidePopupMenuWhileInventoryVisible = true;
        [SerializeField, Tooltip("Optional CanvasGroup controlling the popup HUD visibility. Auto-added on PopupMenu root when empty.")]
        private CanvasGroup _popupMenuCanvasGroup;

        [Header("Preparation Resources Visibility")]
        [SerializeField, Tooltip("If enabled, hides the default preparation ResourcesPanel while Inventory is visible.")]
        private bool _hidePreparationResourcesWhileInventoryVisible = true;
        [SerializeField, Tooltip("Optional explicit reference to the default preparation ResourcesPanel root (not the one inside InventoryPanel).")]
        private GameObject _preparationResourcesPanelRoot;
        [SerializeField, Tooltip("Object name used to auto-find the default preparation ResourcesPanel root when _preparationResourcesPanelRoot is not assigned.")]
        private string _preparationResourcesPanelObjectName = "ResourcesPanel";
        [SerializeField, Tooltip("CanvasGroup controlling the default preparation ResourcesPanel visibility. Auto-added when missing.")]
        private CanvasGroup _preparationResourcesPanelCanvasGroup;

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
        private Coroutine _panelSwitchRoutine;
        private Vector3 _squadPanelBaseScale = Vector3.one;
        private Vector3 _inventoryPanelBaseScale = Vector3.one;
        private bool _squadPanelScaleCaptured;
        private bool _inventoryPanelScaleCaptured;
        private bool _squadPanelStartupHiddenApplied;
        private bool _inventoryPanelStartupHiddenApplied;
        private Transform _squadPanelAnimatedRoot;
        private Transform _inventoryPanelAnimatedRoot;
        private Canvas _panelSwitchOverlayRuntimeCanvas;
        private ISquadSetupController _resolvedSquadSetupController;
        private bool _inventoryUnitPreviewEventsWired;
        private UnitSpellLoadout _inventorySelectedLoadout;
        private Coroutine _inventoryUnitPreviewRoutine;
        private GameObject _inventoryUnitPreviewInstance;
        private GameObject _inventoryUnitPreviewPrefab;
        private bool _tournamentPathPreviewInitialVisibility = true;
        private bool _tournamentPathPreviewVisibilityCaptured;
        private bool _tournamentPathPreviewMissingLogged;
        private bool _popupMenuVisibilityCaptured;
        private float _popupMenuInitialAlpha = 1f;
        private bool _popupMenuInitialInteractable = true;
        private bool _popupMenuInitialBlocksRaycasts = true;
        private bool _preparationResourcesVisibilityCaptured;
        private float _preparationResourcesInitialAlpha = 1f;
        private bool _preparationResourcesInitialInteractable = true;
        private bool _preparationResourcesInitialBlocksRaycasts = true;
        private float _lastInventoryPreviewPlacementInfoTime = -999f;
        private float _lastInventoryPreviewPlacementWarnTime = -999f;
        private readonly List<GameObject> _sceneRootSearchBuffer = new List<GameObject>(16);

        private const float INVENTORY_PREVIEW_WARN_LOG_COOLDOWN_SECONDS = 0.75f;
        private const float INVENTORY_PREVIEW_INFO_LOG_COOLDOWN_SECONDS = 0.75f;

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
            ResolveSquadSetupController();
            ResolveTournamentPathPreviewRoot();
            ResolvePopupMenuCanvasGroup();
            ResolvePreparationResourcesPanelCanvasGroup();
            BindLabels();
            SubscribeLocaleChanged();
            WireHoverFeedback();
            WireSquadPanelButton();
            WireInventoryUnitPreviewSelection();
            RefreshInventorySelectedUnitPreview();
            RefreshLabels();
        }

        private void OnDisable()
        {
            UnsubscribeLocaleChanged();
            UnbindLabels();
            UnwireHoverFeedback();
            UnwireSquadPanelButton();
            UnwireInventoryUnitPreviewSelection();
            StopSquadPanelRoutine();
            StopInventoryPanelRoutine();
            StopPanelSwitchRoutine();
            HidePanelSwitchOverlayImmediate();
            ClearInventoryUnitPreview();
            RestoreTournamentPathPreviewVisibility();
            RestorePopupMenuVisibility();
            RestorePreparationResourcesPanelVisibility();
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

            UpdateInventoryPreviewWorldPlacement();
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

            ResolveInventorySelectedUnitNameLabel();
            ResolveInventorySelectedUnitStatsRoot();
            ResolveInventorySelectedUnitProgressionTargets();
            ResolveInventoryUnitPreviewAnchor();
            EnsureInventoryListPresenter();
        }

        private void EnsureInventoryListPresenter()
        {
            if (_inventoryListPresenter == null)
            {
                _inventoryListPresenter = GetComponent<PreparationInventoryListPresenter>();
            }

            if (_inventoryListPresenter == null)
            {
                _inventoryListPresenter = gameObject.AddComponent<PreparationInventoryListPresenter>();
            }

            ResolveInventoryItemsContentRoot();
            ResolveInventoryPageButtonsRoot();

            _inventoryListPresenter.Configure(
                ResolveCurrentPlayerContext(),
                _inventoryPanel,
                _inventoryItemsContentRoot,
                _inventoryItemPrefab,
                _inventoryItemEmptyPrefab,
                _inventoryPageButtonsRoot,
                _inventoryPageButtonPrefab,
                _equipmentDefinitionRegistry,
                _itemDefinitionRegistry);
        }

        private void ResolveInventoryItemsContentRoot()
        {
            if (_inventoryItemsContentRoot != null)
            {
                return;
            }

            if (_inventoryPanel == null)
            {
                return;
            }

            Transform content = _inventoryPanel.transform.Find(INVENTORY_ITEMS_CONTENT_PATH);
            _inventoryItemsContentRoot = content as RectTransform;
        }

        private void ResolveInventoryPageButtonsRoot()
        {
            if (_inventoryPageButtonsRoot != null || _inventoryPanel == null)
            {
                return;
            }

            _inventoryPageButtonsRoot = FindRectTransformInRoot(_inventoryPanel, _inventoryPageButtonsRootObjectName);
        }

        private PlayerContext ResolveCurrentPlayerContext()
        {
            if (PlayerContext.HasRuntimeInstance && PlayerContext.RuntimeInstance != null)
            {
                return PlayerContext.RuntimeInstance;
            }

            var contexts = Resources.FindObjectsOfTypeAll<PlayerContext>();
            if (contexts != null && contexts.Length > 0)
            {
                return contexts[0];
            }

            return null;
        }

        private void ResolveSquadSetupController()
        {
            if (_resolvedSquadSetupController != null)
            {
                return;
            }

            if (_squadSetupController == null)
            {
                _squadSetupController = UnityEngine.Object.FindFirstObjectByType<SquadSetupController>();
            }

            _resolvedSquadSetupController = _squadSetupController;
        }

        private void ResolveInventoryUnitPreviewAnchor()
        {
            if (_inventoryUnitPreviewAnchor != null)
            {
                return;
            }

            if (_inventoryPanel != null)
            {
                _inventoryUnitPreviewAnchor = FindRectTransformInRoot(_inventoryPanel, _inventoryUnitPreviewAnchorObjectName);
            }

            if (_inventoryUnitPreviewAnchor != null)
            {
                return;
            }

            GameObject anchorObject = FindObjectByNameInSceneRoot(_inventoryUnitPreviewAnchorObjectName);
            if (anchorObject != null)
            {
                _inventoryUnitPreviewAnchor = anchorObject.GetComponent<RectTransform>();
            }
        }

        private void ResolveInventorySelectedUnitNameLabel()
        {
            if (_inventorySelectedUnitNameTMP != null)
            {
                return;
            }

            if (_inventoryPanel != null)
            {
                _inventorySelectedUnitNameTMP = FindTextInRoot(_inventoryPanel, _inventorySelectedUnitNameObjectName);
                if (_inventorySelectedUnitNameTMP == null &&
                    !string.IsNullOrWhiteSpace(_inventorySelectedUnitNameFallbackObjectName) &&
                    !string.Equals(_inventorySelectedUnitNameObjectName, _inventorySelectedUnitNameFallbackObjectName, System.StringComparison.Ordinal))
                {
                    _inventorySelectedUnitNameTMP = FindTextInRoot(_inventoryPanel, _inventorySelectedUnitNameFallbackObjectName);
                }
            }

            if (_inventorySelectedUnitNameTMP != null)
            {
                return;
            }

            _inventorySelectedUnitNameTMP = FindTextByNameInSceneRoot(_inventorySelectedUnitNameObjectName);
            if (_inventorySelectedUnitNameTMP != null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(_inventorySelectedUnitNameFallbackObjectName) &&
                !string.Equals(_inventorySelectedUnitNameObjectName, _inventorySelectedUnitNameFallbackObjectName, System.StringComparison.Ordinal))
            {
                _inventorySelectedUnitNameTMP = FindTextByNameInSceneRoot(_inventorySelectedUnitNameFallbackObjectName);
            }
        }

        private void ResolveInventorySelectedUnitStatsRoot()
        {
            if (_inventorySelectedUnitStatsRoot != null)
            {
                return;
            }

            if (_inventoryPanel != null)
            {
                _inventorySelectedUnitStatsRoot = FindTransformInRoot(_inventoryPanel, _inventorySelectedUnitStatsRootObjectName);
                if (_inventorySelectedUnitStatsRoot == null &&
                    !string.IsNullOrWhiteSpace(_inventorySelectedUnitStatsRootFallbackObjectName) &&
                    !string.Equals(_inventorySelectedUnitStatsRootObjectName, _inventorySelectedUnitStatsRootFallbackObjectName, System.StringComparison.Ordinal))
                {
                    _inventorySelectedUnitStatsRoot = FindTransformInRoot(_inventoryPanel, _inventorySelectedUnitStatsRootFallbackObjectName);
                }
            }

            if (_inventorySelectedUnitStatsRoot != null)
            {
                return;
            }

            _inventorySelectedUnitStatsRoot = FindTransformByNameInSceneRoot(_inventorySelectedUnitStatsRootObjectName);
            if (_inventorySelectedUnitStatsRoot != null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(_inventorySelectedUnitStatsRootFallbackObjectName) &&
                !string.Equals(_inventorySelectedUnitStatsRootObjectName, _inventorySelectedUnitStatsRootFallbackObjectName, System.StringComparison.Ordinal))
            {
                _inventorySelectedUnitStatsRoot = FindTransformByNameInSceneRoot(_inventorySelectedUnitStatsRootFallbackObjectName);
            }
        }

        private void ResolveInventorySelectedUnitProgressionTargets()
        {
            ResolveInventorySelectedUnitLevelLabel();
            ResolveInventorySelectedUnitXpTextLabel();
            ResolveInventorySelectedUnitXpSlider();
        }

        private void ResolveInventorySelectedUnitLevelLabel()
        {
            if (_inventorySelectedUnitLevelTMP != null)
            {
                return;
            }

            if (_inventoryPanel != null)
            {
                _inventorySelectedUnitLevelTMP = FindTextInRoot(_inventoryPanel, _inventorySelectedUnitLevelObjectName);
            }

            if (_inventorySelectedUnitLevelTMP != null)
            {
                return;
            }

            _inventorySelectedUnitLevelTMP = FindTextByNameInSceneRoot(_inventorySelectedUnitLevelObjectName);
        }

        private void ResolveInventorySelectedUnitXpTextLabel()
        {
            if (_inventorySelectedUnitXpTextTMP != null)
            {
                return;
            }

            if (_inventoryPanel != null)
            {
                _inventorySelectedUnitXpTextTMP = FindTextInRoot(_inventoryPanel, _inventorySelectedUnitXpTextObjectName);
            }

            if (_inventorySelectedUnitXpTextTMP != null)
            {
                return;
            }

            _inventorySelectedUnitXpTextTMP = FindTextByNameInSceneRoot(_inventorySelectedUnitXpTextObjectName);
        }

        private void ResolveInventorySelectedUnitXpSlider()
        {
            if (_inventorySelectedUnitXpSlider != null)
            {
                return;
            }

            if (_inventoryPanel != null)
            {
                _inventorySelectedUnitXpSlider = FindSliderInRoot(_inventoryPanel, _inventorySelectedUnitXpSliderObjectName);
            }

            if (_inventorySelectedUnitXpSlider != null)
            {
                return;
            }

            _inventorySelectedUnitXpSlider = FindSliderByNameInSceneRoot(_inventorySelectedUnitXpSliderObjectName);
        }

        private void ResolveTournamentPathPreviewRoot()
        {
            if (_tournamentPathPreviewRoot == null)
            {
                _tournamentPathPreviewRoot = FindObjectByNameInSceneRoot(TOURNAMENT_PATH_PREVIEW_OBJECT_NAME);
            }

            if (_tournamentPathPreviewRoot == null)
            {
                if (_inventoryUnitPreviewDiagnostics && !_tournamentPathPreviewMissingLogged)
                {
                    SBLog.Warn(
                        $"PreparationPopupMenu: '{TOURNAMENT_PATH_PREVIEW_OBJECT_NAME}' was not found. Tournament path visibility cannot be toggled for Inventory view.",
                        this);
                    _tournamentPathPreviewMissingLogged = true;
                }

                return;
            }

            if (_tournamentPathPreviewRoot != null && !_tournamentPathPreviewVisibilityCaptured)
            {
                _tournamentPathPreviewInitialVisibility = _tournamentPathPreviewRoot.activeSelf;
                _tournamentPathPreviewVisibilityCaptured = true;
            }
        }

        private void SetTournamentPathPreviewVisible(bool visible)
        {
            if (!_hideTournamentPathWhileInventoryVisible)
            {
                return;
            }

            ResolveTournamentPathPreviewRoot();
            if (_tournamentPathPreviewRoot == null || _tournamentPathPreviewRoot.activeSelf == visible)
            {
                return;
            }

            _tournamentPathPreviewRoot.SetActive(visible);
            if (_inventoryUnitPreviewDiagnostics)
            {
                SBLog.Info($"PreparationPopupMenu: TournamentPathPreview visibility -> {visible}.", this);
            }
        }

        private void RestoreTournamentPathPreviewVisibility()
        {
            if (!_hideTournamentPathWhileInventoryVisible)
            {
                return;
            }

            ResolveTournamentPathPreviewRoot();
            if (_tournamentPathPreviewRoot == null)
            {
                return;
            }

            bool targetVisibility = _tournamentPathPreviewVisibilityCaptured ? _tournamentPathPreviewInitialVisibility : true;
            if (_tournamentPathPreviewRoot.activeSelf != targetVisibility)
            {
                _tournamentPathPreviewRoot.SetActive(targetVisibility);
            }
        }

        private void ResolvePopupMenuCanvasGroup()
        {
            if (_popupMenuCanvasGroup == null)
            {
                _popupMenuCanvasGroup = GetComponent<CanvasGroup>();
                if (_popupMenuCanvasGroup == null)
                {
                    _popupMenuCanvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }

            if (_popupMenuCanvasGroup != null && !_popupMenuVisibilityCaptured)
            {
                _popupMenuInitialAlpha = _popupMenuCanvasGroup.alpha;
                _popupMenuInitialInteractable = _popupMenuCanvasGroup.interactable;
                _popupMenuInitialBlocksRaycasts = _popupMenuCanvasGroup.blocksRaycasts;
                _popupMenuVisibilityCaptured = true;
            }
        }

        private void SetPopupMenuVisible(bool visible)
        {
            if (!_hidePopupMenuWhileInventoryVisible)
            {
                return;
            }

            ResolvePopupMenuCanvasGroup();
            if (_popupMenuCanvasGroup == null)
            {
                return;
            }

            _popupMenuCanvasGroup.alpha = visible ? _popupMenuInitialAlpha : 0f;
            _popupMenuCanvasGroup.interactable = visible ? _popupMenuInitialInteractable : false;
            _popupMenuCanvasGroup.blocksRaycasts = visible ? _popupMenuInitialBlocksRaycasts : false;
        }

        private void RestorePopupMenuVisibility()
        {
            if (!_hidePopupMenuWhileInventoryVisible)
            {
                return;
            }

            ResolvePopupMenuCanvasGroup();
            if (_popupMenuCanvasGroup == null)
            {
                return;
            }

            _popupMenuCanvasGroup.alpha = _popupMenuInitialAlpha;
            _popupMenuCanvasGroup.interactable = _popupMenuInitialInteractable;
            _popupMenuCanvasGroup.blocksRaycasts = _popupMenuInitialBlocksRaycasts;
        }

        private void ResolvePreparationResourcesPanelCanvasGroup()
        {
            if (_preparationResourcesPanelRoot == null)
            {
                Transform excludedInventoryRoot = _inventoryPanel != null ? _inventoryPanel.transform : null;
                _preparationResourcesPanelRoot = FindObjectByNameInSceneRootExcludingBranch(
                    _preparationResourcesPanelObjectName,
                    excludedInventoryRoot);
            }

            if (_preparationResourcesPanelRoot == null)
            {
                return;
            }

            if (_preparationResourcesPanelCanvasGroup == null)
            {
                _preparationResourcesPanelCanvasGroup = _preparationResourcesPanelRoot.GetComponent<CanvasGroup>();
                if (_preparationResourcesPanelCanvasGroup == null)
                {
                    _preparationResourcesPanelCanvasGroup = _preparationResourcesPanelRoot.AddComponent<CanvasGroup>();
                }
            }

            if (_preparationResourcesPanelCanvasGroup != null && !_preparationResourcesVisibilityCaptured)
            {
                _preparationResourcesInitialAlpha = _preparationResourcesPanelCanvasGroup.alpha;
                _preparationResourcesInitialInteractable = _preparationResourcesPanelCanvasGroup.interactable;
                _preparationResourcesInitialBlocksRaycasts = _preparationResourcesPanelCanvasGroup.blocksRaycasts;
                _preparationResourcesVisibilityCaptured = true;
            }
        }

        private void SetPreparationResourcesPanelVisible(bool visible)
        {
            if (!_hidePreparationResourcesWhileInventoryVisible)
            {
                return;
            }

            ResolvePreparationResourcesPanelCanvasGroup();
            if (_preparationResourcesPanelCanvasGroup == null)
            {
                return;
            }

            _preparationResourcesPanelCanvasGroup.alpha = visible ? _preparationResourcesInitialAlpha : 0f;
            _preparationResourcesPanelCanvasGroup.interactable = visible ? _preparationResourcesInitialInteractable : false;
            _preparationResourcesPanelCanvasGroup.blocksRaycasts = visible ? _preparationResourcesInitialBlocksRaycasts : false;
        }

        private void RestorePreparationResourcesPanelVisibility()
        {
            if (!_hidePreparationResourcesWhileInventoryVisible)
            {
                return;
            }

            ResolvePreparationResourcesPanelCanvasGroup();
            if (_preparationResourcesPanelCanvasGroup == null)
            {
                return;
            }

            _preparationResourcesPanelCanvasGroup.alpha = _preparationResourcesInitialAlpha;
            _preparationResourcesPanelCanvasGroup.interactable = _preparationResourcesInitialInteractable;
            _preparationResourcesPanelCanvasGroup.blocksRaycasts = _preparationResourcesInitialBlocksRaycasts;
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

            EnsureInventoryPanelCanvasRenderSetup();
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

        private void EnsureInventoryPanelCanvasRenderSetup()
        {
            if (!_inventoryPanelForceCameraRenderMode)
            {
                return;
            }

            Canvas inventoryCanvas = _inventoryPanelAnimatedRoot != null
                ? _inventoryPanelAnimatedRoot.GetComponent<Canvas>()
                : null;
            if (inventoryCanvas == null && _inventoryPanel != null)
            {
                inventoryCanvas = _inventoryPanel.GetComponentInChildren<Canvas>(true);
            }

            if (inventoryCanvas == null)
            {
                return;
            }

            if (_inventoryPanelRenderCamera == null)
            {
                _inventoryPanelRenderCamera = Camera.main;
                if (_inventoryPanelRenderCamera == null)
                {
                    _inventoryPanelRenderCamera = UnityEngine.Object.FindFirstObjectByType<Camera>();
                }
            }

            inventoryCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            inventoryCanvas.worldCamera = _inventoryPanelRenderCamera;
            inventoryCanvas.planeDistance = Mathf.Max(0.31f, _inventoryPanelCameraPlaneDistance);
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
            if (TryFindObjectByNameUnderRoot(searchRoot, objectName, out GameObject fromBranch))
            {
                return fromBranch;
            }

            if (TryFindObjectByNameInScene(objectName, excludedRoot: null, out GameObject fromSceneRoots))
            {
                return fromSceneRoots;
            }

            if (!CanUseGlobalObjectFind())
            {
                return null;
            }

            var global = GameObject.Find(objectName);
            return global;
        }

        private GameObject FindObjectByNameInSceneRootExcludingBranch(string objectName, Transform excludedRoot)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            Transform searchRoot = transform.root != null ? transform.root : transform;
            if (TryFindObjectByNameUnderRoot(searchRoot, objectName, out GameObject fromBranch, excludedRoot))
            {
                return fromBranch;
            }

            if (TryFindObjectByNameInScene(objectName, excludedRoot, out GameObject fromSceneRoots))
            {
                return fromSceneRoots;
            }

            if (!CanUseGlobalObjectFind())
            {
                return null;
            }

            GameObject global = GameObject.Find(objectName);
            if (global != null && !IsSameOrDescendantOf(global.transform, excludedRoot))
            {
                return global;
            }

            return null;
        }

        private bool TryFindObjectByNameInScene(string objectName, Transform excludedRoot, out GameObject found)
        {
            found = null;

            Scene scene = gameObject.scene;
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return false;
            }

            _sceneRootSearchBuffer.Clear();
            scene.GetRootGameObjects(_sceneRootSearchBuffer);
            for (int i = 0; i < _sceneRootSearchBuffer.Count; i++)
            {
                GameObject root = _sceneRootSearchBuffer[i];
                if (root == null)
                {
                    continue;
                }

                if (TryFindObjectByNameUnderRoot(root.transform, objectName, out found, excludedRoot))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryFindObjectByNameUnderRoot(Transform root, string objectName, out GameObject found, Transform excludedRoot = null)
        {
            found = null;
            if (root == null || string.IsNullOrWhiteSpace(objectName))
            {
                return false;
            }

            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform node = transforms[i];
                if (node == null || !string.Equals(node.name, objectName, System.StringComparison.Ordinal))
                {
                    continue;
                }

                if (excludedRoot != null && IsSameOrDescendantOf(node, excludedRoot))
                {
                    continue;
                }

                found = node.gameObject;
                return true;
            }

            return false;
        }

        private bool CanUseGlobalObjectFind()
        {
            if (!Application.isPlaying || !isActiveAndEnabled)
            {
                return false;
            }

            if (!gameObject.scene.IsValid() || !gameObject.scene.isLoaded)
            {
                return false;
            }

            return true;
        }

        private static bool IsSameOrDescendantOf(Transform candidate, Transform possibleAncestor)
        {
            if (candidate == null || possibleAncestor == null)
            {
                return false;
            }

            Transform current = candidate;
            while (current != null)
            {
                if (current == possibleAncestor)
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
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

        private TMP_Text FindTextByNameInSceneRoot(string textObjectName)
        {
            if (string.IsNullOrWhiteSpace(textObjectName))
            {
                return null;
            }

            GameObject textObject = FindObjectByNameInSceneRoot(textObjectName);
            if (textObject == null)
            {
                return null;
            }

            TMP_Text text = textObject.GetComponent<TMP_Text>();
            if (text != null)
            {
                return text;
            }

            return textObject.GetComponentInChildren<TMP_Text>(true);
        }

        private Slider FindSliderInRoot(GameObject root, string sliderObjectName)
        {
            if (root == null || string.IsNullOrWhiteSpace(sliderObjectName))
            {
                return null;
            }

            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform node = transforms[i];
                if (node == null || !string.Equals(node.name, sliderObjectName, System.StringComparison.Ordinal))
                {
                    continue;
                }

                Slider slider = node.GetComponent<Slider>();
                if (slider != null)
                {
                    return slider;
                }

                slider = node.GetComponentInChildren<Slider>(true);
                if (slider != null)
                {
                    return slider;
                }
            }

            return null;
        }

        private Slider FindSliderByNameInSceneRoot(string sliderObjectName)
        {
            if (string.IsNullOrWhiteSpace(sliderObjectName))
            {
                return null;
            }

            GameObject sliderObject = FindObjectByNameInSceneRoot(sliderObjectName);
            if (sliderObject == null)
            {
                return null;
            }

            Slider slider = sliderObject.GetComponent<Slider>();
            if (slider != null)
            {
                return slider;
            }

            return sliderObject.GetComponentInChildren<Slider>(true);
        }

        private Transform FindTransformInRoot(GameObject root, string objectName)
        {
            if (root == null || string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform node = transforms[i];
                if (node != null && string.Equals(node.name, objectName, System.StringComparison.Ordinal))
                {
                    return node;
                }
            }

            return null;
        }

        private Transform FindTransformByNameInSceneRoot(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            GameObject target = FindObjectByNameInSceneRoot(objectName);
            return target != null ? target.transform : null;
        }

        private RectTransform FindRectTransformInRoot(GameObject root, string objectName)
        {
            if (root == null || string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform node = transforms[i];
                if (node == null || !string.Equals(node.name, objectName, System.StringComparison.Ordinal))
                {
                    continue;
                }

                if (node is RectTransform rectTransform)
                {
                    return rectTransform;
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
            RefreshInventorySelectedUnitStatLabels();
        }

        private void SubscribeLocaleChanged()
        {
            LocalizationSettings.SelectedLocaleChanged -= HandleSelectedLocaleChanged;
            LocalizationSettings.SelectedLocaleChanged += HandleSelectedLocaleChanged;
        }

        private void UnsubscribeLocaleChanged()
        {
            LocalizationSettings.SelectedLocaleChanged -= HandleSelectedLocaleChanged;
        }

        private void HandleSelectedLocaleChanged(Locale _)
        {
            RefreshLabels();
        }

        private void RefreshInventorySelectedUnitStatLabels()
        {
            ResolveInventorySelectedUnitStatsRoot();
            if (_inventorySelectedUnitStatsRoot == null)
            {
                return;
            }

            SetInventorySelectedUnitStatLabel("Life", "stats.life", "Life");
            SetInventorySelectedUnitStatLabel("Attack", "stats.attack", "Attack");
            SetInventorySelectedUnitStatLabel("Shoot", "stats.shoot", "Shoot");
            SetInventorySelectedUnitStatLabel("Spell", "stats.spell", "Spell");
            SetInventorySelectedUnitStatLabel("Speed", "stats.speed", "Speed");
            SetInventorySelectedUnitStatLabel("Luck", "stats.luck", "Luck");
            SetInventorySelectedUnitStatLabel("Defense", "stats.defense", "Defense");
            SetInventorySelectedUnitStatLabel("Protection", "stats.protection", "Protection");
            SetInventorySelectedUnitStatLabel("Initiative", "stats.initiative", "Initiative");
            SetInventorySelectedUnitStatLabel("Morale", "stats.morale", "Morale");
        }

        private void SetInventorySelectedUnitStatLabel(string rowObjectName, string localizationKey, string fallback)
        {
            TMP_Text label = FindInventorySelectedUnitStatLabelText(rowObjectName);
            if (label == null)
            {
                return;
            }

            label.text = GetLocalizedCommonString(localizationKey, fallback);
        }

        private TMP_Text FindInventorySelectedUnitStatLabelText(string rowObjectName)
        {
            if (_inventorySelectedUnitStatsRoot == null || string.IsNullOrWhiteSpace(rowObjectName))
            {
                return null;
            }

            Transform row = FindTransformInRoot(_inventorySelectedUnitStatsRoot.gameObject, rowObjectName);
            if (row == null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(_inventorySelectedUnitStatLabelObjectName))
            {
                Transform labelNode = FindTransformInRoot(row.gameObject, _inventorySelectedUnitStatLabelObjectName);
                if (labelNode != null)
                {
                    TMP_Text labelText = labelNode.GetComponent<TMP_Text>();
                    if (labelText != null)
                    {
                        return labelText;
                    }

                    labelText = labelNode.GetComponentInChildren<TMP_Text>(true);
                    if (labelText != null)
                    {
                        return labelText;
                    }
                }
            }

            TMP_Text[] texts = row.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text candidate = texts[i];
                if (candidate == null || candidate.gameObject == null)
                {
                    continue;
                }

                string name = candidate.gameObject.name;
                if (!string.IsNullOrWhiteSpace(name) &&
                    name.IndexOf("label", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return candidate;
                }
            }

            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text candidate = texts[i];
                if (candidate == null || candidate.gameObject == null)
                {
                    continue;
                }

                string name = candidate.gameObject.name;
                if (string.IsNullOrWhiteSpace(name) ||
                    name.IndexOf("value", System.StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return candidate;
                }
            }

            return texts.Length > 0 ? texts[0] : null;
        }

        private static string GetLocalizedCommonString(string key, string fallback)
        {
            if (LocalizationSettings.StringDatabase == null)
            {
                return fallback;
            }

            try
            {
                string value = LocalizationSettings.StringDatabase.GetLocalizedString(UI_COMMON_TABLE, key);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
            catch
            {
                // Fallback when localization system is not ready in tests/bootstrap.
            }

            return fallback;
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
            EnsureDefaultPreparationViewVisible();
        }

        private void HandleInventoryButtonClicked()
        {
            ResolveSquadPanel();
            ResolveInventoryTargets();
            RefreshInventorySelectedUnitPreview();
            _inventoryListPresenter?.RefreshNow();
            PlayClickSfx();
            StartPanelSwitch(toInventory: true);
        }

        private void HandleInventoryBackButtonClicked()
        {
            ResolveSquadPanel();
            ResolveInventoryTargets();
            PlayClickSfx();
            StartPanelSwitch(toInventory: false);
        }

        private void WireInventoryUnitPreviewSelection()
        {
            ResolveSquadSetupController();
            if (_inventoryUnitPreviewEventsWired || _resolvedSquadSetupController == null)
            {
                return;
            }

            _resolvedSquadSetupController.UnitSelected += HandleInventoryUnitSelected;
            _inventoryUnitPreviewEventsWired = true;
        }

        private void UnwireInventoryUnitPreviewSelection()
        {
            if (!_inventoryUnitPreviewEventsWired || _resolvedSquadSetupController == null)
            {
                return;
            }

            _resolvedSquadSetupController.UnitSelected -= HandleInventoryUnitSelected;
            _inventoryUnitPreviewEventsWired = false;
        }

        private void HandleInventoryUnitSelected(UnitSpellLoadout loadout)
        {
            _inventorySelectedLoadout = loadout;
            RefreshInventorySelectedUnitPreview();
        }

        private void RefreshInventorySelectedUnitPreview()
        {
            ResolveInventoryTargets();
            ResolveInventoryUnitPreviewAnchor();
            ResolveInventoryPreviewWorldRoot();

            UnitSpellLoadout selected = ResolveInventorySelectedLoadout();
            RefreshInventorySelectedUnitName(selected);
            RefreshInventorySelectedUnitStats(selected);
            RefreshInventorySelectedUnitProgression(selected);

            if (_inventoryUnitPreviewAnchor == null)
            {
                if (_inventoryUnitPreviewDiagnostics)
                {
                    SBLog.Warn("PreparationPopupMenu: Inventory preview anchor is missing. Cannot spawn preview.", this);
                }
                ClearInventoryUnitPreview();
                return;
            }

            if (selected == null || selected.Definition == null || selected.Definition.Prefab == null)
            {
                if (_inventoryUnitPreviewDiagnostics)
                {
                    SBLog.Warn("PreparationPopupMenu: No selected unit prefab resolved for inventory preview.", this);
                }
                ClearInventoryUnitPreview();
                return;
            }

            GameObject selectedPrefab = selected.Definition.Prefab;
            bool shouldRespawn = _inventoryUnitPreviewInstance == null || _inventoryUnitPreviewPrefab != selectedPrefab;
            if (shouldRespawn)
            {
                SpawnInventoryUnitPreview(selectedPrefab);
            }

            if (_inventoryUnitPreviewInstance == null)
            {
                return;
            }

            UpdateInventoryPreviewWorldPlacement();
            ApplyInventoryUnitPreviewTransform(_inventoryUnitPreviewInstance.transform);
            SetInventoryPreviewFrontIdle(_inventoryUnitPreviewInstance);
        }

        private void RefreshInventorySelectedUnitName(UnitSpellLoadout selectedLoadout)
        {
            ResolveInventorySelectedUnitNameLabel();
            if (_inventorySelectedUnitNameTMP == null)
            {
                return;
            }

            _inventorySelectedUnitNameTMP.text = ResolveInventorySelectedUnitName(selectedLoadout);
        }

        private void RefreshInventorySelectedUnitStats(UnitSpellLoadout selectedLoadout)
        {
            ResolveInventorySelectedUnitStatsRoot();
            if (_inventorySelectedUnitStatsRoot == null)
            {
                return;
            }

            if (!TryResolveSelectedUnitStats(selectedLoadout, out UnitStatsData stats))
            {
                ClearInventorySelectedUnitStats();
                return;
            }

            SetInventorySelectedUnitStat("Life", stats.Life);
            SetInventorySelectedUnitStat("Attack", stats.Attack);
            SetInventorySelectedUnitStat("Shoot", stats.Shoot);
            SetInventorySelectedUnitStat("Spell", stats.Spell);
            SetInventorySelectedUnitStat("Speed", stats.Speed);
            SetInventorySelectedUnitStat("Luck", stats.Luck);
            SetInventorySelectedUnitStat("Defense", stats.Defense);
            SetInventorySelectedUnitStat("Protection", stats.Protection);
            SetInventorySelectedUnitStat("Initiative", stats.Initiative);
            SetInventorySelectedUnitStat("Morale", stats.Morale);
        }

        private void RefreshInventorySelectedUnitProgression(UnitSpellLoadout selectedLoadout)
        {
            ResolveInventorySelectedUnitProgressionTargets();

            if (selectedLoadout == null || selectedLoadout.Definition == null)
            {
                ClearInventorySelectedUnitProgression();
                return;
            }

            UnitDefinition definition = selectedLoadout.Definition;
            int level = Mathf.Max(UnitSpellLoadout.DefaultLevel, selectedLoadout.EffectiveLevel);
            int maxLevel = Mathf.Max(UnitSpellLoadout.DefaultLevel, definition.MaxLevel);
            int xpToNext = UnitXpProgressionUtil.GetXpToNextLevel(level, maxLevel, definition.XpToNextLevel);
            int currentXp = Mathf.Max(0, selectedLoadout.EffectiveXp);
            bool isMaxLevel = level >= maxLevel;

            if (_inventorySelectedUnitLevelTMP != null)
            {
                _inventorySelectedUnitLevelTMP.text = level.ToString();
            }

            if (_inventorySelectedUnitXpSlider != null)
            {
                _inventorySelectedUnitXpSlider.minValue = 0f;
                _inventorySelectedUnitXpSlider.maxValue = 1f;

                float normalized = 0f;
                if (isMaxLevel)
                {
                    normalized = 1f;
                }
                else if (xpToNext > 0)
                {
                    normalized = Mathf.Clamp01((float)currentXp / xpToNext);
                }

                _inventorySelectedUnitXpSlider.SetValueWithoutNotify(normalized);
            }

            if (_inventorySelectedUnitXpTextTMP != null)
            {
                int displayXp = xpToNext > 0
                    ? Mathf.Clamp(currentXp, 0, xpToNext)
                    : 0;
                _inventorySelectedUnitXpTextTMP.text = string.Concat(displayXp.ToString(), "/", Mathf.Max(0, xpToNext).ToString());
            }
        }

        private UnitSpellLoadout ResolveInventorySelectedLoadout()
        {
            ResolveSquadSetupController();
            if (_resolvedSquadSetupController != null && _resolvedSquadSetupController.SelectedUnit != null)
            {
                _inventorySelectedLoadout = _resolvedSquadSetupController.SelectedUnit;
            }

            return _inventorySelectedLoadout;
        }

        private string ResolveInventorySelectedUnitName(UnitSpellLoadout selectedLoadout)
        {
            if (_resolvedSquadSetupController != null)
            {
                string resolved = _resolvedSquadSetupController.ResolveDisplayName(selectedLoadout);
                if (!string.IsNullOrWhiteSpace(resolved))
                {
                    return resolved;
                }
            }

            if (selectedLoadout == null || selectedLoadout.Definition == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(selectedLoadout.Definition.name))
            {
                return selectedLoadout.Definition.name;
            }

            return selectedLoadout.Definition.Id ?? string.Empty;
        }

        private static bool TryResolveSelectedUnitStats(UnitSpellLoadout selectedLoadout, out UnitStatsData stats)
        {
            stats = default;
            if (selectedLoadout == null || selectedLoadout.Definition == null)
            {
                return false;
            }

            UnitDefinition definition = selectedLoadout.Definition;
            int level = selectedLoadout.EffectiveLevel;
            stats = definition.LevelBonus.ApplyTo(definition.BaseStats, level);
            return true;
        }

        private void ClearInventorySelectedUnitStats()
        {
            SetInventorySelectedUnitStatText("Life", string.Empty);
            SetInventorySelectedUnitStatText("Attack", string.Empty);
            SetInventorySelectedUnitStatText("Shoot", string.Empty);
            SetInventorySelectedUnitStatText("Spell", string.Empty);
            SetInventorySelectedUnitStatText("Speed", string.Empty);
            SetInventorySelectedUnitStatText("Luck", string.Empty);
            SetInventorySelectedUnitStatText("Defense", string.Empty);
            SetInventorySelectedUnitStatText("Protection", string.Empty);
            SetInventorySelectedUnitStatText("Initiative", string.Empty);
            SetInventorySelectedUnitStatText("Morale", string.Empty);
        }

        private void ClearInventorySelectedUnitProgression()
        {
            if (_inventorySelectedUnitLevelTMP != null)
            {
                _inventorySelectedUnitLevelTMP.text = string.Empty;
            }

            if (_inventorySelectedUnitXpTextTMP != null)
            {
                _inventorySelectedUnitXpTextTMP.text = string.Empty;
            }

            if (_inventorySelectedUnitXpSlider != null)
            {
                _inventorySelectedUnitXpSlider.minValue = 0f;
                _inventorySelectedUnitXpSlider.maxValue = 1f;
                _inventorySelectedUnitXpSlider.SetValueWithoutNotify(0f);
            }
        }

        private void SetInventorySelectedUnitStat(string rowObjectName, int value)
        {
            SetInventorySelectedUnitStatText(rowObjectName, value.ToString());
        }

        private void SetInventorySelectedUnitStatText(string rowObjectName, string value)
        {
            TMP_Text target = FindInventorySelectedUnitStatValueText(rowObjectName);
            if (target != null)
            {
                target.text = value ?? string.Empty;
            }
        }

        private TMP_Text FindInventorySelectedUnitStatValueText(string rowObjectName)
        {
            if (_inventorySelectedUnitStatsRoot == null || string.IsNullOrWhiteSpace(rowObjectName))
            {
                return null;
            }

            Transform row = FindTransformInRoot(_inventorySelectedUnitStatsRoot.gameObject, rowObjectName);
            if (row == null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(_inventorySelectedUnitStatValueObjectName))
            {
                Transform valueNode = FindTransformInRoot(row.gameObject, _inventorySelectedUnitStatValueObjectName);
                if (valueNode != null)
                {
                    TMP_Text valueText = valueNode.GetComponent<TMP_Text>();
                    if (valueText != null)
                    {
                        return valueText;
                    }

                    valueText = valueNode.GetComponentInChildren<TMP_Text>(true);
                    if (valueText != null)
                    {
                        return valueText;
                    }
                }
            }

            TMP_Text[] texts = row.GetComponentsInChildren<TMP_Text>(true);
            if (texts.Length == 0)
            {
                return null;
            }

            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text candidate = texts[i];
                if (candidate == null || candidate.gameObject == null)
                {
                    continue;
                }

                string name = candidate.gameObject.name;
                if (!string.IsNullOrWhiteSpace(name) &&
                    name.IndexOf("value", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return candidate;
                }
            }

            TMP_Text rowText = row.GetComponent<TMP_Text>();
            if (rowText != null)
            {
                return rowText;
            }

            return texts[texts.Length - 1];
        }

        private void SpawnInventoryUnitPreview(GameObject previewPrefab)
        {
            ClearInventoryUnitPreview();
            ResolveInventoryPreviewWorldRoot();
            if (_inventoryUnitPreviewAnchor == null || previewPrefab == null || _inventoryUnitPreviewWorldRoot == null)
            {
                return;
            }

            _inventoryUnitPreviewInstance = Instantiate(previewPrefab, _inventoryUnitPreviewWorldRoot, false);
            _inventoryUnitPreviewInstance.name = $"{previewPrefab.name}_InventoryPreview";
            _inventoryUnitPreviewPrefab = previewPrefab;
            if (_inventoryUnitPreviewAsFirstSibling)
            {
                _inventoryUnitPreviewInstance.transform.SetAsFirstSibling();
            }

            _inventoryUnitPreviewInstance.transform.localScale = Vector3.zero;
            _inventoryUnitPreviewInstance.transform.localPosition = _inventoryUnitPreviewLocalPosition;

            ApplyInventoryUnitPreviewSorting(_inventoryUnitPreviewInstance);
            UpdateInventoryPreviewWorldPlacement();
            SetInventoryPreviewFrontIdle(_inventoryUnitPreviewInstance);

            if (_inventoryUnitPreviewRoutine != null)
            {
                StopCoroutine(_inventoryUnitPreviewRoutine);
            }

            _inventoryUnitPreviewRoutine = StartCoroutine(AutoFitInventoryPreviewRoutine(_inventoryUnitPreviewInstance));
            if (_inventoryUnitPreviewDiagnostics)
            {
                SBLog.Info(
                    $"PreparationPopupMenu: Spawned inventory preview '{_inventoryUnitPreviewInstance.name}' under '{_inventoryUnitPreviewWorldRoot.name}'.",
                    this);
            }
        }

        private System.Collections.IEnumerator AutoFitInventoryPreviewRoutine(GameObject instance)
        {
            yield return null;

            if (instance == null || instance != _inventoryUnitPreviewInstance)
            {
                yield break;
            }

            ApplyInventoryUnitPreviewTransform(instance.transform);
            _inventoryUnitPreviewRoutine = null;
        }

        private void ApplyInventoryUnitPreviewSorting(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            var groups = instance.GetComponentsInChildren<UnityEngine.Rendering.SortingGroup>(true);
            for (int i = 0; i < groups.Length; i++)
            {
                var group = groups[i];
                if (group == null)
                {
                    continue;
                }

                group.sortingLayerName = _inventoryUnitPreviewSortingLayer;
                group.sortingOrder = _inventoryUnitPreviewSortingOrder;
            }

            var renderers = instance.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer sr = renderers[i];
                if (sr != null)
                {
                    sr.sortingLayerName = _inventoryUnitPreviewSortingLayer;
                    sr.sortingOrder = _inventoryUnitPreviewSortingOrder;
                }
            }
        }

        private void ApplyInventoryUnitPreviewTransform(Transform previewTransform)
        {
            if (previewTransform == null)
            {
                return;
            }

            Vector3 configuredScale = _inventoryUnitPreviewLocalScale;
            if (Mathf.Abs(configuredScale.x) <= 0.0001f ||
                Mathf.Abs(configuredScale.y) <= 0.0001f ||
                Mathf.Abs(configuredScale.z) <= 0.0001f)
            {
                configuredScale = Vector3.one;
            }

            previewTransform.localPosition = _inventoryUnitPreviewLocalPosition;
            previewTransform.localScale = configuredScale;
            TryAutoFitInventoryPreviewScale(previewTransform);
        }

        private void ClearInventoryUnitPreview()
        {
            if (_inventoryUnitPreviewRoutine != null)
            {
                StopCoroutine(_inventoryUnitPreviewRoutine);
                _inventoryUnitPreviewRoutine = null;
            }

            _inventoryUnitPreviewPrefab = null;
            if (_inventoryUnitPreviewInstance == null)
            {
                return;
            }

            // Hide immediately to avoid one-frame ghosting while destruction is deferred.
            _inventoryUnitPreviewInstance.SetActive(false);

            if (Application.isPlaying)
            {
                Destroy(_inventoryUnitPreviewInstance);
            }
            else
            {
                DestroyImmediate(_inventoryUnitPreviewInstance);
            }

            _inventoryUnitPreviewInstance = null;
        }

        private void ResolveInventoryPreviewWorldRoot()
        {
            if (_inventoryUnitPreviewWorldRoot != null)
            {
                return;
            }

            Transform root = transform.root != null ? transform.root : transform;
            Transform existing = root.Find("InventoryPreviewWorldRoot_Runtime");
            if (existing != null)
            {
                _inventoryUnitPreviewWorldRoot = existing;
                return;
            }

            GameObject runtimeRoot = new GameObject("InventoryPreviewWorldRoot_Runtime");
            runtimeRoot.transform.SetParent(root, false);
            _inventoryUnitPreviewWorldRoot = runtimeRoot.transform;
        }

        private void UpdateInventoryPreviewWorldPlacement()
        {
            if (_inventoryUnitPreviewInstance == null || _inventoryUnitPreviewAnchor == null)
            {
                return;
            }

            if (!TryResolveInventoryPreviewWorldPosition(out Vector3 targetWorldPosition))
            {
                if (_inventoryUnitPreviewDiagnostics && Time.unscaledTime - _lastInventoryPreviewPlacementWarnTime >= INVENTORY_PREVIEW_WARN_LOG_COOLDOWN_SECONDS)
                {
                    SBLog.Warn("PreparationPopupMenu: Failed to resolve inventory preview world position from anchor.", this);
                    _lastInventoryPreviewPlacementWarnTime = Time.unscaledTime;
                }

                return;
            }

            _inventoryUnitPreviewInstance.transform.position = targetWorldPosition + _inventoryUnitPreviewWorldOffset;
        }

        private bool TryResolveInventoryPreviewWorldPosition(out Vector3 worldPosition)
        {
            worldPosition = Vector3.zero;
            if (_inventoryUnitPreviewAnchor == null)
            {
                return false;
            }

            Canvas inventoryCanvas = _inventoryPanelAnimatedRoot != null
                ? _inventoryPanelAnimatedRoot.GetComponent<Canvas>()
                : null;
            if (inventoryCanvas == null && _inventoryPanel != null)
            {
                inventoryCanvas = _inventoryPanel.GetComponentInChildren<Canvas>(true);
            }

            Camera uiCamera = inventoryCanvas != null ? inventoryCanvas.worldCamera : _inventoryPanelRenderCamera;
            if (uiCamera == null)
            {
                uiCamera = Camera.main;
            }

            if (uiCamera == null)
            {
                return false;
            }

            Vector3[] corners = new Vector3[4];
            _inventoryUnitPreviewAnchor.GetWorldCorners(corners);
            Vector3 centerWorld = (corners[0] + corners[2]) * 0.5f;

            Vector3 screen = RectTransformUtility.WorldToScreenPoint(uiCamera, centerWorld);
            float planeDistance = inventoryCanvas != null && inventoryCanvas.renderMode == RenderMode.ScreenSpaceCamera
                ? Mathf.Max(0.31f, inventoryCanvas.planeDistance)
                : Mathf.Max(0.31f, _inventoryPanelCameraPlaneDistance);
            float targetDistanceFromCamera = planeDistance + _inventoryUnitPreviewPlaneDepthOffset;
            float minDistanceFromCamera = uiCamera.nearClipPlane + 0.02f;
            if (targetDistanceFromCamera < minDistanceFromCamera)
            {
                targetDistanceFromCamera = minDistanceFromCamera;
            }

            screen.z = targetDistanceFromCamera;

            worldPosition = uiCamera.ScreenToWorldPoint(screen);
            if (_inventoryUnitPreviewDiagnostics && Time.unscaledTime - _lastInventoryPreviewPlacementInfoTime >= INVENTORY_PREVIEW_INFO_LOG_COOLDOWN_SECONDS)
            {
                SBLog.Info(
                    $"PreparationPopupMenu: Inventory preview anchor '{_inventoryUnitPreviewAnchor.name}' -> world {worldPosition} (camera '{uiCamera.name}', plane={planeDistance:0.###}, depth={targetDistanceFromCamera:0.###}).",
                    this);
                _lastInventoryPreviewPlacementInfoTime = Time.unscaledTime;
            }

            return true;
        }

        private void StartPanelSwitch(bool toInventory)
        {
            ResolveSquadPanel();
            ResolveInventoryTargets();
            StopPanelSwitchRoutine();
            StopSquadPanelRoutine();
            StopInventoryPanelRoutine();
            SetTournamentPathPreviewVisible(!toInventory);
            if (toInventory)
            {
                SetPopupMenuVisible(false);
                SetPreparationResourcesPanelVisible(false);
            }

            bool panelsReady = _squadPanel != null
                && _squadPanelCanvasGroup != null
                && _inventoryPanel != null
                && _inventoryPanelCanvasGroup != null;
            if (!panelsReady)
            {
                if (toInventory)
                {
                    HideSquadPanel();
                    ShowInventoryPanel();
                }
                else
                {
                    HideInventoryPanel();
                    ShowSquadPanel();
                }

                return;
            }

            float halfDuration = Mathf.Max(0f, _panelSwitchHalfDuration);
            if (halfDuration <= 0.0001f)
            {
                if (toInventory)
                {
                    SetSquadPanelHiddenAndDisabledImmediate();
                    SetInventoryPanelVisibleImmediate();
                }
                else
                {
                    SetInventoryPanelHiddenAndDisabledImmediate();
                    SetSquadPanelVisibleImmediate();
                }

                return;
            }

            _panelSwitchRoutine = StartCoroutine(SwitchPanelsCinematicRoutine(toInventory, halfDuration));
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
            SetTournamentPathPreviewVisible(false);
            SetPopupMenuVisible(false);
            SetPreparationResourcesPanelVisible(false);
            _inventoryListPresenter?.RefreshNow();
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

            SetTournamentPathPreviewVisible(true);
            SetPopupMenuVisible(true);
            SetPreparationResourcesPanelVisible(true);
            ClearInventoryUnitPreview();
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

            SetTournamentPathPreviewVisible(true);
            SetPopupMenuVisible(true);
            SetPreparationResourcesPanelVisible(true);
            ClearInventoryUnitPreview();
            _inventoryPanelStartupHiddenApplied = true;
        }

        private void EnsureDefaultPreparationViewVisible()
        {
            SetTournamentPathPreviewVisible(true);
            SetPopupMenuVisible(true);
            SetPreparationResourcesPanelVisible(true);
            ClearInventoryUnitPreview();
        }

        private System.Collections.IEnumerator SwitchPanelsCinematicRoutine(bool toInventory, float halfDuration)
        {
            CanvasGroup overlay = EnsurePanelSwitchOverlay();
            if (overlay == null)
            {
                if (toInventory)
                {
                    HideSquadPanel();
                    ShowInventoryPanel();
                }
                else
                {
                    HideInventoryPanel();
                    ShowSquadPanel();
                }

                _panelSwitchRoutine = null;
                yield break;
            }

            CanvasGroup outgoingCanvasGroup = toInventory ? _squadPanelCanvasGroup : _inventoryPanelCanvasGroup;
            CanvasGroup incomingCanvasGroup = toInventory ? _inventoryPanelCanvasGroup : _squadPanelCanvasGroup;
            GameObject outgoingPanel = toInventory ? _squadPanel : _inventoryPanel;
            GameObject incomingPanel = toInventory ? _inventoryPanel : _squadPanel;
            Transform outgoingAnimatedRoot = toInventory
                ? (_squadPanelAnimatedRoot != null ? _squadPanelAnimatedRoot : _squadPanel != null ? _squadPanel.transform : null)
                : (_inventoryPanelAnimatedRoot != null ? _inventoryPanelAnimatedRoot : _inventoryPanel != null ? _inventoryPanel.transform : null);
            Transform incomingAnimatedRoot = toInventory
                ? (_inventoryPanelAnimatedRoot != null ? _inventoryPanelAnimatedRoot : _inventoryPanel != null ? _inventoryPanel.transform : null)
                : (_squadPanelAnimatedRoot != null ? _squadPanelAnimatedRoot : _squadPanel != null ? _squadPanel.transform : null);

            Vector3 outgoingBaseScale = toInventory ? _squadPanelBaseScale : _inventoryPanelBaseScale;
            Vector3 incomingBaseScale = toInventory ? _inventoryPanelBaseScale : _squadPanelBaseScale;
            float incomingStartScaleFactor = Mathf.Clamp(toInventory ? _inventoryPanelStartScale : _squadPanelStartScale, 0.8f, 1f);
            float incomingOvershootScale = Mathf.Clamp(_panelSwitchIncomingOvershootScale, 1f, 1.2f);
            float outgoingSlideRatio = Mathf.Clamp01(_panelSwitchOutgoingSlideRatio);
            float slideDuration = Mathf.Max(0.04f, halfDuration * 2f);
            float settleDuration = halfDuration * 0.45f;
            float slideDistance = ResolvePanelSlideDistance(outgoingAnimatedRoot, incomingAnimatedRoot);

            if (incomingPanel != null)
            {
                incomingPanel.transform.SetAsLastSibling();
            }

            if (outgoingPanel != null && !outgoingPanel.activeSelf)
            {
                outgoingPanel.SetActive(true);
            }

            if (incomingPanel != null && !incomingPanel.activeSelf)
            {
                incomingPanel.SetActive(true);
            }

            if (outgoingCanvasGroup != null)
            {
                outgoingCanvasGroup.alpha = Mathf.Clamp01(outgoingCanvasGroup.alpha);
                outgoingCanvasGroup.interactable = false;
                outgoingCanvasGroup.blocksRaycasts = true;
            }

            if (incomingCanvasGroup != null)
            {
                incomingCanvasGroup.alpha = 1f;
                incomingCanvasGroup.interactable = false;
                incomingCanvasGroup.blocksRaycasts = true;
            }

            if (outgoingAnimatedRoot != null)
            {
                outgoingAnimatedRoot.localScale = outgoingBaseScale;
            }

            float outgoingStartX = GetPanelHorizontalPosition(outgoingAnimatedRoot);
            float incomingBaseX = GetPanelHorizontalPosition(incomingAnimatedRoot);
            float incomingStartX = incomingBaseX + slideDistance;
            Vector3 incomingStartScale = new Vector3(
                incomingBaseScale.x * incomingStartScaleFactor,
                incomingBaseScale.y * incomingStartScaleFactor,
                incomingBaseScale.z);
            Vector3 incomingOvershoot = incomingBaseScale * incomingOvershootScale;
            if (incomingAnimatedRoot != null)
            {
                SetPanelHorizontalPosition(incomingAnimatedRoot, incomingStartX);
                incomingAnimatedRoot.localScale = incomingStartScale;
            }

            float peakAlpha = Mathf.Clamp01(_panelSwitchOverlayPeakAlpha);
            float t = 0f;
            while (t < slideDuration)
            {
                t += Time.unscaledDeltaTime;
                float normalized = Mathf.Clamp01(t / slideDuration);
                float eased = EvaluateRevealCurve(_panelSwitchCurve, normalized);

                float veilBlend = Mathf.Sin(normalized * Mathf.PI);
                overlay.alpha = peakAlpha * veilBlend;

                if (outgoingAnimatedRoot != null)
                {
                    float outgoingX = Mathf.LerpUnclamped(outgoingStartX, outgoingStartX - (slideDistance * outgoingSlideRatio), eased);
                    SetPanelHorizontalPosition(outgoingAnimatedRoot, outgoingX);
                }

                if (incomingAnimatedRoot != null)
                {
                    float incomingX = Mathf.LerpUnclamped(incomingStartX, incomingBaseX, eased);
                    SetPanelHorizontalPosition(incomingAnimatedRoot, incomingX);
                    incomingAnimatedRoot.localScale = Vector3.LerpUnclamped(incomingStartScale, incomingOvershoot, eased);
                }

                yield return null;
            }

            if (incomingAnimatedRoot != null)
            {
                if (settleDuration > 0.0001f)
                {
                    float settleT = 0f;
                    Vector3 settleFrom = incomingAnimatedRoot.localScale;
                    while (settleT < settleDuration)
                    {
                        settleT += Time.unscaledDeltaTime;
                        float normalized = Mathf.Clamp01(settleT / settleDuration);
                        float eased = EvaluateRevealCurve(_panelSwitchCurve, normalized);
                        incomingAnimatedRoot.localScale = Vector3.LerpUnclamped(settleFrom, incomingBaseScale, eased);
                        yield return null;
                    }
                }

                SetPanelHorizontalPosition(incomingAnimatedRoot, incomingBaseX);
                incomingAnimatedRoot.localScale = incomingBaseScale;
            }

            if (outgoingAnimatedRoot != null)
            {
                SetPanelHorizontalPosition(outgoingAnimatedRoot, outgoingStartX);
                outgoingAnimatedRoot.localScale = outgoingBaseScale;
            }

            if (toInventory)
            {
                SetSquadPanelHiddenAndDisabledImmediate();
                SetInventoryPanelVisibleImmediate();
            }
            else
            {
                SetInventoryPanelHiddenAndDisabledImmediate();
                SetSquadPanelVisibleImmediate();
            }

            HidePanelSwitchOverlayImmediate();
            _panelSwitchRoutine = null;
        }

        private float ResolvePanelSlideDistance(Transform outgoingAnimatedRoot, Transform incomingAnimatedRoot)
        {
            float outgoingWidth = GetPanelWidth(outgoingAnimatedRoot);
            float incomingWidth = GetPanelWidth(incomingAnimatedRoot);
            float panelWidth = Mathf.Max(outgoingWidth, incomingWidth);
            if (panelWidth <= 0.001f)
            {
                panelWidth = Mathf.Max(1f, Screen.width);
            }

            float multiplier = Mathf.Max(0.2f, _panelSwitchSlideDistanceMultiplier);
            return Mathf.Max(32f, panelWidth * multiplier);
        }

        private static float GetPanelWidth(Transform panelTransform)
        {
            if (panelTransform is RectTransform rectTransform)
            {
                return Mathf.Abs(rectTransform.rect.width);
            }

            return 0f;
        }

        private static float GetPanelHorizontalPosition(Transform panelTransform)
        {
            if (panelTransform == null)
            {
                return 0f;
            }

            if (panelTransform is RectTransform rectTransform)
            {
                return rectTransform.anchoredPosition.x;
            }

            return panelTransform.localPosition.x;
        }

        private static void SetPanelHorizontalPosition(Transform panelTransform, float x)
        {
            if (panelTransform == null)
            {
                return;
            }

            if (panelTransform is RectTransform rectTransform)
            {
                Vector2 anchoredPosition = rectTransform.anchoredPosition;
                anchoredPosition.x = x;
                rectTransform.anchoredPosition = anchoredPosition;
                return;
            }

            Vector3 localPosition = panelTransform.localPosition;
            localPosition.x = x;
            panelTransform.localPosition = localPosition;
        }

        private CanvasGroup EnsurePanelSwitchOverlay()
        {
            if (_panelSwitchOverlayCanvasGroup == null)
            {
                CreateRuntimePanelSwitchOverlay();
            }

            if (_panelSwitchOverlayCanvasGroup == null)
            {
                return null;
            }

            if (_panelSwitchOverlayImage == null)
            {
                _panelSwitchOverlayImage = _panelSwitchOverlayCanvasGroup.GetComponent<Image>();
                if (_panelSwitchOverlayImage == null)
                {
                    _panelSwitchOverlayImage = _panelSwitchOverlayCanvasGroup.gameObject.AddComponent<Image>();
                }
            }

            _panelSwitchOverlayImage.color = _panelSwitchOverlayColor;
            _panelSwitchOverlayImage.raycastTarget = true;
            if (!_panelSwitchOverlayCanvasGroup.gameObject.activeSelf)
            {
                _panelSwitchOverlayCanvasGroup.gameObject.SetActive(true);
            }

            _panelSwitchOverlayCanvasGroup.alpha = 0f;
            _panelSwitchOverlayCanvasGroup.interactable = true;
            _panelSwitchOverlayCanvasGroup.blocksRaycasts = true;
            return _panelSwitchOverlayCanvasGroup;
        }

        private void CreateRuntimePanelSwitchOverlay()
        {
            if (_panelSwitchOverlayRuntimeCanvas != null)
            {
                return;
            }

            GameObject canvasObject = new GameObject(
                PANEL_SWITCH_OVERLAY_RUNTIME_NAME,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

            _panelSwitchOverlayRuntimeCanvas = canvasObject.GetComponent<Canvas>();
            _panelSwitchOverlayRuntimeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _panelSwitchOverlayRuntimeCanvas.overrideSorting = true;
            _panelSwitchOverlayRuntimeCanvas.sortingOrder = short.MaxValue - 16;

            CanvasScaler canvasScaler = canvasObject.GetComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            canvasScaler.matchWidthOrHeight = 0.5f;

            GameObject overlayObject = new GameObject(
                "Overlay",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(CanvasGroup));
            overlayObject.transform.SetParent(canvasObject.transform, false);

            RectTransform overlayRect = overlayObject.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            overlayRect.localScale = Vector3.one;

            _panelSwitchOverlayCanvasGroup = overlayObject.GetComponent<CanvasGroup>();
            _panelSwitchOverlayImage = overlayObject.GetComponent<Image>();
            if (_panelSwitchOverlayImage != null)
            {
                _panelSwitchOverlayImage.color = _panelSwitchOverlayColor;
                _panelSwitchOverlayImage.raycastTarget = true;
            }

            HidePanelSwitchOverlayImmediate();
        }

        private void StopPanelSwitchRoutine()
        {
            if (_panelSwitchRoutine != null)
            {
                StopCoroutine(_panelSwitchRoutine);
                _panelSwitchRoutine = null;
            }
        }

        private void HidePanelSwitchOverlayImmediate()
        {
            if (_panelSwitchOverlayCanvasGroup == null)
            {
                return;
            }

            _panelSwitchOverlayCanvasGroup.alpha = 0f;
            _panelSwitchOverlayCanvasGroup.interactable = false;
            _panelSwitchOverlayCanvasGroup.blocksRaycasts = false;
            if (_panelSwitchOverlayCanvasGroup.gameObject.activeSelf)
            {
                _panelSwitchOverlayCanvasGroup.gameObject.SetActive(false);
            }
        }

        private static void SetInventoryPreviewFrontIdle(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            SetCharacter4DDirection(instance, Vector2.down);
            TryPlayInventoryPreviewIdle(instance);
        }

        private static void SetCharacter4DDirection(GameObject instance, Vector2 direction)
        {
            if (instance == null)
            {
                return;
            }

            try
            {
                MonoBehaviour[] components = instance.GetComponentsInChildren<MonoBehaviour>(true);
                for (int i = 0; i < components.Length; i++)
                {
                    MonoBehaviour component = components[i];
                    if (component == null)
                    {
                        continue;
                    }

                    System.Type type = component.GetType();
                    if (type.Name != "Character4D" &&
                        type.FullName != "Assets.HeroEditor4D.Common.Scripts.CharacterScripts.Character4D")
                    {
                        continue;
                    }

                    System.Reflection.MethodInfo setDirection = type.GetMethod(
                        "SetDirection",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
                        null,
                        new[] { typeof(Vector2) },
                        null);
                    if (setDirection == null)
                    {
                        return;
                    }

                    setDirection.Invoke(component, new object[] { direction });
                    return;
                }
            }
            catch
            {
                // Ignore reflection failures for non-HeroEditor prefabs.
            }
        }

        private static bool TryPlayInventoryPreviewIdle(GameObject instance)
        {
            if (instance == null)
            {
                return false;
            }

            object animationManager = null;
            try
            {
                MonoBehaviour[] components = instance.GetComponentsInChildren<MonoBehaviour>(true);
                for (int i = 0; i < components.Length; i++)
                {
                    MonoBehaviour component = components[i];
                    if (component == null)
                    {
                        continue;
                    }

                    System.Type type = component.GetType();
                    if (type.Name == "AnimationManager" ||
                        type.FullName == "Assets.HeroEditor4D.Common.Scripts.CharacterScripts.AnimationManager")
                    {
                        animationManager = component;
                        break;
                    }
                }
            }
            catch
            {
                // Ignore reflection failures for non-HeroEditor prefabs.
            }

            if (animationManager != null && TryInvokeIdleOnAnimationManager(animationManager))
            {
                return true;
            }

            Animator animator = instance.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                return false;
            }

            int idleHash = Animator.StringToHash("Idle");
            if (animator.HasState(0, idleHash))
            {
                animator.Play(idleHash, 0, 0f);
                return true;
            }

            return false;
        }

        private static bool TryInvokeIdleOnAnimationManager(object animationManager)
        {
            if (animationManager == null)
            {
                return false;
            }

            try
            {
                System.Type managerType = animationManager.GetType();
                System.Reflection.MethodInfo idleMethod = managerType.GetMethod(
                    "Idle",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
                    null,
                    System.Type.EmptyTypes,
                    null);
                if (idleMethod != null)
                {
                    idleMethod.Invoke(animationManager, null);
                    return true;
                }

                System.Reflection.MethodInfo[] methods = managerType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                for (int i = 0; i < methods.Length; i++)
                {
                    System.Reflection.MethodInfo method = methods[i];
                    if (!string.Equals(method.Name, "SetState", System.StringComparison.Ordinal))
                    {
                        continue;
                    }

                    System.Reflection.ParameterInfo[] parameters = method.GetParameters();
                    if (parameters.Length != 1)
                    {
                        continue;
                    }

                    System.Type enumType = parameters[0].ParameterType;
                    if (!enumType.IsEnum)
                    {
                        continue;
                    }

                    if (enumType.FullName != "Assets.HeroEditor4D.Common.Scripts.Enums.CharacterState" && enumType.Name != "CharacterState")
                    {
                        continue;
                    }

                    object enumValue = System.Enum.Parse(enumType, "Idle", true);
                    method.Invoke(animationManager, new[] { enumValue });
                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private void TryAutoFitInventoryPreviewScale(Transform previewTransform)
        {
            if (!_inventoryUnitPreviewAutoFitScale || previewTransform == null)
            {
                return;
            }

            if (!TryGetInventoryPreviewBounds(previewTransform.gameObject, out Bounds previewBounds))
            {
                return;
            }

            if (!TryGetInventoryPreviewCamera(out Camera previewCamera))
            {
                return;
            }

            float currentScreenHeight = GetBoundsScreenHeight(previewBounds, previewCamera);
            if (currentScreenHeight <= 0.01f)
            {
                return;
            }

            float anchorScreenHeight = GetAnchorScreenHeight();
            if (anchorScreenHeight <= 0.01f)
            {
                return;
            }

            float targetFill = Mathf.Clamp(_inventoryUnitPreviewAutoFitFill, 0.2f, 1.2f);
            float targetScreenHeight = anchorScreenHeight * targetFill;
            float rawMultiplier = targetScreenHeight / currentScreenHeight;
            float minMultiplier = Mathf.Max(0.01f, _inventoryUnitPreviewAutoFitMinScaleMultiplier);
            float maxMultiplier = Mathf.Max(minMultiplier, _inventoryUnitPreviewAutoFitMaxScaleMultiplier);
            float multiplier = Mathf.Clamp(rawMultiplier, minMultiplier, maxMultiplier);

            previewTransform.localScale = previewTransform.localScale * multiplier;
            if (_inventoryUnitPreviewDiagnostics)
            {
                SBLog.Info(
                    $"PreparationPopupMenu: Inventory preview auto-fit currentPx={currentScreenHeight:0.##}, anchorPx={anchorScreenHeight:0.##}, multiplier={multiplier:0.###}, finalScale={previewTransform.localScale}.",
                    this);
            }
        }

        private bool TryGetInventoryPreviewCamera(out Camera camera)
        {
            camera = null;

            Canvas inventoryCanvas = _inventoryPanelAnimatedRoot != null
                ? _inventoryPanelAnimatedRoot.GetComponent<Canvas>()
                : null;
            if (inventoryCanvas == null && _inventoryPanel != null)
            {
                inventoryCanvas = _inventoryPanel.GetComponentInChildren<Canvas>(true);
            }

            if (inventoryCanvas != null && inventoryCanvas.worldCamera != null && inventoryCanvas.worldCamera.isActiveAndEnabled)
            {
                camera = inventoryCanvas.worldCamera;
                return true;
            }

            if (_inventoryPanelRenderCamera != null && _inventoryPanelRenderCamera.isActiveAndEnabled)
            {
                camera = _inventoryPanelRenderCamera;
                return true;
            }

            camera = Camera.main;
            if (camera != null && camera.isActiveAndEnabled)
            {
                return true;
            }

            camera = UnityEngine.Object.FindFirstObjectByType<Camera>();
            return camera != null && camera.isActiveAndEnabled;
        }

        private static bool TryGetInventoryPreviewBounds(GameObject instance, out Bounds bounds)
        {
            bounds = default;
            if (instance == null)
            {
                return false;
            }

            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds;
        }

        private float GetAnchorScreenHeight()
        {
            if (_inventoryUnitPreviewAnchor == null)
            {
                return 0f;
            }

            var corners = new Vector3[4];
            _inventoryUnitPreviewAnchor.GetWorldCorners(corners);

            if (!TryGetInventoryPreviewCamera(out Camera previewCamera))
            {
                return 0f;
            }

            Vector2 bottomScreen = RectTransformUtility.WorldToScreenPoint(previewCamera, corners[0]);
            Vector2 topScreen = RectTransformUtility.WorldToScreenPoint(previewCamera, corners[1]);
            return Mathf.Abs(topScreen.y - bottomScreen.y);
        }

        private static float GetBoundsScreenHeight(Bounds bounds, Camera camera)
        {
            if (camera == null)
            {
                return 0f;
            }

            Vector3 top = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
            Vector3 bottom = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            Vector3 topScreen = camera.WorldToScreenPoint(top);
            Vector3 bottomScreen = camera.WorldToScreenPoint(bottom);
            return Mathf.Abs(topScreen.y - bottomScreen.y);
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
