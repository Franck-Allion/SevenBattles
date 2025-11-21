using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization;
using TMPro;
using SevenBattles.Core;

namespace SevenBattles.UI
{
    // HUD panel showing the active unit portrait and End Turn button.
    public class TurnOrderHUD : MonoBehaviour
    {
        [Header("Controller")]
        [SerializeField, Tooltip("Reference to a MonoBehaviour that implements ITurnOrderController.")]
        private MonoBehaviour _controllerBehaviour;
        private ITurnOrderController _controller;

        [Header("Portrait")]
        [SerializeField, Tooltip("Image used to display the active unit portrait.")]
        private Image _activePortraitImage;
        [SerializeField, Tooltip("Optional Button used to detect clicks on the active unit portrait. If null, a Button on the portrait Image GameObject will be used if present.")]
        private Button _portraitButton;

        [Header("End Turn Button")]
        [SerializeField] private Button _endTurnButton;
        [SerializeField, Tooltip("Optional CanvasGroup used to fade the End Turn button when disabled. If null, one will be auto-added on the button root.")]
        private CanvasGroup _endTurnCanvasGroup;
        [SerializeField, Tooltip("Alpha applied to the End Turn button when it is disabled.")]
        private float _disabledAlpha = 0.4f;

        [Header("Action Points")]
        [SerializeField, Tooltip("Root RectTransform for the action point bar. Hidden when the active unit has no action points.")]
        private RectTransform _actionPointBarRoot;
        [SerializeField, Tooltip("Up to 8 Image slots used to display the active unit's action points from left to right.")]
        private Image[] _actionPointSlots = new Image[8];
        [SerializeField, Tooltip("Sprite used for a full (available) action point.")]
        private Sprite _actionPointFullSprite;
        [SerializeField, Tooltip("Sprite used for an empty (spent) action point.")]
        private Sprite _actionPointEmptySprite;

        [Header("Stats Panel")]
        [SerializeField, Tooltip("Root RectTransform of the stats panel that slides in when the portrait is clicked.")]
        private RectTransform _statsPanelRoot;
        [SerializeField, Tooltip("Optional CanvasGroup driving stats panel alpha and interaction. If null, one will be auto-added on the panel root.")]
        private CanvasGroup _statsPanelCanvasGroup;
        [SerializeField, Tooltip("Slide duration (seconds) for the stats panel when opening/closing.")]
        private float _statsPanelSlideDuration = 0.35f;
        [SerializeField, Tooltip("Horizontal offset (in panel local units) used to position the stats panel off-screen to the right when hidden.")]
        private float _statsPanelOffscreenOffset = 400f;
        [SerializeField, Tooltip("Optional full-screen Button that detects clicks outside the stats panel to close it. Typically a transparent overlay behind the panel.")]
        private Button _statsPanelBackgroundButton;

        [Header("Stats Text (TMP)")]
        [SerializeField] private TMP_Text _lifeText;
        [SerializeField] private TMP_Text _forceText;
        [SerializeField] private TMP_Text _shootText;
        [SerializeField] private TMP_Text _spellText;
        [SerializeField] private TMP_Text _speedText;
        [SerializeField] private TMP_Text _luckText;
        [SerializeField] private TMP_Text _defenseText;
        [SerializeField] private TMP_Text _protectionText;
        [SerializeField] private TMP_Text _initiativeText;
        [SerializeField] private TMP_Text _moraleText;

        [Header("Stats Labels (TMP, optional)")]
        [SerializeField] private TMP_Text _lifeLabel;
        [SerializeField] private TMP_Text _attackLabel;
        [SerializeField] private TMP_Text _shootLabel;
        [SerializeField] private TMP_Text _spellLabel;
        [SerializeField] private TMP_Text _speedLabel;
        [SerializeField] private TMP_Text _luckLabel;
        [SerializeField] private TMP_Text _defenseLabel;
        [SerializeField] private TMP_Text _protectionLabel;
        [SerializeField] private TMP_Text _initiativeLabel;
        [SerializeField] private TMP_Text _moraleLabel;

