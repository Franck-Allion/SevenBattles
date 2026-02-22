using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization;
using UnityEngine.EventSystems;
using TMPro;
using SevenBattles.Core;

using SevenBattles.Core.Diagnostics;
namespace SevenBattles.UI
{
    // Simple HUD wiring for portraits and Start Battle button.
    public class SquadPlacementHUD : MonoBehaviour
    {
        [SerializeField, Tooltip("Reference to a MonoBehaviour that implements ISquadPlacementController (e.g., WorldSquadPlacementController).")]
        private MonoBehaviour _controllerBehaviour;
        private ISquadPlacementController _controller;

        [Header("Selection FX")]
        [SerializeField, Tooltip("Base name of the selection frame to toggle per slot (e.g., 'Frame' → Frame0, Frame1...). Fallbacks to a child literally named 'Frame', then 'EdgeGlow'.")]
        private string _frameChildBaseName = "Frame";
        private int _selectedIndex = -1;

        [Header("Audio")]
        [SerializeField, Tooltip("AudioSource used to play UI selection sounds (optional). If not set, PlayClipAtPoint will be used.")]
        private AudioSource _audio;
        [SerializeField, Tooltip("Sound to play when a portrait is selected (e.g., Assets/Art/SFX/Menu_Percussive_Select_3.wav)")]
        private AudioClip _selectClip;
        [SerializeField, Tooltip("Minimum time in seconds between selection sounds.")]
        private float _selectCooldown = 0.12f;
        private float _lastSelectSfxTime = -999f;
        [SerializeField, Tooltip("Sound to play when Start is clicked and placement is confirmed (e.g., Assets/Art/SFX/Menu_Percussive_Confirm_1.wav)")]
        private AudioClip _startClip;

        [Header("HUD Root")]
        [SerializeField, Tooltip("Optional root to hide when placement is confirmed. Defaults to this GameObject if not set.")]
        private GameObject _hudRoot;
        [Header("Battle HUD")]
        [SerializeField, Tooltip("Battle HUD root (e.g., TurnOrderHUD canvas). Activated when placement is locked.")]
        private GameObject _battleHudRoot;

        [Header("Explicit Mapping (optional)")]
        [SerializeField, Tooltip("Optional: explicit portrait Image references per slot (overrides auto-find).")]
        private Image[] _portraitImages;
        [SerializeField, Tooltip("Optional: explicit entry roots per slot (e.g., Wizard1 objects). If set, these are toggled instead of the Button root.")]
        private Transform[] _entryRoots;
        [SerializeField, Tooltip("Optional: explicit TMP level labels per slot.")]
        private TMP_Text[] _levelTexts;
        [SerializeField, Tooltip("Up to 8 portrait buttons, mapped by index to wizard prefabs.")]
        private Button[] _portraitButtons = new Button[8];
        [SerializeField] private Button _startBattleButton;
        [Header("Localization")]
        [SerializeField, Tooltip("Localized label for the Start Battle button (e.g., Table: UI.Common, Entry: StartBattle)")]
        private LocalizedString _startButtonLabel;
        [SerializeField, Tooltip("Optional explicit reference to the Button label Text component. If not set, a child Text will be auto-found at runtime.")]
        private Text _startButtonText;
        [SerializeField, Tooltip("Optional explicit reference to the Button label TMP_Text component. If not set, a child TMP_Text will be auto-found at runtime.")]
        private TMP_Text _startButtonTMP;
        [SerializeField, Tooltip("Optional CanvasGroup driving Start button fade. If null, one will be auto-added on the Start button root.")]
        private CanvasGroup _startButtonCanvasGroup;
        [SerializeField, Tooltip("Fade-out duration (seconds) for Start button when battle begins.")]
        private float _startButtonFadeDuration = 0.75f;
        private bool _startButtonFadePlayed;
        [Space]
        [SerializeField, Tooltip("Localized instructional text describing how to place units (e.g., Table: UI.Common, Entry: Placement.Instructions)")]
        private LocalizedString _placementInstructions;
        [SerializeField, Tooltip("Optional explicit reference to the instructions Text component.")]
        private Text _instructionsText;
        [SerializeField, Tooltip("Optional explicit reference to the instructions TMP_Text component.")]
        private TMP_Text _instructionsTMP;

