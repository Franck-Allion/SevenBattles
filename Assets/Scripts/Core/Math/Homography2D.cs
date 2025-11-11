using UnityEngine;

namespace SevenBattles.Core.Math
{
    // Small, allocation-free 3x3 homography for perspective mapping.
    public struct Homography2D
    {
        // Row-major
        public double m00, m01, m02;
        public double m10, m11, m12;
        public double m20, m21, m22;

        public static Homography2D Identity => new Homography2D
        {
            m00 = 1, m11 = 1, m22 = 1
        };

        public Vector2 TransformPoint(Vector2 p)
        {
            double x = p.x, y = p.y;
            double w = m20 * x + m21 * y + m22;
            double X = (m00 * x + m01 * y + m02) / w;
            double Y = (m10 * x + m11 * y + m12) / w;
            return new Vector2((float)X, (float)Y);
        }

        public Homography2D Inverse()
        {
            // Generic 3x3 inverse (double precision for stability)
            double a = m00, b = m01, c = m02;
            double d = m10, e = m11, f = m12;
            double g = m20, h = m21, i = m22;

            double A = e * i - f * h;
            double B = -(d * i - f * g);
            double C = d * h - e * g;
            double D = -(b * i - c * h);
            double E = a * i - c * g;
            double F = -(a * h - b * g);
            double G = b * f - c * e;
            double H = -(a * f - c * d);
            double I = a * e - b * d;

            double det = a * A + b * B + c * C;
            double invDet = 1.0 / det;

            return new Homography2D
            {
                m00 = A * invDet,
                m01 = D * invDet,
                m02 = G * invDet,
                m10 = B * invDet,
                m11 = E * invDet,
                m12 = H * invDet,
                m20 = C * invDet,
                m21 = F * invDet,
                m22 = I * invDet
            };
        }

        // Compute H that maps src (quad) -> dst (quad/rect).
        // Uses an 8x8 linear solve in double precision.
        public static Homography2D FromPoints(Vector2 s0, Vector2 s1, Vector2 s2, Vector2 s3,
                                              Vector2 d0, Vector2 d1, Vector2 d2, Vector2 d3)
        {
            double[,] A = new double[8, 8];
            double[] B = new double[8];

            void Row(int r, Vector2 s, Vector2 d, bool xEq)
            {
                double x = s.x, y = s.y, X = d.x, Y = d.y;
                if (xEq)
                {
                    A[r, 0] = x; A[r, 1] = y; A[r, 2] = 1; A[r, 3] = 0; A[r, 4] = 0; A[r, 5] = 0;
                    A[r, 6] = -x * X; A[r, 7] = -y * X; B[r] = X;
                }
                else
                {
                    A[r, 0] = 0; A[r, 1] = 0; A[r, 2] = 0; A[r, 3] = x; A[r, 4] = y; A[r, 5] = 1;
                    A[r, 6] = -x * Y; A[r, 7] = -y * Y; B[r] = Y;
                }
            }

            Row(0, s0, d0, true);  Row(1, s0, d0, false);
            Row(2, s1, d1, true);  Row(3, s1, d1, false);
            Row(4, s2, d2, true);  Row(5, s2, d2, false);
            Row(6, s3, d3, true);  Row(7, s3, d3, false);

            double[] h = Solve8x8(A, B);

            return new Homography2D
            {
                m00 = h[0], m01 = h[1], m02 = h[2],
                m10 = h[3], m11 = h[4], m12 = h[5],
                m20 = h[6], m21 = h[7], m22 = 1.0
            };
        }

        // Convenience: map an arbitrary quad to a unit rect [0,1]^2
        public static Homography2D QuadToUnitRect(Vector2 tl, Vector2 tr, Vector2 br, Vector2 bl)
        {
            return FromPoints(tl, tr, br, bl,
                              new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), new Vector2(0, 0));
        }

        // Convenience: map unit rect to an arbitrary quad
        public static Homography2D UnitRectToQuad(Vector2 tl, Vector2 tr, Vector2 br, Vector2 bl)
        {
            return QuadToUnitRect(tl, tr, br, bl).Inverse();
        }

        private static double[] Solve8x8(double[,] A, double[] b)
        {
            // Simple Gauss-Jordan elimination
            int n = 8;
            double[,] M = new double[n, n + 1];
            for (int r = 0; r < n; r++)
            {
                for (int c = 0; c < n; c++) M[r, c] = A[r, c];
                M[r, n] = b[r];
            }

            for (int col = 0; col < n; col++)
            {
                int pivot = col;
                double max = System.Math.Abs(M[pivot, col]);
                for (int r = col + 1; r < n; r++)
                {
                    double v = System.Math.Abs(M[r, col]);
                    if (v > max) { max = v; pivot = r; }
                }
                if (pivot != col)
                {
                    for (int c = col; c <= n; c++)
                    {
                        double tmp = M[col, c];
                        M[col, c] = M[pivot, c];
                        M[pivot, c] = tmp;
                    }
                }

                double diag = M[col, col];
                double inv = 1.0 / diag;
                for (int c = col; c <= n; c++) M[col, c] *= inv;

                for (int r = 0; r < n; r++)
                {
                    if (r == col) continue;
                    double factor = M[r, col];
                    if (factor == 0) continue;
                    for (int c = col; c <= n; c++) M[r, c] -= factor * M[col, c];
                }
            }

            double[] x = new double[n];
            for (int r = 0; r < n; r++) x[r] = M[r, n];
            return x;
        }
    }
}