        [Header("Audio")]
        [SerializeField, Tooltip("AudioSource used to play the End Turn SFX (optional). If not set, PlayClipAtPoint will be used.")]
        private AudioSource _audio;
        [SerializeField, Tooltip("Sound to play when the End Turn button is clicked (e.g., Button_EndTurn).")]
        private AudioClip _endTurnClip;

        [Header("Localization")]
        [SerializeField, Tooltip("Localized label for the End Turn button (e.g., Table: UI.Battle, Entry: EndTurn)")]
        private LocalizedString _endTurnLabel;
        [SerializeField, Tooltip("Optional explicit reference to the Button label Text component. If not set, a child Text will be auto-found at runtime.")]
        private Text _endTurnText;
        [SerializeField, Tooltip("Optional explicit reference to the Button label TMP_Text component. If not set, a child TMP_Text will be auto-found at runtime.")]
        private TMP_Text _endTurnTMP;

        // Runtime-localized stats label bindings (table: UI.Common).
        private LocalizedString _lifeLabelString;
        private LocalizedString _attackLabelString;
        private LocalizedString _shootLabelString;
        private LocalizedString _spellLabelString;
        private LocalizedString _speedLabelString;
        private LocalizedString _luckLabelString;
        private LocalizedString _defenseLabelString;
        private LocalizedString _protectionLabelString;
        private LocalizedString _initiativeLabelString;
        private LocalizedString _moraleLabelString;

        private bool _statsPanelVisible;
        private bool _statsPanelAnimating;
        private Vector2 _statsPanelShownPosition;
        private Vector2 _statsPanelHiddenPosition;
        private System.Collections.IEnumerator _statsPanelRoutine;

        private void Awake()
        {
            EnsureController();
            WireEndTurnButton();
            EnsureEndTurnCanvasGroup();
            SetupEndTurnLocalization();
            SetupStatsPanel();
            SetupStatsLabelLocalization();
            WirePortraitClick();
            RefreshActionPoints();
        }

        private void OnEnable()
        {
            EnsureController();
            if (_controller == null) return;
            _controller.ActiveUnitChanged += HandleActiveUnitChanged;
            _controller.ActiveUnitActionPointsChanged += HandleActiveUnitActionPointsChanged;
            HandleActiveUnitChanged();
            RefreshEndTurnLabel();
            RefreshStatsLabels();
            RefreshActionPoints();
        }

        private void OnDisable()
        {
            if (_controller != null)
            {
                _controller.ActiveUnitChanged -= HandleActiveUnitChanged;
                _controller.ActiveUnitActionPointsChanged -= HandleActiveUnitActionPointsChanged;
            }

            TeardownEndTurnLocalization();
            TeardownStatsLabelLocalization();
            CloseStatsPanelImmediate();

            if (_portraitButton != null)
            {
                _portraitButton.onClick.RemoveListener(HandlePortraitClicked);
            }

            if (_statsPanelBackgroundButton != null)
            {
                _statsPanelBackgroundButton.onClick.RemoveListener(HandleStatsBackgroundClicked);
            }
        }

        private void EnsureController()
        {
            if (_controller != null) return;

            if (_controllerBehaviour == null)
            {
                // Auto-discover a controller that implements ITurnOrderController in the scene.
                var behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
                for (int i = 0; i < behaviours.Length; i++)
                {
                    var candidate = behaviours[i] as ITurnOrderController;
                    if (candidate != null)
                    {
                        _controllerBehaviour = behaviours[i];
                        _controller = candidate;
                        break;
                    }
                }

                if (_controller == null)
                {
                    Debug.LogWarning("TurnOrderHUD: No controller implementing ITurnOrderController found in scene.", this);
                }
            }
            else
            {
                _controller = _controllerBehaviour as ITurnOrderController;
                if (_controller == null)
                {
                    Debug.LogWarning("TurnOrderHUD: Assigned controller does not implement ITurnOrderController.", this);
                }
            }
        }