        [Header("Layout")]
        [SerializeField, Tooltip("Optional container that holds the portrait buttons. If it has a HorizontalLayoutGroup, centering is handled automatically.")]
        private RectTransform _container;
        [SerializeField, Tooltip("Manual center-to-center spacing used when no HorizontalLayoutGroup is present.")]
        private float _centerSpacing = 130f;
        [SerializeField, Tooltip("When enabled, logs portrait binding/visibility decisions for debugging.")]
        private bool _logBindings;
        [Header("Name Tooltip")]
        [SerializeField, Tooltip("When enabled, hovering a portrait during placement shows the unit display name near the cursor.")]
        private bool _enableNameTooltip = true;
        [SerializeField, Min(0f), Tooltip("Delay (seconds, unscaled time) before showing the tooltip on hover. Matches Squad menu behavior.")]
        private float _nameTooltipShowDelaySeconds = 1f;
        [SerializeField, Tooltip("Tooltip horizontal offset from cursor in canvas coordinates.")]
        private float _nameTooltipOffsetX = 16f;
        [SerializeField, Tooltip("Tooltip vertical offset from cursor in canvas coordinates.")]
        private float _nameTooltipOffsetY = -20f;
        [SerializeField, Tooltip("Minimum edge padding from the canvas bounds.")]
        private Vector2 _nameTooltipEdgePadding = new Vector2(8f, 8f);
        [SerializeField, Tooltip("Text padding inside the tooltip background.")]
        private Vector2 _nameTooltipTextPadding = new Vector2(18f, 10f);
        [SerializeField, Min(1f)] private float _nameTooltipMinWidth = 80f;
        [SerializeField, Min(1f)] private float _nameTooltipMinHeight = 36f;
        [SerializeField, Min(1f)] private float _nameTooltipMaxWidth = 420f;
        [SerializeField, Tooltip("When enabled, logs tooltip show/hide diagnostics.")]
        private bool _logTooltip;

        private readonly List<PortraitHoverForwarder> _portraitHoverForwarders = new List<PortraitHoverForwarder>(8);
        private RectTransform _tooltipCanvasRect;
        private RectTransform _nameTooltipRect;
        private CanvasGroup _nameTooltipCanvasGroup;
        private Image _nameTooltipBackground;
        private TMP_Text _nameTooltipLabel;
        private string _nameTooltipText = string.Empty;
        private int _hoveredPortraitIndex = -1;
        private bool _nameTooltipShowPending;
        private int _pendingTooltipIndex = -1;
        private float _pendingTooltipShowTime;
        private string _pendingTooltipText = string.Empty;
        private bool _createdRuntimeTooltip;
        private bool _loggedMissingTooltipCanvas;

        private void Awake()
        {
            // Require explicit assignment to avoid cross-domain lookup and deprecated APIs.
            if (_controllerBehaviour == null)
                SBLog.Warn("SquadPlacementHUD: Please assign a controller (MonoBehaviour implementing ISquadPlacementController).", this);
            _controller = _controllerBehaviour as ISquadPlacementController;

            WireButtons();
            SetupStartButtonLocalization();
            SetupInstructionLocalization();
        }

        private void OnEnable()
        {
            if (_controller == null) return;
            _controller.WizardSelected += HandleSelected;
            _controller.WizardPlaced += HandlePlaced;
            _controller.WizardRemoved += HandleRemoved;
            _controller.ReadyChanged += HandleReady;
            _controller.PlacementLocked += HandlePlacementLocked;
            HandleReady(_controller.IsReady);
            ClearAllHighlights();
            UpdatePortraitButtons(recenter: true);
            UpdateStartButtonVisibility();
            // Ensure localized label is applied when shown
            RefreshStartButtonLabel();
            RefreshInstructionLabel();
            SetInstructionsVisible(!_controller.IsLocked);
        }

        private void OnDisable()
        {
            HideNameTooltip();
            if (_controller == null) return;
            _controller.WizardSelected -= HandleSelected;
            _controller.WizardPlaced -= HandlePlaced;
            _controller.WizardRemoved -= HandleRemoved;
            _controller.ReadyChanged -= HandleReady;
            _controller.PlacementLocked -= HandlePlacementLocked;
            TeardownStartButtonLocalization();
            TeardownInstructionLocalization();
        }

        private void LateUpdate()
        {
            ProcessPendingTooltipShow();

            if (!IsNameTooltipVisible())
            {
                return;
            }

            if (_controller != null && _controller.IsLocked)
            {
                HideNameTooltip();
                return;
            }

            UpdateNameTooltipPosition();
        }

        private void OnDestroy()
        {
            if (!_createdRuntimeTooltip || _nameTooltipRect == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(_nameTooltipRect.gameObject);
                return;
            }

            DestroyImmediate(_nameTooltipRect.gameObject);
        }

        private void WireButtons()
        {
            if (_portraitButtons != null)
            {
                for (int i = 0; i < _portraitButtons.Length; i++)
                {
                    int idx = i;
                    if (_portraitButtons[i] != null)
                    {
                        _portraitButtons[i].onClick.RemoveAllListeners();
                        _portraitButtons[i].onClick.AddListener(() => {
                            // Defer selection highlight to controller event to ensure it was accepted
                            _controller?.SelectWizard(idx);
                        });
                        // Try bind portrait sprite on a child named "Portrait" if present, else on the Button's Image
                        var img = FindPortraitImage(_portraitButtons[i].transform);
                        var sprite = _controller != null ? _controller.GetPortrait(idx) : null;
                        if (img != null && sprite != null) img.sprite = sprite;
                        RegisterPortraitHover(_portraitButtons[i], idx);
                    }
                }
            }
            if (_startBattleButton != null)
            {
                _startBattleButton.onClick.RemoveAllListeners();
                _startBattleButton.onClick.AddListener(() => _controller?.ConfirmAndLock());
            }
        }

