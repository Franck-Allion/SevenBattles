using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenBattles.Preparation
{
    public sealed class PreparationInventoryItemEntryView : MonoBehaviour
    {
        [SerializeField, Tooltip("Root background image (Item).")]
        private Image _backgroundImage;
        [SerializeField, Tooltip("Icon image displayed for the inventory entry.")]
        private Image _itemIconImage;
        [SerializeField, Tooltip("Quantity label shown on the inventory tile.")]
        private TMP_Text _quantityText;

        public void Bind(Sprite icon, Color backgroundColor, int quantity, Sprite fallbackIcon, Color fallbackColor)
        {
            EnsureReferences();

            if (_backgroundImage != null)
            {
                _backgroundImage.color = backgroundColor.a <= 0f ? fallbackColor : backgroundColor;
            }

            if (_itemIconImage != null)
            {
                Sprite resolvedIcon = icon != null ? icon : fallbackIcon;
                _itemIconImage.sprite = resolvedIcon;
                _itemIconImage.enabled = resolvedIcon != null;
            }

            if (_quantityText != null)
            {
                _quantityText.text = Mathf.Max(1, quantity).ToString();
            }
        }

        private void Awake()
        {
            EnsureReferences();
        }

        private void EnsureReferences()
        {
            if (_backgroundImage == null)
            {
                _backgroundImage = GetComponent<Image>();
            }

            if (_itemIconImage == null)
            {
                _itemIconImage = FindImageByName("ItemIcon");
            }

            if (_quantityText == null)
            {
                _quantityText = GetComponentInChildren<TMP_Text>(true);
            }
        }

        private Image FindImageByName(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            Image[] images = GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                Image image = images[i];
                if (image == null)
                {
                    continue;
                }

                if (string.Equals(image.gameObject.name, objectName, System.StringComparison.Ordinal))
                {
                    return image;
                }
            }

            return null;
        }
    }
}
