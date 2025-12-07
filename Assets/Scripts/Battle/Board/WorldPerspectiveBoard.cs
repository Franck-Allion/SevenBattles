using SevenBattles.Core.Math;
using UnityEngine;
using UnityEngine.Rendering;

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
        [SerializeField, Tooltip("Sorting layer for tile markers. Should be 'Default' to render behind units but above board.")]
        private string _highlightSortingLayer = "Default";
        [SerializeField, Tooltip("Sorting order for tile markers. Negative values ensure markers render behind units (which use 100+).")]
        private int _highlightSortingOrder = 1;

        [Header("Behavior")]
        [SerializeField] private bool _autoHoverUpdate = true;
        [SerializeField] private bool _logTileClicks = true;

        private PerspectiveGrid _grid;
        private Camera _cam;
        private Mesh _highlightMesh;
        private MeshRenderer _highlightMr;
        private Color _highlightColor = Color.white;
        private Mesh _secondaryHighlightMesh;
        private MeshRenderer _secondaryHighlightMr;

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

        public void SetHighlightVisible(bool visible)
        {
            if (_highlightMr == null) EnsureHighlightObjects();
            if (_highlightMr != null && _highlightMr.gameObject.activeSelf != visible)
            {
                _highlightMr.gameObject.SetActive(visible);
            }
        }

        public void SetHighlightColor(Color color)
        {
            _highlightColor = color;
            if (_highlightMr != null)
            {
                var mat = _highlightMr.sharedMaterial;
                if (mat != null && mat.HasProperty("_Color"))
                {
                    mat.color = _highlightColor;
                }
            }
        }

        // Allows callers to override the board highlight material at runtime (e.g., different fill vs outline
        // when switching from placement to active-unit markers). If the internal highlight renderer has not
        // been created yet, it will be initialized using the default serialized material and then replaced.
        public void SetHighlightMaterial(Material material)
        {
            if (material == null) return;
            if (_highlightMr == null) EnsureHighlightObjects();
            if (_highlightMr != null)
            {
                _highlightMr.sharedMaterial = material;
            }
            if (_secondaryHighlightMr != null)
            {
                _secondaryHighlightMr.sharedMaterial = material;
            }
        }

        // Enables or disables automatic hover-driven highlight updates.
        // When disabled, the board will not move the highlight based on the mouse.
        public void SetHoverEnabled(bool enabled)
        {
            _autoHoverUpdate = enabled;
        }

        // Sets the sorting order for the highlight (and secondary highlight).
        // Use this to ensure the highlight renders behind units by setting it lower than unit sorting orders.
        public void SetHighlightSortingOrder(int sortingOrder)
        {
            _highlightSortingOrder = sortingOrder;
            if (_highlightMr != null)
            {
                _highlightMr.sortingOrder = sortingOrder;
            }
            if (_secondaryHighlightMr != null)
            {
                _secondaryHighlightMr.sortingOrder = sortingOrder + 1;
            }
        }

        public void PlaceHero(Transform hero, int x, int y, string sortingLayer = "Characters", int sortingOrder = 100)
        {
            if (hero == null) return;
            hero.position = TileCenterWorld(x, y);

            // Validate sorting order - should never be 0 or negative (would render behind board)
            if (sortingOrder <= 0)
            {
                Debug.LogWarning($"[WorldPerspectiveBoard] PlaceHero called with invalid sortingOrder={sortingOrder}. " +
                                 $"This will cause rendering issues. Setting to default 100.", this);
                sortingOrder = 100;
            }

            // Prefer SortingGroup if present
            var group = hero.GetComponentInChildren<SortingGroup>(true);
            if (group != null)
            {
                group.sortingLayerName = sortingLayer;
                group.sortingOrder = sortingOrder;
            }
            else
            {
                var renderers = hero.GetComponentsInChildren<SpriteRenderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    renderers[i].sortingLayerName = sortingLayer;
                    renderers[i].sortingOrder = sortingOrder;
                }
            }
        }

        // Computes a stable sorting order so that tiles closer to the bottom/front of the board
        // render above tiles further back. Increase rowStride to leave gaps for intra-row ordering.
        public int ComputeSortingOrder(int x, int y, int @base = 0, int rowStride = 10, int intraRowOffset = 0)
        {
            // Higher order should be more in front. Assume y=0 is front row.
            // So invert y relative to total rows to push back rows behind.
            int rows = Rows;
            if (rows <= 0) rows = 1;
            int backToFront = (rows - 1 - y) * (-rowStride); // negative so y=0 gets highest when added to base
            // Alternatively: compute frontToBack then invert sign to maintain increasing order on front rows.
            // For clarity, compute directly:
            int frontBias = (rows - 1 - y) * 0; // unused but left for readability
            // We want: y=0 -> largest addend; y=rows-1 -> smallest addend.
            int addend = (rows - 1 - y) * rowStride;
            return @base + addend + intraRowOffset;
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

        private void EnsureSecondaryHighlightObjects()
        {
            if (_highlightMaterial == null || _secondaryHighlightMr != null) return;

            var go = new GameObject("TileMoveHighlight");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            var mf = go.AddComponent<MeshFilter>();
            _secondaryHighlightMr = go.AddComponent<MeshRenderer>();
            _secondaryHighlightMr.sharedMaterial = _highlightMaterial;
            _secondaryHighlightMr.sortingLayerName = _highlightSortingLayer;
            _secondaryHighlightMr.sortingOrder = _highlightSortingOrder + 1;

            _secondaryHighlightMesh = new Mesh { name = "TileMoveHighlightMesh" };
            _secondaryHighlightMesh.MarkDynamic();
            mf.sharedMesh = _secondaryHighlightMesh;

            UpdateSecondaryHighlightMesh(Vector2.zero, Vector2.right * 0.1f, new Vector2(0.1f, -0.1f), Vector2.down * 0.1f);
            _secondaryHighlightMr.gameObject.SetActive(false);
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

        private void UpdateSecondaryHighlightMesh(Vector2 tl, Vector2 tr, Vector2 br, Vector2 bl)
        {
            if (_secondaryHighlightMesh == null) return;
            var v = new Vector3[4]
            {
                new Vector3(tl.x, tl.y, 0f),
                new Vector3(tr.x, tr.y, 0f),
                new Vector3(br.x, br.y, 0f),
                new Vector3(bl.x, bl.y, 0f)
            };
            var uv = new Vector2[4] { Vector2.up, Vector2.one, Vector2.right, Vector2.zero };
            var t = new int[6] { 0, 1, 2, 2, 3, 0 };
            _secondaryHighlightMesh.Clear();
            _secondaryHighlightMesh.vertices = v;
            _secondaryHighlightMesh.uv = uv;
            _secondaryHighlightMesh.triangles = t;
            _secondaryHighlightMesh.RecalculateBounds();
        }

        public void MoveSecondaryHighlightToTile(int x, int y)
        {
            if (_secondaryHighlightMr == null) EnsureSecondaryHighlightObjects();
            if (_secondaryHighlightMr == null || !_grid.IsValid) return;
            _grid.TileQuadLocal(x, y, out var tl, out var tr, out var br, out var bl);
            UpdateSecondaryHighlightMesh(tl, tr, br, bl);
        }

        public void SetSecondaryHighlightVisible(bool visible)
        {
            if (_secondaryHighlightMr == null) EnsureSecondaryHighlightObjects();
            if (_secondaryHighlightMr != null && _secondaryHighlightMr.gameObject.activeSelf != visible)
            {
                _secondaryHighlightMr.gameObject.SetActive(visible);
            }
        }

        public void SetSecondaryHighlightColor(Color color)
        {
            if (_secondaryHighlightMr == null) EnsureSecondaryHighlightObjects();
            if (_secondaryHighlightMr != null)
            {
                var mat = _secondaryHighlightMr.sharedMaterial;
                if (mat != null && mat.HasProperty("_Color"))
                {
                    mat.color = color;
                }
            }
        }

        // Expose grid size for placement logic consumers.
        public int Columns => _grid.IsValid ? _grid.Columns : 0;
        public int Rows => _grid.IsValid ? _grid.Rows : 0;
    }
}