        private void SetupStartButtonLocalization()
        {
            // Ensure we have a Text component to write into (legacy uGUI Text).
            if (_startBattleButton != null)
            {
                if (_startButtonTMP == null)
                    _startButtonTMP = _startBattleButton.GetComponentInChildren<TMP_Text>(true);
                if (_startButtonText == null)
                    _startButtonText = _startBattleButton.GetComponentInChildren<Text>(true);
                // Ensure CanvasGroup exists for smooth alpha tween without touching individual graphics
                if (_startButtonCanvasGroup == null)
                {
                    _startButtonCanvasGroup = _startBattleButton.GetComponent<CanvasGroup>();
                    if (_startButtonCanvasGroup == null)
                        _startButtonCanvasGroup = _startBattleButton.gameObject.AddComponent<CanvasGroup>();
                    _startButtonCanvasGroup.alpha = 1f;
                    _startButtonCanvasGroup.interactable = _startBattleButton.interactable;
                    _startButtonCanvasGroup.blocksRaycasts = _startBattleButton.interactable;
                }
            }

            if (_startButtonLabel != null)
            {
                // Subscribe to label changes (locale switches, smart string updates)
                _startButtonLabel.StringChanged += HandleStartLabelChanged;
                _startButtonLabel.RefreshString();
            }
        }

        private void TeardownStartButtonLocalization()
        {
            if (_startButtonLabel != null)
            {
                _startButtonLabel.StringChanged -= HandleStartLabelChanged;
            }
        }

        private void HandleStartLabelChanged(string value)
        {
            LocalizationCacheDiagnostics.LogDisplay(_startButtonLabel, "SquadPlacementHUD.StartButton", this);
            if (_startButtonTMP != null) _startButtonTMP.text = value;
            else if (_startButtonText != null) _startButtonText.text = value;
        }

        private void RefreshStartButtonLabel()
        {
            if (_startButtonLabel != null)
            {
                _startButtonLabel.RefreshString();
            }
        }

        private void SetupInstructionLocalization()
        {
            // If explicit references are not set, do not auto-pick arbitrary children here to avoid grabbing unrelated labels.
            if (_placementInstructions != null)
            {
                _placementInstructions.StringChanged += HandleInstructionChanged;
                _placementInstructions.RefreshString();
            }
        }

        private void TeardownInstructionLocalization()
        {
            if (_placementInstructions != null)
            {
                _placementInstructions.StringChanged -= HandleInstructionChanged;
            }
        }

        private void HandleInstructionChanged(string value)
        {
            LocalizationCacheDiagnostics.LogDisplay(_placementInstructions, "SquadPlacementHUD.Instructions", this);
            if (_instructionsTMP != null) _instructionsTMP.text = value;
            else if (_instructionsText != null) _instructionsText.text = value;
        }

        private void RefreshInstructionLabel()
        {
            if (_placementInstructions != null)
            {
                _placementInstructions.RefreshString();
            }
        }

        private void HandlePlaced(int index)
        {
            if (index >= 0 && index < _portraitButtons.Length && _portraitButtons[index] != null)
            {
                _portraitButtons[index].gameObject.SetActive(false);
            }
            if (_hoveredPortraitIndex == index)
            {
                HideNameTooltip();
            }
            UpdateLevelLabel(index, false);
            CenterActivePortraits();
            // If the placed wizard was selected, clear selection glow
            if (_selectedIndex == index)
            {
                SetSelected(-1);
            }
            UpdateStartButtonVisibility();
        }

        private void HandleSelected(int index)
        {
            // Only react if selection actually changes
            if (index != _selectedIndex)
            {
                SetSelected(index);
                PlaySelectSound();
            }
        }

        private void HandleRemoved(int index)
        {
            if (index >= 0 && index < _portraitButtons.Length && _portraitButtons[index] != null)
            {
                _portraitButtons[index].gameObject.SetActive(true);
            }
            UpdateLevelLabel(index, true);
            CenterActivePortraits();
            UpdateStartButtonVisibility();
        }

        private void HandleReady(bool ready)
        {
            if (_startBattleButton != null)
            {
                _startBattleButton.interactable = ready;
                _startBattleButton.gameObject.SetActive(ready);
            }
        }

        private void UpdateStartButtonVisibility()
        {
            if (_startBattleButton == null) return;
            bool ready = _controller != null && _controller.IsReady;
            // Visible only when all wizards are placed; otherwise hidden
            _startBattleButton.gameObject.SetActive(ready);
            _startBattleButton.interactable = ready;
        }

