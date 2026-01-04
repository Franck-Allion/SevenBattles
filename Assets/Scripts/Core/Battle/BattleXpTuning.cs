using UnityEngine;

namespace SevenBattles.Core.Battle
{
    [CreateAssetMenu(menuName = "SevenBattles/Battle/XP Tuning", fileName = "BattleXpTuning")]
    public class BattleXpTuning : ScriptableObject
    {
        [Header("Base XP")]
        [Min(0f), Tooltip("Global tuning value for XP per enemy before applying ThreatFactor and LevelFactor.")]
        public float BaseXpPerEnemy = 10f;

        [Tooltip("Optional override per difficulty index. If set and difficulty is in range, this value is used instead of BaseXpPerEnemy.")]
        public float[] BaseXpPerEnemyByDifficulty = System.Array.Empty<float>();

        [Header("Turn Factor (optional)")]
        [Tooltip("If enabled, Total XP is multiplied by clamp(TargetTurns / ActualTurns, 0.8, 1.2).")]
        public bool EnableTurnFactor = true;

        [Min(0), Tooltip("Fallback target turn count when TargetTurnsByDifficulty is not set or out of range.")]
        public int DefaultTargetTurns = 0;

        [Tooltip("Optional target turn counts per difficulty index.")]
        public int[] TargetTurnsByDifficulty = System.Array.Empty<int>();

        public float GetBaseXpPerEnemy(int difficulty)
        {
            if (BaseXpPerEnemyByDifficulty != null &&
                difficulty >= 0 &&
                difficulty < BaseXpPerEnemyByDifficulty.Length)
            {
                return Mathf.Max(0f, BaseXpPerEnemyByDifficulty[difficulty]);
            }

            return Mathf.Max(0f, BaseXpPerEnemy);
        }

        public int GetTargetTurns(int difficulty)
        {
            if (TargetTurnsByDifficulty != null &&
                difficulty >= 0 &&
                difficulty < TargetTurnsByDifficulty.Length)
            {
                return Mathf.Max(0, TargetTurnsByDifficulty[difficulty]);
            }

            return Mathf.Max(0, DefaultTargetTurns);
        }
    }
}

