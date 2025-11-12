using UnityEngine;
using UnityEngine.UI;
using SevenBattles.Core;

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

        [Header("Explicit Mapping (optional)")]
        [SerializeField, Tooltip("Optional: explicit portrait Image references per slot (overrides auto-find).")]
        private Image[] _portraitImages;
        [SerializeField, Tooltip("Optional: explicit entry roots per slot (e.g., Wizard1 objects). If set, these are toggled instead of the Button root.")]
        private Transform[] _entryRoots;
        [SerializeField, Tooltip("Up to 8 portrait buttons, mapped by index to wizard prefabs.")]
        private Button[] _portraitButtons = new Button[8];
        [SerializeField] private Button _startBattleButton;

        [Header("Layout")]
        [SerializeField, Tooltip("Optional container that holds the portrait buttons. If it has a HorizontalLayoutGroup, centering is handled automatically.")]
        private RectTransform _container;
        [SerializeField, Tooltip("Manual center-to-center spacing used when no HorizontalLayoutGroup is present.")]
        private float _centerSpacing = 130f;
        [SerializeField, Tooltip("When enabled, logs portrait binding/visibility decisions for debugging.")]
        private bool _logBindings;

        private void Awake()
        {
            // Require explicit assignment to avoid cross-domain lookup and deprecated APIs.
            if (_controllerBehaviour == null)
                Debug.LogWarning("SquadPlacementHUD: Please assign a controller (MonoBehaviour implementing ISquadPlacementController).", this);
            _controller = _controllerBehaviour as ISquadPlacementController;
            WireButtons();
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
        }

        private void OnDisable()
        {
            if (_controller == null) return;
            _controller.WizardSelected -= HandleSelected;
            _controller.WizardPlaced -= HandlePlaced;
            _controller.WizardRemoved -= HandleRemoved;
            _controller.ReadyChanged -= HandleReady;
            _controller.PlacementLocked -= HandlePlacementLocked;
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
                    }
                }
            }
            if (_startBattleButton != null)
            {
                _startBattleButton.onClick.RemoveAllListeners();
                _startBattleButton.onClick.AddListener(() => _controller?.ConfirmAndLock());
            }
        }

        private void HandlePlaced(int index)
        {
            if (index >= 0 && index < _portraitButtons.Length && _portraitButtons[index] != null)
            {
                _portraitButtons[index].gameObject.SetActive(false);
            }
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

                if (_logBindings && withinSquad && !visible)
                {
                    var reason = placed ? "already placed" : (!hasPortrait ? "no portrait sprite" : "out of squad range");
                    Debug.LogWarning($"HUD Portrait slot {i} hidden ({reason}). ImageFound={(img!=null)} SpriteFromController={(spriteFromController!=null)}", this);
                }

                // Ensure highlight is off when this entry isn't currently usable
                if (!visible) SetHighlightActive(i, false);
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

        [SerializeField, Tooltip("When enabled, logs selection/highlight operations.")]
        private bool _logSelection;

        private void SetSelected(int index)
        {
            if (_logSelection) Debug.Log($"HUD selection -> {index} (was {_selectedIndex})", this);
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
                if (_logSelection) Debug.Log($"HUD highlight {(active ? "ON" : "OFF")} for slot {idx} -> '{tf.name}'", this);
            }
            else if (_logSelection && active)
            {
                Debug.LogWarning($"HUD highlight target not found for slot {idx}. Looked for '{_frameChildBaseName}{idx}', '{_frameChildBaseName}', 'EdgeGlow' under '{root.name}'.", this);
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
            if (_logSelection) Debug.Log("HUD highlights cleared", this);
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
            // Play confirm SFX
            if (_startClip != null)
            {
                if (_audio != null) _audio.PlayOneShot(_startClip);
                else AudioSource.PlayClipAtPoint(_startClip, Vector3.zero, 1f);
            }

            // Hide Start button and HUD
            if (_startBattleButton != null)
            {
                _startBattleButton.gameObject.SetActive(false);
            }
            var root = _hudRoot != null ? _hudRoot : this.gameObject;
            if (root != null)
            {
                root.SetActive(false);
            }
        }
    }
}