        private void WireEndTurnButton()
        {
            if (_endTurnButton == null) return;
            _endTurnButton.onClick.RemoveAllListeners();
            _endTurnButton.onClick.AddListener(() =>
            {
                PlayEndTurnSound();
                _controller?.RequestEndTurn();
            });
        }

        private void EnsureEndTurnCanvasGroup()
        {
            if (_endTurnButton == null || _endTurnCanvasGroup != null) return;
            _endTurnCanvasGroup = _endTurnButton.GetComponent<CanvasGroup>();
            if (_endTurnCanvasGroup == null)
            {
                _endTurnCanvasGroup = _endTurnButton.gameObject.AddComponent<CanvasGroup>();
            }
            _endTurnCanvasGroup.alpha = 1f;
        }

        private void SetupEndTurnLocalization()
        {
            if (_endTurnButton != null)
            {
                if (_endTurnTMP == null)
                    _endTurnTMP = _endTurnButton.GetComponentInChildren<TMP_Text>(true);
                if (_endTurnText == null)
                    _endTurnText = _endTurnButton.GetComponentInChildren<Text>(true);
            }

            if (_endTurnLabel != null)
            {
                _endTurnLabel.StringChanged += HandleEndTurnLabelChanged;
                _endTurnLabel.RefreshString();
            }
        }

        private void TeardownEndTurnLocalization()
        {
            if (_endTurnLabel != null)
            {
                _endTurnLabel.StringChanged -= HandleEndTurnLabelChanged;
            }
        }

        private void HandleEndTurnLabelChanged(string value)
        {
            if (_endTurnTMP != null) _endTurnTMP.text = value;
            else if (_endTurnText != null) _endTurnText.text = value;
        }

        private void RefreshEndTurnLabel()
        {
            if (_endTurnLabel != null)
            {
                _endTurnLabel.RefreshString();
            }
        }

        private void SetupStatsLabelLocalization()
        {
            if (_statsPanelRoot == null)
            {
                return;
            }

            if (_lifeLabel == null || _attackLabel == null || _shootLabel == null ||
                _spellLabel == null || _speedLabel == null || _luckLabel == null ||
                _defenseLabel == null || _protectionLabel == null ||
                _initiativeLabel == null || _moraleLabel == null)
            {
                AutoDiscoverStatsLabelTexts();
            }

            SetupSingleStatLabel(ref _lifeLabelString, _lifeLabel, "stats.life", HandleLifeLabelChanged);
            SetupSingleStatLabel(ref _attackLabelString, _attackLabel, "stats.attack", HandleAttackLabelChanged);
            SetupSingleStatLabel(ref _shootLabelString, _shootLabel, "stats.shoot", HandleShootLabelChanged);
            SetupSingleStatLabel(ref _spellLabelString, _spellLabel, "stats.spell", HandleSpellLabelChanged);
            SetupSingleStatLabel(ref _speedLabelString, _speedLabel, "stats.speed", HandleSpeedLabelChanged);
            SetupSingleStatLabel(ref _luckLabelString, _luckLabel, "stats.luck", HandleLuckLabelChanged);
            SetupSingleStatLabel(ref _defenseLabelString, _defenseLabel, "stats.defense", HandleDefenseLabelChanged);
            SetupSingleStatLabel(ref _protectionLabelString, _protectionLabel, "stats.protection", HandleProtectionLabelChanged);
            SetupSingleStatLabel(ref _initiativeLabelString, _initiativeLabel, "stats.initiative", HandleInitiativeLabelChanged);
            SetupSingleStatLabel(ref _moraleLabelString, _moraleLabel, "stats.morale", HandleMoraleLabelChanged);

            RefreshStatsLabels();
        }

