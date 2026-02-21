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
        private const string LevelTable = "UI.Common";
        private const string LevelKey = "UI.Squad.Level";
        private const float LevelBadgeBaseCenterY = 12f;

        [SerializeField] private Image _portraitImage;
        [SerializeField] private TMP_Text _levelLabel;
        [SerializeField] private TMP_Text _nameLabel;

        private readonly LocalizedString _levelLocalized = new LocalizedString(LevelTable, LevelKey);
        private bool _gridCellLayoutApplied;

        public UnitSpellLoadout Loadout { get; private set; }

        public event Action<UnitPortraitView> Clicked;

        /// <summary>
        /// Normalizes authoring-time prefab transforms for runtime GridLayout cells.
        /// Some legacy prefab values use large offsets/scales that can place the level banner outside the visible cell.
        /// </summary>
        public void ApplyGridCellLayout(float overallScale = 1f)
        {
            overallScale = Mathf.Clamp(overallScale, 0.7f, 2f);

            RectTransform root = transform as RectTransform;
            if (!_gridCellLayoutApplied)
            {
                if (_portraitImage != null)
                {
                    RectTransform portraitRect = _portraitImage.rectTransform;
                    portraitRect.anchorMin = Vector2.zero;
                    portraitRect.anchorMax = Vector2.one;
                    portraitRect.pivot = new Vector2(0.5f, 0.5f);
                    portraitRect.anchoredPosition = Vector2.zero;
                    portraitRect.sizeDelta = new Vector2(-16f, -36f);
                }

                if (_levelLabel != null)
                {
                    RectTransform levelRoot = _levelLabel.rectTransform.parent as RectTransform;
                    if (levelRoot != null)
                    {
                        levelRoot.anchorMin = new Vector2(0.5f, 0f);
                        levelRoot.anchorMax = new Vector2(0.5f, 0f);
                        levelRoot.pivot = new Vector2(0.5f, 0.5f);
                        levelRoot.anchoredPosition = new Vector2(0f, LevelBadgeBaseCenterY);
                        levelRoot.sizeDelta = new Vector2(88f, 20f);
                    }

                    RectTransform levelTextRect = _levelLabel.rectTransform;
                    levelTextRect.anchorMin = Vector2.zero;
                    levelTextRect.anchorMax = Vector2.one;
                    levelTextRect.pivot = new Vector2(0.5f, 0.5f);
                    levelTextRect.anchoredPosition = Vector2.zero;
                    levelTextRect.sizeDelta = new Vector2(-6f, -4f);
                }

                _gridCellLayoutApplied = true;
            }

            if (root != null)
            {
                root.localScale = new Vector3(overallScale, overallScale, 1f);
            }

            if (_levelLabel != null)
            {
                RectTransform levelRoot = _levelLabel.rectTransform.parent as RectTransform;
                if (levelRoot != null)
                {
                    // Keep the badge visually aligned when the whole prefab is scaled from its center.
                    float compensatedY = LevelBadgeBaseCenterY / overallScale;
                    levelRoot.anchoredPosition = new Vector2(0f, compensatedY);
                }
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
