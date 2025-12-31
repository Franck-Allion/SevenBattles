using System;
using SevenBattles.Core.Battle;

namespace SevenBattles.Core
{
    /// <summary>
    /// Cross-domain contract for inspecting a battlefield enchantment.
    /// </summary>
    public interface IEnchantmentInspectionController
    {
        event Action InspectedEnchantmentChanged;

        bool HasInspectedEnchantment { get; }
        SpellDefinition InspectedEnchantmentSpell { get; }
        bool InspectedEnchantmentIsPlayerControlledCaster { get; }

        bool TryGetInspectedEnchantmentSpellAmountPreview(out SpellAmountPreview preview);

        void ClearInspectedEnchantment();
    }
}
