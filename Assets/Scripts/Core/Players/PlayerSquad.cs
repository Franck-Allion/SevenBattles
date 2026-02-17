using System;
using UnityEngine;
using UnityEngine.Localization;
using SevenBattles.Core.Battle;

namespace SevenBattles.Core.Players
{
    [CreateAssetMenu(menuName = "SevenBattles/Player Squad", fileName = "PlayerSquad")]
    public class PlayerSquad : ScriptableObject
    {
        [Header("Presentation")]
        [SerializeField, Tooltip("Localized display name used by UI panels for this squad.")]
        private LocalizedString _localizedSquadName;

        [Header("Battle Setup")]
        [Tooltip("Per-unit spell loadouts for this squad.")]
        public UnitSpellLoadout[] UnitLoadouts = Array.Empty<UnitSpellLoadout>();

        public LocalizedString LocalizedSquadName => _localizedSquadName;

        public UnitSpellLoadout[] GetLoadouts()
        {
            return UnitLoadouts ?? Array.Empty<UnitSpellLoadout>();
        }
    }
}
