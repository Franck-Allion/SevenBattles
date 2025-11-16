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

        [Header("End Turn Button")]
        [SerializeField] private Button _endTurnButton;
        [SerializeField, Tooltip("Optional CanvasGroup used to fade the End Turn button when disabled. If null, one will be auto-added on the button root.")]
        private CanvasGroup _endTurnCanvasGroup;
        [SerializeField, Tooltip("Alpha applied to the End Turn button when it is disabled.")]
        private float _disabledAlpha = 0.4f;

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

        private void Awake()
        {
            EnsureController();
            WireEndTurnButton();
            EnsureEndTurnCanvasGroup();
            SetupEndTurnLocalization();
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
            if (_controller == null) return;
            _controller.ActiveUnitChanged -= HandleActiveUnitChanged;
            TeardownEndTurnLocalization();
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

        private void HandleActiveUnitChanged()
        {
            if (_controller == null) return;

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