        private void TeardownStatsLabelLocalization()
        {
            TeardownSingleStatLabel(_lifeLabelString, HandleLifeLabelChanged);
            TeardownSingleStatLabel(_attackLabelString, HandleAttackLabelChanged);
            TeardownSingleStatLabel(_shootLabelString, HandleShootLabelChanged);
            TeardownSingleStatLabel(_spellLabelString, HandleSpellLabelChanged);
            TeardownSingleStatLabel(_speedLabelString, HandleSpeedLabelChanged);
            TeardownSingleStatLabel(_luckLabelString, HandleLuckLabelChanged);
            TeardownSingleStatLabel(_defenseLabelString, HandleDefenseLabelChanged);
            TeardownSingleStatLabel(_protectionLabelString, HandleProtectionLabelChanged);
            TeardownSingleStatLabel(_initiativeLabelString, HandleInitiativeLabelChanged);
            TeardownSingleStatLabel(_moraleLabelString, HandleMoraleLabelChanged);
        }

        private void RefreshStatsLabels()
        {
            RefreshSingleStatLabel(_lifeLabelString);
            RefreshSingleStatLabel(_attackLabelString);
            RefreshSingleStatLabel(_shootLabelString);
            RefreshSingleStatLabel(_spellLabelString);
            RefreshSingleStatLabel(_speedLabelString);
            RefreshSingleStatLabel(_luckLabelString);
            RefreshSingleStatLabel(_defenseLabelString);
            RefreshSingleStatLabel(_protectionLabelString);
            RefreshSingleStatLabel(_initiativeLabelString);
            RefreshSingleStatLabel(_moraleLabelString);
        }

        private void AutoDiscoverStatsLabelTexts()
        {
            if (_statsPanelRoot == null)
            {
                return;
            }

            var texts = _statsPanelRoot.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                var tmp = texts[i];

                // Skip value fields that are already bound explicitly.
                if (tmp == _lifeText || tmp == _forceText || tmp == _shootText ||
                    tmp == _spellText || tmp == _speedText || tmp == _luckText ||
                    tmp == _defenseText || tmp == _protectionText ||
                    tmp == _initiativeText || tmp == _moraleText)
                {
                    continue;
                }

                var name = tmp.gameObject.name;
                switch (name)
                {
                    case "StatsLabel_Life":
                        if (_lifeLabel == null) _lifeLabel = tmp;
                        break;
                    case "StatsLabel_Attack":
                        if (_attackLabel == null) _attackLabel = tmp;
                        break;
                    case "StatsLabel_Shoot":
                        if (_shootLabel == null) _shootLabel = tmp;
                        break;
                    case "StatsLabel_Spell":
                        if (_spellLabel == null) _spellLabel = tmp;
                        break;
                    case "StatsLabel_Speed":
                        if (_speedLabel == null) _speedLabel = tmp;
                        break;
                    case "StatsLabel_Luck":
                        if (_luckLabel == null) _luckLabel = tmp;
                        break;
                    case "StatsLabel_Defense":
                        if (_defenseLabel == null) _defenseLabel = tmp;
                        break;
                    case "StatsLabel_Protection":
                        if (_protectionLabel == null) _protectionLabel = tmp;
                        break;
                    case "StatsLabel_Initiative":
                        if (_initiativeLabel == null) _initiativeLabel = tmp;
                        break;
                    case "StatsLabel_Morale":
                        if (_moraleLabel == null) _moraleLabel = tmp;
                        break;
                }

                var raw = tmp.text;
                if (string.IsNullOrEmpty(raw))
                {
                    continue;
                }

                raw = raw.Trim();
                var labelKey = raw.EndsWith(":") ? raw.Substring(0, raw.Length - 1) : raw;

                switch (labelKey)
                {
                    case "Health":
                    case "Life":
                        if (_lifeLabel == null) _lifeLabel = tmp;
                        break;
                    case "Force":
                    case "Attack":
                        if (_attackLabel == null) _attackLabel = tmp;
                        break;
                    case "Shoot":
                        if (_shootLabel == null) _shootLabel = tmp;
                        break;
                    case "Spell":
                        if (_spellLabel == null) _spellLabel = tmp;
                        break;
                    case "Speed":
                        if (_speedLabel == null) _speedLabel = tmp;
                        break;
                    case "Luck":
                        if (_luckLabel == null) _luckLabel = tmp;
                        break;
                    case "Defense":
                        if (_defenseLabel == null) _defenseLabel = tmp;
                        break;
                    case "Protection":
                        if (_protectionLabel == null) _protectionLabel = tmp;
                        break;
                    case "Initiative":
                        if (_initiativeLabel == null) _initiativeLabel = tmp;
                        break;
                    case "Morale":
                        if (_moraleLabel == null) _moraleLabel = tmp;
                        break;
                }
            }
        }

