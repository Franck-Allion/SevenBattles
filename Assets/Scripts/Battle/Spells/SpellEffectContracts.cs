using System;
using UnityEngine;
using SevenBattles.Battle.Units;
using SevenBattles.Core.Battle;

namespace SevenBattles.Battle.Spells
{
    public enum SpellRemovalPolicy
    {
        None = 0,
        EphemeralOnly = 1,
        Always = 2
    }

    public interface ISpellUnitProvider
    {
        int Count { get; }
        UnitBattleMetadata GetMetadata(int index);
        UnitStats GetStats(int index);
        bool IsUnitAtTile(int index, Vector2Int tile);
    }

    public readonly struct SpellTargetSelection
    {
        public readonly SpellTargetingMode Mode;
        public readonly Vector2Int Tile;
        public readonly int QuadIndex;

        private SpellTargetSelection(SpellTargetingMode mode, Vector2Int tile, int quadIndex)
        {
            Mode = mode;
            Tile = tile;
            QuadIndex = quadIndex;
        }

        public static SpellTargetSelection ForTile(Vector2Int tile)
        {
            return new SpellTargetSelection(SpellTargetingMode.UnitOrTile, tile, -1);
        }

        public static SpellTargetSelection ForQuad(int quadIndex)
        {
            return new SpellTargetSelection(SpellTargetingMode.Enchantment, default, quadIndex);
        }
    }

    public readonly struct SpellCastContext
    {
        public readonly BattleSpellController SpellController;
        public readonly BattleEnchantmentController EnchantmentController;
        public readonly ISpellUnitProvider Units;
        public readonly UnitBattleMetadata CasterMeta;
        public readonly UnitStats CasterStats;

        public SpellCastContext(
            BattleSpellController spellController,
            BattleEnchantmentController enchantmentController,
            ISpellUnitProvider units,
            UnitBattleMetadata casterMeta,
            UnitStats casterStats)
        {
            SpellController = spellController;
            EnchantmentController = enchantmentController;
            Units = units;
            CasterMeta = casterMeta;
            CasterStats = casterStats;
        }
    }

    public readonly struct SpellCastCallbacks
    {
        public readonly Action OnStart;
        public readonly Action<int> OnApConsumed;
        public readonly Action OnStatsChanged;
        public readonly Action<UnitBattleMetadata> OnUnitDied;
        public readonly Action OnComplete;

        public SpellCastCallbacks(
            Action onStart,
            Action<int> onApConsumed,
            Action onStatsChanged,
            Action<UnitBattleMetadata> onUnitDied,
            Action onComplete)
        {
            OnStart = onStart;
            OnApConsumed = onApConsumed;
            OnStatsChanged = onStatsChanged;
            OnUnitDied = onUnitDied;
            OnComplete = onComplete;
        }
    }

    public interface ISpellEffectHandler
    {
        SpellEffectKind Kind { get; }
        SpellTargetingMode TargetingMode { get; }
        bool UsesActiveEnchantments { get; }
        SpellRemovalPolicy RemovalPolicy { get; }

        bool HasAvailableTargets(SpellDefinition spell, SpellCastContext context);
        bool IsTargetValid(SpellDefinition spell, SpellCastContext context, SpellTargetSelection target);
        void Execute(SpellDefinition spell, SpellCastContext context, SpellTargetSelection target, SpellCastCallbacks callbacks);
    }
}
