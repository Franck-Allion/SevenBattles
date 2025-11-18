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
            WirePortraitClick();
        }

        private void OnEnable()
        {
            EnsureController();
            if (_controller == null) return;
            _controller.ActiveUnitChanged += HandleActiveUnitChanged;
            HandleActiveUnitChanged();
            RefreshEndTurnLabel();
        }

        private void OnDisable()
        {
            if (_controller != null)
            {
                _controller.ActiveUnitChanged -= HandleActiveUnitChanged;
            }

            TeardownEndTurnLocalization();
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
                var behaviours = FindObjectsOfType<MonoBehaviour>();
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

            // Always close any open stats panel when the active unit changes to avoid stale data.
            CloseStatsPanelImmediate();

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
            if (_lifeText != null) _lifeText.text = stats.Life.ToString();
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
