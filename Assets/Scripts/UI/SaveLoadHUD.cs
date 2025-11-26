using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization;
using TMPro;
using SevenBattles.Core.Save;

namespace SevenBattles.UI
{
    /// <summary>
    /// Save/Load overlay showing up to 8 slots.
    /// In Save mode, clicking an empty slot saves immediately; clicking an occupied slot asks for overwrite confirmation.
    /// Uses a CanvasGroup for fade animation and to block input behind the overlay.
    /// </summary>
    public class SaveLoadHUD : MonoBehaviour
    {
        private const int MaxUiSlots = 8;
        private enum Mode
        {
            Save,
            Load
        }

        [Header("Root")]
        [SerializeField, Tooltip("CanvasGroup controlling the save/load overlay (alpha and input).")]
        private CanvasGroup _rootCanvasGroup;
        [SerializeField, Tooltip("Optional RectTransform used for scale-in/scale-out animation of the panel.")]
        private RectTransform _panelRoot;

        [Header("Slots")]
        [SerializeField, Tooltip("Buttons representing each save slot (up to 8).")]
        private Button[] _slotButtons;
        [SerializeField, Tooltip("Optional TMP_Text labels for each slot. If not set, a child TMP_Text will be auto-found per button.")]
        private TMP_Text[] _slotLabels;

        [Header("Save Service")]
        [SerializeField, Tooltip("Reference to a MonoBehaviour that implements ISaveGameService (e.g., SaveGameServiceComponent).")]
        private MonoBehaviour _saveGameServiceBehaviour;

        [Header("Title & Back")]
        [SerializeField, Tooltip("Optional Text component for the title. If not set, a child Text will be auto-found.")]
        private Text _titleText;
        [SerializeField, Tooltip("Optional TMP_Text component for the title. If not set, a child TMP_Text will be auto-found.")]
        private TMP_Text _titleTMP;

        [Header("Controls")]
        [SerializeField, Tooltip("Optional close button for the save/load panel.")]
        private Button _closeButton;
        [SerializeField, Tooltip("Reusable confirmation dialog used when overwriting an existing save.")]
        private ConfirmationMessageBoxHUD _overwriteConfirmation;

        [Header("Localization")]
        [SerializeField, Tooltip("Localized title used when the overlay is in Save mode.")]
        private LocalizedString _saveTitleLabel;
        [SerializeField, Tooltip("Localized title used when the overlay is in Load mode.")]
        private LocalizedString _loadTitleLabel;
        [SerializeField, Tooltip("Localized label used for empty slots (e.g., 'Empty Slot').")]
        private LocalizedString _emptySlotLabel;
        [SerializeField, Tooltip("Localized label format for used slots. Arguments: {0}=timestamp, {1}=run number.")]
        private LocalizedString _usedSlotLabel;
        [SerializeField, Tooltip("Localized label for the Back button that closes the overlay.")]
        private LocalizedString _backLabel;
        [SerializeField, Tooltip("Optional Text component for the Back button label.")]
        private Text _backText;
        [SerializeField, Tooltip("Optional TMP_Text component for the Back button label.")]
        private TMP_Text _backTMP;

        [Header("Animation")]
        [SerializeField, Tooltip("Fade duration in seconds for opening/closing the panel. Uses unscaled time.")]
        private float _fadeDuration = 0.15f;
        [SerializeField, Tooltip("Starting scale factor for the panel when animating in.")]
        private float _scaleFrom = 0.95f;

        private ISaveGameService _saveGameService;
        private SaveSlotMetadata[] _slots = Array.Empty<SaveSlotMetadata>();
        private bool _buttonsWired;
        private bool _isVisible;
        private Coroutine _transitionRoutine;
        private Mode _mode = Mode.Save;

        public bool IsVisible => _isVisible;

        public void SetSaveGameService(ISaveGameService service)
        {
            _saveGameService = service;
        }

        private void ResolveSaveService()
        {
            if (_saveGameService != null)
            {
                return;
            }

            if (_saveGameServiceBehaviour == null)
            {
                return;
            }

            _saveGameService = _saveGameServiceBehaviour as ISaveGameService;
            if (_saveGameService == null)
            {
                Debug.LogWarning("SaveLoadHUD: Assigned save service does not implement ISaveGameService.", this);
            }
        }

