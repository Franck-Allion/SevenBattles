using UnityEngine;

namespace SevenBattles.Battle.Combat
{
    /// <summary>
    /// Pure damage calculation logic for melee combat.
    /// Uses attack vs defense mitigation formula with random variance.
    /// </summary>
    public static class BattleDamageCalculator
    {
        /// <summary>
        /// Calculates damage dealt by an attacker to a defender.
        /// </summary>
        /// <param name="attack">Attacker's attack stat (must be > 0 to deal damage)</param>
        /// <param name="defense">Defender's defense stat (reduces damage via mitigation)</param>
        /// <returns>Final damage amount (integer, >= 0)</returns>
        public static int Calculate(int attack, int defense)
        {
            // No damage if the attacker has no attack power.
            if (attack <= 0)
            {
                return 0;
            }

            // Apply random variance (0.95 to 1.05)
            float variance = Random.Range(0.95f, 1.05f);
            float rawDamage = attack * variance;

            // If defense is zero or negative, treat it as "no mitigation" and
            // guarantee that at least 1 point of damage is dealt.
            if (defense <= 0)
            {
                return Mathf.Max(1, Mathf.FloorToInt(rawDamage));
            }

            // Calculate mitigation – higher defense reduces effective damage.
            float mitigation = (float)attack / (attack + defense);

            // Final damage (minimum 1 if attack > 0)
            float finalDamage = rawDamage * mitigation;

            // Round down to integer
            return Mathf.Max(1, Mathf.FloorToInt(finalDamage));
        }
    }
}