        private void UpdatePortraitButtons(bool recenter)
        {
            int fallbackCount = 0;
            for (int i = 0; i < _portraitButtons.Length; i++) if (_portraitButtons[i] != null) fallbackCount++;
            int size = _controller != null ? Mathf.Clamp(_controller.SquadSize, 1, Mathf.Min(_portraitButtons.Length, 8)) : fallbackCount;
            for (int i = 0; i < _portraitButtons.Length; i++)
            {
                var btn = _portraitButtons[i];
                if (btn == null) continue;
                bool withinSquad = i < size;
                var img = GetPortraitImage(i, btn.transform);
                var spriteFromController = _controller != null ? _controller.GetPortrait(i) : null;
                if (img != null && spriteFromController != null) img.sprite = spriteFromController; // bind if provided
                bool hasPortrait = img != null && img.sprite != null; // allow pre-assigned scene sprites
                bool placed = _controller != null && _controller.IsPlaced(i);
                bool visible = withinSquad && hasPortrait && !placed;

                // Activate/deactivate the entire entry based on visibility
                var root = GetEntryRoot(i, btn.transform);
                if (root.gameObject.activeSelf != visible)
                {
                    if (visible) EnsureAncestorsActive(root, _container != null ? _container.transform : null);
                    root.gameObject.SetActive(visible);
                }

                // Ensure the portrait Image GameObject is active when visible (even if it was authored inactive)
                if (img != null && img.gameObject.activeSelf != visible)
                {
                    img.gameObject.SetActive(visible);
                }

                UpdateLevelLabel(i, visible);

                if (_logBindings && withinSquad && !visible)
                {
                    var reason = placed ? "already placed" : (!hasPortrait ? "no portrait sprite" : "out of squad range");
                    SBLog.Warn($"HUD Portrait slot {i} hidden ({reason}). ImageFound={(img!=null)} SpriteFromController={(spriteFromController!=null)}", this);
                }

                // Ensure highlight is off when this entry isn't currently usable
                if (!visible)
                {
                    SetHighlightActive(i, false);
                    if (_hoveredPortraitIndex == i)
                    {
                        HideNameTooltip();
                    }
                }
            }
            if (recenter) CenterActivePortraits();
        }

        private Transform GetEntryRoot(int index, Transform fallback)
        {
            if (_entryRoots != null && index >= 0 && index < _entryRoots.Length)
            {
                var r = _entryRoots[index];
                if (r != null) return r;
            }
            return fallback;
        }

        private void CenterActivePortraits()
        {
            if (_container == null) return;
            // If a HorizontalLayoutGroup is present, assume it handles centering.
            var hasHlg = _container.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>() != null;
            if (hasHlg) return;

            // Manually position buttons by counting those with an active Portrait child.
            var actives = 0;
            for (int i = 0; i < _portraitButtons.Length; i++)
            {
                var btn = _portraitButtons[i];
                if (btn == null) continue;
                var img = GetPortraitImage(i, btn.transform);
                if (img != null && img.gameObject.activeInHierarchy) actives++;
            }
            if (actives == 0) return;

            float total = (actives - 1) * _centerSpacing;
            float startX = -total * 0.5f;
            int k = 0;
            for (int i = 0; i < _portraitButtons.Length; i++)
            {
                var btn = _portraitButtons[i];
                if (btn == null) continue;
                var img = GetPortraitImage(i, btn.transform);
                if (img == null || !img.gameObject.activeInHierarchy) continue;
                var rt = btn.transform as RectTransform;
                if (rt != null)
                {
                    var pos = rt.anchoredPosition;
                    pos.x = startX + k * _centerSpacing;
                    pos.y = 0f;
                    rt.anchoredPosition = pos;
                }
                k++;
            }
        }

        private Image GetPortraitImage(int index, Transform fallbackRoot)
        {
            if (_portraitImages != null && index >= 0 && index < _portraitImages.Length)
            {
                var explicitImg = _portraitImages[index];
                if (explicitImg != null) return explicitImg;
            }
            // Try exact numbered child first: Portrait{index}
            if (fallbackRoot != null)
            {
                var exact = fallbackRoot.Find($"Portrait{index}");
                if (exact != null)
                {
                    var exactImg = exact.GetComponentInChildren<Image>(true);
                    if (exactImg != null) return exactImg;
                }
            }
            return FindPortraitImage(fallbackRoot);
        }

        private TMP_Text GetLevelText(int index)
        {
            if (_levelTexts != null && index >= 0 && index < _levelTexts.Length)
            {
                return _levelTexts[index];
            }

            return null;
        }

        private void UpdateLevelLabel(int index, bool visible)
        {
            var levelText = GetLevelText(index);
            if (levelText == null)
            {
                return;
            }

            int level = _controller != null ? _controller.GetLevel(index) : 0;
            levelText.text = level > 0 ? level.ToString() : string.Empty;
            if (levelText.gameObject.activeSelf != visible)
            {
                levelText.gameObject.SetActive(visible);
            }
        }

