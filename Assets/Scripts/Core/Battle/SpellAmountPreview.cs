using System;

namespace SevenBattles.Core.Battle
{
    [Serializable]
    public struct SpellAmountPreview
    {
        public SpellPrimaryAmountKind Kind;
        public DamageElement Element;
        public int BaseAmount;
        public int ModifiedAmount;
    }

    public struct SpellAmountCalculationContext
    {
        public SpellPrimaryAmountKind Kind;
        public DamageElement Element;
        public int BaseAmount;
        public int Amount;
        public int CasterSpellStat;
    }

    public interface ISpellAmountModifierProvider
    {
        void ModifySpellAmount(SpellDefinition spell, ref SpellAmountCalculationContext context);
    }
}

