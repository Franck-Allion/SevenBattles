using System;
using NUnit.Framework;
using UnityEngine;
using SevenBattles.Battle.Units;
using SevenBattles.Core.Units;

namespace SevenBattles.Tests.Battle.Units
{
    public class UnitStatsChangedTests
    {
        [Test]
        public void Changed_Fires_OnDamage_AndHeal()
        {
            var go = new GameObject("UnitStats");
            var stats = go.AddComponent<UnitStats>();

            int changedCount = 0;
            stats.Changed += () => changedCount++;

            stats.ApplyBase(new UnitStatsData { Life = 10, ActionPoints = 1 });
            changedCount = 0;

            stats.TakeDamage(2);
            stats.Heal(1);

            Assert.AreEqual(2, changedCount, "Changed should fire for damage and heal.");

            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void Changed_DoesNotAllocate_OnDamage()
        {
            var go = new GameObject("UnitStats");
            var stats = go.AddComponent<UnitStats>();
            stats.ApplyBase(new UnitStatsData { Life = 10, ActionPoints = 1 });

            stats.Changed += () => { };

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long before = GC.GetAllocatedBytesForCurrentThread();
            stats.TakeDamage(1);
            long after = GC.GetAllocatedBytesForCurrentThread();

            Assert.AreEqual(before, after, "Changed event should not allocate on damage.");

            UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
