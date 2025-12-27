using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using SevenBattles.Core;
using SevenBattles.Core.Battle;

namespace SevenBattles.UI
{
    // Displays the active unit's spells inside a HorizontalLayoutGroup container.
    public sealed class BattleSpellsHUD : MonoBehaviour
    {
        [Header("Controller")]
        [SerializeField, Tooltip("Reference to a MonoBehaviour that implements ITurnOrderController (e.g., SimpleTurnOrderController).")]
        private MonoBehaviour _controllerBehaviour;
        private ITurnOrderController _controller;
        private ISpellSelectionController _spellSelectionController;

        [Header("Audio (optional)")]
        [SerializeField, Tooltip("AudioSource used to play spell click SFX (optional). If not set, PlayClipAtPoint will be used unless a custom player is assigned.")]
        private AudioSource _audio;
        [SerializeField, Tooltip("Optional: custom UI SFX player (MonoBehaviour implementing IUiSfxPlayer). Takes precedence over _audio.")]
        private MonoBehaviour _sfxPlayerBehaviour;
        private IUiSfxPlayer _sfxPlayer;
        [SerializeField, Tooltip("Sound to play when a spell slot is clicked.")]
        private AudioClip _spellClickClip;
        [SerializeField, Range(0f, 1.5f), Tooltip("Volume multiplier for the spell click SFX.")]
        private float _spellClickVolume = 1f;
        [SerializeField, Tooltip("Minimum time in seconds between spell click sounds.")]
        private float _spellClickCooldown = 0.05f;
        private float _lastSpellClickSfxTime = -999f;

        [Header("Layout")]
        [SerializeField, Tooltip("Container under BattleHUD that has a HorizontalLayoutGroup (e.g., 'SpellsContainer').")]
        private RectTransform _spellsContainer;
        [SerializeField, Tooltip("When enabled, binds to pre-authored child slots (e.g., Spell0..Spell6) under the spells container and never instantiates new UI at runtime.")]
        private bool _useFixedSlots = true;
        [SerializeField, Tooltip("Optional explicit slot roots (order matters). If empty, slots are auto-found by name (Spell0..Spell6).")]
        private RectTransform[] _fixedSlotRoots = Array.Empty<RectTransform>();
        [SerializeField, Tooltip("Optional template used to create spell slots when fixed slots are not available.")]
        private RectTransform _slotTemplate;
        [SerializeField, Tooltip("If true, hides spells while the active unit is not player-controlled.")]
        private bool _hideWhenNotPlayerControlled = true;

        [Header("Slot Auto-Wiring")]
        [SerializeField, Tooltip("Child name to find an icon Image under a slot (optional). If not found, the first Image in children is used.")]
        private string _iconChildName = "Icon";
        [SerializeField, Tooltip("Child name to find an AP cost TMP label under a slot (optional). If not found, one is auto-created.")]
        private string _apCostChildName = "APCost";
        [SerializeField, Tooltip("Child name of a selection frame GameObject toggled when a slot is selected (e.g., 'Frame0').")]
        private string _selectionFrameChildName = "Frame0";
        [SerializeField, Tooltip("Font size used when auto-creating the AP cost overlay label (TMP).")]
        private float _apCostFontSize = 24f;

        [Header("Selected Spell Description")]
        [SerializeField, Tooltip("Optional TMP label that displays the selected spell description (localized).")]
        private TMP_Text _selectedSpellDescriptionText;
        [SerializeField, Tooltip("Localization table used for spell descriptions (default: UI.Common).")]
        private string _spellDescriptionTable = "UI.Common";
        [Header("Selected Spell Description Icons")]
        [SerializeField, Tooltip("Sprite index for the nature element icon in the ElementsIcon TMP sprite asset.")]
        private int _natureElementSpriteIndex = 0;
        [SerializeField, Tooltip("Sprite index for the fire element icon in the ElementsIcon TMP sprite asset.")]
        private int _fireElementSpriteIndex = 1;
        [SerializeField, Tooltip("Sprite index for the physical element icon in the ElementsIcon TMP sprite asset.")]
        private int _physicalElementSpriteIndex = 2;
        [SerializeField, Tooltip("Sprite index for the water element icon in the ElementsIcon TMP sprite asset.")]
        private int _waterElementSpriteIndex = 3;
        [SerializeField, Tooltip("Sprite index for the electric element icon in the ElementsIcon TMP sprite asset.")]
        private int _electricElementSpriteIndex = 4;

