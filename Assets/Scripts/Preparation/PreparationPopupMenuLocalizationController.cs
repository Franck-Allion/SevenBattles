using TMPro;
using UnityEngine;
using UnityEngine.Localization;
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

        [Header("Localization")]
        [SerializeField, Tooltip("Localized label for the Shop button.")]
        private LocalizedString _shopLabel;
        [SerializeField, Tooltip("Localized label for the Squad button.")]
        private LocalizedString _squadLabel;

        private void Awake()
        {
            SetupLocalizationDefaults();
            ResolveLabelTargets();
        }

        private void OnEnable()
        {
            SetupLocalizationDefaults();
            ResolveLabelTargets();
            BindLabels();
            RefreshLabels();
        }

        private void OnDisable()
        {
            UnbindLabels();
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
    }
}
