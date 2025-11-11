using NUnit.Framework;
using SevenBattles.Core.Math;
using UnityEngine;

namespace SevenBattles.Tests.Core
{
    public class PerspectiveGridQuadTests
    {
        [Test]
        public void TileQuadCenter_Matches_TileCenterLocal()
        {
            var tl = new Vector2(-1f, 1f);
            var tr = new Vector2(2f, 1.2f);
            var br = new Vector2(2.1f, -0.8f);
            var bl = new Vector2(-0.9f, -1f);
            var grid = PerspectiveGrid.FromQuad(tl, tr, br, bl, 6, 4);

            int x = 3, y = 2;
            grid.TileQuadLocal(x, y, out var qtl, out var qtr, out var qbr, out var qbl);

            var centerFromQuad = (qtl + qtr + qbr + qbl) * 0.25f;
            var centerLocal = grid.TileCenterLocal(x, y);

            Assert.That(Vector2.Distance(centerFromQuad, centerLocal), Is.LessThan(1e-3f));
        }
    }
}