        private void SetupSingleStatLabel(ref LocalizedString labelString, TMP_Text labelTarget, string key, LocalizedString.ChangeHandler handler)
        {
            if (labelTarget == null)
            {
                return;
            }

            if (labelString == null)
            {
                labelString = new LocalizedString("UI.Common", key);
            }

            labelString.StringChanged += handler;
        }

        private void TeardownSingleStatLabel(LocalizedString labelString, LocalizedString.ChangeHandler handler)
        {
            if (labelString == null)
            {
                return;
            }

            labelString.StringChanged -= handler;
        }

        private void RefreshSingleStatLabel(LocalizedString labelString)
        {
            if (labelString == null)
            {
                return;
            }

            labelString.RefreshString();
        }

        private void HandleLifeLabelChanged(string value)
        {
            if (_lifeLabel != null)
            {
                _lifeLabel.text = value;
            }
        }

        private void HandleAttackLabelChanged(string value)
        {
            if (_attackLabel != null)
            {
                _attackLabel.text = value;
            }
        }

        private void HandleShootLabelChanged(string value)
        {
            if (_shootLabel != null)
            {
                _shootLabel.text = value;
            }
        }

        private void HandleSpellLabelChanged(string value)
        {
            if (_spellLabel != null)
            {
                _spellLabel.text = value;
            }
        }

        private void HandleSpeedLabelChanged(string value)
        {
            if (_speedLabel != null)
            {
                _speedLabel.text = value;
            }
        }

        private void HandleLuckLabelChanged(string value)
        {
            if (_luckLabel != null)
            {
                _luckLabel.text = value;
            }
        }

        private void HandleDefenseLabelChanged(string value)
        {
            if (_defenseLabel != null)
            {
                _defenseLabel.text = value;
            }
        }

        private void HandleProtectionLabelChanged(string value)
        {
            if (_protectionLabel != null)
            {
                _protectionLabel.text = value;
            }
        }

        private void HandleInitiativeLabelChanged(string value)
        {
            if (_initiativeLabel != null)
            {
                _initiativeLabel.text = value;
            }
        }

        private void HandleMoraleLabelChanged(string value)
        {
            if (_moraleLabel != null)
            {
                _moraleLabel.text = value;
            }
        }

        private void SetupStatsPanel()
        {
            if (_statsPanelRoot == null) return;

            _statsPanelShownPosition = _statsPanelRoot.anchoredPosition;

            float offset = Mathf.Abs(_statsPanelOffscreenOffset) > 0.01f ? _statsPanelOffscreenOffset : _statsPanelRoot.rect.width;
            if (offset <= 0f)
            {
                offset = 400f;
            }

            _statsPanelHiddenPosition = _statsPanelShownPosition + new Vector2(offset, 0f);

            if (_statsPanelCanvasGroup == null)
            {
                _statsPanelCanvasGroup = _statsPanelRoot.GetComponent<CanvasGroup>();
                if (_statsPanelCanvasGroup == null)
                {
                    _statsPanelCanvasGroup = _statsPanelRoot.gameObject.AddComponent<CanvasGroup>();
                }
            }

            _statsPanelCanvasGroup.alpha = 0f;
            _statsPanelCanvasGroup.interactable = false;
            _statsPanelCanvasGroup.blocksRaycasts = false;
            _statsPanelRoot.anchoredPosition = _statsPanelHiddenPosition;
            _statsPanelRoot.gameObject.SetActive(false);

            if (_statsPanelBackgroundButton != null)
            {
                _statsPanelBackgroundButton.onClick.RemoveAllListeners();
                _statsPanelBackgroundButton.onClick.AddListener(HandleStatsBackgroundClicked);
                _statsPanelBackgroundButton.gameObject.SetActive(false);
            }

            _statsPanelVisible = false;
            _statsPanelAnimating = false;
        }

