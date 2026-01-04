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

        [Tooltip("Current XP for this unit. XP is tracked towards the next level; it is reduced on level-up.")]
        public int Xp;

        [Tooltip("Spells assigned to this specific unit (per-squad loadout).")]
        public SpellDefinition[] Spells;

        public int EffectiveLevel => Level > 0 ? Level : DefaultLevel;

        public int EffectiveXp => Mathf.Max(0, Xp);

        public static UnitSpellLoadout Clone(UnitSpellLoadout source)
        {
            if (source == null)
            {
                return null;
            }

            return new UnitSpellLoadout
            {
                Definition = source.Definition,
                Level = source.EffectiveLevel,
                Xp = source.EffectiveXp,
                Spells = source.Spells != null ? (SpellDefinition[])source.Spells.Clone() : null
            };
        }

        public static UnitSpellLoadout[] CloneArray(UnitSpellLoadout[] source)
        {
            if (source == null || source.Length == 0)
            {
                return Array.Empty<UnitSpellLoadout>();
            }

            var result = new UnitSpellLoadout[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                result[i] = Clone(source[i]);
            }

            return result;
        }
    }
}
