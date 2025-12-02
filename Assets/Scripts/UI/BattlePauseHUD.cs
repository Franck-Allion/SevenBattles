using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization;
using TMPro;
using SevenBattles.Core;

namespace SevenBattles.UI
{
    // Pause menu overlay for battle scenes, driven by the Escape key.
    // Uses IBattleTurnController to respect player/AI turns and interaction locking.
    public class BattlePauseHUD : MonoBehaviour
    {
        [Header("Controller")]
        [SerializeField, Tooltip("Reference to a MonoBehaviour that implements IBattleTurnController. If not assigned, one will be auto-found at runtime.")]
        private MonoBehaviour _controllerBehaviour;

        [Header("Menu Root")]
        [SerializeField, Tooltip("CanvasGroup controlling the pause menu panel (alpha and input).")]
        private CanvasGroup _menuCanvasGroup;
        [SerializeField, Tooltip("Optional RectTransform used for scale-in/scale-out animation of the menu panel.")]
        private RectTransform _menuRoot;

        [Header("Blur Overlay")]
        [SerializeField, Tooltip("CanvasGroup for the full-screen blur overlay behind the menu.")]
        private CanvasGroup _blurCanvasGroup;

        [Header("Buttons")]
        [SerializeField] private Button _saveButton;
        [SerializeField] private Button _loadButton;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _quitButton;
        [SerializeField, Tooltip("Optional Cancel/Back button that closes the pause menu without quitting.")]
        private Button _cancelButton;
        [SerializeField, Tooltip("Optional explicit close button (e.g., X). If null, ESC will still close the menu.")]
        private Button _closeButton;

        [Header("Title (Localization)")]
        [SerializeField, Tooltip("Localized title label for the pause menu (Table: UI.Common, Entry: Pause.Title).")]
        private LocalizedString _titleLabel;
        [SerializeField, Tooltip("Optional explicit reference to the title Text component. If not set, a child Text will be auto-found at runtime.")]
        private Text _titleText;
        [SerializeField, Tooltip("Optional explicit reference to the title TMP_Text component. If not set, a child TMP_Text will be auto-found at runtime.")]
        private TMP_Text _titleTMP;

        [Header("Button Labels (Localization)")]
        [SerializeField, Tooltip("Localized label for the Save button (Table: UI.Common, Entry: Pause.Save).")]
        private LocalizedString _saveLabel;
        [SerializeField, Tooltip("Localized label for the Load button (Table: UI.Common, Entry: Pause.Load).")]
        private LocalizedString _loadLabel;
        [SerializeField, Tooltip("Localized label for the Settings button (Table: UI.Common, Entry: Pause.Settings).")]
        private LocalizedString _settingsLabel;
        [SerializeField, Tooltip("Localized label for the Quit button (Table: UI.Common, Entry: Pause.Quit).")]
        private LocalizedString _quitLabel;
        [SerializeField, Tooltip("Localized label for the Cancel button (Table: UI.Common, Entry: Common.Cancel).")]
        private LocalizedString _cancelLabel;

        [SerializeField, Tooltip("Optional explicit reference to the Save button label Text component. If not set, a child Text will be auto-found at runtime.")]
        private Text _saveText;
        [SerializeField, Tooltip("Optional explicit reference to the Save button label TMP_Text component. If not set, a child TMP_Text will be auto-found at runtime.")]
        private TMP_Text _saveTMP;
        [SerializeField, Tooltip("Optional explicit reference to the Load button label Text component. If not set, a child Text will be auto-found at runtime.")]
        private Text _loadText;
        [SerializeField, Tooltip("Optional explicit reference to the Load button label TMP_Text component. If not set, a child TMP_Text will be auto-found at runtime.")]
        private TMP_Text _loadTMP;
        [SerializeField, Tooltip("Optional explicit reference to the Settings button label Text component. If not set, a child Text will be auto-found at runtime.")]
        private Text _settingsText;
        [SerializeField, Tooltip("Optional explicit reference to the Settings button label TMP_Text component. If not set, a child TMP_Text will be auto-found at runtime.")]
        private TMP_Text _settingsTMP;
        [SerializeField, Tooltip("Optional explicit reference to the Quit button label Text component. If not set, a child Text will be auto-found at runtime.")]
        private Text _quitText;
        [SerializeField, Tooltip("Optional explicit reference to the Quit button label TMP_Text component. If not set, a child TMP_Text will be auto-found at runtime.")]
        private TMP_Text _quitTMP;
        [SerializeField, Tooltip("Optional explicit reference to the Cancel button label Text component. If not set, a child Text will be auto-found at runtime.")]
        private Text _cancelText;
        [SerializeField, Tooltip("Optional explicit reference to the Cancel button label TMP_Text component. If not set, a child TMP_Text will be auto-found at runtime.")]
        private TMP_Text _cancelTMP;

