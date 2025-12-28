using System;
using UnityEngine;
using SevenBattles.Core.Units;

namespace SevenBattles.Core.Battle
{
    /// <summary>
    /// Per-unit spell assignment for a squad slot.
    /// </summary>
    [Serializable]
    public sealed class UnitSpellLoadout
    {
        public const int DefaultLevel = 1;

        [Tooltip("Unit type for this squad slot.")]
        public UnitDefinition Definition;

        [Tooltip("Unit level for this squad slot (minimum 1).")]
        public int Level = DefaultLevel;

        [Tooltip("Spells assigned to this specific unit (per-squad loadout).")]
        public SpellDefinition[] Spells;

        public int EffectiveLevel => Level > 0 ? Level : DefaultLevel;
    }
}
