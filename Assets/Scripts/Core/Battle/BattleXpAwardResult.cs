using System;
using UnityEngine;

namespace SevenBattles.Core.Battle
{
    [Serializable]
    public sealed class BattleXpAwardResult
    {
        [Serializable]
        public struct UnitAward
        {
            public int SquadIndex;
            public string UnitId;
            public Sprite Portrait;
            public bool IsAlive;
            public int XpAwarded;
            public int XpApplied;
            public int XpBefore;
            public int XpAfter;
            public int XpToNextBefore;
            public int XpToNextAfter;
            public int LevelBefore;
            public int LevelAfter;
            public int MaxLevel;
            public bool ReachedMaxLevel;
            public UnitXpProgressionStep[] XpSteps;
        }

        public BattleOutcome Outcome;
        public int TotalXp;
        public int AlivePlayerUnits;
        public int TotalPlayerUnits;
        public int ActualTurns;
        public UnitAward[] Units = Array.Empty<UnitAward>();
    }
}
