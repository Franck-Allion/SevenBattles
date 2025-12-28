using UnityEngine;

namespace SevenBattles.Core.Math
{
    // Pure math helper that maps between a perspective quad and a logical grid.
    public struct PerspectiveGrid
    {
        public PerspectiveGridMappingMode MappingMode;
        public Homography2D QuadToUnit;   // maps local space (quad) -> unit rect [0,1]^2
        public Homography2D UnitToQuad;   // inverse mapping
        public int Columns;
        public int Rows;
        public bool IsValid;

        private Vector2 _tl;
        private Vector2 _tr;
        private Vector2 _br;
        private Vector2 _bl;

        // Cached per-tile quads for Bilinear mapping (TL, TR, BR, BL) for each tile.
        // Index: (y * Columns + x) * 4 + cornerIndex
        private Vector2[] _tileQuads;

        public static PerspectiveGrid FromQuad(Vector2 tl, Vector2 tr, Vector2 br, Vector2 bl, int cols, int rows)
        {
            return FromQuad(tl, tr, br, bl, cols, rows, PerspectiveGridMappingMode.Homography);
        }

        public static PerspectiveGrid FromQuad(Vector2 tl, Vector2 tr, Vector2 br, Vector2 bl, int cols, int rows, PerspectiveGridMappingMode mappingMode)
        {
            int columns = Mathf.Max(1, cols);
            int rowCount = Mathf.Max(1, rows);

            if (mappingMode == PerspectiveGridMappingMode.Bilinear)
            {
                bool ok = AllFinite(tl) && AllFinite(tr) && AllFinite(br) && AllFinite(bl);
                var grid = new PerspectiveGrid
                {
                    MappingMode = PerspectiveGridMappingMode.Bilinear,
                    Columns = columns,
                    Rows = rowCount,
                    IsValid = ok,
                    _tl = tl,
                    _tr = tr,
                    _br = br,
                    _bl = bl,
                    QuadToUnit = Homography2D.Identity,
                    UnitToQuad = Homography2D.Identity
                };

                if (ok)
                {
                    grid._tileQuads = BuildBilinearTileQuads(tl, tr, br, bl, columns, rowCount);
                }

                return grid;
            }

            var q2u = Homography2D.QuadToUnitRect(tl, tr, br, bl);
            var u2q = q2u.Inverse();
            bool valid = AllFinite(q2u) && AllFinite(u2q);
            return new PerspectiveGrid
            {
                MappingMode = PerspectiveGridMappingMode.Homography,
                QuadToUnit = q2u,
                UnitToQuad = u2q,
                Columns = columns,
                Rows = rowCount,
                IsValid = valid,
                _tl = tl,
                _tr = tr,
                _br = br,
                _bl = bl,
                _tileQuads = null
            };
        }

        public bool TryLocalToTile(Vector2 local, out int x, out int y)
        {
            if (MappingMode == PerspectiveGridMappingMode.Bilinear)
            {
                if (!IsValid || _tileQuads == null || _tileQuads.Length != Columns * Rows * 4)
                {
                    x = y = -1;
                    return false;
                }

                for (int ty = 0; ty < Rows; ty++)
                for (int tx = 0; tx < Columns; tx++)
                {
                    int idx = (ty * Columns + tx) * 4;
                    var tl = _tileQuads[idx + 0];
                    var tr = _tileQuads[idx + 1];
                    var br = _tileQuads[idx + 2];
                    var bl = _tileQuads[idx + 3];
                    if (PointInQuad(local, tl, tr, br, bl))
                    {
                        x = tx;
                        y = ty;
                        return true;
                    }
                }

                x = y = -1;
                return false;
            }

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

            if (MappingMode == PerspectiveGridMappingMode.Bilinear)
            {
                return BilinearPoint(_tl, _tr, _br, _bl, u, v);
            }

            return UnitToQuad.TransformPoint(new Vector2(u, v));
        }

        // Returns the 4 local-space corners of a tile quad (TL, TR, BR, BL)
        public void TileQuadLocal(int x, int y, out Vector2 tl, out Vector2 tr, out Vector2 br, out Vector2 bl)
        {
            if (MappingMode == PerspectiveGridMappingMode.Bilinear)
            {
                if (_tileQuads != null && _tileQuads.Length == Columns * Rows * 4)
                {
                    int idx = (y * Columns + x) * 4;
                    tl = _tileQuads[idx + 0];
                    tr = _tileQuads[idx + 1];
                    br = _tileQuads[idx + 2];
                    bl = _tileQuads[idx + 3];
                    return;
                }

                float bu0 = (float)x / Columns;
                float bu1 = (float)(x + 1) / Columns;
                float bv0 = (float)y / Rows;
                float bv1 = (float)(y + 1) / Rows;
                tl = BilinearPoint(_tl, _tr, _br, _bl, bu0, bv1);
                tr = BilinearPoint(_tl, _tr, _br, _bl, bu1, bv1);
                br = BilinearPoint(_tl, _tr, _br, _bl, bu1, bv0);
                bl = BilinearPoint(_tl, _tr, _br, _bl, bu0, bv0);
                return;
            }

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

        private static Vector2[] BuildBilinearTileQuads(Vector2 tl, Vector2 tr, Vector2 br, Vector2 bl, int cols, int rows)
        {
            var quads = new Vector2[cols * rows * 4];
            for (int y = 0; y < rows; y++)
            for (int x = 0; x < cols; x++)
            {
                float u0 = (float)x / cols;
                float u1 = (float)(x + 1) / cols;
                float v0 = (float)y / rows;
                float v1 = (float)(y + 1) / rows;

                var qbl = BilinearPoint(tl, tr, br, bl, u0, v0);
                var qbr = BilinearPoint(tl, tr, br, bl, u1, v0);
                var qtl = BilinearPoint(tl, tr, br, bl, u0, v1);
                var qtr = BilinearPoint(tl, tr, br, bl, u1, v1);

                int idx = (y * cols + x) * 4;
                quads[idx + 0] = qtl;
                quads[idx + 1] = qtr;
                quads[idx + 2] = qbr;
                quads[idx + 3] = qbl;
            }
            return quads;
        }

        private static Vector2 BilinearPoint(Vector2 tl, Vector2 tr, Vector2 br, Vector2 bl, float u, float v)
        {
            var bottom = Vector2.Lerp(bl, br, u);
            var top = Vector2.Lerp(tl, tr, u);
            return Vector2.Lerp(bottom, top, v);
        }

        private static bool PointInQuad(Vector2 p, Vector2 tl, Vector2 tr, Vector2 br, Vector2 bl)
        {
            // Split into two triangles.
            return PointInTriangle(p, tl, tr, br) || PointInTriangle(p, br, bl, tl);
        }

        private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Sign(p, a, b);
            float d2 = Sign(p, b, c);
            float d3 = Sign(p, c, a);
            bool hasNeg = (d1 < 0f) || (d2 < 0f) || (d3 < 0f);
            bool hasPos = (d1 > 0f) || (d2 > 0f) || (d3 > 0f);
            return !(hasNeg && hasPos);
        }

        private static float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
        {
            return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
        }

        private static bool AllFinite(Vector2 v)
        {
            return !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsInfinity(v.x) && !float.IsInfinity(v.y);
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