        private void WirePortraitClick()
        {
            // Prefer explicit assignment from inspector.
            if (_portraitButton == null && _activePortraitImage != null)
            {
                // First, look for a Button on the same GameObject as the portrait Image.
                _portraitButton = _activePortraitImage.GetComponent<Button>();
                if (_portraitButton == null)
                {
                    // Fallback: many UIs author the Image as a child of a Button root.
                    _portraitButton = _activePortraitImage.GetComponentInParent<Button>();
                }
            }

            if (_portraitButton != null)
            {
                _portraitButton.onClick.RemoveListener(HandlePortraitClicked);
                _portraitButton.onClick.AddListener(HandlePortraitClicked);
            }
            else if (_activePortraitImage != null)
            {
                Debug.LogWarning("TurnOrderHUD: No Button found for active portrait; stats panel will not open on click.", this);
            }
        }

        private void HandleActiveUnitChanged()
        {
            if (_controller == null) return;

            bool wasStatsPanelVisible = _statsPanelVisible;

            if (!_controller.HasActiveUnit)
            {
                CloseStatsPanelImmediate();
            }

            if (_activePortraitImage != null)
            {
                _activePortraitImage.sprite = _controller.ActiveUnitPortrait;
                _activePortraitImage.enabled = _controller.ActiveUnitPortrait != null;
            }

            if (_endTurnButton != null)
            {
                bool interactable = _controller.HasActiveUnit && _controller.IsActiveUnitPlayerControlled;
                _endTurnButton.interactable = interactable;
                if (_endTurnCanvasGroup != null)
                {
                    _endTurnCanvasGroup.alpha = interactable ? 1f : Mathf.Clamp01(_disabledAlpha);
                }
            }

            if (wasStatsPanelVisible && _controller.HasActiveUnit)
            {
                if (_controller.TryGetActiveUnitStats(out var stats))
                {
                    ApplyStatsToUI(stats);
                    if (!_statsPanelVisible)
                    {
                        ShowStatsPanel();
                    }
                }
            }

            RefreshActionPoints();
        }

        private void HandleActiveUnitActionPointsChanged()
        {
            RefreshActionPoints();
        }

        private void HandlePortraitClicked()
        {
            if (_controller == null || !_controller.HasActiveUnit)
            {
                return;
            }

            if (_statsPanelVisible)
            {
                HideStatsPanel();
                return;
            }

            if (_controller.TryGetActiveUnitStats(out var stats))
            {
                ApplyStatsToUI(stats);
                ShowStatsPanel();
            }
        }

        private void HandleStatsBackgroundClicked()
        {
            HideStatsPanel();
        }

        private void ApplyStatsToUI(UnitStatsViewData stats)
        {
            if (_lifeText != null)
            {
                if (stats.MaxLife > 0)
                {
                    _lifeText.text = $"{stats.Life} / {stats.MaxLife}";
                }
                else
                {
                    _lifeText.text = stats.Life.ToString();
                }
            }
            if (_forceText != null) _forceText.text = stats.Force.ToString();
            if (_shootText != null) _shootText.text = stats.Shoot.ToString();
            if (_spellText != null) _spellText.text = stats.Spell.ToString();
            if (_speedText != null) _speedText.text = stats.Speed.ToString();
            if (_luckText != null) _luckText.text = stats.Luck.ToString();
            if (_defenseText != null) _defenseText.text = stats.Defense.ToString();
            if (_protectionText != null) _protectionText.text = stats.Protection.ToString();
            if (_initiativeText != null) _initiativeText.text = stats.Initiative.ToString();
            if (_moraleText != null) _moraleText.text = stats.Morale.ToString();
        }

