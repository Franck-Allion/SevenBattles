using NUnit.Framework;
using UnityEngine;
using SevenBattles.Battle.Units;
using SevenBattles.Core.Units;
using SevenBattles.Core.Save;

namespace SevenBattles.Tests.Battle
{
    public class UnitStatsLevelScalingTests
    {
        [Test]
        public void ApplyBase_ScalesStatsByLevelBonus()
        {
            var go = new GameObject("Unit");
            var stats = go.AddComponent<UnitStats>();

            var baseStats = new UnitStatsData
            {
                Life = 10,
                Attack = 2,
                Speed = 1,
                ActionPoints = 2
            };

            var levelBonus = new UnitLevelBonusData
            {
                Life = 2,
                Attack = 1,
                Speed = 1
            };

            stats.ApplyBase(baseStats, levelBonus, 3);

            Assert.AreEqual(3, stats.Level);
            Assert.AreEqual(16, stats.MaxLife);
            Assert.AreEqual(16, stats.Life);
            Assert.AreEqual(5, stats.Attack);
            Assert.AreEqual(4, stats.Speed);
            Assert.AreEqual(2, stats.ActionPoints);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void SetLevel_UsesDelta_AndKeepsDefaults_WhenSaveMissingLevel()
        {
            var go = new GameObject("Unit");
            var stats = go.AddComponent<UnitStats>();

            var baseStats = new UnitStatsData { Life = 10, Attack = 1 };
            var levelBonus = new UnitLevelBonusData { Life = 2, Attack = 1 };
            stats.ApplyBase(baseStats, levelBonus, 1);

            stats.SetLevel(3);
            Assert.AreEqual(3, stats.Level);
            Assert.AreEqual(16, stats.MaxLife);
            Assert.AreEqual(4, stats.Attack);

            stats.SetLevel(2);
            Assert.AreEqual(2, stats.Level);
            Assert.AreEqual(14, stats.MaxLife);
            Assert.AreEqual(3, stats.Attack);

            var saved = new UnitStatsSaveData { Life = 5, MaxLife = 14, Level = 0 };
            stats.ApplySaved(saved);
            Assert.AreEqual(2, stats.Level, "Missing level should preserve the current level.");

            Object.DestroyImmediate(go);
        }
    }
}
