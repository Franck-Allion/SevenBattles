using System;
using UnityEngine;
using SevenBattles.Core.Battle;

namespace SevenBattles.Core.Players
{
    [CreateAssetMenu(menuName = "SevenBattles/Player Squad", fileName = "PlayerSquad")]
    public class PlayerSquad : ScriptableObject
    {
        [Tooltip("Per-unit spell loadouts for this squad.")]
        public UnitSpellLoadout[] UnitLoadouts = Array.Empty<UnitSpellLoadout>();

        public UnitSpellLoadout[] GetLoadouts()
        {
            return UnitLoadouts ?? Array.Empty<UnitSpellLoadout>();
        }
    }
}