        [Header("Animation")]
        [SerializeField, Tooltip("Fade duration in seconds for opening/closing the pause menu. Uses unscaled time so it works when the game is paused.")]
        private float _fadeDuration = 0.2f;
        [SerializeField, Tooltip("Starting scale factor for the menu panel when animating in.")]
        private float _menuScaleFrom = 0.9f;

        [Header("Quit Confirmation")]
        [SerializeField, Tooltip("Optional reusable confirmation dialog prefab used when the Quit button is pressed.")]
        private ConfirmationMessageBoxHUD _quitConfirmation;

        [Header("Save/Load")]
        [SerializeField, Tooltip("Optional Save/Load HUD overlay to open when the Save button is pressed.")]
        private SaveLoadHUD _saveLoadHud;

        private IBattleTurnController _battleTurnController;
        private bool _buttonsWired;
        private bool _isOpen;
        private bool _hasInteractionLock;
        private bool _timeScaleOverridden;
        private float _previousTimeScale = 1f;
        private Coroutine _transitionRoutine;

        public event Action SaveClicked;
        public event Action LoadClicked;
        public event Action SettingsClicked;
        public event Action QuitClicked;

        private void Awake()
        {
            ResolveController();
            EnsureCanvasReferences();
            EnsureInitialState();
            WireButtonsIfNeeded();
            SetupTitleLocalization();
            SetupButtonLocalization();

            if (_saveLoadHud != null)
            {
                _saveLoadHud.LoadCompleted += HandleLoadCompleted;
            }
        }

        private void OnEnable()
        {
            ResolveController();
            EnsureCanvasReferences();
            EnsureInitialState();
            RefreshTitleLabel();
            RefreshButtonLabels();
        }

        private void OnDisable()
        {
            TeardownTitleLocalization();
            TeardownButtonLocalization();

              if (_isOpen)
              {
                  CloseImmediate();
              }

            if (_saveLoadHud != null)
            {
                _saveLoadHud.LoadCompleted -= HandleLoadCompleted;
            }
        }

          private void Update()
          {
              if (Input.GetKeyDown(KeyCode.Escape))
              {
                  HandleEscapePressed();
              }
          }

          private void HandleEscapePressed()
          {
              if (_quitConfirmation != null && _quitConfirmation.IsVisible)
              {
                  // Let the confirmation dialog handle ESC (it will map to Cancel).
                  return;
              }

              if (_isOpen)
              {
                  ClosePauseMenu();
              }
              else
              {
                  ResolveController();

                  // Block pause only when an AI-controlled unit is actively taking a turn.
                  if (_battleTurnController != null &&
                      _battleTurnController.HasActiveUnit &&
                      !_battleTurnController.IsActiveUnitPlayerControlled)
                  {
                      return;
                  }

                  OpenPauseMenu();
              }
          }

        private void ResolveController()
        {
            if (_battleTurnController != null)
            {
                return;
            }

            if (_controllerBehaviour != null)
            {
                _battleTurnController = _controllerBehaviour as IBattleTurnController;
                return;
            }

            var behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            for (int i = 0; i < behaviours.Length; i++)
            {
                var candidate = behaviours[i] as IBattleTurnController;
                if (candidate != null)
                {
                    _battleTurnController = candidate;
                    _controllerBehaviour = behaviours[i];
                    break;
                }
            }
        }

