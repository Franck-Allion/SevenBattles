using NUnit.Framework;
using UnityEngine;
using SevenBattles.Battle.Units;
using SevenBattles.Core.Units;
using SevenBattles.Core.Save;

namespace SevenBattles.Tests.Battle
{
    public class UnitStatsShootPersistenceTests
    {
        [Test]
        public void ApplyBase_DefaultsShootRange_WhenMissing()
        {
            var go = new GameObject("Unit");
            var stats = go.AddComponent<UnitStats>();

            stats.ApplyBase(new UnitStatsData { Life = 10, Shoot = 4, ShootRange = 0 });

            Assert.AreEqual(1, stats.ShootRange);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void ApplySaved_ClampsShootStats_WhenNegative()
        {
            var go = new GameObject("Unit");
            var stats = go.AddComponent<UnitStats>();
            stats.ApplyBase(new UnitStatsData { Life = 10, ShootRange = 3, ShootDefense = 2 });

            var saved = new UnitStatsSaveData
            {
                Life = 10,
                MaxLife = 10,
                ShootRange = -5,
                ShootDefense = -2
            };

            Assert.DoesNotThrow(() => stats.ApplySaved(saved));
            Assert.AreEqual(3, stats.ShootRange);
            Assert.AreEqual(2, stats.ShootDefense);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void ApplySaved_MissingShootStats_PreservesBaseValues()
        {
            var go = new GameObject("Unit");
            var stats = go.AddComponent<UnitStats>();
            stats.ApplyBase(new UnitStatsData { Life = 10, ShootRange = 3, ShootDefense = 2 });

            var saved = new UnitStatsSaveData
            {
                Life = 10,
                MaxLife = 10
            };

            Assert.DoesNotThrow(() => stats.ApplySaved(saved));
            Assert.AreEqual(3, stats.ShootRange);
            Assert.AreEqual(2, stats.ShootDefense);

            Object.DestroyImmediate(go);
        }
    }
}