        private void RefreshActionPoints()
        {
            if (_actionPointSlots == null || _actionPointSlots.Length == 0)
            {
                if (_actionPointBarRoot != null)
                {
                    _actionPointBarRoot.gameObject.SetActive(false);
                }
                return;
            }

            if (_controller == null || !_controller.HasActiveUnit)
            {
                SetActionPointBarVisible(false);
                return;
            }

            int current = Mathf.Max(0, _controller.ActiveUnitCurrentActionPoints);
            int max = Mathf.Max(0, _controller.ActiveUnitMaxActionPoints);

            int slotCount = _actionPointSlots.Length;
            if (slotCount <= 0)
            {
                SetActionPointBarVisible(false);
                return;
            }

            max = Mathf.Clamp(max, 0, slotCount);
            current = Mathf.Clamp(current, 0, max);

            if (max <= 0 || current <= 0)
            {
                SetActionPointBarVisible(false);
                return;
            }

            SetActionPointBarVisible(true);

            for (int i = 0; i < slotCount; i++)
            {
                var img = _actionPointSlots[i];
                if (img == null)
                {
                    continue;
                }

                // Ensure the slot occupies layout space while the bar is visible.
                if (!img.gameObject.activeSelf)
                {
                    img.gameObject.SetActive(true);
                }

                if (i < max)
                {
                    bool isFull = i < current;
                    if (_actionPointFullSprite != null && _actionPointEmptySprite != null)
                    {
                        img.sprite = isFull ? _actionPointFullSprite : _actionPointEmptySprite;
                    }
                    img.enabled = true;
                }
                else
                {
                    // Hide the icon visually but keep the RectTransform active for spacing.
                    img.enabled = false;
                }
            }
        }

        private void SetActionPointBarVisible(bool visible)
        {
            if (_actionPointBarRoot != null)
            {
                _actionPointBarRoot.gameObject.SetActive(visible);
            }

            if (!visible && _actionPointSlots != null)
            {
                for (int i = 0; i < _actionPointSlots.Length; i++)
                {
                    var img = _actionPointSlots[i];
                    if (img != null)
                    {
                        img.gameObject.SetActive(false);
                    }
                }
            }
        }

        private void ShowStatsPanel()
        {
            if (_statsPanelRoot == null) return;

            _statsPanelVisible = true;

            if (_statsPanelBackgroundButton != null)
            {
                _statsPanelBackgroundButton.gameObject.SetActive(true);
            }

            if (!Application.isPlaying)
            {
                ApplyStatsPanelImmediate(true);
                return;
            }

            StopStatsPanelRoutine();
            _statsPanelRoutine = StatsPanelRoutine(true);
            StartCoroutine(_statsPanelRoutine);
        }

        private void HideStatsPanel()
        {
            if (_statsPanelRoot == null) return;

            if (!_statsPanelVisible && !_statsPanelAnimating)
            {
                return;
            }

            _statsPanelVisible = false;

            if (_statsPanelBackgroundButton != null)
            {
                _statsPanelBackgroundButton.gameObject.SetActive(false);
            }

            if (!Application.isPlaying)
            {
                ApplyStatsPanelImmediate(false);
                return;
            }

            StopStatsPanelRoutine();
            _statsPanelRoutine = StatsPanelRoutine(false);
            StartCoroutine(_statsPanelRoutine);
        }

