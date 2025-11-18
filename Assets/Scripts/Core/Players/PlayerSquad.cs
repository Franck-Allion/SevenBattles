using UnityEngine;
using SevenBattles.Core.Units;

namespace SevenBattles.Core.Players
{
    [CreateAssetMenu(menuName = "SevenBattles/Player Squad", fileName = "PlayerSquad")]
    public class PlayerSquad : ScriptableObject
    {
        [Tooltip("1..8 wizard definitions that make up this player's squad.")]
        public UnitDefinition[] Wizards = new UnitDefinition[3];
    }
}

