using UnityEngine;

namespace SevenBattles.Core.Battle
{
    /// <summary>
    /// Helper for encoding/decoding tournament battle identifiers in BattleSessionConfig.CampaignMissionId.
    /// </summary>
    public static class TournamentMissionIdUtil
    {
        private const string PREFIX = "tournament:";

        public static string BuildMissionId(int roundIndex)
        {
            return PREFIX + Mathf.Max(1, roundIndex).ToString();
        }

        public static bool TryParseRoundIndex(string missionId, out int roundIndex)
        {
            roundIndex = 0;
            if (string.IsNullOrWhiteSpace(missionId))
            {
                return false;
            }

            if (!missionId.StartsWith(PREFIX, System.StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string number = missionId.Substring(PREFIX.Length);
            if (!int.TryParse(number, out int parsed) || parsed < 1)
            {
                return false;
            }

            roundIndex = parsed;
            return true;
        }
    }
}
