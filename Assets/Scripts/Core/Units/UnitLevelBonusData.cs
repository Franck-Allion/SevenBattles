using System;

namespace SevenBattles.Core.Units
{
    [Serializable]
    public struct UnitLevelBonusData
    {
        public int Life;
        public int Attack;
        public int Shoot;
        public int ShootRange;
        public int ShootDefense;
        public int Spell;
        public int Speed;
        public int Luck;
        public int Defense;
        public int Protection;
        public int Initiative;
        public int Morale;

        public bool IsZero =>
            Life == 0 &&
            Attack == 0 &&
            Shoot == 0 &&
            ShootRange == 0 &&
            ShootDefense == 0 &&
            Spell == 0 &&
            Speed == 0 &&
            Luck == 0 &&
            Defense == 0 &&
            Protection == 0 &&
            Initiative == 0 &&
            Morale == 0;

        public UnitStatsData ApplyTo(UnitStatsData baseStats, int level)
        {
            int clampedLevel = level < 0 ? 0 : level;
            return new UnitStatsData
            {
                Life = baseStats.Life + (Life * clampedLevel),
                Attack = baseStats.Attack + (Attack * clampedLevel),
                Shoot = baseStats.Shoot + (Shoot * clampedLevel),
                ShootRange = baseStats.ShootRange + (ShootRange * clampedLevel),
                ShootDefense = baseStats.ShootDefense + (ShootDefense * clampedLevel),
                Spell = baseStats.Spell + (Spell * clampedLevel),
                Speed = baseStats.Speed + (Speed * clampedLevel),
                Luck = baseStats.Luck + (Luck * clampedLevel),
                Defense = baseStats.Defense + (Defense * clampedLevel),
                Protection = baseStats.Protection + (Protection * clampedLevel),
                Initiative = baseStats.Initiative + (Initiative * clampedLevel),
                Morale = baseStats.Morale + (Morale * clampedLevel),
                ActionPoints = baseStats.ActionPoints,
                DeckCapacity = baseStats.DeckCapacity,
                DrawCapacity = baseStats.DrawCapacity
            };
        }
    }
}
