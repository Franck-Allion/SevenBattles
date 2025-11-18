using System;
using UnityEngine.Serialization;

namespace SevenBattles.Core.Wizards
{
    [Serializable]
    public struct UnitStatsData
    {
        // Primary design stats for a unit (used by ScriptableObjects and runtime stats).
        [FormerlySerializedAs("MaxHP")]
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
        public int ActionPoints;

        // Backwards-compatible accessors so existing code/tests using old names keep working.
        public int MaxHP
        {
            get => Life;
            set => Life = value;
        }

    }
}