        private void CloseStatsPanelImmediate()
        {
            if (_statsPanelRoot == null) return;

            if (!_statsPanelVisible && !_statsPanelAnimating)
            {
                return;
            }

            _statsPanelVisible = false;

            if (_statsPanelBackgroundButton != null)
            {
                _statsPanelBackgroundButton.gameObject.SetActive(false);
            }

            StopStatsPanelRoutine();
            ApplyStatsPanelImmediate(false);
        }

        private void ApplyStatsPanelImmediate(bool open)
        {
            if (_statsPanelRoot == null) return;

            if (_statsPanelCanvasGroup == null)
            {
                _statsPanelCanvasGroup = _statsPanelRoot.GetComponent<CanvasGroup>();
                if (_statsPanelCanvasGroup == null)
                {
                    _statsPanelCanvasGroup = _statsPanelRoot.gameObject.AddComponent<CanvasGroup>();
                }
            }

            _statsPanelRoot.gameObject.SetActive(open);
            _statsPanelRoot.anchoredPosition = open ? _statsPanelShownPosition : _statsPanelHiddenPosition;
            _statsPanelCanvasGroup.alpha = open ? 1f : 0f;
            _statsPanelCanvasGroup.interactable = open;
            _statsPanelCanvasGroup.blocksRaycasts = open;
        }

        private System.Collections.IEnumerator StatsPanelRoutine(bool open)
        {
            if (_statsPanelRoot == null)
            {
                yield break;
            }

            if (_statsPanelCanvasGroup == null)
            {
                _statsPanelCanvasGroup = _statsPanelRoot.GetComponent<CanvasGroup>();
                if (_statsPanelCanvasGroup == null)
                {
                    _statsPanelCanvasGroup = _statsPanelRoot.gameObject.AddComponent<CanvasGroup>();
                }
            }

            _statsPanelAnimating = true;

            float duration = Mathf.Max(0.01f, _statsPanelSlideDuration);
            float t = 0f;

            Vector2 fromPos = open ? _statsPanelHiddenPosition : _statsPanelShownPosition;
            Vector2 toPos = open ? _statsPanelShownPosition : _statsPanelHiddenPosition;
            float fromAlpha = open ? 0f : 1f;
            float toAlpha = open ? 1f : 0f;

            _statsPanelRoot.gameObject.SetActive(true);
            _statsPanelCanvasGroup.alpha = fromAlpha;
            _statsPanelCanvasGroup.interactable = false;
            _statsPanelCanvasGroup.blocksRaycasts = false;

            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / duration);
                // Smooth in/out using a cubic smoothstep.
                float eased = p * p * (3f - 2f * p);
                _statsPanelRoot.anchoredPosition = Vector2.LerpUnclamped(fromPos, toPos, eased);
                _statsPanelCanvasGroup.alpha = Mathf.LerpUnclamped(fromAlpha, toAlpha, eased);
                yield return null;
            }

            _statsPanelRoot.anchoredPosition = toPos;
            _statsPanelCanvasGroup.alpha = toAlpha;
            _statsPanelCanvasGroup.interactable = open;
            _statsPanelCanvasGroup.blocksRaycasts = open;

            if (!open)
            {
                _statsPanelRoot.gameObject.SetActive(false);
            }

            _statsPanelAnimating = false;
            _statsPanelRoutine = null;
        }

        private void StopStatsPanelRoutine()
        {
            if (_statsPanelRoutine == null) return;
            try
            {
                StopCoroutine(_statsPanelRoutine);
            }
            catch
            {
                // Ignore if coroutine is not running.
            }
            _statsPanelRoutine = null;
            _statsPanelAnimating = false;
        }

        private void PlayEndTurnSound()
        {
            if (_endTurnClip == null) return;
            if (_audio != null)
            {
                _audio.PlayOneShot(_endTurnClip);
            }
            else
            {
                AudioSource.PlayClipAtPoint(_endTurnClip, Vector3.zero, 1f);
            }
        }
    }
}
