using UnityEngine;

namespace SevenBattles.Core.Battle
{
    public enum SpellTargetFilter
    {
        EnemyUnit = 0,
        FriendlyUnit = 1,
        AnyUnit = 2,
        EmptyTile = 3,
        AnyTile = 4
    }

    public enum SpellPrimaryAmountKind
    {
        None = 0,
        Damage = 1,
        Heal = 2
    }

    public enum DamageElement
    {
        None = 0,
        Fire = 1,
        Arcane = 2,
        Frost = 3,
        Lightning = 4,
        Poison = 5,
        Holy = 6,
        Shadow = 7
    }

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

        [Header("Targeting")]
        [Tooltip("What this spell can target when selecting a tile on the board.")]
        public SpellTargetFilter TargetFilter = SpellTargetFilter.EnemyUnit;

        [Min(0)]
        [Tooltip("Minimum Manhattan range (in tiles) from the caster tile to the target tile.")]
        public int MinCastRange = 1;

        [Min(0)]
        [Tooltip("Maximum Manhattan range (in tiles) from the caster tile to the target tile.")]
        public int MaxCastRange = 3;

        [Tooltip("Optional cursor texture used while this spell is selected for targeting. If null, the default cursor is used.")]
        public Texture2D TargetingCursorTexture;

        [Tooltip("Hotspot offset for the targeting cursor (typically center of the texture).")]
        public Vector2 TargetingCursorHotspot = new Vector2(16f, 16f);

        [Header("VFX (optional)")]
        [Tooltip("Optional prefab instantiated at the target position when the spell is cast.")]
        public GameObject TargetVfxPrefab;

        [Min(0f)]
        [Tooltip("Optional lifetime in seconds before the target VFX instance is destroyed. 0 means 'do not auto-destroy'.")]
        public float TargetVfxLifetimeSeconds = 2f;

        [Min(0f)]
        [Tooltip("Multiplies the instantiated target VFX transform scale (uniform).")]
        public float TargetVfxScaleMultiplier = 1f;

        [Tooltip("Optional sorting layer override for the target VFX. When empty, the VFX inherits target/caster layer.")]
        public string TargetVfxSortingLayerOverride;

        [Tooltip("Sorting order offset added on top of the chosen base sorting order (target/caster).")]
        public int TargetVfxSortingOrderOffset = 25;

        [Header("SFX (optional)")]
        [Tooltip("Optional AudioClip played when the spell is cast.")]
        public AudioClip CastSfxClip;

        [Range(0f, 1.5f)]
        [Tooltip("Volume multiplier for CastSfxClip.")]
        public float CastSfxVolume = 1f;

        [Tooltip("If true, plays CastSfxClip at the target position; otherwise plays at the caster position.")]
        public bool CastSfxAtTarget = true;

        [Header("Primary Amount (optional)")]
        [Tooltip("Primary numeric effect previewed in UI (e.g., Damage for Firebolt).")]
        public SpellPrimaryAmountKind PrimaryAmountKind = SpellPrimaryAmountKind.None;

        [Tooltip("Element used when PrimaryAmountKind is Damage.")]
        public DamageElement PrimaryDamageElement = DamageElement.None;

        [Min(0)]
        [Tooltip("Base amount for the primary numeric effect before any scaling or modifiers.")]
        public int PrimaryBaseAmount;

        [Tooltip("Adds round(casterSpellStat * scaling) to PrimaryBaseAmount. Use this for player characteristics scaling.")]
        public float PrimarySpellStatScaling;
    }
}
