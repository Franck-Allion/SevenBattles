using NUnit.Framework;
using SevenBattles.Core.Battle;

namespace SevenBattles.Tests.Core
{
    public class BattleVictoryRewardTransferTests
    {
        [SetUp]
        public void SetUp()
        {
            BattleVictoryRewardTransfer.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            BattleVictoryRewardTransfer.Clear();
        }

        [Test]
        public void SetPending_WithPositiveValues_StoresAndConsumesOnce()
        {
            BattleVictoryRewardTransfer.SetPending(100, 4, 55, 2);

            Assert.IsTrue(BattleVictoryRewardTransfer.HasPending);
            Assert.IsTrue(BattleVictoryRewardTransfer.TryConsume(out var data));
            Assert.AreEqual(100, data.FromGold);
            Assert.AreEqual(4, data.FromGems);
            Assert.AreEqual(55, data.GoldGained);
            Assert.AreEqual(2, data.GemsGained);
            Assert.AreEqual(155, data.ToGold);
            Assert.AreEqual(6, data.ToGems);
            Assert.IsFalse(BattleVictoryRewardTransfer.HasPending);
            Assert.IsFalse(BattleVictoryRewardTransfer.TryConsume(out _));
        }

        [Test]
        public void SetPending_WithNoGains_ClearsPending()
        {
            BattleVictoryRewardTransfer.SetPending(100, 10, 0, 0);

            Assert.IsFalse(BattleVictoryRewardTransfer.HasPending);
            Assert.IsFalse(BattleVictoryRewardTransfer.TryConsume(out _));
        }

        [Test]
        public void SetPending_ClampsNegativeValues()
        {
            BattleVictoryRewardTransfer.SetPending(-10, -2, -7, 4);

            Assert.IsTrue(BattleVictoryRewardTransfer.TryConsume(out var data));
            Assert.AreEqual(0, data.FromGold);
            Assert.AreEqual(0, data.FromGems);
            Assert.AreEqual(0, data.GoldGained);
            Assert.AreEqual(4, data.GemsGained);
            Assert.AreEqual(0, data.ToGold);
            Assert.AreEqual(4, data.ToGems);
        }
    }
}