        private sealed class SlotView
        {
            public GameObject Root;
            public BattleSpellSlotView View;
            public Button Button;
            public GameObject SelectionFrame;
            public Image Icon;
            public TMP_Text ApCost;
            public Sprite LastIconSprite;
            public int LastApCost = int.MinValue;
            public UnityAction ClickAction;
        }

        private sealed class SpellDescriptionArgs
        {
            public string DamageNumber { get; }
            public string PrimaryElementIcon { get; }

            public SpellDescriptionArgs(string damageNumber, string primaryElementIcon)
            {
                DamageNumber = damageNumber ?? string.Empty;
                PrimaryElementIcon = primaryElementIcon ?? string.Empty;
            }
        }

        private readonly List<SlotView> _slots = new List<SlotView>(8);
        private int _selectedIndex = -1;
        private bool _boundFixedSlots;
        private SpellDefinition[] _currentSpells = Array.Empty<SpellDefinition>();
        private SpellDefinition _selectedSpell;
        private LocalizedString _selectedSpellDescriptionString;

        private void Awake()
        {
            if (_controllerBehaviour == null)
                Debug.LogWarning("BattleSpellsHUD: Please assign a controller (MonoBehaviour implementing ITurnOrderController).", this);
            _controller = _controllerBehaviour as ITurnOrderController;
            _spellSelectionController = _controllerBehaviour as ISpellSelectionController;

            _sfxPlayer = _sfxPlayerBehaviour as IUiSfxPlayer;

            if (_spellsContainer == null)
                Debug.LogWarning("BattleSpellsHUD: Please assign a spells container RectTransform (e.g., BattleHUD/SpellsContainer).", this);

            if (_slotTemplate == null && _spellsContainer != null)
            {
                var inferred = _spellsContainer.Find("SpellSlotTemplate") as RectTransform;
                if (inferred != null)
                {
                    _slotTemplate = inferred;
                }
            }

            if (_slotTemplate != null)
            {
                _slotTemplate.gameObject.SetActive(false);
            }

            if (_useFixedSlots)
            {
                TryBindFixedSlots();
            }
        }

        private void OnEnable()
        {
            if (_controller == null && _controllerBehaviour != null)
            {
                _controller = _controllerBehaviour as ITurnOrderController;
            }

            if (_spellSelectionController == null && _controllerBehaviour != null)
            {
                _spellSelectionController = _controllerBehaviour as ISpellSelectionController;
            }

            if (_sfxPlayer == null && _sfxPlayerBehaviour != null)
            {
                _sfxPlayer = _sfxPlayerBehaviour as IUiSfxPlayer;
            }

            if (_controller != null)
            {
                _controller.ActiveUnitChanged += HandleActiveUnitChanged;
                _controller.ActiveUnitStatsChanged += HandleActiveUnitStatsChanged;
            }

            if (_spellSelectionController != null)
            {
                _spellSelectionController.SelectedSpellChanged += HandleSelectedSpellChanged;
            }

            if (_useFixedSlots)
            {
                TryBindFixedSlots();
            }

            Refresh();
        }

        private void OnDisable()
        {
            if (_controller != null)
            {
                _controller.ActiveUnitChanged -= HandleActiveUnitChanged;
                _controller.ActiveUnitStatsChanged -= HandleActiveUnitStatsChanged;
            }

            if (_spellSelectionController != null)
            {
                _spellSelectionController.SelectedSpellChanged -= HandleSelectedSpellChanged;
            }

            UnwireSlotClicks();
            UnbindSelectedSpellDescription();
            _selectedSpell = null;
            if (_selectedSpellDescriptionText != null)
            {
                _selectedSpellDescriptionText.text = string.Empty;
            }
        }

