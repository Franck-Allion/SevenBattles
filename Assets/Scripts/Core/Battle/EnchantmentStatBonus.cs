using System;
using UnityEngine;

namespace SevenBattles.Core.Battle
{
    public enum EnchantmentTargetScope
    {
        AllUnits = 0,
        FriendlyUnits = 1,
        EnemyUnits = 2
    }

    [Serializable]
    public struct EnchantmentStatBonus
    {
        public int Life;
        public int Attack;
        public int Shoot;
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
            Spell == 0 &&
            Speed == 0 &&
            Luck == 0 &&
            Defense == 0 &&
            Protection == 0 &&
            Initiative == 0 &&
            Morale == 0;

        public static EnchantmentStatBonus Add(EnchantmentStatBonus a, EnchantmentStatBonus b)
        {
            return new EnchantmentStatBonus
            {
                Life = a.Life + b.Life,
                Attack = a.Attack + b.Attack,
                Shoot = a.Shoot + b.Shoot,
                Spell = a.Spell + b.Spell,
                Speed = a.Speed + b.Speed,
                Luck = a.Luck + b.Luck,
                Defense = a.Defense + b.Defense,
                Protection = a.Protection + b.Protection,
                Initiative = a.Initiative + b.Initiative,
                Morale = a.Morale + b.Morale
            };
        }
    }
}
