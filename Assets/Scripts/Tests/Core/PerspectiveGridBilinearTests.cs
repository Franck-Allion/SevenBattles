using NUnit.Framework;
using SevenBattles.Core.Math;
using UnityEngine;

namespace SevenBattles.Tests.Core
{
    public class PerspectiveGridBilinearTests
    {
        [Test]
        public void Local_To_Tile_And_Center_Work_Bilinear()
        {
            var tl = new Vector2(-1f, 1f);
            var tr = new Vector2(1.3f, 1.2f);
            var br = new Vector2(2f, -1f);
            var bl = new Vector2(-1.2f, -1.1f);
            var grid = PerspectiveGrid.FromQuad(tl, tr, br, bl, 7, 6, PerspectiveGridMappingMode.Bilinear);

            int tx = 5, ty = 1;
            var centerLocal = grid.TileCenterLocal(tx, ty);
            Assert.That(grid.TryLocalToTile(centerLocal, out var x, out var y));
            Assert.AreEqual(tx, x);
            Assert.AreEqual(ty, y);
        }

        [Test]
        public void TileQuadCenter_Matches_TileCenterLocal_Bilinear()
        {
            var tl = new Vector2(-1f, 1f);
            var tr = new Vector2(2f, 1.2f);
            var br = new Vector2(2.1f, -0.8f);
            var bl = new Vector2(-0.9f, -1f);
            var grid = PerspectiveGrid.FromQuad(tl, tr, br, bl, 6, 4, PerspectiveGridMappingMode.Bilinear);

            int x = 3, y = 2;
            grid.TileQuadLocal(x, y, out var qtl, out var qtr, out var qbr, out var qbl);
            var centerFromQuad = (qtl + qtr + qbr + qbl) * 0.25f;
            var centerLocal = grid.TileCenterLocal(x, y);

            Assert.That(Vector2.Distance(centerFromQuad, centerLocal), Is.LessThan(1e-6f));
        }
    }
}

