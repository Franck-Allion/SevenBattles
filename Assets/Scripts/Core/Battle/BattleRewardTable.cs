using System;
using UnityEngine;

namespace SevenBattles.Core.Battle
{
    [Serializable]
    public sealed class BattleRewardTable
    {
        [Header("Guaranteed Gold")]
        [Min(0)]
        public int GoldMin = 50;

        [Min(0)]
        public int GoldMax = 150;

        [Header("Bonus Reward Pool (0-2 items rolled from this pool)")]
        [Range(0, 2)]
        public int MaxBonusRewards = 2;

        public BattleRewardEntry[] BonusPool = Array.Empty<BattleRewardEntry>();
    }
}
