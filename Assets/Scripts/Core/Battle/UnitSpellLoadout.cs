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
        [Tooltip("Unit type for this squad slot.")]
        public UnitDefinition Definition;

        [Tooltip("Spells assigned to this specific unit (per-squad loadout).")]
        public SpellDefinition[] Spells;
    }
}
