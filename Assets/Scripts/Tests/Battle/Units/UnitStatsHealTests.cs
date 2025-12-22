using NUnit.Framework;
using UnityEngine;
using SevenBattles.Battle.Units;
using SevenBattles.Core.Units;

namespace SevenBattles.Tests.Battle.Units
{
    public class UnitStatsHealTests
    {
        [Test]
        public void Heal_ReturnsEffectiveAmount_WhenCappedByMaxLife()
        {
            var go = new GameObject("UnitStats");
            var stats = go.AddComponent<UnitStats>();
            stats.ApplyBase(new UnitStatsData { Life = 10 });

            stats.TakeDamage(7);
            Assert.AreEqual(3, stats.Life);

            int effective = stats.Heal(10);

            Assert.AreEqual(7, effective);
            Assert.AreEqual(10, stats.Life);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void Heal_RaisesHealedEvent_WithEffectiveAmount_IncludingZero()
        {
            var go = new GameObject("UnitStats");
            var stats = go.AddComponent<UnitStats>();
            stats.ApplyBase(new UnitStatsData { Life = 10 });

            int callCount = 0;
            int lastValue = -1;
            stats.Healed += (_, amount) =>
            {
                callCount++;
                lastValue = amount;
            };

            int effectiveZeroInput = stats.Heal(0);
            Assert.AreEqual(0, effectiveZeroInput);
            Assert.AreEqual(1, callCount);
            Assert.AreEqual(0, lastValue);

            int effectiveCapped = stats.Heal(5);
            Assert.AreEqual(0, effectiveCapped);
            Assert.AreEqual(2, callCount);
            Assert.AreEqual(0, lastValue);

            Object.DestroyImmediate(go);
        }
    }
}