        private void EnsureCanvasReferences()
        {
            // Try to auto-discover blur and menu CanvasGroups / RectTransforms when not explicitly assigned.
            if (_blurCanvasGroup == null || _menuCanvasGroup == null)
            {
                var groups = GetComponentsInChildren<CanvasGroup>(true);
                for (int i = 0; i < groups.Length; i++)
                {
                    var cg = groups[i];

                    if (_blurCanvasGroup == null &&
                        cg.gameObject.name.IndexOf("blur", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        _blurCanvasGroup = cg;
                        continue;
                    }

                    if (_menuCanvasGroup == null &&
                        cg != _blurCanvasGroup)
                    {
                        _menuCanvasGroup = cg;
                    }
                }
            }

            if (_menuRoot == null && _menuCanvasGroup != null)
            {
                _menuRoot = _menuCanvasGroup.GetComponent<RectTransform>();
            }
        }

        private void EnsureInitialState()
        {
            if (_menuCanvasGroup != null)
            {
                _menuCanvasGroup.alpha = 0f;
                _menuCanvasGroup.interactable = false;
                _menuCanvasGroup.blocksRaycasts = false;
                _menuCanvasGroup.gameObject.SetActive(false);
            }

            if (_blurCanvasGroup != null)
            {
                _blurCanvasGroup.alpha = 0f;
                _blurCanvasGroup.blocksRaycasts = false;
                _blurCanvasGroup.gameObject.SetActive(false);
            }

            if (_menuRoot != null)
            {
                _menuRoot.localScale = Vector3.one;
            }

            _isOpen = false;
        }

        private void SetupTitleLocalization()
        {
            AutoDiscoverTitleTarget();

            if (_titleLabel == null)
            {
                _titleLabel = new LocalizedString("UI.Common", "Pause.Title");
            }

            _titleLabel.StringChanged += HandleTitleLabelChanged;
        }

        private void TeardownTitleLocalization()
        {
            if (_titleLabel != null)
            {
                _titleLabel.StringChanged -= HandleTitleLabelChanged;
            }
        }

        private void WireButtonsIfNeeded()
        {
            if (_buttonsWired)
            {
                return;
            }

            if (_saveButton != null)
            {
                _saveButton.onClick.AddListener(OnSaveClicked);
            }

            if (_loadButton != null)
            {
                _loadButton.onClick.AddListener(OnLoadClicked);
            }

            if (_settingsButton != null)
            {
                _settingsButton.onClick.AddListener(OnSettingsClicked);
            }

            if (_quitButton != null)
            {
                _quitButton.onClick.AddListener(OnQuitClicked);
            }

            if (_cancelButton != null)
            {
                _cancelButton.onClick.AddListener(OnCancelClicked);
            }

            if (_closeButton != null)
            {
                _closeButton.onClick.AddListener(OnCloseClicked);
            }

            _buttonsWired = true;
        }

        private void SetupButtonLocalization()
        {
            AutoDiscoverButtonLabelTargets();

            if (_saveButton != null)
            {
                if (_saveLabel == null)
                {
                    _saveLabel = new LocalizedString("UI.Common", "Pause.Save");
                }
                _saveLabel.StringChanged += HandleSaveLabelChanged;
            }

            if (_loadButton != null)
            {
                if (_loadLabel == null)
                {
                    _loadLabel = new LocalizedString("UI.Common", "Pause.Load");
                }
                _loadLabel.StringChanged += HandleLoadLabelChanged;
            }

            if (_settingsButton != null)
            {
                if (_settingsLabel == null)
                {
                    _settingsLabel = new LocalizedString("UI.Common", "Pause.Settings");
                }
                _settingsLabel.StringChanged += HandleSettingsLabelChanged;
            }

            if (_quitButton != null)
            {
                if (_quitLabel == null)
                {
                    _quitLabel = new LocalizedString("UI.Common", "Pause.Quit");
                }
                _quitLabel.StringChanged += HandleQuitLabelChanged;
            }

            if (_cancelButton != null)
            {
                if (_cancelLabel == null)
                {
                    _cancelLabel = new LocalizedString("UI.Common", "Common.Cancel");
                }

                _cancelLabel.StringChanged += HandleCancelLabelChanged;
            }

            RefreshButtonLabels();
        }

        private void RefreshTitleLabel()
        {
            _titleLabel?.RefreshString();
        }

        private void TeardownButtonLocalization()
        {
            if (_saveLabel != null)
            {
                _saveLabel.StringChanged -= HandleSaveLabelChanged;
            }

            if (_loadLabel != null)
            {
                _loadLabel.StringChanged -= HandleLoadLabelChanged;
            }

            if (_settingsLabel != null)
            {
                _settingsLabel.StringChanged -= HandleSettingsLabelChanged;
            }

            if (_quitLabel != null)
            {
                _quitLabel.StringChanged -= HandleQuitLabelChanged;
            }

            if (_cancelLabel != null)
            {
                _cancelLabel.StringChanged -= HandleCancelLabelChanged;
            }
        }

        private void RefreshButtonLabels()
        {
            _saveLabel?.RefreshString();
            _loadLabel?.RefreshString();
            _settingsLabel?.RefreshString();
            _quitLabel?.RefreshString();
            _cancelLabel?.RefreshString();
        }

        private void AutoDiscoverButtonLabelTargets()
        {
            AutoDiscoverTitleTarget();

            if (_saveButton != null)
            {
                if (_saveTMP == null)
                {
                    _saveTMP = _saveButton.GetComponentInChildren<TMP_Text>(true);
                }
                if (_saveText == null)
                {
                    _saveText = _saveButton.GetComponentInChildren<Text>(true);
                }
            }

            if (_loadButton != null)
            {
                if (_loadTMP == null)
                {
                    _loadTMP = _loadButton.GetComponentInChildren<TMP_Text>(true);
                }
                if (_loadText == null)
                {
                    _loadText = _loadButton.GetComponentInChildren<Text>(true);
                }
            }

            if (_settingsButton != null)
            {
                if (_settingsTMP == null)
                {
                    _settingsTMP = _settingsButton.GetComponentInChildren<TMP_Text>(true);
                }
                if (_settingsText == null)
                {
                    _settingsText = _settingsButton.GetComponentInChildren<Text>(true);
                }
            }

            if (_quitButton != null)
            {
                if (_quitTMP == null)
                {
                    _quitTMP = _quitButton.GetComponentInChildren<TMP_Text>(true);
                }
                if (_quitText == null)
                {
                    _quitText = _quitButton.GetComponentInChildren<Text>(true);
                }
            }

            if (_cancelButton != null)
            {
                if (_cancelTMP == null)
                {
                    _cancelTMP = _cancelButton.GetComponentInChildren<TMP_Text>(true);
                }

                if (_cancelText == null)
                {
                    _cancelText = _cancelButton.GetComponentInChildren<Text>(true);
                }
            }
        }

        private void OpenPauseMenu()
        {
            if (_isOpen)
            {
                return;
            }

            _isOpen = true;

            if (!_timeScaleOverridden)
            {
                _previousTimeScale = Time.timeScale;
                Time.timeScale = 0f;
                _timeScaleOverridden = true;
            }

            if (_battleTurnController != null && !_hasInteractionLock)
            {
                _battleTurnController.SetInteractionLocked(true);
                _hasInteractionLock = true;
            }

            StartTransition(true);
        }

        private void ClosePauseMenu()
        {
            if (!_isOpen)
            {
                return;
            }

            _isOpen = false;
            StartTransition(false);
        }

        private void CloseImmediate()
        {
            _isOpen = false;

            if (_transitionRoutine != null)
            {
                try
                {
                    StopCoroutine(_transitionRoutine);
                }
                catch
                {
                    // Ignore if coroutine is not running.
                }

                _transitionRoutine = null;
            }

            if (_menuCanvasGroup != null)
            {
                _menuCanvasGroup.alpha = 0f;
                _menuCanvasGroup.interactable = false;
                _menuCanvasGroup.blocksRaycasts = false;
                _menuCanvasGroup.gameObject.SetActive(false);
            }

            if (_blurCanvasGroup != null)
            {
                _blurCanvasGroup.alpha = 0f;
                _blurCanvasGroup.blocksRaycasts = false;
                _blurCanvasGroup.gameObject.SetActive(false);
            }

            if (_menuRoot != null)
            {
                _menuRoot.localScale = Vector3.one;
            }

            ReleaseInteractionLockAndTimeScale();
        }

        private void StartTransition(bool open)
        {
            if (_menuCanvasGroup == null && _blurCanvasGroup == null)
            {
                return;
            }

            if (_fadeDuration <= 0f)
            {
                ApplyInstantState(open);
                return;
            }

            if (_transitionRoutine != null)
            {
                try
                {
                    StopCoroutine(_transitionRoutine);
                }
                catch
                {
                    // Ignore if coroutine is not running.
                }
            }

            _transitionRoutine = StartCoroutine(TransitionRoutine(open));
        }

        private void ApplyInstantState(bool open)
        {
            float alpha = open ? 1f : 0f;

            if (_blurCanvasGroup != null)
            {
                _blurCanvasGroup.gameObject.SetActive(open);
                _blurCanvasGroup.alpha = alpha;
                // Blur is visual only; do not consume raycasts so menu buttons remain clickable.
                _blurCanvasGroup.blocksRaycasts = false;
            }

            if (_menuCanvasGroup != null)
            {
                _menuCanvasGroup.gameObject.SetActive(open);
                _menuCanvasGroup.alpha = alpha;
                _menuCanvasGroup.interactable = open;
                _menuCanvasGroup.blocksRaycasts = open;
            }

            if (_menuRoot != null)
            {
                _menuRoot.localScale = Vector3.one;
            }

            if (!open)
            {
                ReleaseInteractionLockAndTimeScale();
            }
        }

        private System.Collections.IEnumerator TransitionRoutine(bool open)
        {
            float duration = Mathf.Max(0.01f, _fadeDuration);
            float t = 0f;

            if (open)
            {
                if (_blurCanvasGroup != null)
                {
                    _blurCanvasGroup.gameObject.SetActive(true);
                    _blurCanvasGroup.blocksRaycasts = true;
                    _blurCanvasGroup.alpha = 0f;
                }

                if (_menuCanvasGroup != null)
                {
                    _menuCanvasGroup.gameObject.SetActive(true);
                    _menuCanvasGroup.interactable = true;
                    _menuCanvasGroup.blocksRaycasts = true;
                    _menuCanvasGroup.alpha = 0f;
                }

                if (_menuRoot != null)
                {
                    _menuRoot.localScale = Vector3.one * Mathf.Max(0.01f, _menuScaleFrom);
                }
            }

            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / duration);
                float eased = p * p * (3f - 2f * p);

                float alpha = open ? eased : 1f - eased;

                if (_blurCanvasGroup != null)
                {
                    _blurCanvasGroup.alpha = alpha;
                }

                if (_menuCanvasGroup != null)
                {
                    _menuCanvasGroup.alpha = alpha;
                }

                if (_menuRoot != null)
                {
                    float from = Mathf.Max(0.01f, _menuScaleFrom);
                    float to = 1f;
                    float scale = open
                        ? Mathf.LerpUnclamped(from, to, eased)
                        : Mathf.LerpUnclamped(to, from, eased);
                    _menuRoot.localScale = new Vector3(scale, scale, 1f);
                }

                yield return null;
            }

