using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SevenBattles.Core.Battle;

namespace SevenBattles.UI
{
    public sealed class RewardItemView : MonoBehaviour
    {
        [Header("View")]
        [SerializeField] private Image _glow;
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _label;
        [SerializeField] private TMP_Text _amountText;

        [Header("Currency Overrides (optional)")]
        [SerializeField] private Sprite _goldGlowSprite;
        [SerializeField] private Sprite _goldIconSprite;
        [SerializeField] private Sprite _gemsGlowSprite;
        [SerializeField] private Sprite _gemsIconSprite;

        [Header("Glow Color Overrides")]
        [SerializeField] private Color _itemGlowColor = Color.white;
        [SerializeField] private Color _goldGlowColor = Color.white;
        [SerializeField] private Color _gemsGlowColor = Color.white;
        private BattleRewardType _currentRewardType = BattleRewardType.Item;

        private void Awake()
        {
            AutoResolveReferences();
        }

        public void SetReward(BattleRewardResultEntry entry)
        {
            AutoResolveReferences();
            _currentRewardType = entry != null ? entry.Type : BattleRewardType.Item;

            if (entry == null)
            {
                SetVisuals(null, null, _itemGlowColor, string.Empty, null);
                return;
            }

            string displayName = string.IsNullOrEmpty(entry.DisplayName) ? entry.Type.ToString() : entry.DisplayName;
            string amount = ShouldShowAmount(entry)
                ? entry.Amount.ToString(CultureInfo.InvariantCulture)
                : null;

            ResolveVisuals(entry, out Sprite glowSprite, out Sprite iconSprite, out Color glowColor);
            SetVisuals(glowSprite, iconSprite, glowColor, displayName, amount);
        }

        public void SetGold(int amount)
        {
            SetReward(new BattleRewardResultEntry(BattleRewardType.Gold, amount));
        }

        public BattleRewardType CurrentRewardType => _currentRewardType;

        public TMP_Text AmountText => _amountText;

        public RectTransform AmountRectTransform => _amountText != null ? _amountText.rectTransform : null;

        public void SetCurrencyAmountDisplay(int amount)
        {
            AutoResolveReferences();
            if (_amountText == null)
            {
                return;
            }

            int clamped = Mathf.Max(0, amount);
            _amountText.gameObject.SetActive(true);
            _amountText.text = clamped.ToString(CultureInfo.InvariantCulture);
        }

        private void AutoResolveReferences()
        {
            if (_glow == null)
            {
                _glow = FindImageByName("glow");
            }

            if (_icon == null)
            {
                _icon = FindImageByName("itemicon");
                if (_icon == null)
                {
                    _icon = FindImageByName("icon");
                }
            }

            if (_label == null)
            {
                _label = FindTmpByName("text", "label");
                if (_label == null)
                {
                    _label = GetComponentInChildren<TMP_Text>(true);
                }
            }

            if (_amountText == null)
            {
                _amountText = FindTmpByName("amount");
            }
        }

        private Image FindImageByName(string contains)
        {
            var images = GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                var image = images[i];
                if (image == null)
                {
                    continue;
                }

                if (image.gameObject == gameObject)
                {
                    continue;
                }

                if (image.gameObject.name.IndexOf(contains, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return image;
                }
            }

            return images.Length > 0 ? images[0] : null;
        }

        private TMP_Text FindTmpByName(params string[] containsAny)
        {
            var tmps = GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < tmps.Length; i++)
            {
                var tmp = tmps[i];
                if (tmp == null)
                {
                    continue;
                }

                for (int j = 0; j < containsAny.Length; j++)
                {
                    string token = containsAny[j];
                    if (string.IsNullOrEmpty(token))
                    {
                        continue;
                    }

                    if (tmp.gameObject.name.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return tmp;
                    }
                }
            }

            return null;
        }

        private static bool ShouldShowAmount(BattleRewardResultEntry entry)
        {
            if (entry == null)
            {
                return false;
            }

            if (entry.Type == BattleRewardType.Gold || entry.Type == BattleRewardType.Gems)
            {
                return true;
            }

            return entry.Type == BattleRewardType.Item && entry.Amount > 1;
        }

        private void ResolveVisuals(BattleRewardResultEntry entry, out Sprite glowSprite, out Sprite iconSprite, out Color glowColor)
        {
            glowSprite = _glow != null ? _glow.sprite : null;
            iconSprite = entry != null ? entry.Icon : null;
            glowColor = _itemGlowColor;

            if (entry == null)
            {
                return;
            }

            switch (entry.Type)
            {
                case BattleRewardType.Gold:
                    glowColor = _goldGlowColor;
                    if (_goldGlowSprite != null)
                    {
                        glowSprite = _goldGlowSprite;
                    }

                    if (_goldIconSprite != null)
                    {
                        iconSprite = _goldIconSprite;
                    }
                    break;
                case BattleRewardType.Gems:
                    glowColor = _gemsGlowColor;
                    if (_gemsGlowSprite != null)
                    {
                        glowSprite = _gemsGlowSprite;
                    }

                    if (_gemsIconSprite != null)
                    {
                        iconSprite = _gemsIconSprite;
                    }
                    break;
            }
        }

        private void SetVisuals(Sprite glow, Sprite icon, Color glowColor, string label, string amountText)
        {
            if (_glow != null)
            {
                _glow.sprite = glow;
                _glow.enabled = glow != null;
                _glow.color = glowColor;
            }

            if (_icon != null)
            {
                _icon.sprite = icon;
                _icon.enabled = icon != null;
            }

            if (_label != null)
            {
                _label.text = label ?? string.Empty;
            }

            if (_amountText != null)
            {
                bool hasAmount = !string.IsNullOrEmpty(amountText);
                _amountText.gameObject.SetActive(hasAmount);
                _amountText.text = hasAmount ? amountText : string.Empty;
            }
        }
    }
}
