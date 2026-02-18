using NUnit.Framework;
using SevenBattles.Core.Battle;

namespace SevenBattles.Tests.Core
{
    public class TournamentMissionIdUtilTests
    {
        [Test]
        public void BuildMissionId_AndParseRoundIndex_RoundTrips()
        {
            string missionId = TournamentMissionIdUtil.BuildMissionId(5);

            bool parsed = TournamentMissionIdUtil.TryParseRoundIndex(missionId, out int roundIndex);

            Assert.IsTrue(parsed);
            Assert.AreEqual(5, roundIndex);
        }

        [Test]
        public void TryParseRoundIndex_InvalidMissionId_ReturnsFalse()
        {
            bool parsed = TournamentMissionIdUtil.TryParseRoundIndex("campaign:3", out int roundIndex);

            Assert.IsFalse(parsed);
            Assert.AreEqual(0, roundIndex);
        }
    }
}