            float finalAlpha = open ? 1f : 0f;

            if (_blurCanvasGroup != null)
            {
                _blurCanvasGroup.alpha = finalAlpha;
                // Keep blur as a purely visual overlay; clicks should go to the menu.
                _blurCanvasGroup.blocksRaycasts = false;
                _blurCanvasGroup.gameObject.SetActive(open);
            }

            if (_menuCanvasGroup != null)
            {
                _menuCanvasGroup.alpha = finalAlpha;
                _menuCanvasGroup.interactable = open;
                _menuCanvasGroup.blocksRaycasts = open;
                _menuCanvasGroup.gameObject.SetActive(open);
            }

            if (!open)
            {
                if (_menuRoot != null)
                {
                    _menuRoot.localScale = Vector3.one;
                }

                ReleaseInteractionLockAndTimeScale();
            }

            _transitionRoutine = null;
        }

        private void ReleaseInteractionLockAndTimeScale()
        {
            if (_hasInteractionLock && _battleTurnController != null)
            {
                _battleTurnController.SetInteractionLocked(false);
            }

            _hasInteractionLock = false;

            if (_timeScaleOverridden)
            {
                Time.timeScale = _previousTimeScale;
                _timeScaleOverridden = false;
            }
        }

        private void OnSaveClicked()
        {
            if (!_isOpen)
            {
                return;
            }

            SaveClicked?.Invoke();

            if (_saveLoadHud != null)
            {
                _saveLoadHud.ShowSave();
            }
        }

