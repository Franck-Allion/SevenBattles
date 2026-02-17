using System;
using System.Collections.Generic;
using SevenBattles.Core;
using UnityEngine;

namespace SevenBattles.Core.Battle
{
    public sealed class BattleRewardService : IBattleRewardService
    {
        private const double FirstBonusDropChance = 0.70;
        private const double SecondBonusDropChance = 0.40;
        private const int MaximumSupportedBonusSlots = 2;

        public BattleRewardResult ComputeRewards(BattleRewardTable table)
        {
            return ComputeRewards(table, new System.Random(Environment.TickCount));
        }

        public BattleRewardResult ComputeRewards(BattleRewardTable table, System.Random rng)
        {
            if (table == null)
            {
                return new BattleRewardResult(0, Array.Empty<BattleRewardResultEntry>());
            }

            System.Random activeRng = rng ?? new System.Random(Environment.TickCount);
            int goldAmount = RollInclusive(activeRng, Mathf.Max(0, table.GoldMin), Mathf.Max(0, table.GoldMax));

            BattleRewardEntry[] sourcePool = table.BonusPool ?? Array.Empty<BattleRewardEntry>();
            var remainingPool = new List<BattleRewardEntry>(sourcePool);
            if (remainingPool.Count == 0 || !HasPositiveWeight(remainingPool))
            {
                return new BattleRewardResult(goldAmount, Array.Empty<BattleRewardResultEntry>());
            }

            int maxBonusRewards = Mathf.Clamp(table.MaxBonusRewards, 0, MaximumSupportedBonusSlots);
            var rewards = new List<BattleRewardResultEntry>(maxBonusRewards);

            for (int slotIndex = 0; slotIndex < maxBonusRewards; slotIndex++)
            {
                if (!ShouldDropForSlot(activeRng, slotIndex))
                {
                    continue;
                }

                if (!TryPickWeightedIndex(remainingPool, activeRng, out int pickedIndex))
                {
                    break;
                }

                BattleRewardEntry pickedEntry = remainingPool[pickedIndex];
                remainingPool.RemoveAt(pickedIndex);

                BattleRewardResultEntry resultEntry = BuildResultEntry(pickedEntry, activeRng);
                if (resultEntry != null)
                {
                    rewards.Add(resultEntry);
                }
            }

            return new BattleRewardResult(goldAmount, rewards.ToArray());
        }

        private static bool ShouldDropForSlot(System.Random rng, int slotIndex)
        {
            double chance = slotIndex == 0 ? FirstBonusDropChance : SecondBonusDropChance;
            return rng.NextDouble() < chance;
        }

        private static bool HasPositiveWeight(List<BattleRewardEntry> pool)
        {
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i].Weight > 0f)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryPickWeightedIndex(List<BattleRewardEntry> pool, System.Random rng, out int pickedIndex)
        {
            float totalWeight = 0f;
            for (int i = 0; i < pool.Count; i++)
            {
                totalWeight += Mathf.Max(0f, pool[i].Weight);
            }

            if (totalWeight <= 0f)
            {
                pickedIndex = -1;
                return false;
            }

            float roll = (float)(rng.NextDouble() * totalWeight);
            float cumulative = 0f;
            int lastPositiveWeightIndex = -1;

            for (int i = 0; i < pool.Count; i++)
            {
                float weight = Mathf.Max(0f, pool[i].Weight);
                if (weight <= 0f)
                {
                    continue;
                }

                lastPositiveWeightIndex = i;
                cumulative += weight;
                if (roll <= cumulative)
                {
                    pickedIndex = i;
                    return true;
                }
            }

            pickedIndex = lastPositiveWeightIndex;
            return pickedIndex >= 0;
        }

        private static BattleRewardResultEntry BuildResultEntry(BattleRewardEntry entry, System.Random rng)
        {
            switch (entry.Type)
            {
                case BattleRewardType.Gold:
                case BattleRewardType.Gems:
                    return new BattleRewardResultEntry(entry.Type, RollInclusive(rng, Mathf.Max(0, entry.MinAmount), Mathf.Max(0, entry.MaxAmount)));

                case BattleRewardType.Equipment:
                    return entry.EquipmentRef != null ? new BattleRewardResultEntry(entry.EquipmentRef) : null;

                case BattleRewardType.Spell:
                    return entry.SpellRef != null ? new BattleRewardResultEntry(entry.SpellRef) : null;

                case BattleRewardType.Item:
                    return entry.ItemRef != null ? new BattleRewardResultEntry(entry.ItemRef, 1) : null;

                default:
                    return null;
            }
        }

        private static int RollInclusive(System.Random rng, int min, int max)
        {
            int clampedMin = Mathf.Max(0, min);
            int clampedMax = Mathf.Max(0, max);
            if (clampedMax < clampedMin)
            {
                (clampedMin, clampedMax) = (clampedMax, clampedMin);
            }

            if (clampedMin == clampedMax)
            {
                return clampedMin;
            }

            return rng.Next(clampedMin, clampedMax + 1);
        }
    }
}
