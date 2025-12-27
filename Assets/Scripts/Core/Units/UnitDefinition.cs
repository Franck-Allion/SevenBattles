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

        [Header("Spells (Legacy)")]
        [Tooltip("Legacy default spells for this unit type. Runtime uses per-unit loadouts; only used as a fallback when no per-unit spells are assigned.")]
        public SpellDefinition[] Spells;
    }
}