        private void OnLoadClicked()
        {
            if (!_isOpen)
            {
                return;
            }

            LoadClicked?.Invoke();

            if (_saveLoadHud != null)
            {
                _saveLoadHud.ShowLoad();
            }
        }

          private void OnSettingsClicked()
          {
              if (!_isOpen)
              {
                  return;
              }

            SettingsClicked?.Invoke();
        }

          private void OnQuitClicked()
          {
              if (!_isOpen)
              {
                  return;
              }

              if (_quitConfirmation == null)
              {
                  QuitClicked?.Invoke();
                  QuitGame();
                  return;
              }

              var title = new LocalizedString("UI.Common", "Confirm.QuitTitle");
              var message = new LocalizedString("UI.Common", "Confirm.QuitMessage");
              var confirmLabel = new LocalizedString("UI.Common", "Common.Yes");
              var cancelLabel = new LocalizedString("UI.Common", "Common.No");

              _quitConfirmation.Show(
                  title,
                  message,
                  confirmLabel,
                  cancelLabel,
                  () =>
                  {
                      QuitClicked?.Invoke();
                      QuitGame();
                  },
                  () =>
                  {
                      // Cancel: keep pause menu visible, no-op.
                  });
          }

          private void OnCancelClicked()
          {
              if (!_isOpen)
              {
                  return;
              }

              ClosePauseMenu();
          }

