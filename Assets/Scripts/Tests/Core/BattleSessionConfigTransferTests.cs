using NUnit.Framework;
using SevenBattles.Core.Battle;

namespace SevenBattles.Tests.Core
{
    public class BattleSessionConfigTransferTests
    {
        [SetUp]
        public void SetUp()
        {
            BattleSessionConfigTransfer.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            BattleSessionConfigTransfer.Clear();
        }

        [Test]
        public void TryConsume_ReturnsFalse_WhenNoPendingConfig()
        {
            var consumed = BattleSessionConfigTransfer.TryConsume(out var config);

            Assert.IsFalse(consumed);
            Assert.IsNull(config);
        }

        [Test]
        public void SetPending_ThenTryConsume_ReturnsConfigAndClearsPending()
        {
            var pending = new BattleSessionConfig();

            BattleSessionConfigTransfer.SetPending(pending);

            var consumed = BattleSessionConfigTransfer.TryConsume(out var config);

            Assert.IsTrue(consumed);
            Assert.AreSame(pending, config);
            Assert.IsFalse(BattleSessionConfigTransfer.HasPending);
        }
    }
}
