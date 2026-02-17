using System;
using SevenBattles.Core.Battle;

namespace SevenBattles.Core
{
    public interface IBattleRewardService
    {
        BattleRewardResult ComputeRewards(BattleRewardTable table);
        BattleRewardResult ComputeRewards(BattleRewardTable table, Random rng);
    }
}
