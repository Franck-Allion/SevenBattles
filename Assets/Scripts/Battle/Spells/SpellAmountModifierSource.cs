using UnityEngine;
using SevenBattles.Core.Battle;

namespace SevenBattles.Battle.Spells
{
    public sealed class SpellAmountModifierSource : MonoBehaviour, ISpellAmountModifierProvider
    {
        [Header("Filter (optional)")]
        [SerializeField, Tooltip("If set, only applies to a spell with this stable id (e.g., 'spell.firebolt').")]
        private string _spellIdFilter;

        [SerializeField, Tooltip("If set (not None), only applies to spells with this primary amount kind.")]
        private SpellPrimaryAmountKind _kindFilter = SpellPrimaryAmountKind.None;

        [SerializeField, Tooltip("If set (not None), only applies to spells with this damage element.")]
        private DamageElement _elementFilter = DamageElement.None;

        [Header("Modifier")]
        [SerializeField, Tooltip("Flat amount added after base+scaling (can be negative for debuffs).")]
        private int _flatBonus;

        [SerializeField, Tooltip("Multiplier applied after the flat bonus (1 = no change).")]
        private float _multiplier = 1f;

        [SerializeField, Tooltip("Minimum clamp for the final amount.")]
        private int _minAmount = 0;

        public void ModifySpellAmount(SpellDefinition spell, ref SpellAmountCalculationContext context)
        {
            if (spell == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(_spellIdFilter) && !string.Equals(spell.Id, _spellIdFilter, System.StringComparison.Ordinal))
            {
                return;
            }

            if (_kindFilter != SpellPrimaryAmountKind.None && context.Kind != _kindFilter)
            {
                return;
            }

            if (_elementFilter != DamageElement.None && context.Element != _elementFilter)
            {
                return;
            }

            float multiplier = Mathf.Approximately(_multiplier, 0f) ? 0f : _multiplier;
            context.Amount = Mathf.RoundToInt((context.Amount + _flatBonus) * multiplier);
            context.Amount = Mathf.Max(_minAmount, context.Amount);
        }
    }
}

