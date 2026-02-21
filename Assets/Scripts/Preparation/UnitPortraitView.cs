using System;
using SevenBattles.Core.Battle;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Localization;
using UnityEngine.UI;

namespace SevenBattles.Preparation
{
    public sealed class UnitPortraitView : MonoBehaviour, IPointerClickHandler
    {
        private const float MinGridScale = 0.1f;
        private const string LevelTable = "UI.Common";
        private const string LevelKey = "UI.Squad.Level";

        [SerializeField] private Image _portraitImage;
        [SerializeField] private TMP_Text _levelLabel;
        [SerializeField] private TMP_Text _nameLabel;

        private readonly LocalizedString _levelLocalized = new LocalizedString(LevelTable, LevelKey);

        public UnitSpellLoadout Loadout { get; private set; }

        public event Action<UnitPortraitView> Clicked;

        /// <summary>
        /// Applies a uniform scale for mini-portrait usage while preserving authored child layout.
        /// </summary>
        public void ApplyGridCellLayout(float overallScale = 1f)
        {
            overallScale = Mathf.Clamp(overallScale, MinGridScale, 2f);

            RectTransform root = transform as RectTransform;
            if (root != null)
            {
                // Keep the root centered in the grid host without touching child sizes/offsets.
                root.anchorMin = new Vector2(0.5f, 0.5f);
                root.anchorMax = new Vector2(0.5f, 0.5f);
                root.pivot = new Vector2(0.5f, 0.5f);
                root.anchoredPosition = Vector2.zero;
                root.localScale = new Vector3(overallScale, overallScale, 1f);
            }
        }

        public void Bind(UnitSpellLoadout loadout, string displayName = null)
        {
            Loadout = loadout;

            if (_portraitImage != null)
            {
                Sprite portrait = loadout != null && loadout.Definition != null ? loadout.Definition.Portrait : null;
                _portraitImage.sprite = portrait;
                _portraitImage.enabled = portrait != null;
            }

            if (_levelLabel != null)
            {
                if (loadout == null)
                {
                    _levelLabel.text = string.Empty;
                }
                else
                {
                    _levelLocalized.Arguments = new object[] { loadout.EffectiveLevel };
                    _levelLabel.text = TryGetLocalizedLevelText(loadout.EffectiveLevel);
                }
            }

            if (_nameLabel != null)
            {
                if (loadout == null)
                {
                    _nameLabel.text = string.Empty;
                }
                else
                {
                    _nameLabel.text = ResolveDisplayName(loadout, displayName);
                }
            }
        }

        public void Clear()
        {
            Loadout = null;

            if (_portraitImage != null)
            {
                _portraitImage.sprite = null;
                _portraitImage.enabled = false;
            }

            if (_levelLabel != null)
            {
                _levelLabel.text = string.Empty;
            }

            if (_nameLabel != null)
            {
                _nameLabel.text = string.Empty;
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Clicked?.Invoke(this);
        }

        private string TryGetLocalizedLevelText(int level)
        {
            try
            {
                string localized = _levelLocalized.GetLocalizedString();
                if (!string.IsNullOrEmpty(localized))
                {
                    return localized;
                }
            }
            catch
            {
                // Fallback to a minimal value if localization tables are not ready/missing.
            }

            return level.ToString();
        }

        private static string ResolveDisplayName(UnitSpellLoadout loadout, string explicitDisplayName)
        {
            if (!string.IsNullOrWhiteSpace(explicitDisplayName))
            {
                return explicitDisplayName;
            }

            if (loadout == null || loadout.Definition == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(loadout.Definition.name))
            {
                return loadout.Definition.name;
            }

            return loadout.Definition.Id ?? string.Empty;
        }
    }
}
