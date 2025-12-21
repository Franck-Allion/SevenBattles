using UnityEngine;

namespace SevenBattles.Core.Battle
{
    [CreateAssetMenu(menuName = "SevenBattles/Spells/Spell Definition", fileName = "SpellDefinition")]
    public sealed class SpellDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable identifier for save/load and referencing (e.g., 'spell.firebolt').")]
        public string Id;

        [Header("Presentation")]
        [Tooltip("Display name of the spell (data-only; UI should localize when shown).")]
        public string Name;

        [TextArea]
        [Tooltip("Description of the spell effect (data-only; UI should localize when shown).")]
        public string Description;

        [Tooltip("Localization key for the spell description (e.g., Table: UI.Common, Entry: Spells.Firebolt.Description).")]
        public string DescriptionLocalizationKey;

        [Tooltip("Icon displayed in the spell interface.")]
        public Sprite Icon;

        [Header("Cost")]
        [Min(0)]
        [Tooltip("Action Points cost required to cast this spell.")]
        public int ActionPointCost;
    }
}