        private static Image FindPortraitImage(Transform root)
        {
            if (root == null) return null;
            // Prefer a child explicitly named "Portrait"
            var portraitTf = root.Find("Portrait");
            if (portraitTf != null)
            {
                var img = portraitTf.GetComponent<Image>();
                if (img != null) return img;
            }
            // Try case-insensitive prefix match for names like Portrait1, Portrait2
            var imgs = root.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < imgs.Length; i++)
            {
                var n = imgs[i].transform.name;
                if (!string.IsNullOrEmpty(n) && n.StartsWith("Portrait", System.StringComparison.OrdinalIgnoreCase))
                    return imgs[i];
            }
            // Otherwise prefer the first Image under the button
            var any = root.GetComponentInChildren<Image>();
            if (any != null) return any;
            // Fallback to the button's own Image, if any
            return root.GetComponent<Image>();
        }

        private void RegisterPortraitHover(Button button, int index)
        {
            if (button == null)
            {
                return;
            }

            var forwarder = button.GetComponent<PortraitHoverForwarder>();
            if (forwarder == null)
            {
                forwarder = button.gameObject.AddComponent<PortraitHoverForwarder>();
            }

            forwarder.Configure(this, index);
            if (!_portraitHoverForwarders.Contains(forwarder))
            {
                _portraitHoverForwarders.Add(forwarder);
            }
        }

        private void HandlePortraitPointerEnter(int index)
        {
            if (!_enableNameTooltip || _controller == null || _controller.IsLocked)
            {
                return;
            }

            string displayName = _controller.GetDisplayName(index);
            if (string.IsNullOrWhiteSpace(displayName))
            {
                HideNameTooltip();
                return;
            }

            if (!EnsureNameTooltipReady())
            {
                return;
            }

            _hoveredPortraitIndex = index;
            if (_nameTooltipShowDelaySeconds <= 0f)
            {
                CancelPendingTooltipShow();
                ShowNameTooltipNow(index, displayName);
                return;
            }

            HideVisibleNameTooltip();
            _pendingTooltipIndex = index;
            _pendingTooltipText = displayName;
            _pendingTooltipShowTime = Time.unscaledTime + _nameTooltipShowDelaySeconds;
            _nameTooltipShowPending = true;

            if (_logTooltip)
            {
                SBLog.Info(
                    $"SquadPlacementHUD: Scheduled name tooltip '{displayName}' for slot {index} in {_nameTooltipShowDelaySeconds:0.###}s.",
                    this);
            }
        }

        private void HandlePortraitPointerExit(int index)
        {
            if (index != _hoveredPortraitIndex)
            {
                return;
            }

            HideNameTooltip();
        }

        private bool EnsureNameTooltipReady()
        {
            if (_nameTooltipRect != null && _nameTooltipCanvasGroup != null && _nameTooltipLabel != null && _tooltipCanvasRect != null)
            {
                return true;
            }

            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
            }

            Canvas rootCanvas = canvas != null ? (canvas.isRootCanvas ? canvas : canvas.rootCanvas) : null;
            if (rootCanvas == null)
            {
                if (!_loggedMissingTooltipCanvas)
                {
                    _loggedMissingTooltipCanvas = true;
                    SBLog.Warn("SquadPlacementHUD: Unable to resolve a canvas for portrait name tooltip.", this);
                }

                return false;
            }

            _loggedMissingTooltipCanvas = false;
            _tooltipCanvasRect = rootCanvas.transform as RectTransform;

            if (_nameTooltipRect == null)
            {
                var tooltipObject = new GameObject(
                    "SquadPlacementNameTooltip",
                    typeof(RectTransform),
                    typeof(CanvasGroup),
                    typeof(Image));

                tooltipObject.layer = rootCanvas.gameObject.layer;
                _nameTooltipRect = tooltipObject.GetComponent<RectTransform>();
                _nameTooltipCanvasGroup = tooltipObject.GetComponent<CanvasGroup>();
                _nameTooltipBackground = tooltipObject.GetComponent<Image>();
                _nameTooltipRect.SetParent(rootCanvas.transform, false);
                _nameTooltipRect.anchorMin = new Vector2(0.5f, 0.5f);
                _nameTooltipRect.anchorMax = new Vector2(0.5f, 0.5f);
                _nameTooltipRect.pivot = new Vector2(0.5f, 0.5f);
                _nameTooltipRect.anchoredPosition = Vector2.zero;
                _nameTooltipRect.sizeDelta = new Vector2(1f, 1f);

                var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                labelObject.layer = tooltipObject.layer;
                RectTransform labelRect = labelObject.GetComponent<RectTransform>();
                labelRect.SetParent(_nameTooltipRect, false);
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = Vector2.zero;
                labelRect.offsetMax = Vector2.zero;
                _nameTooltipLabel = labelObject.GetComponent<TextMeshProUGUI>();
                _createdRuntimeTooltip = true;
            }

            if (_nameTooltipBackground != null)
            {
                _nameTooltipBackground.raycastTarget = false;
                _nameTooltipBackground.color = new Color32(24, 28, 35, 235);
            }

