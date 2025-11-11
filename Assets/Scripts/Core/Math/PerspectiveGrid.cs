using UnityEngine;

namespace SevenBattles.Core.Math
{
    // Pure math helper that maps between a perspective quad and a logical grid.
    public struct PerspectiveGrid
    {
        public Homography2D QuadToUnit;   // maps local space (quad) -> unit rect [0,1]^2
        public Homography2D UnitToQuad;   // inverse mapping
        public int Columns;
        public int Rows;
        public bool IsValid;

        public static PerspectiveGrid FromQuad(Vector2 tl, Vector2 tr, Vector2 br, Vector2 bl, int cols, int rows)
        {
            var q2u = Homography2D.QuadToUnitRect(tl, tr, br, bl);
            var u2q = q2u.Inverse();
            bool ok = AllFinite(q2u) && AllFinite(u2q);
            return new PerspectiveGrid
            {
                QuadToUnit = q2u,
                UnitToQuad = u2q,
                Columns = Mathf.Max(1, cols),
                Rows = Mathf.Max(1, rows),
                IsValid = ok
            };
        }

        public bool TryLocalToTile(Vector2 local, out int x, out int y)
        {
            var uv = QuadToUnit.TransformPoint(local);
            if (uv.x < 0f || uv.y < 0f || uv.x > 1f || uv.y > 1f)
            {
                x = y = -1; return false;
            }
            x = Mathf.Clamp((int)(uv.x * Columns), 0, Columns - 1);
            y = Mathf.Clamp((int)(uv.y * Rows), 0, Rows - 1);
            return true;
        }

        public Vector2 TileCenterLocal(int x, int y)
        {
            float u = (x + 0.5f) / Columns;
            float v = (y + 0.5f) / Rows;
            return UnitToQuad.TransformPoint(new Vector2(u, v));
        }

        // Returns the 4 local-space corners of a tile quad (TL, TR, BR, BL)
        public void TileQuadLocal(int x, int y, out Vector2 tl, out Vector2 tr, out Vector2 br, out Vector2 bl)
        {
            float u0 = (float)x / Columns;
            float u1 = (float)(x + 1) / Columns;
            float v0 = (float)y / Rows;
            float v1 = (float)(y + 1) / Rows;
            // Note: v increases top->bottom depending on how corners were defined; we keep consistent with FromQuad
            tl = UnitToQuad.TransformPoint(new Vector2(u0, v1));
            tr = UnitToQuad.TransformPoint(new Vector2(u1, v1));
            br = UnitToQuad.TransformPoint(new Vector2(u1, v0));
            bl = UnitToQuad.TransformPoint(new Vector2(u0, v0));
        }
        private static bool AllFinite(Homography2D h)
        {
            bool F(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
            return F(h.m00) && F(h.m01) && F(h.m02)
                && F(h.m10) && F(h.m11) && F(h.m12)
                && F(h.m20) && F(h.m21) && F(h.m22);
        }
    }
}