        private void HandleActiveUnitChanged()
        {
            if (_spellSelectionController != null)
            {
                SyncSelectedIndexFromController();
                Refresh();
                return;
            }

            SetSelectedIndex(-1);
        }

        private void HandleActiveUnitStatsChanged()
        {
            RefreshSelectedSpellDescriptionValue();
        }

        private void Refresh()
        {
            if (_spellsContainer == null)
            {
                return;
            }

            bool shouldShow = _controller != null && _controller.HasActiveUnit;
            if (shouldShow && _hideWhenNotPlayerControlled)
            {
                shouldShow = _controller.IsActiveUnitPlayerControlled;
            }

            var spells = shouldShow && _controller != null ? _controller.ActiveUnitSpells : Array.Empty<SpellDefinition>();
            _currentSpells = spells ?? Array.Empty<SpellDefinition>();

            if (_spellSelectionController != null)
            {
                _selectedIndex = FindSpellIndex(_spellSelectionController.SelectedSpell, _currentSpells);
            }

            if (_selectedIndex >= _currentSpells.Length)
            {
                _selectedIndex = -1;
            }
            EnsureSlotCount(spells.Length);

            for (int i = 0; i < _slots.Count; i++)
            {
                bool visible = i < spells.Length && spells[i] != null;
                var slot = _slots[i];
                if (slot.Root != null && slot.Root.activeSelf != visible)
                {
                    slot.Root.SetActive(visible);
                }

                if (!visible)
                {
                    if (slot.Icon != null)
                    {
                        slot.Icon.sprite = null;
                        slot.Icon.enabled = false;
                        slot.LastIconSprite = null;
                    }
                    if (slot.ApCost != null)
                    {
                        slot.ApCost.text = string.Empty;
                        slot.ApCost.gameObject.SetActive(false);
                        slot.LastApCost = int.MinValue;
                    }

                    if (slot.SelectionFrame != null && slot.SelectionFrame.activeSelf)
                    {
                        slot.SelectionFrame.SetActive(false);
                    }
                    continue;
                }

                var spell = spells[i];
                int apCost = Mathf.Max(0, spell.ActionPointCost);
                bool spent = _controller != null && _controller.IsActiveUnitSpellSpentThisTurn(spell);

                if (slot.Icon != null)
                {
                    if (slot.LastIconSprite != spell.Icon)
                    {
                        slot.LastIconSprite = spell.Icon;
                        slot.Icon.sprite = spell.Icon;
                        slot.Icon.enabled = slot.Icon.sprite != null;
                    }
                }

                if (slot.ApCost != null)
                {
                    slot.ApCost.gameObject.SetActive(true);
                    if (slot.LastApCost != apCost)
                    {
                        slot.LastApCost = apCost;
                        slot.ApCost.text = apCost.ToString();
                    }
                }

                if (slot.Button != null)
                {
                    slot.Button.interactable = !spent;
                }

                if (slot.SelectionFrame != null)
                {
                    bool selected = i == _selectedIndex;
                    if (slot.SelectionFrame.activeSelf != selected)
                    {
                        slot.SelectionFrame.SetActive(selected);
                    }
                }
            }

            RefreshSelectedSpellDescription(shouldShow);
        }

        private void EnsureSlotCount(int required)
        {
            required = Mathf.Max(0, required);

            if (_useFixedSlots)
            {
                TryBindFixedSlots();
                if (_slots.Count > 0 && required > _slots.Count)
                {
                    Debug.LogWarning($"BattleSpellsHUD: Active unit has {required} spells but only {_slots.Count} fixed slots are available. Extra spells are not shown.", this);
                }
                return;
            }

            while (_slots.Count < required)
            {
                var slot = CreateSlot(_slots.Count);
                _slots.Add(slot);
            }
        }

        private void TryBindFixedSlots()
        {
            if (_boundFixedSlots) return;
            if (_spellsContainer == null) return;

            var roots = GetFixedSlotRoots();
            if (roots.Count == 0) return;

            UnwireSlotClicks();
            _slots.Clear();

            for (int i = 0; i < roots.Count; i++)
            {
                var root = roots[i];
                if (root == null) continue;
                _slots.Add(BuildSlotViewFromExisting(root, i));
            }

            WireSlotClicks();
            _boundFixedSlots = true;
        }

