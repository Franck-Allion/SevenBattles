using SevenBattles.Core.Math;
using UnityEngine;

namespace SevenBattles.Battle.Board
{
    // World-space version of the perspective board.
    // Use when your characters are SpriteRenderers (not UI). The board is a world object
    // (SpriteRenderer on a quad, or MeshRenderer) that sits behind the characters via Sorting Layers.
    public class WorldPerspectiveBoard : MonoBehaviour
    {
        [Header("Grid")]
        [SerializeField] private int _columns = 7;
        [SerializeField] private int _rows = 7;

        [Header("Inner Quad (local, in this transform's plane)")]
        [SerializeField] private Vector2 _topLeft;
        [SerializeField] private Vector2 _topRight;
        [SerializeField] private Vector2 _bottomRight;
        [SerializeField] private Vector2 _bottomLeft;

        [Header("Highlight (optional)")]
        [SerializeField] private Material _highlightMaterial;
        [SerializeField] private string _highlightSortingLayer = "Default";
        [SerializeField] private int _highlightSortingOrder = 10;
        
        [Header("Behavior")]
        [SerializeField] private bool _autoHoverUpdate = true;
        [SerializeField] private bool _logTileClicks = true;

        private PerspectiveGrid _grid;
        private Camera _cam;
        private Mesh _highlightMesh;
        private MeshRenderer _highlightMr;

        private void Awake()
        {
            RebuildGrid();
        }

        private void OnEnable()
        {
            if (_cam == null) _cam = Camera.main;
            EnsureHighlightObjects();
        }

        public void RebuildGrid()
        {
            _grid = PerspectiveGrid.FromQuad(_topLeft, _topRight, _bottomRight, _bottomLeft, _columns, _rows);
        }

        public bool TryScreenToTile(Vector2 screen, out int x, out int y)
        {
            if (_cam == null) _cam = Camera.main;
            var ray = _cam.ScreenPointToRay(screen);
            var plane = new Plane(transform.forward, transform.position);
            if (!plane.Raycast(ray, out var dist)) { x = y = -1; return false; }
            var world = ray.origin + ray.direction * dist;
            var local = transform.InverseTransformPoint(world);
            if (!_grid.IsValid) { x = y = -1; return false; }
            return _grid.TryLocalToTile(new Vector2(local.x, local.y), out x, out y);
        }

        public Vector3 TileCenterWorld(int x, int y)
        {
            var local = _grid.TileCenterLocal(x, y);
            return transform.TransformPoint(new Vector3(local.x, local.y, 0f));
        }

        public void MoveHighlightToTile(int x, int y)
        {
            if (_highlightMr == null || !_grid.IsValid) return;
            _grid.TileQuadLocal(x, y, out var tl, out var tr, out var br, out var bl);
            UpdateHighlightMesh(tl, tr, br, bl);
        }

        public void PlaceHero(Transform hero, int x, int y, string sortingLayer = "Characters", int sortingOrder = 0)
        {
            if (hero == null) return;
            hero.position = TileCenterWorld(x, y);

            var sr = hero.GetComponentInChildren<SpriteRenderer>();
            if (sr != null)
            {
                sr.sortingLayerName = sortingLayer;
                sr.sortingOrder = sortingOrder;
            }
        }

        private void EnsureHighlightObjects()
        {
            if (_highlightMaterial == null || _highlightMr != null) return;

            var go = new GameObject("TileHighlight");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            var mf = go.AddComponent<MeshFilter>();
            _highlightMr = go.AddComponent<MeshRenderer>();
            _highlightMr.sharedMaterial = _highlightMaterial;
            _highlightMr.sortingLayerName = _highlightSortingLayer;
            _highlightMr.sortingOrder = _highlightSortingOrder;

            _highlightMesh = new Mesh { name = "TileHighlightMesh" };
            _highlightMesh.MarkDynamic();
            mf.sharedMesh = _highlightMesh;

            // Initialize as small quad
            UpdateHighlightMesh(Vector2.zero, Vector2.right * 0.1f, new Vector2(0.1f, -0.1f), Vector2.down * 0.1f);
        }

        private void Update()
        {
            if (!_autoHoverUpdate) return;
            if (_cam == null) _cam = Camera.main;
            if (_cam == null || !_grid.IsValid) return;
            if (_highlightMr == null) EnsureHighlightObjects();
            var screen = Input.mousePosition;
            if (TryScreenToTile(screen, out var tx, out var ty))
            {
                MoveHighlightToTile(tx, ty);
                if (_highlightMr != null && !_highlightMr.gameObject.activeSelf)
                    _highlightMr.gameObject.SetActive(true);
            }
            if (_logTileClicks && Input.GetMouseButtonDown(0))
            {
                if (TryScreenToTile(Input.mousePosition, out var cx, out var cy))
                {
                    Debug.Log($"WorldPerspectiveBoard click tile=({cx},{cy})", this);
                }
            }
        }

        private void UpdateHighlightMesh(Vector2 tl, Vector2 tr, Vector2 br, Vector2 bl)
        {
            if (_highlightMesh == null) return;
            var v = new Vector3[4]
            {
                new Vector3(tl.x, tl.y, 0f),
                new Vector3(tr.x, tr.y, 0f),
                new Vector3(br.x, br.y, 0f),
                new Vector3(bl.x, bl.y, 0f)
            };
            var uv = new Vector2[4] { Vector2.up, Vector2.one, Vector2.right, Vector2.zero };
            var t = new int[6] { 0, 1, 2, 2, 3, 0 };
            _highlightMesh.Clear();
            _highlightMesh.vertices = v;
            _highlightMesh.uv = uv;
            _highlightMesh.triangles = t;
            _highlightMesh.RecalculateBounds();
        }
    }
}
