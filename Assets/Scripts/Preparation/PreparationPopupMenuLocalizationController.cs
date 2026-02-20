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

        [Header("Label Targets")]
        [SerializeField, Tooltip("TMP label shown in the Shop menu button.")]
        private TMP_Text _shopLabelTMP;
        [SerializeField, Tooltip("TMP label shown in the Squad menu button.")]
        private TMP_Text _squadLabelTMP;
        [SerializeField, Tooltip("Child object name used to auto-find the Shop button when _shopLabelTMP is not assigned.")]
        private string _shopButtonObjectName = "ShopButtonMenu";
        [SerializeField, Tooltip("Child object name used to auto-find the Squad button when _squadLabelTMP is not assigned.")]
        private string _squadButtonObjectName = "SquadButtonMenu";

        [Header("Button Targets")]
        [SerializeField, Tooltip("Optional explicit reference to the Shop menu button. Auto-found when null.")]
        private Button _shopButton;
        [SerializeField, Tooltip("Optional explicit reference to the Squad menu button. Auto-found when null.")]
        private Button _squadButton;

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

        [Header("Localization")]
        [SerializeField, Tooltip("Localized label for the Shop button.")]
        private LocalizedString _shopLabel;
        [SerializeField, Tooltip("Localized label for the Squad button.")]
        private LocalizedString _squadLabel;

        [Header("Hover Feedback")]
        [SerializeField, Tooltip("Cursor texture used while hovering menu buttons.")]
        private Texture2D _hoverCursorTexture;
        [SerializeField, Tooltip("Hotspot used with the hover cursor texture.")]
        private Vector2 _hoverCursorHotspot = new Vector2(16f, 16f);
        [SerializeField, Tooltip("Default cursor texture restored when no menu button is hovered.")]
        private Texture2D _defaultCursorTexture;
        [SerializeField, Tooltip("Hotspot used with the default cursor texture.")]
        private Vector2 _defaultCursorHotspot = new Vector2(4f, 4f);
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
        private readonly List<ButtonClickSubscription> _panelClickSubscriptions = new List<ButtonClickSubscription>(1);
        private int _hoveredButtonCount;
        private float _lastClickSfxTime = -999f;
        private Coroutine _squadPanelRoutine;
        private Vector3 _squadPanelBaseScale = Vector3.one;
        private bool _squadPanelScaleCaptured;
        private bool _squadPanelStartupHiddenApplied;
        private Transform _squadPanelAnimatedRoot;

        private void Awake()
        {
            SetupLocalizationDefaults();
            ResolveLabelTargets();
            ResolveButtonTargets();
            ResolveSquadPanel();
        }

        private void OnEnable()
        {
            SetupLocalizationDefaults();
            ResolveLabelTargets();
            ResolveButtonTargets();
            ResolveSquadPanel();
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
            RestoreDefaultCursor();
        }

        private void LateUpdate()
        {
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
        }

        private void ResolveSquadPanel()
        {
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

            EnsureSquadPanelStartupHidden();
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
        }

        private void RefreshLabels()
        {
            _shopLabel?.RefreshString();
            _squadLabel?.RefreshString();
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
            if (_squadButton == null)
            {
                return;
            }

            UnityAction clickAction = HandleSquadButtonClicked;
            _squadButton.onClick.AddListener(clickAction);
            _panelClickSubscriptions.Add(new ButtonClickSubscription(_squadButton, clickAction));
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
            if (_hoverCursorTexture == null)
            {
                return;
            }

            Cursor.SetCursor(_hoverCursorTexture, _hoverCursorHotspot, CursorMode.Auto);
        }

        private void RestoreDefaultCursor()
        {
            Cursor.SetCursor(_defaultCursorTexture, _defaultCursorHotspot, CursorMode.Auto);
        }

        private void HandleMenuButtonClicked()
        {
            PlayClickSfx();
        }

        private void HandleSquadButtonClicked()
        {
            ResolveSquadPanel();
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
                float eased = EvaluateRevealCurve(normalized);
                _squadPanelCanvasGroup.alpha = Mathf.LerpUnclamped(fromAlpha, 1f, eased);
                animatedRoot.localScale = Vector3.LerpUnclamped(fromScale, toScale, eased);
                yield return null;
            }

            SetSquadPanelVisibleImmediate();
            _squadPanelRoutine = null;
        }

        private float EvaluateRevealCurve(float value)
        {
            float clamped = Mathf.Clamp01(value);
            if (_squadPanelRevealCurve == null || _squadPanelRevealCurve.length == 0)
            {
                // SmoothStep fallback keeps the reveal polished even when no custom curve is assigned.
                return clamped * clamped * (3f - (2f * clamped));
            }

            return _squadPanelRevealCurve.Evaluate(clamped);
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