            if (_nameTooltipLabel != null)
            {
                _nameTooltipLabel.raycastTarget = false;
                _nameTooltipLabel.textWrappingMode = TextWrappingModes.NoWrap;
                _nameTooltipLabel.overflowMode = TextOverflowModes.Overflow;
                _nameTooltipLabel.alignment = TextAlignmentOptions.Center;
                _nameTooltipLabel.color = Color.white;
            }

            HideVisibleNameTooltip();
            return _nameTooltipRect != null && _nameTooltipCanvasGroup != null && _nameTooltipLabel != null;
        }

        private void ResizeNameTooltipToContent()
        {
            if (_nameTooltipRect == null || _nameTooltipLabel == null)
            {
                return;
            }

            RectTransform labelRect = _nameTooltipLabel.rectTransform;
            float halfPadX = _nameTooltipTextPadding.x * 0.5f;
            float halfPadY = _nameTooltipTextPadding.y * 0.5f;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(halfPadX, halfPadY);
            labelRect.offsetMax = new Vector2(-halfPadX, -halfPadY);

            Vector2 preferred = _nameTooltipLabel.GetPreferredValues(_nameTooltipText, _nameTooltipMaxWidth, 0f);
            float width = Mathf.Clamp(preferred.x + _nameTooltipTextPadding.x, _nameTooltipMinWidth, _nameTooltipMaxWidth);
            float height = Mathf.Max(_nameTooltipMinHeight, preferred.y + _nameTooltipTextPadding.y);

            _nameTooltipRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            _nameTooltipRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        }

        private void UpdateNameTooltipPosition()
        {
            if (_nameTooltipRect == null || _tooltipCanvasRect == null)
            {
                return;
            }

            Camera eventCamera = ResolveTooltipEventCamera();
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _tooltipCanvasRect,
                    Input.mousePosition,
                    eventCamera,
                    out Vector2 localPoint))
            {
                return;
            }

            Vector2 tooltipSize = _nameTooltipRect.rect.size;
            Vector2 tooltipPivot = _nameTooltipRect.pivot;
            Rect canvasBounds = _tooltipCanvasRect.rect;
            Vector2 desired = localPoint + new Vector2(_nameTooltipOffsetX, _nameTooltipOffsetY);

            float minX = canvasBounds.xMin + (tooltipSize.x * tooltipPivot.x) + _nameTooltipEdgePadding.x;
            float maxX = canvasBounds.xMax - (tooltipSize.x * (1f - tooltipPivot.x)) - _nameTooltipEdgePadding.x;
            float minY = canvasBounds.yMin + (tooltipSize.y * tooltipPivot.y) + _nameTooltipEdgePadding.y;
            float maxY = canvasBounds.yMax - (tooltipSize.y * (1f - tooltipPivot.y)) - _nameTooltipEdgePadding.y;

            if (minX > maxX)
            {
                float midX = (canvasBounds.xMin + canvasBounds.xMax) * 0.5f;
                minX = midX;
                maxX = midX;
            }

            if (minY > maxY)
            {
                float midY = (canvasBounds.yMin + canvasBounds.yMax) * 0.5f;
                minY = midY;
                maxY = midY;
            }

            desired.x = Mathf.Clamp(desired.x, minX, maxX);
            desired.y = Mathf.Clamp(desired.y, minY, maxY);

            Vector3 worldPoint = _tooltipCanvasRect.TransformPoint(new Vector3(desired.x, desired.y, 0f));
            _nameTooltipRect.position = worldPoint;
        }

        private Camera ResolveTooltipEventCamera()
        {
            if (_tooltipCanvasRect == null)
            {
                return null;
            }

            var canvas = _tooltipCanvasRect.GetComponent<Canvas>();
            if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return null;
            }

            if (canvas.worldCamera != null)
            {
                return canvas.worldCamera;
            }

            var raycaster = canvas.GetComponent<GraphicRaycaster>();
            if (raycaster != null && raycaster.eventCamera != null)
            {
                return raycaster.eventCamera;
            }

            return Camera.main;
        }

        private bool IsNameTooltipVisible()
        {
            return _nameTooltipCanvasGroup != null && _nameTooltipCanvasGroup.alpha > 0.001f;
        }

        private void ProcessPendingTooltipShow()
        {
            if (!_nameTooltipShowPending)
            {
                return;
            }

            if (!_enableNameTooltip || _controller == null || _controller.IsLocked)
            {
                HideNameTooltip();
                return;
            }

            if (_pendingTooltipIndex < 0 || _pendingTooltipIndex != _hoveredPortraitIndex)
            {
                CancelPendingTooltipShow();
                return;
            }

            if (Time.unscaledTime < _pendingTooltipShowTime)
            {
                return;
            }

            string text = _pendingTooltipText;
            int index = _pendingTooltipIndex;
            CancelPendingTooltipShow();

            if (string.IsNullOrWhiteSpace(text))
            {
                HideNameTooltip();
                return;
            }

            ShowNameTooltipNow(index, text);
        }

        private void ShowNameTooltipNow(int index, string text)
        {
            if (!EnsureNameTooltipReady())
            {
                return;
            }

            _hoveredPortraitIndex = index;
            if (!string.Equals(_nameTooltipText, text, StringComparison.Ordinal))
            {
                _nameTooltipText = text;
                _nameTooltipLabel.SetText(_nameTooltipText);
                ResizeNameTooltipToContent();
            }

            _nameTooltipRect.SetAsLastSibling();
            _nameTooltipCanvasGroup.alpha = 1f;
            _nameTooltipCanvasGroup.interactable = false;
            _nameTooltipCanvasGroup.blocksRaycasts = false;
            UpdateNameTooltipPosition();

            if (_logTooltip)
            {
                SBLog.Info($"SquadPlacementHUD: Show name tooltip '{text}' for slot {index}.", this);
            }
        }

        private void CancelPendingTooltipShow()
        {
            _nameTooltipShowPending = false;
            _pendingTooltipIndex = -1;
            _pendingTooltipShowTime = 0f;
            _pendingTooltipText = string.Empty;
        }

        private void HideVisibleNameTooltip()
        {
            _nameTooltipText = string.Empty;
            if (_nameTooltipCanvasGroup != null)
            {
                _nameTooltipCanvasGroup.alpha = 0f;
                _nameTooltipCanvasGroup.interactable = false;
                _nameTooltipCanvasGroup.blocksRaycasts = false;
            }
        }

        private void HideNameTooltip()
        {
            CancelPendingTooltipShow();
            _hoveredPortraitIndex = -1;
            HideVisibleNameTooltip();

            if (_logTooltip)
            {
                SBLog.Info("SquadPlacementHUD: Hide name tooltip.", this);
            }
        }

        [SerializeField, Tooltip("When enabled, logs selection/highlight operations.")]
        private bool _logSelection;

        private void SetSelected(int index)
        {
            if (_logSelection) SBLog.Info($"HUD selection -> {index} (was {_selectedIndex})", this);
            if (index == _selectedIndex) return;
            // Turn off previous
            if (_selectedIndex >= 0 && _selectedIndex < _portraitButtons.Length)
            {
                SetHighlightActive(_selectedIndex, false);
            }
            _selectedIndex = index;
            // Turn on new
            if (_selectedIndex >= 0 && _selectedIndex < _portraitButtons.Length)
            {
                SetHighlightActive(_selectedIndex, true);
            }
        }

        private void SetHighlightActive(int idx, bool active)
        {
            if (idx < 0 || idx >= _portraitButtons.Length) return;
            var btn = _portraitButtons[idx];
            if (btn == null) return;
            var root = btn.transform;
            Transform tf = FindHighlightTransform(root, idx);
            if (tf == null && root.parent != null)
            {
                // Fallback: if the button is the PortraitX child, look under its parent (WizardX)
                tf = FindHighlightTransform(root.parent, idx);
            }
            if (tf != null)
            {
                tf.gameObject.SetActive(active);
                if (_logSelection) SBLog.Info($"HUD highlight {(active ? "ON" : "OFF")} for slot {idx} -> '{tf.name}'", this);
            }
            else if (_logSelection && active)
            {
                SBLog.Warn($"HUD highlight target not found for slot {idx}. Looked for '{_frameChildBaseName}{idx}', '{_frameChildBaseName}', 'EdgeGlow' under '{root.name}'.", this);
            }
        }

        private Transform FindHighlightTransform(Transform searchRoot, int idx)
        {
            if (searchRoot == null) return null;
            Transform tf = searchRoot.Find($"{_frameChildBaseName}{idx}");
            if (tf == null) tf = searchRoot.Find(_frameChildBaseName);
            if (tf == null) tf = searchRoot.Find("EdgeGlow");
            return tf;
        }

        private static void EnsureAncestorsActive(Transform t, Transform stopAt)
        {
            if (t == null) return;
            var cur = t.parent;
            while (cur != null && cur != stopAt)
            {
                if (!cur.gameObject.activeSelf) cur.gameObject.SetActive(true);
                cur = cur.parent;
            }
        }

        private void ClearAllHighlights()
        {
            for (int i = 0; i < _portraitButtons.Length; i++)
            {
                SetHighlightActive(i, false);
            }
            _selectedIndex = -1;
            if (_logSelection) SBLog.Info("HUD highlights cleared", this);
        }

        private void PlaySelectSound()
        {
            if (_selectClip == null) return;
            if (Time.unscaledTime - _lastSelectSfxTime < _selectCooldown) return;
            if (_audio != null)
            {
                _audio.PlayOneShot(_selectClip);
            }
            else
            {
                AudioSource.PlayClipAtPoint(_selectClip, Vector3.zero, 1f);
            }
            _lastSelectSfxTime = Time.unscaledTime;
        }

        private void HandlePlacementLocked()
        {
            HideNameTooltip();

            // Play confirm SFX
            if (_startClip != null)
            {
                if (_audio != null) _audio.PlayOneShot(_startClip);
                else AudioSource.PlayClipAtPoint(_startClip, Vector3.zero, 1f);
            }

            // Begin Start button fade (one-shot), disable interaction immediately for UX safety
            if (_startBattleButton != null && !_startButtonFadePlayed)
            {
                _startButtonFadePlayed = true;
                _startBattleButton.interactable = false;
                if (_startButtonCanvasGroup == null)
                {
                    _startButtonCanvasGroup = _startBattleButton.GetComponent<CanvasGroup>();
                    if (_startButtonCanvasGroup == null)
                        _startButtonCanvasGroup = _startBattleButton.gameObject.AddComponent<CanvasGroup>();
                }
                _startButtonCanvasGroup.interactable = false;
                _startButtonCanvasGroup.blocksRaycasts = false;
                StopCoroutineSafe(nameof(FadeOutStartButtonRoutine));
                if (Application.isPlaying)
                {
                    StartCoroutine(FadeOutStartButtonRoutine());
                }
                else
                {
                    // In edit mode (tests), run the fade outcome synchronously so that coroutines are not required.
                    _startButtonCanvasGroup.alpha = 0f;
                    _startBattleButton.gameObject.SetActive(false);
                }
            }

            // Hide instructional text immediately to reduce UI clutter during transition
            SetInstructionsVisible(false);
        }

        public void EnterBattleModeFromLoad()
        {
            HideNameTooltip();

            // Hide Start button immediately
            if (_startBattleButton != null)
            {
                if (_startButtonCanvasGroup == null)
                {
                    _startButtonCanvasGroup = _startBattleButton.GetComponent<CanvasGroup>();
                    if (_startButtonCanvasGroup == null)
                    {
                        _startButtonCanvasGroup = _startBattleButton.gameObject.AddComponent<CanvasGroup>();
                    }
                }

                _startButtonCanvasGroup.alpha = 0f;
                _startButtonCanvasGroup.interactable = false;
                _startButtonCanvasGroup.blocksRaycasts = false;
                _startBattleButton.interactable = false;
                _startBattleButton.gameObject.SetActive(false);
            }

            // Hide instructional text
            SetInstructionsVisible(false);

            // Hide placement HUD root
            var root = _hudRoot != null ? _hudRoot : this.gameObject;
            if (root != null)
            {
                root.SetActive(false);
            }

            // Show battle HUD root if assigned
            if (_battleHudRoot != null)
            {
                _battleHudRoot.SetActive(true);
            }
        }

        private System.Collections.IEnumerator FadeOutStartButtonRoutine()
        {
            if (_startBattleButton == null) yield break;
            if (_startButtonCanvasGroup == null)
            {
                _startButtonCanvasGroup = _startBattleButton.GetComponent<CanvasGroup>();
                if (_startButtonCanvasGroup == null)
                    _startButtonCanvasGroup = _startBattleButton.gameObject.AddComponent<CanvasGroup>();
            }

            float duration = Mathf.Max(0.01f, _startButtonFadeDuration);
            float t = 0f;
            // Make sure we start fully visible (alpha=1)
            _startButtonCanvasGroup.alpha = 1f;
            // Unscaled time to avoid timescale effects on transition
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / duration);
                // Cubic ease-out for a natural feel, alpha 1 -> 0
                float alpha = Mathf.Pow(1f - p, 3f);
                _startButtonCanvasGroup.alpha = alpha;
                yield return null;
            }
            _startButtonCanvasGroup.alpha = 0f;
            // Finally hide the button in hierarchy for clean state
            _startBattleButton.gameObject.SetActive(false);
            // Optionally hide the HUD root once the button is gone to finish the transition
            var root = _hudRoot != null ? _hudRoot : this.gameObject;
            if (root != null) root.SetActive(false);
        }

        private void StopCoroutineSafe(string routineName)
        {
            try { StopCoroutine(routineName); } catch { /* ignore if not running */ }
        }

        private void SetInstructionsVisible(bool visible)
        {
            var target = _instructionsTMP != null ? _instructionsTMP.gameObject : (_instructionsText != null ? _instructionsText.gameObject : null);
            if (target != null && target.activeSelf != visible)
            {
                target.SetActive(visible);
            }
        }

        private sealed class PortraitHoverForwarder : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            private SquadPlacementHUD _owner;
            private int _slotIndex;

            public void Configure(SquadPlacementHUD owner, int slotIndex)
            {
                _owner = owner;
                _slotIndex = slotIndex;
            }

            public void OnPointerEnter(PointerEventData eventData)
            {
                _owner?.HandlePortraitPointerEnter(_slotIndex);
            }

            public void OnPointerExit(PointerEventData eventData)
            {
                _owner?.HandlePortraitPointerExit(_slotIndex);
            }

            private void OnDisable()
            {
                _owner?.HandlePortraitPointerExit(_slotIndex);
            }
        }
    }
}
