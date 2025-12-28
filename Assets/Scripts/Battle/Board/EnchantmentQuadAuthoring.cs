using UnityEngine;
using SevenBattles.Core.Battle;

namespace SevenBattles.Battle.Board
{
    /// <summary>
    /// Editor-only helper for authoring enchantment quad coordinates in board local space.
    /// Attach to a scene object and use its custom editor to capture quad points.
    /// </summary>
    [ExecuteAlways]
    public sealed class EnchantmentQuadAuthoring : MonoBehaviour
    {
        [Header("Board Reference")]
        [SerializeField, Tooltip("Board transform used as the local space for quad coordinates.")]
        private WorldPerspectiveBoard _board;

        [Header("Gizmo Display")]
        [SerializeField] private bool _drawGizmos = true;
        [SerializeField] private Color _quadColor = new Color(0.2f, 1f, 0.6f, 0.8f);
        [SerializeField] private Color _centerColor = new Color(1f, 0.9f, 0.2f, 0.9f);
        [SerializeField, Min(0.001f)] private float _centerGizmoRadius = 0.06f;

        [Header("Quads (local board space)")]
        [SerializeField] private EnchantmentQuadDefinition[] _quads = System.Array.Empty<EnchantmentQuadDefinition>();

        public WorldPerspectiveBoard Board => _board;
        public EnchantmentQuadDefinition[] Quads => _quads ?? System.Array.Empty<EnchantmentQuadDefinition>();

        private void Reset()
        {
            ResolveBoard();
        }

        private void OnValidate()
        {
            ResolveBoard();
        }

        private void OnDrawGizmos()
        {
            if (!_drawGizmos)
            {
                return;
            }

            if (!ResolveBoard())
            {
                return;
            }

            if (_quads == null || _quads.Length == 0)
            {
                return;
            }

            var tr = _board.transform;
            for (int i = 0; i < _quads.Length; i++)
            {
                var quad = _quads[i];
                if (!IsQuadValid(quad))
                {
                    continue;
                }

                var tl = tr.TransformPoint(new Vector3(quad.TopLeft.x, quad.TopLeft.y, 0f));
                var trw = tr.TransformPoint(new Vector3(quad.TopRight.x, quad.TopRight.y, 0f));
                var br = tr.TransformPoint(new Vector3(quad.BottomRight.x, quad.BottomRight.y, 0f));
                var bl = tr.TransformPoint(new Vector3(quad.BottomLeft.x, quad.BottomLeft.y, 0f));
                var center = tr.TransformPoint(new Vector3(quad.Center.x + quad.Offset.x, quad.Center.y + quad.Offset.y, 0f));

                Gizmos.color = _quadColor;
                Gizmos.DrawLine(tl, trw);
                Gizmos.DrawLine(trw, br);
                Gizmos.DrawLine(br, bl);
                Gizmos.DrawLine(bl, tl);

                Gizmos.color = _centerColor;
                Gizmos.DrawSphere(center, _centerGizmoRadius);
            }
        }

        private bool ResolveBoard()
        {
            if (_board != null)
            {
                return true;
            }

            _board = GetComponent<WorldPerspectiveBoard>();
            if (_board != null)
            {
                return true;
            }

            _board = FindObjectOfType<WorldPerspectiveBoard>();
            return _board != null;
        }

        private static bool IsQuadValid(EnchantmentQuadDefinition quad)
        {
            return AllFinite(quad.TopLeft) &&
                   AllFinite(quad.TopRight) &&
                   AllFinite(quad.BottomRight) &&
                   AllFinite(quad.BottomLeft);
        }

        private static bool AllFinite(Vector2 v)
        {
            return !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsInfinity(v.x) && !float.IsInfinity(v.y);
        }
    }
}
