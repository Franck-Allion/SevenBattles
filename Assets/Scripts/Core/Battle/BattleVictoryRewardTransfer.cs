using UnityEngine;

namespace SevenBattles.Core.Battle
{
    /// <summary>
    /// One-shot handoff of victory resource deltas from BattleScene to PreparationScene.
    /// </summary>
    public static class BattleVictoryRewardTransfer
    {
        private static BattleVictoryRewardData _pending;
        private static bool _hasPending;

        public static bool HasPending => _hasPending;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Clear();
        }

        public static void SetPending(int fromGold, int fromGems, int goldGained, int gemsGained)
        {
            int safeFromGold = Mathf.Max(0, fromGold);
            int safeFromGems = Mathf.Max(0, fromGems);
            int safeGoldGained = Mathf.Max(0, goldGained);
            int safeGemsGained = Mathf.Max(0, gemsGained);

            if (safeGoldGained <= 0 && safeGemsGained <= 0)
            {
                Clear();
                return;
            }

            _pending = new BattleVictoryRewardData(
                safeFromGold,
                safeFromGems,
                safeGoldGained,
                safeGemsGained);
            _hasPending = true;
        }

        public static bool TryConsume(out BattleVictoryRewardData data)
        {
            if (!_hasPending)
            {
                data = default;
                return false;
            }

            data = _pending;
            _pending = default;
            _hasPending = false;
            return true;
        }

        public static void Clear()
        {
            _pending = default;
            _hasPending = false;
        }
    }

    public readonly struct BattleVictoryRewardData
    {
        public BattleVictoryRewardData(int fromGold, int fromGems, int goldGained, int gemsGained)
        {
            FromGold = Mathf.Max(0, fromGold);
            FromGems = Mathf.Max(0, fromGems);
            GoldGained = Mathf.Max(0, goldGained);
            GemsGained = Mathf.Max(0, gemsGained);
        }

        public int FromGold { get; }
        public int FromGems { get; }
        public int GoldGained { get; }
        public int GemsGained { get; }
        public int ToGold => FromGold + GoldGained;
        public int ToGems => FromGems + GemsGained;
    }
}
