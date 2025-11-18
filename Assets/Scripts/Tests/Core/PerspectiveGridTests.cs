using NUnit.Framework;
using SevenBattles.Core.Math;
using UnityEngine;

namespace SevenBattles.Tests.Core
{
    public class PerspectiveGridTests
    {
        [Test]
        public void Local_To_Tile_And_Center_Work()
        {
            var tl = new Vector2(-1f, 1f);
            var tr = new Vector2(1f, 1.1f);
            var br = new Vector2(1.2f, -1f);
            var bl = new Vector2(-1.1f, -1f);
            var grid = PerspectiveGrid.FromQuad(tl, tr, br, bl, 5, 5);

            // Pick a specific tile and verify mapping around its center
            int tx = 2, ty = 3;
            var centerLocal = grid.TileCenterLocal(tx, ty);
            Assert.That(grid.TryLocalToTile(centerLocal, out var x, out var y));
            Assert.AreEqual(tx, x);
            Assert.AreEqual(ty, y);
        }
    }
}