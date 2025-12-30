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

    public enum SpellTargetingMode
    {
        UnitOrTile = 0,
        Enchantment = 1
    }

    public enum SpellEffectKind
    {
        Standard = 0,
        EnchantmentPlacement = 1,
        EnchantmentRemoval = 2
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

    public enum ProjectileOrientation
    {
        LookAtTarget = 0,
        FaceCamera2D = 1
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

        [Header("Lifecycle")]
        [Tooltip("If true, the spell is removed from the unit's deck after it is cast (battle-only).")]
        public bool IsEphemeral;

        [Header("Effect")]
        [Tooltip("Controls which runtime effect handler executes this spell. Enchantment spells are routed automatically.")]
        public SpellEffectKind EffectKind = SpellEffectKind.Standard;

        [Header("Enchantment (optional)")]
        [Tooltip("If true, this spell is a permanent battlefield enchantment.")]
        public bool IsEnchantment;

        [Tooltip("Sprite displayed on the battlefield when this enchantment is placed.")]
        public Sprite EnchantmentBoardSprite;

        [Tooltip("Target scope for this enchantment's permanent effect.")]
        public EnchantmentTargetScope EnchantmentTargetScope = EnchantmentTargetScope.AllUnits;

        [Tooltip("Permanent stat bonus applied while this enchantment is active.")]
        public EnchantmentStatBonus EnchantmentStatBonus;

        [Header("Enchantment VFX (optional)")]
        [Tooltip("Optional VFX prefab spawned when the enchantment is placed.")]
        public GameObject EnchantmentVfxPrefab;

        [Min(0f)]
        [Tooltip("Optional lifetime in seconds before the enchantment VFX instance is destroyed. 0 means 'do not auto-destroy'.")]
        public float EnchantmentVfxLifetimeSeconds = 2f;

        [Min(0f)]
        [Tooltip("Multiplies the instantiated enchantment VFX transform scale (uniform).")]
        public float EnchantmentVfxScaleMultiplier = 1f;

        [Tooltip("Additional local offset applied when spawning the enchantment VFX (board local space).")]
        public Vector2 EnchantmentVfxOffset;

        [Min(0f)]
        [Tooltip("Seconds to fade out the enchantment VFX before its lifetime ends. 0 disables fading.")]
        public float EnchantmentVfxFadeOutDurationSeconds;

        [Min(0f)]
        [Tooltip("Seconds to fade the enchantment sprite from 0 to 1 alpha when placed.")]
        public float EnchantmentAppearDurationSeconds = 0.35f;

        [Min(0f)]
        [Tooltip("Delay before starting the enchantment sprite fade-in (allows VFX to lead).")]
        public float EnchantmentAppearDelaySeconds = 0f;

        [Header("Targeting")]
        [Tooltip("Controls how the spell selects targets (unit/tile vs. active enchantment).")]
        public SpellTargetingMode TargetingMode = SpellTargetingMode.UnitOrTile;

        [Tooltip("What this spell can target when selecting a tile on the board.")]
        public SpellTargetFilter TargetFilter = SpellTargetFilter.EnemyUnit;

        [Tooltip("If true, the target must be in the same row or the same column as the caster (no diagonal targeting).")]
        public bool RequiresSameRowOrColumn;

        [Tooltip("If true, the line between caster and target must be clear (no unit between them). Only applies when RequiresSameRowOrColumn is enabled.")]
        public bool RequiresClearLineOfSight;

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

        [Header("Projectile (optional)")]
        [Tooltip("Optional projectile prefab instantiated at the caster and moved toward the target. If set, the spell's primary effect is applied on projectile impact.")]
        public GameObject ProjectilePrefab;

        [Min(0f)]
        [Tooltip("Optional override for HS_ProjectileMover2D.speed on the spawned projectile. 0 means 'keep prefab default'.")]
        public float ProjectileSpeedOverride;

        [Min(0f)]
        [Tooltip("Distance in world units to spawn the projectile in front of the caster (to avoid immediate self-collisions).")]
        public float ProjectileSpawnOffset = 0.15f;

        [Tooltip("Orientation used when spawning the projectile (3D look-at vs. flat 2D rotation).")]
        public ProjectileOrientation ProjectileOrientation = ProjectileOrientation.LookAtTarget;

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

        [Tooltip("Optional AudioClip played when the spell projectile impacts a valid target.")]
        public AudioClip ImpactSfxClip;

        [Range(0f, 1.5f)]
        [Tooltip("Volume multiplier for ImpactSfxClip.")]
        public float ImpactSfxVolume = 1f;

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
