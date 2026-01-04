using UnityEngine;
using SevenBattles.Core.Battle;

namespace SevenBattles.Core.Units
{
    [CreateAssetMenu(menuName = "SevenBattles/Wizard Definition", fileName = "WizardDefinition")]
    public class UnitDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string Id;

        [Header("Presentation")]
        public Sprite Portrait;

        [Header("Progression")]
        [Min(0f), Tooltip("Threat factor used for end-of-battle XP calculation. This is data-driven and can be tuned per unit type (e.g., Common=0.7, Uncommon=1.0, Rare=1.4, Epic=2.0, Legend=3.0).")]
        public float ThreatFactor = 1f;

        [Min(1), Tooltip("Maximum level this unit can reach.")]
        public int MaxLevel = 10;

        [Tooltip("XP thresholds required to advance from level N to level N+1. Index 0 = level 1 -> 2.")]
        public int[] XpToNextLevel = System.Array.Empty<int>();

        [Header("Audio")]
        [Tooltip("Optional SFX played when a unit of this type dies (e.g., Assets/Art/SFX/Wizard_Death.wav).")]
        public AudioClip DeathSfx;
        [Range(0f, 1.5f)]
        [Tooltip("Volume multiplier for the death SFX (0 = silent, 1 = default, >1 = boosted).")]
        public float DeathSfxVolume = 1f;

        [Header("Prefab")]
        public GameObject Prefab;

        [Header("Base Stats")]
        public UnitStatsData BaseStats;

        [Header("Level Scaling")]
        public UnitLevelBonusData LevelBonus;

        [Header("Spells (Legacy)")]
        [Tooltip("Legacy default spells for this unit type. Runtime uses per-unit loadouts; only used as a fallback when no per-unit spells are assigned.")]
        public SpellDefinition[] Spells;
    }
}
