using System;
using SevenBattles.Core.Battle;

namespace SevenBattles.Core
{
    /// <summary>
    /// Cross-domain contract for spell selection so UI can drive battle targeting
    /// without referencing Battle-domain implementations directly.
    /// </summary>
    public interface ISpellSelectionController
    {
        /// <summary>
        /// Currently selected spell for targeting/casting. Null means no spell selected.
        /// </summary>
        SpellDefinition SelectedSpell { get; }

        /// <summary>
        /// Raised whenever <see cref="SelectedSpell"/> changes.
        /// </summary>
        event Action SelectedSpellChanged;

        /// <summary>
        /// Selects a spell (or clears selection by passing null).
        /// Implementations may ignore spells not available to the active unit.
        /// </summary>
        void SetSelectedSpell(SpellDefinition spell);
    }
}

