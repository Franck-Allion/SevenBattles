using System;
using UnityEngine;
using SevenBattles.Core.Units;

namespace SevenBattles.Core.Battle
{
    [Serializable]
    public struct UnitXpProgressionStep
    {
        public int Level;
        public int XpFrom;
        public int XpTo;
        public int XpToNext;
        public bool LevelUpAtEnd;
        public bool ReachedMaxLevelAtEnd;
    }

    public readonly struct UnitXpApplyResult
    {
        public readonly int XpRequested;
        public readonly int XpApplied;
        public readonly int LevelBefore;
        public readonly int LevelAfter;
        public readonly bool ReachedMaxLevel;

        public UnitXpApplyResult(int xpRequested, int xpApplied, int levelBefore, int levelAfter, bool reachedMaxLevel)
        {
            XpRequested = xpRequested;
            XpApplied = xpApplied;
            LevelBefore = levelBefore;
            LevelAfter = levelAfter;
            ReachedMaxLevel = reachedMaxLevel;
        }
    }

    public static class UnitXpProgressionUtil
    {
        public static int GetXpToNextLevel(int level, int maxLevel, int[] thresholds)
        {
            if (level < 1)
            {
                return 0;
            }

            if (maxLevel > 0 && level >= maxLevel)
            {
                return 0;
            }

            if (thresholds == null)
            {
                return 0;
            }

            int index = level - 1;
            if (index < 0 || index >= thresholds.Length)
            {
                return 0;
            }

            return Mathf.Max(0, thresholds[index]);
        }

        public static UnitXpProgressionStep[] BuildXpSteps(int levelBefore, int xpBefore, int xpApplied, int maxLevel, int[] thresholds)
        {
            if (xpApplied <= 0)
            {
                return Array.Empty<UnitXpProgressionStep>();
            }

            int safeMaxLevel = Mathf.Max(1, maxLevel);
            int level = Mathf.Max(1, levelBefore);
            if (level >= safeMaxLevel)
            {
                return Array.Empty<UnitXpProgressionStep>();
            }

            if (thresholds == null || thresholds.Length == 0)
            {
                return Array.Empty<UnitXpProgressionStep>();
            }

            var steps = new System.Collections.Generic.List<UnitXpProgressionStep>();
            int remaining = Mathf.Max(0, xpApplied);

            int xp = Mathf.Max(0, xpBefore);

            while (remaining > 0 && level < safeMaxLevel)
            {
                int toNext = GetXpToNextLevel(level, safeMaxLevel, thresholds);
                if (toNext <= 0)
                {
                    break;
                }

                xp = Mathf.Clamp(xp, 0, toNext);

                int needed = Mathf.Max(0, toNext - xp);
                int delta = Mathf.Min(remaining, needed);

                int stepLevel = level;
                int xpFrom = xp;
                int xpTo = xp + delta;

                remaining -= delta;

                bool levelUpAtEnd = false;
                bool reachedMaxAtEnd = false;

                if (xpTo >= toNext)
                {
                    levelUpAtEnd = true;
                    level++;
                    xp = 0;
                    reachedMaxAtEnd = level >= safeMaxLevel;
                }
                else
                {
                    xp = xpTo;
                }

                steps.Add(new UnitXpProgressionStep
                {
                    Level = stepLevel,
                    XpFrom = xpFrom,
                    XpTo = xpTo,
                    XpToNext = toNext,
                    LevelUpAtEnd = levelUpAtEnd,
                    ReachedMaxLevelAtEnd = reachedMaxAtEnd
                });

                if (reachedMaxAtEnd)
                {
                    break;
                }
            }

            return steps.Count == 0 ? Array.Empty<UnitXpProgressionStep>() : steps.ToArray();
        }

        public static UnitXpApplyResult ApplyXp(UnitSpellLoadout loadout, int xpGained)
        {
            if (loadout == null)
            {
                return new UnitXpApplyResult(xpGained, 0, 0, 0, reachedMaxLevel: false);
            }

            int requested = Mathf.Max(0, xpGained);
            int levelBefore = loadout.EffectiveLevel;
            int level = levelBefore;
            int xp = loadout.EffectiveXp;

            var def = loadout.Definition;
            int maxLevel = def != null ? Mathf.Max(1, def.MaxLevel) : level;
            int[] thresholds = def != null ? (def.XpToNextLevel ?? System.Array.Empty<int>()) : System.Array.Empty<int>();

            if (level >= maxLevel || requested == 0)
            {
                bool reached = level >= maxLevel;
                if (reached)
                {
                    loadout.Level = maxLevel;
                    loadout.Xp = 0;
                }
                return new UnitXpApplyResult(requested, 0, levelBefore, levelBefore, reached);
            }

            int applied = 0;
            int remaining = requested;

            while (remaining > 0 && level < maxLevel)
            {
                int thresholdIndex = level - 1;
                if (thresholdIndex < 0 || thresholdIndex >= thresholds.Length)
                {
                    break;
                }

                int toNext = thresholds[thresholdIndex];
                if (toNext <= 0)
                {
                    break;
                }

                int needed = Mathf.Max(0, toNext - xp);
                if (needed <= 0)
                {
                    level++;
                    xp = 0;
                    continue;
                }

                int delta = Mathf.Min(remaining, needed);
                xp += delta;
                remaining -= delta;
                applied += delta;

                if (xp >= toNext)
                {
                    level++;
                    xp = 0;
                }
            }

            bool reachedMax = level >= maxLevel;
            loadout.Level = reachedMax ? maxLevel : level;
            loadout.Xp = reachedMax ? 0 : xp;

            return new UnitXpApplyResult(requested, applied, levelBefore, loadout.Level, reachedMax);
        }

        public static float GetThreatFactor(UnitDefinition definition)
        {
            if (definition == null)
            {
                return 1f;
            }

            float f = definition.ThreatFactor;
            if (float.IsNaN(f) || float.IsInfinity(f))
            {
                return 1f;
            }

            return Mathf.Max(0f, f);
        }
    }
}
