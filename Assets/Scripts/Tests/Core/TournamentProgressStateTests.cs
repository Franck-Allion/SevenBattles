using NUnit.Framework;
using SevenBattles.Core.Players;

namespace SevenBattles.Tests.Core
{
    public class TournamentProgressStateTests
    {
        [Test]
        public void MarkBattleCompleted_AdvancesToNextIncompleteRound()
        {
            var state = new TournamentProgressState();
            state.SetState(1, new[] { false, false, false, false, false, false, false }, 7);

            bool changed = state.MarkBattleCompleted(1, 7);

            Assert.IsTrue(changed);
            Assert.IsTrue(state.IsBattleCompleted(1));
            Assert.AreEqual(2, state.CurrentRoundIndex);
        }

        [Test]
        public void MarkBattleCompleted_WhenAllCompleted_StaysOnLastRound()
        {
            var state = new TournamentProgressState();
            state.SetState(1, new[] { true, true, true, true, true, true, true }, 7);

            bool changed = state.MarkBattleCompleted(7, 7);

            Assert.IsFalse(changed);
            Assert.AreEqual(7, state.CurrentRoundIndex);
        }

        [Test]
        public void SetState_WhenCurrentRoundAlreadyCompleted_MovesToFirstIncomplete()
        {
            var state = new TournamentProgressState();

            state.SetState(2, new[] { true, true, false, false, false, false, false }, 7);

            Assert.AreEqual(3, state.CurrentRoundIndex);
            Assert.IsTrue(state.IsBattleCompleted(1));
            Assert.IsTrue(state.IsBattleCompleted(2));
            Assert.IsFalse(state.IsBattleCompleted(3));
        }
    }
}
