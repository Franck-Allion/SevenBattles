using NUnit.Framework;
using UnityEngine;
using SevenBattles.Core.Battle;
using SevenBattles.Core.Units;

namespace SevenBattles.Tests.Core
{
    public class UnitXpProgressionUtilTests
    {
        [Test]
        public void ApplyXp_LevelsUpMultipleTimes_AndClampsAtMax()
        {
            var def = ScriptableObject.CreateInstance<UnitDefinition>();
            def.Id = "UnitA";
            def.MaxLevel = 3;
            def.XpToNextLevel = new[] { 10, 20 }; // 1->2, 2->3

            var loadout = new UnitSpellLoadout
            {
                Definition = def,
                Level = 1,
                Xp = 0
            };

            var result = UnitXpProgressionUtil.ApplyXp(loadout, 35);

            Assert.AreEqual(35, result.XpRequested);
            Assert.AreEqual(30, result.XpApplied, "Only 10+20 XP are needed to reach the max level.");
            Assert.AreEqual(1, result.LevelBefore);
            Assert.AreEqual(3, result.LevelAfter);
            Assert.IsTrue(result.ReachedMaxLevel);
            Assert.AreEqual(3, loadout.Level);
            Assert.AreEqual(0, loadout.Xp, "XP should be cleared once max level is reached.");
        }

        [Test]
        public void BuildXpSteps_SplitsAcrossLevels_AndMarksMaxLevel()
        {
            int maxLevel = 4;
            int[] thresholds = { 10, 20, 30 }; // 1->2, 2->3, 3->4

            var steps = UnitXpProgressionUtil.BuildXpSteps(levelBefore: 2, xpBefore: 5, xpApplied: 45, maxLevel, thresholds);

            Assert.AreEqual(2, steps.Length);

            Assert.AreEqual(2, steps[0].Level);
            Assert.AreEqual(5, steps[0].XpFrom);
            Assert.AreEqual(20, steps[0].XpTo);
            Assert.AreEqual(20, steps[0].XpToNext);
            Assert.IsTrue(steps[0].LevelUpAtEnd);
            Assert.IsFalse(steps[0].ReachedMaxLevelAtEnd);

            Assert.AreEqual(3, steps[1].Level);
            Assert.AreEqual(0, steps[1].XpFrom);
            Assert.AreEqual(30, steps[1].XpTo);
            Assert.AreEqual(30, steps[1].XpToNext);
            Assert.IsTrue(steps[1].LevelUpAtEnd);
            Assert.IsTrue(steps[1].ReachedMaxLevelAtEnd);
        }

        [Test]
        public void BuildXpSteps_StaysWithinLevel_WhenThresholdNotReached()
        {
            int maxLevel = 3;
            int[] thresholds = { 10, 20 };

            var steps = UnitXpProgressionUtil.BuildXpSteps(levelBefore: 1, xpBefore: 2, xpApplied: 5, maxLevel, thresholds);

            Assert.AreEqual(1, steps.Length);
            Assert.AreEqual(1, steps[0].Level);
            Assert.AreEqual(2, steps[0].XpFrom);
            Assert.AreEqual(7, steps[0].XpTo);
            Assert.AreEqual(10, steps[0].XpToNext);
            Assert.IsFalse(steps[0].LevelUpAtEnd);
            Assert.IsFalse(steps[0].ReachedMaxLevelAtEnd);
        }
    }
}
