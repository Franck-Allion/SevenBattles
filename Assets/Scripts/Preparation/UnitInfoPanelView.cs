using SevenBattles.Core.Battle;
using SevenBattles.Core.Players;
using SevenBattles.Core.Units;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace SevenBattles.Preparation
{
    public sealed class UnitInfoPanelView : MonoBehaviour
    {
        private const string DefaultLocalizationTable = "UI.Common";

        [SerializeField] private Image _portrait;
        [SerializeField] private TMP_Text _nameLabel;
        [SerializeField] private TMP_InputField _nameInputField;
        [SerializeField] private GameObject _nameDisplayRoot;
        [SerializeField] private GameObject _nameEditRoot;
        [SerializeField] private TMP_Text _nameValidationLabel;
        [SerializeField, Tooltip("Localized placeholder shown while editing unit name.")]
        private LocalizedString _editNamePlaceholder = new LocalizedString(DefaultLocalizationTable, "UI.Squad.EditName.Placeholder");
        [SerializeField, Tooltip("Localized validation shown when the typed name exceeds max length.")]
        private LocalizedString _nameTooLongValidation = new LocalizedString(DefaultLocalizationTable, "UI.Squad.EditName.Validation.TooLong");
        [SerializeField] private TMP_Text _levelLabel;
        [SerializeField] private TMP_Text _lifeValue;
        [SerializeField] private TMP_Text _attackValue;
        [SerializeField] private TMP_Text _shootValue;
        [SerializeField] private TMP_Text _spellValue;
        [SerializeField] private TMP_Text _speedValue;
        [SerializeField] private TMP_Text _luckValue;
        [SerializeField] private TMP_Text _defenseValue;
        [SerializeField] private TMP_Text _protectionValue;
        [SerializeField] private TMP_Text _initiativeValue;
        [SerializeField] private TMP_Text _moraleValue;
        [SerializeField] private GameObject _emptyState;
        [SerializeField] private GameObject _statsContainer;
        [SerializeField, Tooltip("Child object name used to auto-find stat label TMP under each stat row.")]
        private string _statLabelObjectName = "Label";

        private string _selectedOwnedUnitId;
        private string _editingOwnedUnitId;
        private string _displayName;
        private bool _isEditingName;
        private bool _nameEditFinalized;
        private TMP_Text _lifeLabel;
        private TMP_Text _attackLabel;
        private TMP_Text _shootLabel;
        private TMP_Text _spellLabel;
        private TMP_Text _speedLabel;
        private TMP_Text _luckLabel;
        private TMP_Text _defenseLabel;
        private TMP_Text _protectionLabel;
        private TMP_Text _initiativeLabel;
        private TMP_Text _moraleLabel;

        public event System.Action<string, string> NameCommitRequested;

        private void Awake()
        {
            EnsureNameDisplayRoot();
            EnsureNameInputField();
            WireNameEditEvents();
            SetNameEditMode(false);
        }

        private void OnEnable()
        {
            WireNameEditEvents();
            RefreshNamePlaceholder();
            RefreshStatLabels();
            LocalizationSettings.SelectedLocaleChanged -= HandleSelectedLocaleChanged;
            LocalizationSettings.SelectedLocaleChanged += HandleSelectedLocaleChanged;
            ClearNameValidation();
        }

        private void OnDisable()
        {
            LocalizationSettings.SelectedLocaleChanged -= HandleSelectedLocaleChanged;
            CancelNameEditSilently();
            UnwireNameEditEvents();
        }

        private void Update()
        {
            if (!_isEditingName)
            {
                return;
            }

            if (!Input.GetKeyDown(KeyCode.Escape))
            {
                return;
            }

            CancelNameEditSilently();
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        public void ShowUnit(UnitSpellLoadout loadout, string ownedUnitId, string displayName)
        {
            if (loadout == null || loadout.Definition == null)
            {
                Clear();
                return;
            }

            if (_isEditingName && !string.Equals(_selectedOwnedUnitId, ownedUnitId, System.StringComparison.Ordinal))
            {
                CommitNameEditFromSelectionSwitch();
            }

            UnitDefinition def = loadout.Definition;
            int level = loadout.EffectiveLevel;
            UnitStatsData baseStats = def.BaseStats;
            UnitStatsData stats = def.LevelBonus.ApplyTo(baseStats, level);
            _selectedOwnedUnitId = ownedUnitId;
            _displayName = !string.IsNullOrWhiteSpace(displayName)
                ? displayName
                : ResolveDefinitionDisplayName(def);

            if (_portrait != null)
            {
                _portrait.sprite = def.Portrait;
                _portrait.enabled = _portrait.sprite != null;
            }

            SetText(_nameLabel, _displayName);
            SetText(_levelLabel, level.ToString());
            SetText(_lifeValue, stats.Life.ToString());
            SetText(_attackValue, stats.Attack.ToString());
            SetText(_shootValue, stats.Shoot.ToString());
            SetText(_spellValue, stats.Spell.ToString());
            SetText(_speedValue, stats.Speed.ToString());
            SetText(_luckValue, stats.Luck.ToString());
            SetText(_defenseValue, stats.Defense.ToString());
            SetText(_protectionValue, stats.Protection.ToString());
            SetText(_initiativeValue, stats.Initiative.ToString());
            SetText(_moraleValue, stats.Morale.ToString());

            if (_emptyState != null)
            {
                _emptyState.SetActive(false);
            }

            if (_statsContainer != null)
            {
                _statsContainer.SetActive(true);
            }

            SetNameEditMode(false);
            ClearNameValidation();
        }

        public void ShowUnit(UnitSpellLoadout loadout)
        {
            string fallbackName = loadout != null ? ResolveDefinitionDisplayName(loadout.Definition) : string.Empty;
            ShowUnit(loadout, null, fallbackName);
        }

        public void SetDisplayedName(string name)
        {
            _displayName = name ?? string.Empty;
            SetText(_nameLabel, _displayName);
            if (_nameInputField != null && !_isEditingName)
            {
                _nameInputField.SetTextWithoutNotify(_displayName);
            }
        }

        public void Clear()
        {
            CancelNameEditSilently();
            _selectedOwnedUnitId = null;
            _displayName = string.Empty;

            if (_portrait != null)
            {
                _portrait.sprite = null;
                _portrait.enabled = false;
            }

            SetText(_nameLabel, string.Empty);
            SetText(_levelLabel, string.Empty);
            SetText(_lifeValue, string.Empty);
            SetText(_attackValue, string.Empty);
            SetText(_shootValue, string.Empty);
            SetText(_spellValue, string.Empty);
            SetText(_speedValue, string.Empty);
            SetText(_luckValue, string.Empty);
            SetText(_defenseValue, string.Empty);
            SetText(_protectionValue, string.Empty);
            SetText(_initiativeValue, string.Empty);
            SetText(_moraleValue, string.Empty);

            if (_emptyState != null)
            {
                _emptyState.SetActive(true);
            }

            if (_statsContainer != null)
            {
                _statsContainer.SetActive(false);
            }

            ClearNameValidation();
            SetNameEditMode(false);
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null)
            {
                target.text = value ?? string.Empty;
            }
        }

        private static string ResolveDefinitionDisplayName(UnitDefinition definition)
        {
            if (definition == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(definition.name))
            {
                return definition.name;
            }

            return definition.Id ?? string.Empty;
        }

        private void HandleSelectedLocaleChanged(Locale _)
        {
            RefreshNamePlaceholder();
            RefreshStatLabels();
        }

        private void RefreshStatLabels()
        {
            _lifeLabel = ResolveStatLabel(_lifeValue, _lifeLabel);
            _attackLabel = ResolveStatLabel(_attackValue, _attackLabel);
            _shootLabel = ResolveStatLabel(_shootValue, _shootLabel);
            _spellLabel = ResolveStatLabel(_spellValue, _spellLabel);
            _speedLabel = ResolveStatLabel(_speedValue, _speedLabel);
            _luckLabel = ResolveStatLabel(_luckValue, _luckLabel);
            _defenseLabel = ResolveStatLabel(_defenseValue, _defenseLabel);
            _protectionLabel = ResolveStatLabel(_protectionValue, _protectionLabel);
            _initiativeLabel = ResolveStatLabel(_initiativeValue, _initiativeLabel);
            _moraleLabel = ResolveStatLabel(_moraleValue, _moraleLabel);

            SetLabelText(_lifeLabel, "stats.life", "Life");
            SetLabelText(_attackLabel, "stats.attack", "Attack");
            SetLabelText(_shootLabel, "stats.shoot", "Shoot");
            SetLabelText(_spellLabel, "stats.spell", "Spell");
            SetLabelText(_speedLabel, "stats.speed", "Speed");
            SetLabelText(_luckLabel, "stats.luck", "Luck");
            SetLabelText(_defenseLabel, "stats.defense", "Defense");
            SetLabelText(_protectionLabel, "stats.protection", "Protection");
            SetLabelText(_initiativeLabel, "stats.initiative", "Initiative");
            SetLabelText(_moraleLabel, "stats.morale", "Morale");
        }

        private TMP_Text ResolveStatLabel(TMP_Text valueText, TMP_Text cachedLabel)
        {
            if (cachedLabel != null)
            {
                return cachedLabel;
            }

            if (valueText == null || valueText.transform == null || valueText.transform.parent == null)
            {
                return null;
            }

            Transform row = valueText.transform.parent;
            if (!string.IsNullOrWhiteSpace(_statLabelObjectName))
            {
                Transform labelNode = FindChildByName(row, _statLabelObjectName);
                if (labelNode != null)
                {
                    TMP_Text label = labelNode.GetComponent<TMP_Text>();
                    if (label != null)
                    {
                        return label;
                    }

                    label = labelNode.GetComponentInChildren<TMP_Text>(true);
                    if (label != null)
                    {
                        return label;
                    }
                }
            }

            TMP_Text[] texts = row.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text candidate = texts[i];
                if (candidate == null || candidate == valueText || candidate.gameObject == null)
                {
                    continue;
                }

                string name = candidate.gameObject.name;
                if (!string.IsNullOrWhiteSpace(name) &&
                    name.IndexOf("label", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return candidate;
                }
            }

            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text candidate = texts[i];
                if (candidate != null && candidate != valueText)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static Transform FindChildByName(Transform root, string childName)
        {
            if (root == null || string.IsNullOrWhiteSpace(childName))
            {
                return null;
            }

            var nodes = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < nodes.Length; i++)
            {
                Transform node = nodes[i];
                if (node != null && string.Equals(node.name, childName, System.StringComparison.Ordinal))
                {
                    return node;
                }
            }

            return null;
        }

        private static void SetLabelText(TMP_Text target, string key, string fallback)
        {
            if (target == null)
            {
                return;
            }

            target.text = GetLocalizedCommonString(key, fallback);
        }

        private static string GetLocalizedCommonString(string key, string fallback)
        {
            if (LocalizationSettings.StringDatabase == null)
            {
                return fallback;
            }

            try
            {
                string localized = LocalizationSettings.StringDatabase.GetLocalizedString(DefaultLocalizationTable, key);
                return !string.IsNullOrWhiteSpace(localized) ? localized : fallback;
            }
            catch
            {
                return fallback;
            }
        }

        private void EnsureNameDisplayRoot()
        {
            if (_nameDisplayRoot == null && _nameLabel != null)
            {
                _nameDisplayRoot = _nameLabel.gameObject;
            }

            if (_nameDisplayRoot == null)
            {
                return;
            }

            Button button = _nameDisplayRoot.GetComponent<Button>();
            if (button == null)
            {
                button = _nameDisplayRoot.AddComponent<Button>();
            }

            button.transition = Selectable.Transition.None;
            if (button.targetGraphic == null && _nameLabel != null)
            {
                button.targetGraphic = _nameLabel;
            }

            button.onClick.RemoveListener(TryBeginNameEdit);
            button.onClick.AddListener(TryBeginNameEdit);
        }

        private void EnsureNameInputField()
        {
            if (_nameInputField != null)
            {
                if (_nameEditRoot == null)
                {
                    _nameEditRoot = _nameInputField.gameObject;
                }
                EnsureInputFieldViewport();
                return;
            }

            if (_nameLabel == null)
            {
                return;
            }

            RectTransform sourceRect = _nameLabel.rectTransform;
            Transform parent = sourceRect.parent;
            if (parent == null)
            {
                return;
            }

            var rootObject = new GameObject("NameEdit", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
            rootObject.transform.SetParent(parent, false);
            RectTransform rootRect = rootObject.GetComponent<RectTransform>();
            rootRect.anchorMin = sourceRect.anchorMin;
            rootRect.anchorMax = sourceRect.anchorMax;
            rootRect.pivot = sourceRect.pivot;
            rootRect.anchoredPosition = sourceRect.anchoredPosition;
            rootRect.sizeDelta = sourceRect.sizeDelta;
            rootRect.localRotation = sourceRect.localRotation;
            rootRect.localScale = sourceRect.localScale;

            Image background = rootObject.GetComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.35f);

            _nameInputField = rootObject.GetComponent<TMP_InputField>();
            _nameInputField.characterLimit = OwnedUnitNamingPolicy.MaxCustomNameLength;
            _nameInputField.lineType = TMP_InputField.LineType.SingleLine;
            _nameInputField.richText = false;

            RectTransform viewport = CreateInputViewport(rootObject.transform);
            TMP_Text inputText = CreateInputTextChild("Text", viewport);
            TMP_Text placeholder = CreateInputTextChild("Placeholder", viewport);
            placeholder.fontStyle = FontStyles.Italic;
            Color placeholderColor = placeholder.color;
            placeholderColor.a = 0.45f;
            placeholder.color = placeholderColor;
            placeholder.text = "Name";

            _nameInputField.textComponent = inputText;
            _nameInputField.placeholder = placeholder;
            _nameInputField.textViewport = viewport;
            _nameEditRoot = rootObject;
            EnsureInputFieldViewport();
        }

        private static RectTransform CreateInputViewport(Transform parent)
        {
            var viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewportObject.transform.SetParent(parent, false);
            RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            return viewportRect;
        }

        private static TMP_Text CreateInputTextChild(string objectName, Transform parent)
        {
            var textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(8f, 4f);
            rect.offsetMax = new Vector2(-8f, -4f);

            TMP_Text text = textObject.GetComponent<TextMeshProUGUI>();
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.fontSize = 36f;
            text.alignment = TextAlignmentOptions.Center;
            return text;
        }

        private void EnsureInputFieldViewport()
        {
            if (_nameInputField == null || _nameInputField.textViewport != null)
            {
                return;
            }

            RectTransform viewport = null;
            if (_nameInputField.textComponent != null)
            {
                viewport = _nameInputField.textComponent.rectTransform.parent as RectTransform;
            }

            if (viewport == null && _nameInputField.placeholder is TMP_Text placeholderText)
            {
                viewport = placeholderText.rectTransform.parent as RectTransform;
            }

            if (viewport == null)
            {
                viewport = _nameInputField.GetComponent<RectTransform>();
            }

            _nameInputField.textViewport = viewport;
        }

        private void WireNameEditEvents()
        {
            if (_nameInputField == null)
            {
                return;
            }

            _nameInputField.onSubmit.RemoveListener(HandleNameSubmit);
            _nameInputField.onSubmit.AddListener(HandleNameSubmit);
            _nameInputField.onEndEdit.RemoveListener(HandleNameEndEdit);
            _nameInputField.onEndEdit.AddListener(HandleNameEndEdit);
            _nameInputField.onValueChanged.RemoveListener(HandleNameValueChanged);
            _nameInputField.onValueChanged.AddListener(HandleNameValueChanged);
        }

        private void UnwireNameEditEvents()
        {
            if (_nameInputField == null)
            {
                return;
            }

            _nameInputField.onSubmit.RemoveListener(HandleNameSubmit);
            _nameInputField.onEndEdit.RemoveListener(HandleNameEndEdit);
            _nameInputField.onValueChanged.RemoveListener(HandleNameValueChanged);
        }

        private void TryBeginNameEdit()
        {
            if (_nameInputField == null || string.IsNullOrWhiteSpace(_selectedOwnedUnitId))
            {
                return;
            }

            _isEditingName = true;
            _editingOwnedUnitId = _selectedOwnedUnitId;
            _nameEditFinalized = false;
            SetNameEditMode(true);
            _nameInputField.SetTextWithoutNotify(_displayName ?? string.Empty);
            _nameInputField.ActivateInputField();
            _nameInputField.Select();
            ClearNameValidation();
        }

        private void HandleNameSubmit(string value)
        {
            CommitNameEdit(value);
        }

        private void HandleNameEndEdit(string value)
        {
            CommitNameEdit(value);
        }

        private void HandleNameValueChanged(string value)
        {
            if (!_isEditingName)
            {
                return;
            }

            if (value != null && value.Length > OwnedUnitNamingPolicy.MaxCustomNameLength)
            {
                ShowNameValidation(
                    _nameTooLongValidation,
                    $"Name must be at most {OwnedUnitNamingPolicy.MaxCustomNameLength} characters.",
                    OwnedUnitNamingPolicy.MaxCustomNameLength);
            }
            else
            {
                ClearNameValidation();
            }
        }

        private void CommitNameEdit(string enteredValue)
        {
            if (!_isEditingName || _nameEditFinalized)
            {
                return;
            }

            string targetOwnedUnitId = !string.IsNullOrWhiteSpace(_editingOwnedUnitId)
                ? _editingOwnedUnitId
                : _selectedOwnedUnitId;

            _nameEditFinalized = true;
            _isEditingName = false;
            _editingOwnedUnitId = null;
            SetNameEditMode(false);
            if (!string.IsNullOrWhiteSpace(targetOwnedUnitId))
            {
                NameCommitRequested?.Invoke(targetOwnedUnitId, enteredValue);
            }
            ClearNameValidation();
        }

        private void CancelNameEditSilently()
        {
            if (!_isEditingName && _nameInputField != null && _nameEditRoot != null && _nameEditRoot.activeSelf)
            {
                SetNameEditMode(false);
                return;
            }

            _nameEditFinalized = true;
            _isEditingName = false;
            _editingOwnedUnitId = null;
            if (_nameInputField != null)
            {
                _nameInputField.SetTextWithoutNotify(_displayName ?? string.Empty);
            }
            SetNameEditMode(false);
            ClearNameValidation();
        }

        private void SetNameEditMode(bool editing)
        {
            if (_nameDisplayRoot != null)
            {
                _nameDisplayRoot.SetActive(!editing);
            }

            if (_nameEditRoot != null)
            {
                _nameEditRoot.SetActive(editing);
            }
        }

        private void RefreshNamePlaceholder()
        {
            if (_nameInputField == null)
            {
                return;
            }

            TMP_Text placeholder = _nameInputField.placeholder as TMP_Text;
            if (placeholder == null)
            {
                return;
            }

            string text = GetLocalizedString(_editNamePlaceholder, "Enter unit name");
            placeholder.text = text;
        }

        private void ShowNameValidation(LocalizedString localized, string fallback, params object[] arguments)
        {
            if (_nameValidationLabel == null)
            {
                return;
            }

            _nameValidationLabel.gameObject.SetActive(true);
            _nameValidationLabel.text = GetLocalizedString(localized, fallback, arguments);
        }

        private void ClearNameValidation()
        {
            if (_nameValidationLabel == null)
            {
                return;
            }

            _nameValidationLabel.text = string.Empty;
            _nameValidationLabel.gameObject.SetActive(false);
        }

        private static string GetLocalizedString(LocalizedString localized, string fallback, params object[] arguments)
        {
            if (localized == null || localized.IsEmpty || LocalizationSettings.StringDatabase == null)
            {
                return fallback;
            }

            try
            {
                localized.Arguments = arguments;
                string resolved = localized.GetLocalizedString();
                return !string.IsNullOrWhiteSpace(resolved) ? resolved : fallback;
            }
            catch
            {
                return fallback;
            }
        }

        private void CommitNameEditFromSelectionSwitch()
        {
            if (!_isEditingName)
            {
                return;
            }

            if (_nameInputField == null)
            {
                CancelNameEditSilently();
                return;
            }

            CommitNameEdit(_nameInputField.text);
        }

    }
}
