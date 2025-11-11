using NUnit.Framework;
using System.Collections.Generic;
using SevenBattles.Core.Math;
using UnityEngine;

namespace SevenBattles.Tests.Core
{
    public class Homography2DTests
    {
        [Test]
        public void Quad_To_Unit_And_Back_Roundtrips()
        {
            var tl = new Vector2(-1f, 1.2f);
            var tr = new Vector2(2.2f, 1.1f);
            var br = new Vector2(2.0f, -0.4f);
            var bl = new Vector2(-0.8f, -0.3f);

            var q2u = Homography2D.QuadToUnitRect(tl, tr, br, bl);
            var u2q = q2u.Inverse();

            // Corners map exactly
            Assert.That(q2u.TransformPoint(tl), Is.EqualTo(new Vector2(0, 1)).Using(Vector2Comparer(1e-4f)));
            Assert.That(q2u.TransformPoint(tr), Is.EqualTo(new Vector2(1, 1)).Using(Vector2Comparer(1e-4f)));
            Assert.That(q2u.TransformPoint(br), Is.EqualTo(new Vector2(1, 0)).Using(Vector2Comparer(1e-4f)));
            Assert.That(q2u.TransformPoint(bl), Is.EqualTo(new Vector2(0, 0)).Using(Vector2Comparer(1e-4f)));

            // Center roundtrip
            var centerUnit = new Vector2(0.5f, 0.5f);
            var toQuad = u2q.TransformPoint(centerUnit);
            var back = q2u.TransformPoint(toQuad);
            Assert.That(back, Is.EqualTo(centerUnit).Using(Vector2Comparer(1e-4f)));
        }

        private static IEqualityComparer<Vector2> Vector2Comparer(float epsilon)
        {
            return ComparerBuilder<Vector2>.Create((a, b) =>
                Mathf.Abs(a.x - b.x) <= epsilon && Mathf.Abs(a.y - b.y) <= epsilon);
        }
    }

    internal static class ComparerBuilder<T>
    {
        public static IEqualityComparer<T> Create(System.Func<T, T, bool> eq)
            => new LambdaComparer(eq);

        private class LambdaComparer : IEqualityComparer<T>
        {
            private readonly System.Func<T, T, bool> _eq;
            public LambdaComparer(System.Func<T, T, bool> eq) { _eq = eq; }
            public bool Equals(T x, T y) => _eq(x, y);
            public int GetHashCode(T obj) => 0;
        }
    }
}