        private List<RectTransform> GetFixedSlotRoots()
        {
            var results = new List<RectTransform>(8);

            if (_fixedSlotRoots != null && _fixedSlotRoots.Length > 0)
            {
                for (int i = 0; i < _fixedSlotRoots.Length; i++)
                {
                    if (_fixedSlotRoots[i] != null)
                    {
                        results.Add(_fixedSlotRoots[i]);
                    }
                }
                return results;
            }

            // Auto-find by convention: Spell0..Spell6
            for (int i = 0; i < 32; i++)
            {
                var tf = _spellsContainer.Find($"Spell{i}");
                if (tf == null)
                {
                    // stop at the first missing index to avoid scanning a large range forever
                    if (i > 0) break;
                    continue;
                }
                if (tf is RectTransform rt)
                {
                    results.Add(rt);
                }
            }

            return results;
        }

        private SlotView BuildSlotViewFromExisting(RectTransform root, int index)
        {
            var typedView = root.GetComponentInChildren<BattleSpellSlotView>(true);
            var icon = FindIconImage(root);
            var ap = FindOrCreateApCost(root);
            var button = typedView != null && typedView.Button != null ? typedView.Button : root.GetComponentInChildren<Button>(true);

            GameObject frame = null;
            if (typedView != null && typedView.SelectionFrame != null)
            {
                frame = typedView.SelectionFrame;
            }
            else if (!string.IsNullOrEmpty(_selectionFrameChildName))
            {
                var frameTf = FindChildByName(root, _selectionFrameChildName);
                if (frameTf != null) frame = frameTf.gameObject;
            }

            ConfigureSlotRaycasts(root, button);

            if (frame != null) frame.SetActive(false);
            root.gameObject.SetActive(false);

            return new SlotView
            {
                Root = root.gameObject,
                View = typedView,
                Button = button,
                SelectionFrame = frame,
                Icon = icon,
                ApCost = ap
            };
        }

