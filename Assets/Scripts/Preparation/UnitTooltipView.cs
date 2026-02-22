using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenBattles.Preparation
{
    [DisallowMultipleComponent]
    public sealed class UnitTooltipView : MonoBehaviour
    {
        [SerializeField] private RectTransform _rootRect;
        [SerializeField] private CanvasGroup _rootCanvasGroup;
        [SerializeField] private Image _backgroundImage;
        [SerializeField] private TMP_Text _label;
        [SerializeField] private Vector2 _padding = new Vector2(18f, 10f);
        [SerializeField, Min(1f)] private float _minWidth = 80f;
        [SerializeField, Min(1f)] private float _minHeight = 36f;
        [SerializeField, Min(1f)] private float _maxWidth = 420f;

        private string _currentText = string.Empty;

        public RectTransform RootRect => _rootRect;
        public bool IsVisible => _rootCanvasGroup != null && _rootCanvasGroup.alpha > 0.001f;

        private void Awake()
        {
            EnsureReferences();
            HideImmediate();
        }

        public void SetRuntimeReferences(
            RectTransform rootRect,
            CanvasGroup rootCanvasGroup,
            Image backgroundImage,
            TMP_Text label)
        {
            _rootRect = rootRect;
            _rootCanvasGroup = rootCanvasGroup;
            _backgroundImage = backgroundImage;
            _label = label;
            EnsureReferences();
            ApplyDefaultStyling();
            HideImmediate();
        }

        public void EnsureReferences()
        {
            if (_rootRect == null)
            {
                _rootRect = transform as RectTransform;
            }

            if (_rootCanvasGroup == null)
            {
                _rootCanvasGroup = GetComponent<CanvasGroup>();
            }

            if (_backgroundImage == null)
            {
                _backgroundImage = GetComponent<Image>();
            }

            if (_label == null)
            {
                _label = GetComponentInChildren<TMP_Text>(true);
            }
        }

        public void ApplyDefaultStyling()
        {
            EnsureReferences();
            if (_backgroundImage != null)
            {
                _backgroundImage.raycastTarget = false;
                _backgroundImage.color = new Color32(24, 28, 35, 235);
            }

            if (_label != null)
            {
                _label.raycastTarget = false;
                _label.enableWordWrapping = false;
                _label.overflowMode = TextOverflowModes.Overflow;
                _label.alignment = TextAlignmentOptions.Center;
                _label.color = Color.white;
            }
        }

        public void SetText(string text)
        {
            EnsureReferences();
            text = text ?? string.Empty;
            if (string.Equals(_currentText, text, System.StringComparison.Ordinal))
            {
                return;
            }

            _currentText = text;
            if (_label != null)
            {
                _label.SetText(_currentText);
            }

            ResizeToContent();
        }

        public void Show()
        {
            EnsureReferences();
            if (_rootCanvasGroup == null)
            {
                return;
            }

            _rootCanvasGroup.alpha = 1f;
            _rootCanvasGroup.interactable = false;
            _rootCanvasGroup.blocksRaycasts = false;
        }

        public void HideImmediate()
        {
            EnsureReferences();
            if (_rootCanvasGroup == null)
            {
                return;
            }

            _rootCanvasGroup.alpha = 0f;
            _rootCanvasGroup.interactable = false;
            _rootCanvasGroup.blocksRaycasts = false;
        }

        private void ResizeToContent()
        {
            if (_rootRect == null || _label == null)
            {
                return;
            }

            RectTransform labelRect = _label.rectTransform;
            float halfPadX = _padding.x * 0.5f;
            float halfPadY = _padding.y * 0.5f;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(halfPadX, halfPadY);
            labelRect.offsetMax = new Vector2(-halfPadX, -halfPadY);

            Vector2 preferred = _label.GetPreferredValues(_currentText, _maxWidth, 0f);
            float width = Mathf.Clamp(preferred.x + _padding.x, _minWidth, _maxWidth);
            float height = Mathf.Max(_minHeight, preferred.y + _padding.y);

            _rootRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            _rootRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        }
    }
}
