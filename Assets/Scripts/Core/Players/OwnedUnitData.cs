using System;
using SevenBattles.Core.Battle;
using SevenBattles.Core.Units;

namespace SevenBattles.Core.Players
{
    /// <summary>
    /// Persistent player-owned unit entry with stable identity.
    /// </summary>
    [Serializable]
    public sealed class OwnedUnitData
    {
        public string OwnedUnitId;
        public UnitDefinition Definition;
        public int Level = UnitSpellLoadout.DefaultLevel;
        public int Xp;
        public SpellDefinition[] Spells = Array.Empty<SpellDefinition>();

        public int EffectiveLevel => Level > 0 ? Level : UnitSpellLoadout.DefaultLevel;
        public int EffectiveXp => Xp > 0 ? Xp : 0;
        public string UnitId => Definition != null ? Definition.Id : null;

        public static OwnedUnitData Clone(OwnedUnitData source)
        {
            if (source == null)
            {
                return null;
            }

            return new OwnedUnitData
            {
                OwnedUnitId = source.OwnedUnitId,
                Definition = source.Definition,
                Level = source.EffectiveLevel,
                Xp = source.EffectiveXp,
                Spells = source.Spells != null ? (SpellDefinition[])source.Spells.Clone() : Array.Empty<SpellDefinition>()
            };
        }
    }
}
