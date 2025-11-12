using NUnit.Framework;
using UnityEngine;
using SevenBattles.Battle.Start;

namespace SevenBattles.Tests.Battle
{
    public class SquadPlacementModelTests
    {
        [Test]
        public void Validates_PlayerArea_And_Occupancy()
        {
            var model = new SquadPlacementModel(columns: 7, rows: 7, allowedPlayerRows: 2, squadSize: 3);

            // Inside grid but not in first two rows
            Assert.IsFalse(model.CanPlace(new Vector2Int(0, 3)));

            // Valid area
            var t0 = new Vector2Int(1, 0);
            var t1 = new Vector2Int(2, 1);
            Assert.IsTrue(model.CanPlace(t0));
            Assert.IsTrue(model.CanPlace(t1));

            // Place first wizard
            Assert.IsTrue(model.TryPlace(0, t0));
            Assert.IsFalse(model.CanPlace(t0), "Tile becomes occupied after placement");
            Assert.IsTrue(model.TryGetWizardAt(t0, out var wIndex0) && wIndex0 == 0);

            // Place second wizard
            Assert.IsTrue(model.TryPlace(1, t1));
            Assert.IsFalse(model.CanPlace(t1));

            // Removing
            Assert.IsTrue(model.TryRemoveAt(t0, out var removedIndex) && removedIndex == 0);
            Assert.IsTrue(model.CanPlace(t0));
        }

        [Test]
        public void Completes_After_All_Wizards_Placed()
        {
            var model = new SquadPlacementModel(columns: 5, rows: 5, allowedPlayerRows: 2, squadSize: 3);
            Assert.IsFalse(model.IsComplete());
            Assert.IsTrue(model.TryPlace(0, new Vector2Int(0, 0)));
            Assert.IsFalse(model.IsComplete());
            Assert.IsTrue(model.TryPlace(1, new Vector2Int(1, 0)));
            Assert.IsFalse(model.IsComplete());
            Assert.IsTrue(model.TryPlace(2, new Vector2Int(2, 1)));
            Assert.IsTrue(model.IsComplete());
        }
    }
}