        private void WireSlotClicks()
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                int idx = i;
                var btn = _slots[i].Button;
                if (btn == null) continue;
                if (_slots[i].ClickAction == null)
                {
                    _slots[i].ClickAction = () => HandleSlotClicked(idx);
                }
                btn.onClick.RemoveListener(_slots[i].ClickAction);
                btn.onClick.AddListener(_slots[i].ClickAction);
            }
        }

        private void UnwireSlotClicks()
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                var btn = _slots[i].Button;
                if (btn == null) continue;
                if (_slots[i].ClickAction != null)
                {
                    btn.onClick.RemoveListener(_slots[i].ClickAction);
                }
            }
        }

        private void HandleSlotClicked(int index)
        {
            if (index < 0 || index >= _slots.Count) return;
            if (_slots[index].Root == null || !_slots[index].Root.activeInHierarchy) return;

            PlaySpellClickSfx();

            if (_spellSelectionController != null)
            {
                var spell = index >= 0 && index < _currentSpells.Length ? _currentSpells[index] : null;
                if (index == _selectedIndex)
                {
                    _spellSelectionController.SetSelectedSpell(null);
                }
                else
                {
                    _spellSelectionController.SetSelectedSpell(spell);
                }

                SyncSelectedIndexFromController();
                Refresh();
                return;
            }

            if (index == _selectedIndex)
            {
                SetSelectedIndex(-1);
                return;
            }

            SetSelectedIndex(index);
        }

        private void SetSelectedIndex(int index)
        {
            _selectedIndex = index;
            Refresh();
        }

        private void HandleSelectedSpellChanged()
        {
            SyncSelectedIndexFromController();
            Refresh();
        }

        private void SyncSelectedIndexFromController()
        {
            if (_spellSelectionController == null)
            {
                return;
            }

            _selectedIndex = FindSpellIndex(_spellSelectionController.SelectedSpell, _currentSpells);
        }

        private static int FindSpellIndex(SpellDefinition spell, SpellDefinition[] spells)
        {
            if (spell == null || spells == null)
            {
                return -1;
            }

            for (int i = 0; i < spells.Length; i++)
            {
                if (ReferenceEquals(spells[i], spell))
                {
                    return i;
                }
            }

            return -1;
        }

        private void RefreshSelectedSpellDescription(bool shouldShow)
        {
            if (_selectedSpellDescriptionText == null)
            {
                return;
            }

            var spell = shouldShow && _selectedIndex >= 0 && _selectedIndex < _currentSpells.Length
                ? _currentSpells[_selectedIndex]
                : null;

            if (ReferenceEquals(_selectedSpell, spell))
            {
                return;
            }

            _selectedSpell = spell;
            BindSelectedSpellDescription(spell);
        }

        private void RefreshSelectedSpellDescriptionValue()
        {
            if (_selectedSpellDescriptionText == null)
            {
                return;
            }

            if (_selectedSpell == null)
            {
                return;
            }

            if (_selectedSpellDescriptionString != null)
            {
                UpdateSelectedSpellDescriptionArgs(_selectedSpell);
                _selectedSpellDescriptionString.RefreshString();
                return;
            }

            _selectedSpellDescriptionText.text = BuildFallbackSpellDescription(_selectedSpell);
        }

        private void BindSelectedSpellDescription(SpellDefinition spell)
        {
            UnbindSelectedSpellDescription();

            if (_selectedSpellDescriptionText == null)
            {
                return;
            }

            if (spell == null)
            {
                _selectedSpellDescriptionText.text = string.Empty;
                return;
            }

            var key = spell.DescriptionLocalizationKey;
            if (!string.IsNullOrWhiteSpace(_spellDescriptionTable) && !string.IsNullOrWhiteSpace(key))
            {
                EnsureSpellDescriptionEntryIsSmart(key);
                _selectedSpellDescriptionString = new LocalizedString(_spellDescriptionTable, key);
                UpdateSelectedSpellDescriptionArgs(spell);
                _selectedSpellDescriptionString.StringChanged += HandleSelectedSpellDescriptionChanged;
                _selectedSpellDescriptionString.RefreshString();
                return;
            }

            // Fallback for dev/debug when a spell asset is missing a localization key.
            _selectedSpellDescriptionText.text = BuildFallbackSpellDescription(spell);
        }

        private void EnsureSpellDescriptionEntryIsSmart(string key)
        {
            if (string.IsNullOrWhiteSpace(_spellDescriptionTable) || string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            var locale = LocalizationSettings.SelectedLocale;
            var table = LocalizationSettings.StringDatabase.GetTable(_spellDescriptionTable, locale);
            if (table == null)
            {
                return;
            }

            var entry = table.GetEntry(key);
            if (entry != null && !entry.IsSmart)
            {
                entry.IsSmart = true;
            }
        }

        private void UpdateSelectedSpellDescriptionArgs(SpellDefinition spell)
        {
            if (_selectedSpellDescriptionString == null)
            {
                return;
            }

            var amount = BuildFormattedPrimaryAmount(spell);
            var elementIcon = BuildPrimaryElementIconTag(spell);
            _selectedSpellDescriptionString.Arguments = new object[]
            {
                new SpellDescriptionArgs(amount, elementIcon)
            };
        }

        private string BuildFallbackSpellDescription(SpellDefinition spell)
        {
            var template = spell != null ? (spell.Description ?? string.Empty) : string.Empty;
            if (string.IsNullOrWhiteSpace(template))
            {
                return string.Empty;
            }

            var amount = BuildFormattedPrimaryAmount(spell);
            var elementIcon = BuildPrimaryElementIconTag(spell);
            var text = template;

            if (text.Contains("{DamageNumber}", StringComparison.Ordinal))
            {
                text = text.Replace("{DamageNumber}", amount);
            }

            if (text.Contains("{PrimaryElementIcon}", StringComparison.Ordinal))
            {
                text = text.Replace("{PrimaryElementIcon}", elementIcon);
            }

            if (text.Contains("{0}", StringComparison.Ordinal))
            {
                return string.Format(CultureInfo.InvariantCulture, text, amount);
            }

            if (text.Contains("X", StringComparison.Ordinal))
            {
                return text.Replace("X", amount);
            }

            return text;
        }

        private string BuildFormattedPrimaryAmount(SpellDefinition spell)
        {
            if (_controller != null && spell != null && _controller.TryGetActiveUnitSpellAmountPreview(spell, out var preview))
            {
                int baseAmount = preview.BaseAmount;
                int modifiedAmount = preview.ModifiedAmount;
                string number = modifiedAmount.ToString(CultureInfo.InvariantCulture);

                if (modifiedAmount > baseAmount)
                {
                    return $"<color=#00C853><b>{number}</b></color>";
                }

                if (modifiedAmount < baseAmount)
                {
                    return $"<color=#D50000><b>{number}</b></color>";
                }

                return number;
            }

            return "X";
        }

        private string BuildPrimaryElementIconTag(SpellDefinition spell)
        {
            if (spell == null)
            {
                return string.Empty;
            }

            if (spell.PrimaryAmountKind != SpellPrimaryAmountKind.Damage)
            {
                return string.Empty;
            }

            var element = spell.PrimaryDamageElement;
            int spriteIndex;
            switch (element)
            {
                case DamageElement.Fire:
                    spriteIndex = _fireElementSpriteIndex;
                    break;
                case DamageElement.Frost:
                    spriteIndex = _waterElementSpriteIndex;
                    break;
                case DamageElement.Lightning:
                    spriteIndex = _electricElementSpriteIndex;
                    break;
                case DamageElement.Poison:
                    spriteIndex = _natureElementSpriteIndex;
                    break;
                case DamageElement.None:
                    spriteIndex = _physicalElementSpriteIndex;
                    break;
                default:
                    return string.Empty;
            }

            return $" <sprite={spriteIndex}>";
        }

        private void HandleSelectedSpellDescriptionChanged(string value)
        {
            if (_selectedSpellDescriptionText == null) return;
            _selectedSpellDescriptionText.text = value ?? string.Empty;
        }

        private void UnbindSelectedSpellDescription()
        {
            if (_selectedSpellDescriptionString != null)
            {
                _selectedSpellDescriptionString.StringChanged -= HandleSelectedSpellDescriptionChanged;
                _selectedSpellDescriptionString = null;
            }
        }

        private SlotView CreateSlot(int index)
        {
            GameObject root;
            RectTransform rootRt;

            if (_slotTemplate != null)
            {
                rootRt = Instantiate(_slotTemplate, _spellsContainer);
                root = rootRt.gameObject;
                root.name = $"SpellSlot{index}";
                root.SetActive(true);
            }
            else
            {
                root = new GameObject($"SpellSlot{index}", typeof(RectTransform));
                rootRt = root.GetComponent<RectTransform>();
                rootRt.SetParent(_spellsContainer, false);
                rootRt.sizeDelta = new Vector2(100f, 100f);

                var bg = root.AddComponent<Image>();
                bg.color = new Color(1f, 1f, 1f, 0.08f);
                bg.raycastTarget = false;

                var iconGo = new GameObject(_iconChildName, typeof(RectTransform));
                var iconRt = iconGo.GetComponent<RectTransform>();
                iconRt.SetParent(rootRt, false);
                iconRt.anchorMin = Vector2.zero;
                iconRt.anchorMax = Vector2.one;
                iconRt.offsetMin = new Vector2(10f, 10f);
                iconRt.offsetMax = new Vector2(-10f, -10f);
                var iconImg = iconGo.AddComponent<Image>();
                iconImg.raycastTarget = false;
            }

            var icon = FindIconImage(rootRt);
            var ap = FindOrCreateApCost(rootRt);

            return new SlotView
            {
                Root = root,
                View = rootRt.GetComponentInChildren<BattleSpellSlotView>(true),
                Icon = icon,
                ApCost = ap
            };
        }

        private Image FindIconImage(RectTransform slotRoot)
        {
            if (slotRoot == null) return null;

            var typedView = slotRoot.GetComponentInChildren<BattleSpellSlotView>(true);
            if (typedView != null && typedView.Icon != null)
            {
                return typedView.Icon;
            }

            if (!string.IsNullOrEmpty(_iconChildName))
            {
                var child = FindChildByName(slotRoot, _iconChildName);
                if (child != null)
                {
                    var img = child.GetComponent<Image>();
                    if (img != null) return img;
                }
            }

            var images = slotRoot.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] == null) continue;
                if (images[i].transform == slotRoot) continue;

                var n = images[i].transform.name;
                if (!string.IsNullOrEmpty(n) &&
                    (string.Equals(n, "Bg", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(n, "Background", StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                return images[i];
            }

            return slotRoot.GetComponent<Image>();
        }

        private TMP_Text FindOrCreateApCost(RectTransform slotRoot)
        {
            if (slotRoot == null) return null;

            var typedView = slotRoot.GetComponentInChildren<BattleSpellSlotView>(true);
            if (typedView != null && typedView.ApCost != null)
            {
                return typedView.ApCost;
            }

            Transform child = null;
            if (!string.IsNullOrEmpty(_apCostChildName))
            {
                child = FindChildByName(slotRoot, _apCostChildName);
            }

            if (child == null)
            {
                var existing = slotRoot.GetComponentInChildren<TMP_Text>(true);
                if (existing != null)
                {
                    return existing;
                }
            }

            if (child == null)
            {
                var go = new GameObject(_apCostChildName, typeof(RectTransform));
                var rt = go.GetComponent<RectTransform>();
                rt.SetParent(slotRoot, false);
                rt.anchorMin = new Vector2(1f, 0f);
                rt.anchorMax = new Vector2(1f, 0f);
                rt.pivot = new Vector2(1f, 0f);
                rt.anchoredPosition = new Vector2(-6f, 6f);
                rt.sizeDelta = new Vector2(64f, 32f);
                child = rt.transform;
            }

            var tmp = child.GetComponent<TextMeshProUGUI>();
            if (tmp == null) tmp = child.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.alignment = TextAlignmentOptions.BottomRight;
            tmp.fontSize = Mathf.Max(8f, _apCostFontSize);
            tmp.raycastTarget = false;
            tmp.gameObject.SetActive(false);
            return tmp;
        }

        private static Transform FindChildByName(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name)) return null;

            // Fast path: direct child (common case).
            var direct = root.Find(name);
            if (direct != null) return direct;

            var children = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                var t = children[i];
                if (t == null || t == root) continue;
                if (string.Equals(t.name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return t;
                }
            }

            return null;
        }

        private static void ConfigureSlotRaycasts(RectTransform slotRoot, Button button)
        {
            if (slotRoot == null) return;

            var targetGraphic = button != null ? button.targetGraphic : null;
            var graphics = slotRoot.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                var g = graphics[i];
                if (g == null) continue;
                g.raycastTarget = false;
            }

            if (targetGraphic != null)
            {
                targetGraphic.raycastTarget = true;
            }
            else
            {
                // Fallback: allow clicking on the first Graphic on the same object as the Button.
                if (button != null)
                {
                    var g = button.GetComponent<Graphic>();
                    if (g != null) g.raycastTarget = true;
                }
            }
        }

        private void PlaySpellClickSfx()
        {
            if (_spellClickClip == null) return;
            if (Time.unscaledTime - _lastSpellClickSfxTime < _spellClickCooldown) return;

            float volume = Mathf.Clamp(_spellClickVolume, 0f, 1.5f);
            if (_sfxPlayer != null)
            {
                _sfxPlayer.PlayOneShot(_spellClickClip, volume);
            }
            else if (_audio != null)
            {
                _audio.PlayOneShot(_spellClickClip, volume);
            }
            else
            {
                AudioSource.PlayClipAtPoint(_spellClickClip, Vector3.zero, volume);
            }

            _lastSpellClickSfxTime = Time.unscaledTime;
        }
    }
}
