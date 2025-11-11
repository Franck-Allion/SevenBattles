using UnityEngine;

namespace SevenBattles.Battle.Board
{
    // Scales and positions a SpriteRenderer so it fills the camera view.
    // Works with both perspective and orthographic cameras.
    [RequireComponent(typeof(SpriteRenderer))]
    public class FullscreenSpriteFitter : MonoBehaviour
    {
        public enum FitMode { Cover, Contain }

        [Header("Camera & Alignment")]
        [SerializeField] private Camera _camera;              // Defaults to Camera.main
        [SerializeField] private bool _alignToCamera = true;  // Position in front of camera and face it
        [SerializeField] private float _distance = 10f;       // Used when aligning in perspective

        [Header("Sizing")]
        [SerializeField] private FitMode _mode = FitMode.Cover;
        [SerializeField] private float _overscan = 1.0f;      // Multiplies final width/height
        [SerializeField] private int _pixelPadding = 2;       // Extra pixels around to avoid gaps
        [SerializeField] private bool _fitEveryFrame = true;  // Update on resolution/FOV change

        private SpriteRenderer _sr;
        private Vector3 _baseScale;

        private void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            _baseScale = transform.localScale;
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
            if (_sr == null || _sr.sprite == null || _camera == null) return;

            // Align plane to camera if requested
            if (_alignToCamera)
            {
                transform.rotation = Quaternion.LookRotation(_camera.transform.forward, Vector3.up);
                if (_camera.orthographic)
                {
                    // For ortho, distance does not matter visually, but keep it consistent
                    transform.position = _camera.transform.position + _camera.transform.forward * Mathf.Max(0.01f, _distance);
                }
                else
                {
                    transform.position = _camera.transform.position + _camera.transform.forward * Mathf.Max(0.01f, _distance);
                }
            }

            // Target frustum size at the plane distance
            float targetWidth, targetHeight;
            if (_camera.orthographic)
            {
                targetHeight = 2f * _camera.orthographicSize;
                targetWidth = targetHeight * _camera.aspect;
            }
            else
            {
                float d;
                if (_alignToCamera)
                {
                    d = Mathf.Max(0.01f, _distance);
                }
                else
                {
                    var toPlane = transform.position - _camera.transform.position;
                    d = Mathf.Abs(Vector3.Dot(toPlane, _camera.transform.forward));
                    d = Mathf.Max(0.01f, d);
                }
                targetHeight = 2f * d * Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
                targetWidth = targetHeight * _camera.aspect;
            }

            // Convert pixel padding to world units at this frustum size
            int pxW = Mathf.Max(1, _camera.pixelWidth);
            int pxH = Mathf.Max(1, _camera.pixelHeight);
            float padX = (targetWidth / pxW) * Mathf.Max(0, _pixelPadding);
            float padY = (targetHeight / pxH) * Mathf.Max(0, _pixelPadding);

            targetWidth = targetWidth * _overscan + 2f * padX;
            targetHeight = targetHeight * _overscan + 2f * padY;

            // Current sprite size in world units at base scale (ignoring z)
            var spriteSize = _sr.sprite.bounds.size; // in local units
            Vector2 baseWorld = new Vector2(Mathf.Abs(_baseScale.x) * spriteSize.x, Mathf.Abs(_baseScale.y) * spriteSize.y);
            if (baseWorld.x <= 0f || baseWorld.y <= 0f) return;

            float sx = targetWidth / baseWorld.x;
            float sy = targetHeight / baseWorld.y;
            float s = _mode == FitMode.Cover ? Mathf.Max(sx, sy) : Mathf.Min(sx, sy);

            var sign = new Vector3(Mathf.Sign(_baseScale.x), Mathf.Sign(_baseScale.y), Mathf.Sign(_baseScale.z));
            var newScale = Vector3.Scale(sign * s, Vector3.one);
            newScale.z = _baseScale.z; // keep original depth scaling
            transform.localScale = new Vector3(_baseScale.x * s, _baseScale.y * s, _baseScale.z);
        }
    }
}

