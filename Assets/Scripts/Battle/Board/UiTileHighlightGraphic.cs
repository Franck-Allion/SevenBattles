using UnityEngine;
using UnityEngine.UI;

namespace SevenBattles.Battle.Board
{
    // Renders a 4-corner quad in the local space of its RectTransform.
    // Put this as a child of the board RectTransform with identical anchors/pivot/size
    // so local coordinates match the board's local space.
    [RequireComponent(typeof(CanvasRenderer))]
    public class UiTileHighlightGraphic : Graphic
    {
        [SerializeField] private Vector2 _tl;
        [SerializeField] private Vector2 _tr;
        [SerializeField] private Vector2 _br;
        [SerializeField] private Vector2 _bl;

        public void SetQuad(Vector2 tl, Vector2 tr, Vector2 br, Vector2 bl)
        {
            _tl = tl; _tr = tr; _br = br; _bl = bl;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var col = color;

            if (!Finite(_tl) || !Finite(_tr) || !Finite(_br) || !Finite(_bl))
            {
                // Avoid invalid geometry when inputs are not yet initialized
                return;
            }

            UIVertex v = UIVertex.simpleVert; v.color = col;

            v.position = _tl; vh.AddVert(v);
            v.position = _tr; vh.AddVert(v);
            v.position = _br; vh.AddVert(v);
            v.position = _bl; vh.AddVert(v);

            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(2, 3, 0);
        }

        private static bool Finite(Vector2 p)
        {
            return !(float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsInfinity(p.x) || float.IsInfinity(p.y));
        }
    }
}
