using SevenBattles.Core.Math;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SevenBattles.Battle.Board
{
    // Maps pointer to tiles on a perspective board drawn as a single Image inside a Canvas.
    // Place heroes and a hover highlight at tile centers while preserving the board perspective.
    [ExecuteAlways]
    public class UiPerspectiveBoard : MonoBehaviour, IPointerClickHandler
    {
        [Header("References")]
        [SerializeField] private RectTransform _boardRect;      // The Image's RectTransform
        [SerializeField, Tooltip("Optional simple center marker (RectTransform)." )]
        private RectTransform _highlight;
        [SerializeField, Tooltip("Optional full-quad highlight (UiTileHighlightGraphic). If left empty, it will be auto-found on this object or its children.")]
        private UiTileHighlightGraphic _tileHighlight; // Optional full-quad highlight
        [SerializeField] private RectTransform _heroParent;      // Optional parent for hero UI objects

        [Header("Grid")]
        [SerializeField] private int _columns = 7;
        [SerializeField] private int _rows = 7;
        [SerializeField] private PerspectiveGridMappingMode _gridMappingMode = PerspectiveGridMappingMode.Homography;

        [Header("Inner Quad (local coords of play area)")]
        [SerializeField] private Vector2 _topLeft;
        [SerializeField] private Vector2 _topRight;
        [SerializeField] private Vector2 _bottomRight;
        [SerializeField] private Vector2 _bottomLeft;

        private PerspectiveGrid _grid;
        private Canvas _rootCanvas;
        private Camera _uiCamera; // null for Overlay

        [Header("Behavior")]
        [SerializeField] private bool _autoHoverUpdate = true;
        [Header("Debug")]
        [SerializeField] private bool _logTileClicks = true;

        private void OnEnable()
        {
            EnsureRefs();
            EnsureHighlightActive();
            EnsureHeroLayer();
            _rootCanvas = _boardRect != null ? _boardRect.GetComponentInParent<Canvas>() : null;
            _uiCamera = _rootCanvas != null && _rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? _rootCanvas.worldCamera
                : null;
            RebuildGrid();
        }

        private void Reset()
        {
            EnsureRefs();
        }

        private void OnValidate()
        {
            if (_columns < 1) _columns = 1;
            if (_rows < 1) _rows = 1;
            EnsureRefs();
            EnsureHeroLayer();
        }

        public void RebuildGrid()
        {
            _grid = PerspectiveGrid.FromQuad(_topLeft, _topRight, _bottomRight, _bottomLeft, _columns, _rows, _gridMappingMode);
        }

        

        public bool TryScreenToTile(Vector2 screen, out int x, out int y)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_boardRect, screen, _uiCamera, out var local))
            { x = y = -1; return false; }
            return _grid.TryLocalToTile(local, out x, out y);
        }

        public Vector2 GetTileCenterScreen(int x, int y)
        {
            var local = _grid.TileCenterLocal(x, y);
            var world = _boardRect.TransformPoint(local);
            return RectTransformUtility.WorldToScreenPoint(_uiCamera, world);
        }

        public void MoveHighlightToTile(int x, int y)
        {
            if (!_grid.IsValid) return;
            var local = _grid.TileCenterLocal(x, y);
            if (_highlight != null)
            {
                if (!_highlight.gameObject.activeSelf) _highlight.gameObject.SetActive(true);
                _highlight.anchoredPosition = local;
            }
            if (_tileHighlight != null)
            {
                if (!_tileHighlight.gameObject.activeSelf) _tileHighlight.gameObject.SetActive(true);
                _grid.TileQuadLocal(x, y, out var tl, out var tr, out var br, out var bl);
                _tileHighlight.SetQuad(tl, tr, br, bl);
            }
        }

        public void PlaceHero(RectTransform hero, int x, int y)
        {
            if (hero == null) return;
            if (!_grid.IsValid)
            {
                Debug.LogWarning("UiPerspectiveBoard: Grid is invalid (inner quad not set). Hero placement skipped.");
                return;
            }
            // Ensure hero is under a UI parent that renders above the board
            if (_heroParent != null)
            {
                if (hero.parent != _heroParent) hero.SetParent(_heroParent, worldPositionStays: false);
            }
            else if (hero.parent != _boardRect)
            {
                hero.SetParent(_boardRect, worldPositionStays: false);
            }
            hero.SetAsLastSibling();
            hero.anchoredPosition = _grid.TileCenterLocal(x, y);
        }

        // Optional convenience: call every frame to update hover from mouse.
        public void UpdateHoverFromMouse()
        {
            var screen = Input.mousePosition;
            if (TryScreenToTile(screen, out int x, out int y)) MoveHighlightToTile(x, y);
        }

        private void Update()
        {
            if (_autoHoverUpdate) UpdateHoverFromMouse();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_logTileClicks) return;
            if (TryScreenToTile(eventData.position, out var tx, out var ty))
            {
                Debug.Log($"UiPerspectiveBoard click tile=({tx},{ty})", this);
            }
        }

        private void EnsureRefs()
        {
            if (_boardRect == null) _boardRect = GetComponent<RectTransform>();
            if (_tileHighlight == null) _tileHighlight = GetComponentInChildren<UiTileHighlightGraphic>(true);
        }

        private void EnsureHighlightActive()
        {
            if (_highlight != null && !_highlight.gameObject.activeSelf)
                _highlight.gameObject.SetActive(true);
            if (_tileHighlight != null && !_tileHighlight.gameObject.activeSelf)
                _tileHighlight.gameObject.SetActive(true);
        }

        private void EnsureHeroLayer()
        {
            if (_boardRect == null || _heroParent != null) return;

            // Create a dedicated UI layer above the board to render heroes in UI mode only.
            // If the board's Canvas is World Space or absent, do NOT create an extra Canvas (prevents invalid AABB issues).
            var parentCanvas = _boardRect.GetComponentInParent<Canvas>();

            GameObject go;
            RectTransform rt;

            if (parentCanvas != null && parentCanvas.renderMode != RenderMode.WorldSpace)
            {
                go = new GameObject("HeroLayer", typeof(RectTransform), typeof(Canvas));
                rt = go.GetComponent<RectTransform>();
                var layerCanvas = go.GetComponent<Canvas>();
                layerCanvas.overrideSorting = true;
                layerCanvas.sortingLayerID = parentCanvas.sortingLayerID;
                layerCanvas.sortingOrder = parentCanvas.sortingOrder + 1;
            }
            else
            {
                // World-space or no-canvas parent: just a RectTransform as a sibling container
                go = new GameObject("HeroLayer", typeof(RectTransform));
                rt = go.GetComponent<RectTransform>();
            }

            rt.SetParent(_boardRect, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            _heroParent = rt;
        }

    }
}
