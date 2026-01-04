using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization;
using TMPro;

namespace SevenBattles.UI
{
    /// <summary>
    /// Reusable confirmation message box with localized title/message and confirm/cancel buttons.
    /// Uses a CanvasGroup for fade/scale animation and to block input behind the dialog.
    /// </summary>
    public class ConfirmationMessageBoxHUD : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField, Tooltip("CanvasGroup controlling the entire confirmation overlay (alpha and input).")]
        private CanvasGroup _rootCanvasGroup;
        [SerializeField, Tooltip("Optional RectTransform used for scale-in/scale-out animation of the dialog panel.")]
        private RectTransform _dialogRoot;

        [Header("Title and Message")]
        [SerializeField, Tooltip("Optional Text component for the title. If not set, a child Text will be auto-found.")]
        private Text _titleText;
        [SerializeField, Tooltip("Optional TMP_Text component for the title. If not set, a child TMP_Text will be auto-found.")]
        private TMP_Text _titleTMP;
        [SerializeField, Tooltip("Optional Text component for the main message body. If not set, a child Text will be auto-found.")]
        private Text _messageText;
        [SerializeField, Tooltip("Optional TMP_Text component for the main message body. If not set, a child TMP_Text will be auto-found.")]
        private TMP_Text _messageTMP;

        [Header("Buttons")]
        [SerializeField, Tooltip("Confirm (Yes) button.")]
        private Button _confirmButton;
        [SerializeField, Tooltip("Cancel (No) button.")]
        private Button _cancelButton;

        [Header("Localization")]
        [SerializeField, Tooltip("Default localized title for the confirmation box.")]
        private LocalizedString _titleString;
        [SerializeField, Tooltip("Default localized message text for the confirmation box.")]
        private LocalizedString _messageString;
        [SerializeField, Tooltip("Default localized label for the Confirm button.")]
        private LocalizedString _confirmLabel;
        [SerializeField, Tooltip("Default localized label for the Cancel button.")]
        private LocalizedString _cancelLabel;

        [SerializeField, Tooltip("Optional Text component for the Confirm button label.")]
        private Text _confirmText;
        [SerializeField, Tooltip("Optional TMP_Text component for the Confirm button label.")]
        private TMP_Text _confirmTMP;
        [SerializeField, Tooltip("Optional Text component for the Cancel button label.")]
        private Text _cancelText;
        [SerializeField, Tooltip("Optional TMP_Text component for the Cancel button label.")]
        private TMP_Text _cancelTMP;

        [Header("Animation")]
        [SerializeField, Tooltip("Fade duration in seconds for opening/closing the confirmation box. Uses unscaled time.")]
        private float _fadeDuration = 0.15f;
        [SerializeField, Tooltip("Starting scale factor for the dialog panel when animating in.")]
        private float _scaleFrom = 0.95f;

        private bool _buttonsWired;
        private bool _isVisible;
        private Coroutine _transitionRoutine;

        private Action _confirmCallback;
        private Action _cancelCallback;

        private LocalizedString _defaultTitleString;
        private LocalizedString _defaultMessageString;
        private LocalizedString _defaultConfirmLabel;
        private LocalizedString _defaultCancelLabel;

        public bool IsVisible => _isVisible;

        private void Awake()
        {
            _defaultTitleString = _titleString;
            _defaultMessageString = _messageString;
            _defaultConfirmLabel = _confirmLabel;
            _defaultCancelLabel = _cancelLabel;

            EnsureCanvasReferences();
            AutoDiscoverTextTargets();
            WireButtonsIfNeeded();
            HideImmediate();
        }

        private void OnDisable()
        {
            TeardownLocalization();

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

            _isVisible = false;
            _confirmCallback = null;
            _cancelCallback = null;
        }

        private void Update()
        {
            if (!_isVisible)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CancelFromInput();
            }
        }

        /// <summary>
        /// Shows the confirmation box using the currently configured localized strings.
        /// </summary>
        public void Show(Action onConfirm, Action onCancel = null)
        {
            if (onConfirm == null)
            {
                throw new ArgumentNullException(nameof(onConfirm));
            }

            _confirmCallback = onConfirm;
            _cancelCallback = onCancel;

            SetupLocalization();
            RefreshAllLocalizedText();

            StartTransition(true);
        }

        /// <summary>
        /// Shows the confirmation box with dynamic localized strings for title, message and button labels.
        /// </summary>
        public void Show(LocalizedString title, LocalizedString message, LocalizedString confirmLabel, LocalizedString cancelLabel, Action onConfirm, Action onCancel = null)
        {
            _titleString = title;
            _messageString = message;
            _confirmLabel = confirmLabel;
            _cancelLabel = cancelLabel;

            Show(onConfirm, onCancel);
        }

        /// <summary>
        /// Shows the confirmation box using per-call overrides, while falling back to the component defaults for any null overrides.
        /// This avoids forcing callers to provide title/confirm/cancel when only the message needs to be dynamic.
        /// </summary>
        public void ShowWithOverrides(LocalizedString titleOverride, LocalizedString messageOverride, LocalizedString confirmLabelOverride, LocalizedString cancelLabelOverride, Action onConfirm, Action onCancel = null)
        {
            _titleString = titleOverride ?? _defaultTitleString;
            _messageString = messageOverride ?? _defaultMessageString;
            _confirmLabel = confirmLabelOverride ?? _defaultConfirmLabel;
            _cancelLabel = cancelLabelOverride ?? _defaultCancelLabel;

            Show(onConfirm, onCancel);
        }

        /// <summary>
        /// Cancels the dialog programmatically (invokes the cancel callback if any).
        /// </summary>
        public void Cancel()
        {
            if (!_isVisible)
            {
                return;
            }

            InvokeCancel();
        }

        private void EnsureCanvasReferences()
        {
            if (_rootCanvasGroup == null)
            {
                _rootCanvasGroup = GetComponent<CanvasGroup>();
                if (_rootCanvasGroup == null)
                {
                    _rootCanvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }

            if (_dialogRoot == null)
            {
                _dialogRoot = GetComponent<RectTransform>();
            }
        }

        private void AutoDiscoverTextTargets()
        {
            if (_titleTMP == null || _titleText == null || _messageTMP == null || _messageText == null ||
                _confirmTMP == null || _confirmText == null || _cancelTMP == null || _cancelText == null)
            {
                var texts = GetComponentsInChildren<Text>(true);
                var tmps = GetComponentsInChildren<TMP_Text>(true);

                if (_titleText == null && texts.Length > 0)
                {
                    _titleText = texts[0];
                }

                if (_messageText == null && texts.Length > 1)
                {
                    _messageText = texts[1];
                }

                if (_confirmText == null && texts.Length > 2)
                {
                    _confirmText = texts[2];
                }

                if (_cancelText == null && texts.Length > 3)
                {
                    _cancelText = texts[3];
                }

                if (_titleTMP == null && tmps.Length > 0)
                {
                    _titleTMP = tmps[0];
                }

                if (_messageTMP == null && tmps.Length > 1)
                {
                    _messageTMP = tmps[1];
                }

                if (_confirmTMP == null && tmps.Length > 2)
                {
                    _confirmTMP = tmps[2];
                }

                if (_cancelTMP == null && tmps.Length > 3)
                {
                    _cancelTMP = tmps[3];
                }
            }
        }

        private void WireButtonsIfNeeded()
        {
            if (_buttonsWired)
            {
                return;
            }

            if (_confirmButton != null)
            {
                _confirmButton.onClick.AddListener(OnConfirmClicked);
            }

            if (_cancelButton != null)
            {
                _cancelButton.onClick.AddListener(OnCancelClicked);
            }

            _buttonsWired = true;
        }

        private void SetupLocalization()
        {
            TeardownLocalization();

            if (_titleString != null)
            {
                _titleString.StringChanged += HandleTitleChanged;
            }

            if (_messageString != null)
            {
                _messageString.StringChanged += HandleMessageChanged;
            }

            if (_confirmLabel != null)
            {
                _confirmLabel.StringChanged += HandleConfirmLabelChanged;
            }

            if (_cancelLabel != null)
            {
                _cancelLabel.StringChanged += HandleCancelLabelChanged;
            }
        }

        private void TeardownLocalization()
        {
            if (_titleString != null)
            {
                _titleString.StringChanged -= HandleTitleChanged;
            }

            if (_messageString != null)
            {
                _messageString.StringChanged -= HandleMessageChanged;
            }

            if (_confirmLabel != null)
            {
                _confirmLabel.StringChanged -= HandleConfirmLabelChanged;
            }

            if (_cancelLabel != null)
            {
                _cancelLabel.StringChanged -= HandleCancelLabelChanged;
            }
        }

        private void RefreshAllLocalizedText()
        {
            _titleString?.RefreshString();
            _messageString?.RefreshString();
            _confirmLabel?.RefreshString();
            _cancelLabel?.RefreshString();
        }

        private void HandleTitleChanged(string value)
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

        private void HandleMessageChanged(string value)
        {
            if (_messageTMP != null)
            {
                _messageTMP.text = value;
            }
            else if (_messageText != null)
            {
                _messageText.text = value;
            }
        }

        private void HandleConfirmLabelChanged(string value)
        {
            if (_confirmTMP != null)
            {
                _confirmTMP.text = value;
            }
            else if (_confirmText != null)
            {
                _confirmText.text = value;
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

        private void StartTransition(bool visible)
        {
            EnsureCanvasReferences();

            if (_rootCanvasGroup == null)
            {
                return;
            }

            // Ensure the GameObject is active before starting a coroutine-driven
            // transition. This makes the dialog more robust when authors keep the
            // root object disabled in the scene and rely on code to show it.
            if (visible && !gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            if (_fadeDuration <= 0f)
            {
                ApplyInstantState(visible);
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

            _transitionRoutine = StartCoroutine(TransitionRoutine(visible));
        }

        private void ApplyInstantState(bool visible)
        {
            _isVisible = visible;

            _rootCanvasGroup.gameObject.SetActive(visible);
            _rootCanvasGroup.alpha = visible ? 1f : 0f;
            _rootCanvasGroup.interactable = visible;
            _rootCanvasGroup.blocksRaycasts = visible;

            if (_dialogRoot != null)
            {
                _dialogRoot.localScale = Vector3.one;
            }

            if (!visible)
            {
                _confirmCallback = null;
                _cancelCallback = null;
            }
        }

        private System.Collections.IEnumerator TransitionRoutine(bool visible)
        {
            float duration = Mathf.Max(0.01f, _fadeDuration);
            float t = 0f;

            if (visible)
            {
                _isVisible = true;
                _rootCanvasGroup.gameObject.SetActive(true);
                _rootCanvasGroup.alpha = 0f;
                _rootCanvasGroup.interactable = false;
                _rootCanvasGroup.blocksRaycasts = true;

                if (_dialogRoot != null)
                {
                    float startScale = Mathf.Max(0.01f, _scaleFrom);
                    _dialogRoot.localScale = new Vector3(startScale, startScale, 1f);
                }
            }
            else
            {
                _isVisible = false;
                _rootCanvasGroup.interactable = false;
                _rootCanvasGroup.blocksRaycasts = true;
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

                if (_dialogRoot != null)
                {
                    float from = Mathf.Max(0.01f, _scaleFrom);
                    float to = 1f;
                    float scale = visible
                        ? Mathf.LerpUnclamped(from, to, eased)
                        : Mathf.LerpUnclamped(to, from, eased);
                    _dialogRoot.localScale = new Vector3(scale, scale, 1f);
                }

                yield return null;
            }

            if (!visible)
            {
                _rootCanvasGroup.alpha = 0f;
                _rootCanvasGroup.blocksRaycasts = false;
                _rootCanvasGroup.gameObject.SetActive(false);

                _confirmCallback = null;
                _cancelCallback = null;
            }
            else
            {
                _rootCanvasGroup.alpha = 1f;
                _rootCanvasGroup.interactable = true;
                _rootCanvasGroup.blocksRaycasts = true;
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
                    // Ignore if coroutine is not running.
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

            if (_dialogRoot != null)
            {
                _dialogRoot.localScale = Vector3.one;
            }

            _confirmCallback = null;
            _cancelCallback = null;
        }

        private void OnConfirmClicked()
        {
            if (!_isVisible)
            {
                return;
            }

            var callback = _confirmCallback;
            _confirmCallback = null;
            _cancelCallback = null;

            callback?.Invoke();
            RestoreDefaults();
            StartTransition(false);
        }

        private void OnCancelClicked()
        {
            if (!_isVisible)
            {
                return;
            }

            InvokeCancel();
        }

        private void CancelFromInput()
        {
            if (!_isVisible)
            {
                return;
            }

            InvokeCancel();
        }

        private void InvokeCancel()
        {
            var callback = _cancelCallback;
            _confirmCallback = null;
            _cancelCallback = null;

            callback?.Invoke();
            RestoreDefaults();
            StartTransition(false);
        }

        private void RestoreDefaults()
        {
            _titleString = _defaultTitleString;
            _messageString = _defaultMessageString;
            _confirmLabel = _defaultConfirmLabel;
            _cancelLabel = _defaultCancelLabel;
        }
    }
}
