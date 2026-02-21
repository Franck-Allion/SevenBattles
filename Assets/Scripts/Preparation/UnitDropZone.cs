using System;
using SevenBattles.Core.Battle;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SevenBattles.Preparation
{
    public sealed class UnitDropZone : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public enum ZoneType
        {
            AllUnits,
            ActiveSquad
        }

        [SerializeField] private ZoneType _zoneType;
        [Header("Drop Zone Highlight")]
        [SerializeField] private bool _enableHighlightEffect = true;
        [SerializeField] private RectTransform _highlightRoot;
        [SerializeField] private Image _highlightImage;
        [SerializeField] private CanvasGroup _highlightCanvasGroup;
        [SerializeField] private Color _availableColor = new Color(0.29f, 0.84f, 1f, 1f);
        [SerializeField] private Color _preferredColor = new Color(0.55f, 0.95f, 1f, 1f);
        [SerializeField] private Color _hoverColor = new Color(1f, 0.88f, 0.56f, 1f);
        [SerializeField, Range(0f, 1f)] private float _availableAlpha = 0.10f;
        [SerializeField, Range(0f, 1f)] private float _preferredAlpha = 0.24f;
        [SerializeField, Range(0f, 1f)] private float _hoverAlpha = 0.42f;
        [SerializeField, Range(0f, 0.35f)] private float _pulseAmplitude = 0.06f;
        [SerializeField, Min(0.1f)] private float _pulseSpeed = 2.6f;
        [SerializeField, Min(0.1f)] private float _fadeSpeed = 8f;
        [SerializeField, Range(1f, 1.2f)] private float _hoverScale = 1.035f;
        [Header("Completion State")]
        [SerializeField] private Color _completionColor = new Color(0.31f, 0.86f, 0.56f, 1f);
        [SerializeField, Range(0f, 1f)] private float _completionAlpha = 0.28f;
        [SerializeField, Range(1f, 1.2f)] private float _completionScale = 1.02f;

        public ZoneType Type => _zoneType;
        public bool IsCompletionVisualActive => _isCompletionVisualActive;

        public event Action<UnitSpellLoadout, ZoneType> DropReceived;

        private bool _isPointerInside;
        private bool _isCompletionVisualActive;
        private float _pulseTime;
        private Vector3 _baseHighlightScale = Vector3.one;
        private bool _highlightReady;

        private void Awake()
        {
            EnsureHighlightOverlay();
            ApplyHighlightImmediate(0f, _availableColor, 1f);
        }

        private void Update()
        {
            if (!_highlightReady)
            {
                return;
            }

            if (!_enableHighlightEffect)
            {
                return;
            }

            if (!UnitDragHandler.IsDragging)
            {
                _pulseTime = 0f;
                if (_isCompletionVisualActive)
                {
                    AnimateHighlight(_completionAlpha, _completionColor, _completionScale);
                }
                else
                {
                    AnimateHighlight(0f, _availableColor, 1f);
                }
                return;
            }

            bool isPreferred = IsPreferredDropTarget();
            float baseAlpha = isPreferred ? _preferredAlpha : _availableAlpha;
            Color targetColor = isPreferred ? _preferredColor : _availableColor;
            float targetScale = 1f;

            if (_isPointerInside)
            {
                targetColor = _hoverColor;
                baseAlpha = Mathf.Max(baseAlpha, _hoverAlpha);
                targetScale = _hoverScale;
            }

            _pulseTime += Time.unscaledDeltaTime;
            float pulse = Mathf.Sin(_pulseTime * _pulseSpeed * Mathf.PI * 2f) * _pulseAmplitude;
            float targetAlpha = Mathf.Clamp01(baseAlpha + pulse);

            AnimateHighlight(targetAlpha, targetColor, targetScale);
        }

        public void SetZoneType(ZoneType zoneType)
        {
            _zoneType = zoneType;
        }

        public void SetCompletionVisual(bool isCompletionVisualActive)
        {
            _isCompletionVisualActive = isCompletionVisualActive;
            if (!_highlightReady || UnitDragHandler.IsDragging)
            {
                return;
            }

            if (_isCompletionVisualActive)
            {
                ApplyHighlightImmediate(_completionAlpha, _completionColor, _completionScale);
            }
            else
            {
                ApplyHighlightImmediate(0f, _availableColor, 1f);
            }
        }

        public void OnDrop(PointerEventData eventData)
        {
            UnitSpellLoadout loadout = UnitDragHandler.DraggingLoadout;
            if (loadout == null)
            {
                return;
            }

            DropReceived?.Invoke(loadout, _zoneType);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isPointerInside = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isPointerInside = false;
        }

        private bool IsPreferredDropTarget()
        {
            if (!UnitDragHandler.TryGetDragOriginZone(out ZoneType originZone))
            {
                return true;
            }

            if (originZone == ZoneType.AllUnits)
            {
                return _zoneType == ZoneType.ActiveSquad;
            }

            if (originZone == ZoneType.ActiveSquad)
            {
                return _zoneType == ZoneType.AllUnits;
            }

            return true;
        }

        private void EnsureHighlightOverlay()
        {
            if (!_enableHighlightEffect)
            {
                return;
            }

            RectTransform selfRect = transform as RectTransform;
            if (selfRect == null)
            {
                return;
            }

            if (_highlightRoot == null)
            {
                var go = new GameObject("DropZoneHighlight", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
                go.transform.SetParent(selfRect, false);

                _highlightRoot = go.GetComponent<RectTransform>();
                _highlightRoot.anchorMin = Vector2.zero;
                _highlightRoot.anchorMax = Vector2.one;
                _highlightRoot.anchoredPosition = Vector2.zero;
                _highlightRoot.sizeDelta = Vector2.zero;
                _highlightRoot.localScale = Vector3.one;
                _highlightRoot.SetAsFirstSibling();
            }

            if (_highlightImage == null)
            {
                _highlightImage = _highlightRoot.GetComponent<Image>();
                if (_highlightImage == null)
                {
                    _highlightImage = _highlightRoot.gameObject.AddComponent<Image>();
                }
            }

            if (_highlightCanvasGroup == null)
            {
                _highlightCanvasGroup = _highlightRoot.GetComponent<CanvasGroup>();
                if (_highlightCanvasGroup == null)
                {
                    _highlightCanvasGroup = _highlightRoot.gameObject.AddComponent<CanvasGroup>();
                }
            }

            Image zoneImage = GetComponent<Image>();
            if (zoneImage != null)
            {
                _highlightImage.sprite = zoneImage.sprite;
                _highlightImage.type = zoneImage.type;
                _highlightImage.preserveAspect = zoneImage.preserveAspect;
            }
            else
            {
                _highlightImage.type = Image.Type.Sliced;
            }

            _highlightImage.raycastTarget = false;
            _baseHighlightScale = _highlightRoot.localScale;
            _highlightReady = true;
        }

        private void ApplyHighlightImmediate(float alpha, Color color, float scaleMultiplier)
        {
            if (!_highlightReady)
            {
                return;
            }

            _highlightCanvasGroup.alpha = Mathf.Clamp01(alpha);
            _highlightImage.color = color;
            _highlightRoot.localScale = _baseHighlightScale * Mathf.Max(1f, scaleMultiplier);
        }

        private void AnimateHighlight(float targetAlpha, Color targetColor, float targetScaleMultiplier)
        {
            if (!_highlightReady)
            {
                return;
            }

            float dt = Time.unscaledDeltaTime;
            float lerpT = 1f - Mathf.Exp(-Mathf.Max(0.1f, _fadeSpeed) * dt);
            _highlightCanvasGroup.alpha = Mathf.Lerp(_highlightCanvasGroup.alpha, Mathf.Clamp01(targetAlpha), lerpT);
            _highlightImage.color = Color.Lerp(_highlightImage.color, targetColor, lerpT);
            Vector3 targetScale = _baseHighlightScale * Mathf.Max(1f, targetScaleMultiplier);
            _highlightRoot.localScale = Vector3.Lerp(_highlightRoot.localScale, targetScale, lerpT);
        }
    }
}