          private void OnCloseClicked()
          {
              ClosePauseMenu();
          }

          private void QuitGame()
          {
#if UNITY_EDITOR
              UnityEditor.EditorApplication.isPlaying = false;
#else
              Application.Quit();
#endif
          }

        private void HandleLoadCompleted()
        {
            if (_isOpen)
            {
                ClosePauseMenu();
            }

            ResolveController();

            if (_battleTurnController != null &&
                _battleTurnController.TurnIndex > 0)
            {
                var placementHud = UnityEngine.Object.FindFirstObjectByType<SquadPlacementHUD>();
                if (placementHud != null)
                {
                    placementHud.EnterBattleModeFromLoad();
                }
            }
        }

        private void HandleSaveLabelChanged(string value)
        {
            if (_saveTMP != null)
            {
                _saveTMP.text = value;
            }
            else if (_saveText != null)
            {
                _saveText.text = value;
            }
        }

        private void HandleLoadLabelChanged(string value)
        {
            if (_loadTMP != null)
            {
                _loadTMP.text = value;
            }
            else if (_loadText != null)
            {
                _loadText.text = value;
            }
        }

        private void HandleSettingsLabelChanged(string value)
        {
            if (_settingsTMP != null)
            {
                _settingsTMP.text = value;
            }
            else if (_settingsText != null)
            {
                _settingsText.text = value;
            }
        }

        private void HandleQuitLabelChanged(string value)
        {
            if (_quitTMP != null)
            {
                _quitTMP.text = value;
            }
            else if (_quitText != null)
            {
                _quitText.text = value;
            }
        }

        private void HandleCancelLabelChanged(string value)
        {
            if (_cancelTMP != null)
            {
                _cancelTMP.text = value;
            }
            else if (_cancelText != null)
            {
                _cancelText.text = value;
            }
        }

        private void AutoDiscoverTitleTarget()
        {
            if (_titleTMP == null || _titleText == null)
            {
                var texts = GetComponentsInChildren<TMP_Text>(true);
                for (int i = 0; i < texts.Length; i++)
                {
                    var tmp = texts[i];
                    if (tmp.gameObject.name.IndexOf("Title", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        _titleTMP = tmp;
                        break;
                    }
                }

                if (_titleTMP == null)
                {
                    var legacyTexts = GetComponentsInChildren<Text>(true);
                    for (int i = 0; i < legacyTexts.Length; i++)
                    {
                        var text = legacyTexts[i];
                        if (text.gameObject.name.IndexOf("Title", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            _titleText = text;
                            break;
                        }
                    }
                }
            }
        }

        private void HandleTitleLabelChanged(string value)
        {
            if (_titleTMP != null)
            {
                _titleTMP.text = value;
            }
            else if (_titleText != null)
            {
                _titleText.text = value;
            }
        }
    }
}
