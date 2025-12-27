using NUnit.Framework;
using UnityEngine;
using SevenBattles.Battle.Units;
using SevenBattles.Core.Save;
using SevenBattles.Core.Units;

namespace SevenBattles.Tests.Battle
{
    public class UnitStatsDeckCapacityTests
    {
        [Test]
        public void ApplySaved_KeepsBaseDeckAndDraw_WhenSaveMissingFields()
        {
            var go = new GameObject("Unit");
            var stats = go.AddComponent<UnitStats>();
            stats.ApplyBase(new UnitStatsData { Life = 5, ActionPoints = 1, DeckCapacity = 3, DrawCapacity = 2 });

            var saved = new UnitStatsSaveData
            {
                Life = 5,
                MaxLife = 5
            };

            stats.ApplySaved(saved);

            Assert.AreEqual(3, stats.DeckCapacity);
            Assert.AreEqual(2, stats.DrawCapacity);

            Object.DestroyImmediate(go);
        }
    }
}
