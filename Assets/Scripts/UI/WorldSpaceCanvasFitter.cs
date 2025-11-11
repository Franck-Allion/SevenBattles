using UnityEngine;

namespace SevenBattles.UI
{
    // Ensures a World-Space Canvas fills the camera view.
    // Attach to the board Canvas (set to World Space). Heroes using SpriteRenderer will then render above/below via sorting layers.
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(Canvas))]
    public class WorldSpaceCanvasFitter : MonoBehaviour
    {
        [Header("Camera & Fitting")]
        [SerializeField] private Camera _camera;                // Uses Camera.main if null
        [SerializeField] private bool _alignToCamera = true;    // Face the camera and sit in front of it
        [SerializeField] private float _distance = 5f;          // Used when Align To Camera is true
        [SerializeField] private float _overscan = 1.0f;        // >1 slightly extends beyond edges
        [SerializeField] private int _pixelPadding = 2;          // Extra pixels around edges to kill seams
        [SerializeField] private bool _fitEveryFrame = true;    // Refit on resolution/FOV changes

        private RectTransform _rt;
        private Canvas _canvas;

        private void Awake()
        {
            _rt = GetComponent<RectTransform>();
            _canvas = GetComponent<Canvas>();
        }

        private void OnEnable()
        {
            Fit();
        }

        private void LateUpdate()
        {
            if (_fitEveryFrame) Fit();
        }

        public void Fit()
        {
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;
            if (_canvas != null && _canvas.renderMode != RenderMode.WorldSpace)
            {
                Debug.LogWarning("WorldSpaceCanvasFitter: Canvas must be set to World Space.", this);
                return;
            }

            if (_alignToCamera)
            {
                transform.rotation = Quaternion.LookRotation(_camera.transform.forward, Vector3.up);
                transform.position = _camera.transform.position + _camera.transform.forward * Mathf.Max(0.01f, _distance);
            }

            float width, height;
            if (_camera.orthographic)
            {
                height = 2f * _camera.orthographicSize;
                width = height * _camera.aspect;
            }
            else
            {
                // Distance along camera forward to the canvas plane
                float d;
                if (_alignToCamera)
                {
                    d = _distance;
                }
                else
                {
                    var toPlane = transform.position - _camera.transform.position;
                    d = Mathf.Abs(Vector3.Dot(toPlane, _camera.transform.forward));
                    d = Mathf.Max(0.01f, d);
                }
                height = 2f * d * Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
                width = height * _camera.aspect;
            }

            // Add overscan and pixel-based padding converted to world units at this distance
            var screenW = Mathf.Max(1, _camera.pixelWidth);
            var screenH = Mathf.Max(1, _camera.pixelHeight);
            float worldPerPixelY = height / screenH;
            float worldPerPixelX = width / screenW;
            float padX = worldPerPixelX * Mathf.Max(0, _pixelPadding);
            float padY = worldPerPixelY * Mathf.Max(0, _pixelPadding);

            width = width * _overscan + padX * 2f;
            height = height * _overscan + padY * 2f;
            _rt.sizeDelta = new Vector2(width, height);
            _rt.localScale = Vector3.one;
            _rt.pivot = new Vector2(0.5f, 0.5f);
            _rt.anchorMin = new Vector2(0.5f, 0.5f);
            _rt.anchorMax = new Vector2(0.5f, 0.5f);
            _rt.anchoredPosition3D = Vector3.zero;
        }
    }
}
