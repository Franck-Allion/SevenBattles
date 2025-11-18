using System;

namespace SevenBattles.Core
{
    /// <summary>
    /// Lightweight value type describing the combat stats exposed to UI.
    /// Names follow the game design terminology (Life, Force, Shoot, etc.).
    /// </summary>
    [Serializable]
    public struct UnitStatsViewData
    {
        public int Life;
        public int MaxLife;
        public int Force;
        public int Shoot;
        public int Spell;
        public int Speed;
        public int Luck;
        public int Defense;
        public int Protection;
        public int Initiative;
        public int Morale;
    }
}