        private void Awake()
        {
            EnsureCanvasReferences();
            AutoDiscoverSlotLabels();
            WireButtonsIfNeeded();
            SetupLocalizationDefaults();
            SetupTitleAndBackLocalization();
            HideImmediate();
        }

        private void Update()
        {
            if (!_isVisible)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_overwriteConfirmation != null && _overwriteConfirmation.IsVisible)
                {
                    // Let the confirmation dialog handle ESC.
                    return;
                }

                Hide();
            }
        }

        /// <summary>
        /// Opens the panel in Save mode and refreshes slot metadata.
        /// </summary>
        public void ShowSave()
        {
            ShowInternal(Mode.Save);
        }

        /// <summary>
        /// Opens the panel in Load mode and refreshes slot metadata.
        /// Note: Load behavior is not implemented yet; slot clicks are ignored in this mode.
        /// </summary>
        public void ShowLoad()
        {
            ShowInternal(Mode.Load);
        }

        private void ShowInternal(Mode mode)
        {
            if (_saveGameService == null)
            {
                ResolveSaveService();
                if (_saveGameService == null)
                {
                    Debug.LogWarning("SaveLoadHUD: ISaveGameService is null. Assign a MonoBehaviour implementing ISaveGameService in the inspector.", this);
                    return;
                }
            }

            _mode = mode;

            EnsureCanvasReferences();
            AutoDiscoverSlotLabels();
            WireButtonsIfNeeded();
            SetupLocalizationDefaults();
            SetupTitleAndBackLocalization();
            RefreshTitleAndBackLabels();

            if (_isVisible)
            {
                StartCoroutine(LoadSlotsRoutine());
                return;
            }

            _isVisible = true;

            if (_rootCanvasGroup != null)
            {
                _rootCanvasGroup.gameObject.SetActive(true);
                _rootCanvasGroup.interactable = false;
                _rootCanvasGroup.blocksRaycasts = true;
                _rootCanvasGroup.alpha = 0f;
            }

            if (_panelRoot != null)
            {
                _panelRoot.localScale = Vector3.one * Mathf.Max(0.01f, _scaleFrom);
            }

            gameObject.SetActive(true);

            StartTransition(true);
            StartCoroutine(LoadSlotsRoutine());
        }

        private void Hide()
        {
            if (!_isVisible)
            {
                return;
            }

            StartTransition(false);
        }

        private void EnsureCanvasReferences()
        {
            if (_rootCanvasGroup == null)
            {
                _rootCanvasGroup = GetComponentInChildren<CanvasGroup>(true);
            }

            if (_panelRoot == null && _rootCanvasGroup != null)
            {
                _panelRoot = _rootCanvasGroup.GetComponent<RectTransform>();
            }
        }

        private void AutoDiscoverSlotLabels()
        {
            if (_slotButtons == null || _slotButtons.Length == 0)
            {
                return;
            }

            if (_slotLabels == null || _slotLabels.Length != _slotButtons.Length)
            {
                _slotLabels = new TMP_Text[_slotButtons.Length];
            }

            for (int i = 0; i < _slotButtons.Length; i++)
            {
                if (_slotLabels[i] != null)
                {
                    continue;
                }

                var btn = _slotButtons[i];
                if (btn == null)
                {
                    continue;
                }

                var label = btn.GetComponentInChildren<TMP_Text>(true);
                _slotLabels[i] = label;
            }
        }

        private void WireButtonsIfNeeded()
        {
            if (_buttonsWired)
            {
                return;
            }

            if (_slotButtons != null)
            {
                for (int i = 0; i < _slotButtons.Length && i < MaxUiSlots; i++)
                {
                    int captured = i;
                    var btn = _slotButtons[i];
                    if (btn != null)
                    {
                        btn.onClick.AddListener(() => OnSlotButtonClicked(captured));
                    }
                }
            }

            if (_closeButton != null)
            {
                _closeButton.onClick.AddListener(Hide);
            }

            _buttonsWired = true;
        }

        private void AutoDiscoverTitleTarget()
        {
            if (_titleTMP != null || _titleText != null)
            {
                return;
            }

            var tmps = GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < tmps.Length; i++)
            {
                var tmp = tmps[i];
                if (tmp.gameObject.name.IndexOf("Title", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _titleTMP = tmp;
                    return;
                }
            }

            var texts = GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                var txt = texts[i];
                if (txt.gameObject.name.IndexOf("Title", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _titleText = txt;
                    return;
                }
            }
        }

        private void AutoDiscoverBackLabelTargets()
        {
            if (_backTMP != null || _backText != null)
            {
                return;
            }

            if (_closeButton != null)
            {
                var tmps = _closeButton.GetComponentsInChildren<TMP_Text>(true);
                if (tmps.Length > 0)
                {
                    _backTMP = tmps[0];
                    return;
                }

                var texts = _closeButton.GetComponentsInChildren<Text>(true);
                if (texts.Length > 0)
                {
                    _backText = texts[0];
                    return;
                }
            }
        }

        private void SetupLocalizationDefaults()
        {
            if (_saveTitleLabel == null)
            {
                _saveTitleLabel = new LocalizedString("UI.Common", "SaveLoad.SaveTitle");
            }

            if (_loadTitleLabel == null)
            {
                _loadTitleLabel = new LocalizedString("UI.Common", "SaveLoad.LoadTitle");
            }

            if (_emptySlotLabel == null)
            {
                _emptySlotLabel = new LocalizedString("UI.Common", "Save.EmptySlot");
            }

            if (_usedSlotLabel == null)
            {
                _usedSlotLabel = new LocalizedString("UI.Common", "Save.SlotFormat");
            }

            if (_backLabel == null)
            {
                _backLabel = new LocalizedString("UI.Common", "Common.Back");
            }
        }

        private void SetupTitleAndBackLocalization()
        {
            AutoDiscoverTitleTarget();
            AutoDiscoverBackLabelTargets();
        }

        private IEnumerator LoadSlotsRoutine()
        {
            if (_saveGameService == null)
            {
                yield break;
            }

            var task = _saveGameService.LoadAllSlotMetadataAsync();
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.Exception != null)
            {
                Debug.LogError($"SaveLoadHUD: Failed to load save slot metadata. {task.Exception}");
                _slots = Array.Empty<SaveSlotMetadata>();
            }
            else
            {
                _slots = task.Result ?? Array.Empty<SaveSlotMetadata>();
            }

            UpdateAllSlotLabels();
        }

        private void RefreshTitleAndBackLabels()
        {
            LocalizedString titleLabel = null;

            switch (_mode)
            {
                case Mode.Save:
                    titleLabel = _saveTitleLabel;
                    break;
                case Mode.Load:
                    titleLabel = _loadTitleLabel;
                    break;
            }

            if (titleLabel != null)
            {
                var value = titleLabel.GetLocalizedString();
                if (_titleTMP != null)
                {
                    _titleTMP.text = value;
                }
                else if (_titleText != null)
                {
                    _titleText.text = value;
                }
            }

            if (_backLabel != null)
            {
                var back = _backLabel.GetLocalizedString();
                if (_backTMP != null)
                {
                    _backTMP.text = back;
                }
                else if (_backText != null)
                {
                    _backText.text = back;
                }
            }
        }

        private void UpdateAllSlotLabels()
        {
            if (_slotButtons == null || _slotButtons.Length == 0)
            {
                return;
            }

            int count = Mathf.Min(_slotButtons.Length, MaxUiSlots);
            for (int i = 0; i < count; i++)
            {
                UpdateSingleSlotLabel(i);
            }
        }

        private void UpdateSingleSlotLabel(int index)
        {
            if (_slotLabels == null || index < 0 || index >= _slotLabels.Length)
            {
                return;
            }

            var label = _slotLabels[index];
            if (label == null)
            {
                return;
            }

            SaveSlotMetadata metadata = null;
            if (_slots != null && index < _slots.Length)
            {
                metadata = _slots[index];
            }

            string text;

            if (metadata == null || !metadata.HasSave)
            {
                text = _emptySlotLabel.GetLocalizedString();
            }
            else
            {
                _usedSlotLabel.Arguments = new object[]
                {
                    metadata.TimestampString ?? string.Empty,
                    metadata.RunNumber
                };
                text = _usedSlotLabel.GetLocalizedString();
            }

            label.text = text;
        }

        private void OnSlotButtonClicked(int index)
        {
            if (!_isVisible)
            {
                return;
            }

            if (_mode == Mode.Load)
            {
                Debug.LogWarning("SaveLoadHUD: Slot clicks are ignored in Load mode (not implemented yet).", this);
                return;
            }

            bool hasSave = false;
            if (_slots != null && index >= 0 && index < _slots.Length)
            {
                hasSave = _slots[index].HasSave;
            }

            if (!hasSave)
            {
                StartCoroutine(SaveToSlotRoutine(index));
            }
            else
            {
                ShowOverwriteConfirmation(index);
            }
        }

        private void ShowOverwriteConfirmation(int index)
        {
            if (_overwriteConfirmation == null)
            {
                StartCoroutine(SaveToSlotRoutine(index));
                return;
            }

            var title = new LocalizedString("UI.Common", "Confirm.OverwriteSaveTitle");
            var message = new LocalizedString("UI.Common", "Confirm.OverwriteSaveMessage");
            var confirmLabel = new LocalizedString("UI.Common", "Common.Yes");
            var cancelLabel = new LocalizedString("UI.Common", "Common.No");

            _overwriteConfirmation.Show(
                title,
                message,
                confirmLabel,
                cancelLabel,
                () => { StartCoroutine(SaveToSlotRoutine(index)); },
                () => { });
        }

        private IEnumerator SaveToSlotRoutine(int index)
        {
            if (_saveGameService == null)
            {
                yield break;
            }

            int slotIndex = index + 1;

            var task = _saveGameService.SaveSlotAsync(slotIndex);
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.Exception != null)
            {
                Debug.LogError($"SaveLoadHUD: Failed to save slot {slotIndex}. {task.Exception}");
                yield break;
            }

            var metadata = task.Result;
            if (_slots == null || _slots.Length < slotIndex)
            {
                var resized = new SaveSlotMetadata[MaxUiSlots];
                if (_slots != null)
                {
                    for (int i = 0; i < _slots.Length && i < resized.Length; i++)
                    {
                        resized[i] = _slots[i];
                    }
                }

                _slots = resized;
            }

            _slots[index] = metadata;
            UpdateSingleSlotLabel(index);
        }

        private void StartTransition(bool visible)
        {
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

            _transitionRoutine = StartCoroutine(TransitionRoutine(visible));
        }

        private IEnumerator TransitionRoutine(bool visible)
        {
            if (_rootCanvasGroup == null)
            {
                yield break;
            }

            float duration = Mathf.Max(0.001f, _fadeDuration);
            float t = 0f;

            if (visible)
            {
                _rootCanvasGroup.gameObject.SetActive(true);
                _rootCanvasGroup.blocksRaycasts = true;
            }
            else
            {
                _rootCanvasGroup.interactable = false;
            }

            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float eased = Mathf.Clamp01(t / duration);

                if (visible)
                {
                    _rootCanvasGroup.alpha = Mathf.LerpUnclamped(0f, 1f, eased);
                }
                else
                {
                    _rootCanvasGroup.alpha = Mathf.LerpUnclamped(1f, 0f, eased);
                }

                if (_panelRoot != null)
                {
                    float from = Mathf.Max(0.01f, _scaleFrom);
                    float to = 1f;
                    float scale = visible
                        ? Mathf.LerpUnclamped(from, to, eased)
                        : Mathf.LerpUnclamped(to, from, eased);
                    _panelRoot.localScale = new Vector3(scale, scale, 1f);
                }

                yield return null;
            }

            if (!visible)
            {
                _rootCanvasGroup.alpha = 0f;
                _rootCanvasGroup.blocksRaycasts = false;
                _rootCanvasGroup.gameObject.SetActive(false);
                gameObject.SetActive(false);
                _isVisible = false;
            }
            else
            {
                _rootCanvasGroup.alpha = 1f;
                _rootCanvasGroup.interactable = true;
            }

            _transitionRoutine = null;
        }

        private void HideImmediate()
        {
            if (_transitionRoutine != null)
            {
                try
                {
                    StopCoroutine(_transitionRoutine);
                }
                catch
                {
                    // Ignore.
                }

                _transitionRoutine = null;
            }

            _isVisible = false;

            if (_rootCanvasGroup != null)
            {
                _rootCanvasGroup.alpha = 0f;
                _rootCanvasGroup.interactable = false;
                _rootCanvasGroup.blocksRaycasts = false;
                _rootCanvasGroup.gameObject.SetActive(false);
            }

            if (_panelRoot != null)
            {
                _panelRoot.localScale = Vector3.one;
            }

            gameObject.SetActive(false);
        }
    }
}
