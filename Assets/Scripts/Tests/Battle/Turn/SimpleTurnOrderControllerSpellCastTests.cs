using NUnit.Framework;
using UnityEngine;
using SevenBattles.Battle.Turn;
using SevenBattles.Battle.Units;
using SevenBattles.Battle.Board;
using SevenBattles.Battle.Spells;
using SevenBattles.Core.Battle;
using SevenBattles.Core.Units;
using System.Reflection;

namespace SevenBattles.Tests.Battle
{
    public class SimpleTurnOrderControllerSpellCastTests
    {
        [Test]
        public void TryExecuteSpellCast_DamagesEnemy_AndConsumesApCost()
        {
            var boardGo = new GameObject("Board");
            var board = boardGo.AddComponent<WorldPerspectiveBoard>();
            SetPrivate(board, "_columns", 6);
            SetPrivate(board, "_rows", 6);
            CallPrivate(board, "RebuildGrid");

            var ctrlGo = new GameObject("TurnController");
            // Add dependencies first so Awake finds them
            var spellCtrl = ctrlGo.AddComponent<BattleSpellController>();
            SetPrivate(spellCtrl, "_board", board);

            var ctrl = ctrlGo.AddComponent<SimpleTurnOrderController>();
            SetPrivate(ctrl, "_board", board);
            // No manual injection of _spellController needed due to Awake auto-wire

            var playerDef = ScriptableObject.CreateInstance<UnitDefinition>();
            var enemyDef = ScriptableObject.CreateInstance<UnitDefinition>();

            var playerGo = new GameObject("PlayerUnit");
            var playerStats = playerGo.AddComponent<UnitStats>();
            playerStats.ApplyBase(new UnitStatsData { Life = 10, ActionPoints = 3, Speed = 1, Initiative = 10, Spell = 0 });
            UnitBattleMetadata.Ensure(playerGo, true, playerDef, new Vector2Int(2, 2));

            var enemyGo = new GameObject("EnemyUnit");
            var enemyStats = enemyGo.AddComponent<UnitStats>();
            enemyStats.ApplyBase(new UnitStatsData { Life = 10, ActionPoints = 1, Speed = 1, Initiative = 1 });
            UnitBattleMetadata.Ensure(enemyGo, false, enemyDef, new Vector2Int(4, 2));

            CallPrivate(ctrl, "BeginBattle");

            var vfxPrefab = new GameObject("VfxPrefab", typeof(ParticleSystem));

            var spell = ScriptableObject.CreateInstance<SpellDefinition>();
            spell.ActionPointCost = 2;
            spell.TargetFilter = SpellTargetFilter.EnemyUnit;
            spell.MinCastRange = 1;
            spell.MaxCastRange = 4;
            spell.PrimaryAmountKind = SpellPrimaryAmountKind.Damage;
            spell.PrimaryBaseAmount = 5;
            spell.PrimarySpellStatScaling = 0f;
            spell.TargetVfxPrefab = vfxPrefab;
            spell.TargetVfxScaleMultiplier = 2f;
            spell.TargetVfxSortingLayerOverride = "Default";
            spell.TargetVfxSortingOrderOffset = 123;

            Assert.AreEqual(3, ctrl.ActiveUnitCurrentActionPoints);
            Assert.AreEqual(10, enemyStats.Life);

            CallPrivate(ctrl, "TryExecuteSpellCast", spell, new Vector2Int(4, 2));

            Assert.AreEqual(1, ctrl.ActiveUnitCurrentActionPoints, "Should consume ActionPointCost.");
            Assert.AreEqual(5, enemyStats.Life, "Should apply primary damage amount.");

            var vfxClone = GameObject.Find("VfxPrefab(Clone)");
            Assert.IsNotNull(vfxClone, "Should instantiate target VFX prefab.");
            var pr = vfxClone.GetComponentInChildren<ParticleSystemRenderer>(true);
            Assert.IsNotNull(pr);
            Assert.AreEqual("Default", pr.sortingLayerName);
            Assert.GreaterOrEqual(pr.sortingOrder, 123);
            Assert.AreEqual(Vector3.one * 2f, vfxClone.transform.localScale);

            if (vfxClone != null) Object.DestroyImmediate(vfxClone);
            Object.DestroyImmediate(ctrlGo);
            Object.DestroyImmediate(boardGo);
            Object.DestroyImmediate(playerGo);
            Object.DestroyImmediate(enemyGo);
            Object.DestroyImmediate(playerDef);
            Object.DestroyImmediate(enemyDef);
        }

        [Test]
        public void TryExecuteSpellCast_HealsFriendly_ClampedToMaxLife()
        {
            var boardGo = new GameObject("Board");
            var board = boardGo.AddComponent<WorldPerspectiveBoard>();
            SetPrivate(board, "_columns", 6);
            SetPrivate(board, "_rows", 6);
            CallPrivate(board, "RebuildGrid");

            var ctrlGo = new GameObject("TurnController");
            // Add dependencies first so Awake finds them
            var spellCtrl = ctrlGo.AddComponent<BattleSpellController>();
            SetPrivate(spellCtrl, "_board", board);

            var ctrl = ctrlGo.AddComponent<SimpleTurnOrderController>();
            SetPrivate(ctrl, "_board", board);
            // No manual injection of _spellController needed due to Awake auto-wire

            var playerDef = ScriptableObject.CreateInstance<UnitDefinition>();

            var playerGo = new GameObject("PlayerUnit");
            var playerStats = playerGo.AddComponent<UnitStats>();
            playerStats.ApplyBase(new UnitStatsData { Life = 10, ActionPoints = 3, Speed = 1, Initiative = 10, Spell = 0 });
            UnitBattleMetadata.Ensure(playerGo, true, playerDef, new Vector2Int(2, 2));

            CallPrivate(ctrl, "BeginBattle");

            playerStats.TakeDamage(6);
            Assert.AreEqual(4, playerStats.Life);
            Assert.AreEqual(10, playerStats.MaxLife);

            var vfxPrefab = new GameObject("VfxPrefabHeal", typeof(ParticleSystem));

            var heal = ScriptableObject.CreateInstance<SpellDefinition>();
            heal.ActionPointCost = 1;
            heal.TargetFilter = SpellTargetFilter.FriendlyUnit;
            heal.MinCastRange = 0;
            heal.MaxCastRange = 3;
            heal.PrimaryAmountKind = SpellPrimaryAmountKind.Heal;
            heal.PrimaryBaseAmount = 20;
            heal.PrimarySpellStatScaling = 0f;
            heal.TargetVfxPrefab = vfxPrefab;
            heal.TargetVfxScaleMultiplier = 0.5f;
            heal.TargetVfxSortingLayerOverride = "Default";
            heal.TargetVfxSortingOrderOffset = 5;

            CallPrivate(ctrl, "TryExecuteSpellCast", heal, new Vector2Int(2, 2));

            Assert.AreEqual(10, playerStats.Life, "Heal should clamp to MaxLife.");
            Assert.AreEqual(2, ctrl.ActiveUnitCurrentActionPoints);

            Object.DestroyImmediate(heal);
            var vfxClone = GameObject.Find("VfxPrefabHeal(Clone)");
            if (vfxClone != null) Object.DestroyImmediate(vfxClone);
            Object.DestroyImmediate(vfxPrefab);
            Object.DestroyImmediate(ctrlGo);
            Object.DestroyImmediate(boardGo);
            Object.DestroyImmediate(playerGo);
            Object.DestroyImmediate(playerDef);
        }

        [Test]
        public void TryExecuteSpellCast_DoesNotAllowRecastInSameTurn()
        {
            var boardGo = new GameObject("Board");
            var board = boardGo.AddComponent<WorldPerspectiveBoard>();
            SetPrivate(board, "_columns", 6);
            SetPrivate(board, "_rows", 6);
            CallPrivate(board, "RebuildGrid");

            var ctrlGo = new GameObject("TurnController");
            var spellCtrl = ctrlGo.AddComponent<BattleSpellController>();
            SetPrivate(spellCtrl, "_board", board);

            var ctrl = ctrlGo.AddComponent<SimpleTurnOrderController>();
            SetPrivate(ctrl, "_board", board);

            var playerDef = ScriptableObject.CreateInstance<UnitDefinition>();
            var enemyDef = ScriptableObject.CreateInstance<UnitDefinition>();

            var playerGo = new GameObject("PlayerUnit");
            var playerStats = playerGo.AddComponent<UnitStats>();
            playerStats.ApplyBase(new UnitStatsData { Life = 10, ActionPoints = 3, Speed = 1, Initiative = 10, Spell = 0 });
            UnitBattleMetadata.Ensure(playerGo, true, playerDef, new Vector2Int(2, 2));

            var enemyGo = new GameObject("EnemyUnit");
            var enemyStats = enemyGo.AddComponent<UnitStats>();
            enemyStats.ApplyBase(new UnitStatsData { Life = 10, ActionPoints = 1, Speed = 1, Initiative = 1 });
            UnitBattleMetadata.Ensure(enemyGo, false, enemyDef, new Vector2Int(4, 2));

            CallPrivate(ctrl, "BeginBattle");

            var spell = ScriptableObject.CreateInstance<SpellDefinition>();
            spell.ActionPointCost = 1;
            spell.TargetFilter = SpellTargetFilter.EnemyUnit;
            spell.MinCastRange = 1;
            spell.MaxCastRange = 4;
            spell.PrimaryAmountKind = SpellPrimaryAmountKind.Damage;
            spell.PrimaryBaseAmount = 3;
            spell.PrimarySpellStatScaling = 0f;

            CallPrivate(ctrl, "TryExecuteSpellCast", spell, new Vector2Int(4, 2));
            int apAfterFirst = ctrl.ActiveUnitCurrentActionPoints;
            int enemyLifeAfterFirst = enemyStats.Life;

            CallPrivate(ctrl, "TryExecuteSpellCast", spell, new Vector2Int(4, 2));

            Assert.AreEqual(apAfterFirst, ctrl.ActiveUnitCurrentActionPoints, "Second cast should not consume action points.");
            Assert.AreEqual(enemyLifeAfterFirst, enemyStats.Life, "Second cast should not apply damage.");
            Assert.IsTrue(ctrl.IsActiveUnitSpellSpentThisTurn(spell), "Spell should be marked spent after first cast.");

            Object.DestroyImmediate(spell);
            Object.DestroyImmediate(ctrlGo);
            Object.DestroyImmediate(boardGo);
            Object.DestroyImmediate(playerGo);
            Object.DestroyImmediate(enemyGo);
            Object.DestroyImmediate(playerDef);
            Object.DestroyImmediate(enemyDef);
        }

        [Test]
        public void TryExecuteSpellCast_EphemeralSpell_RemovesFromDeckAfterCast()
        {
            var boardGo = new GameObject("Board");
            var board = boardGo.AddComponent<WorldPerspectiveBoard>();
            SetPrivate(board, "_columns", 6);
            SetPrivate(board, "_rows", 6);
            CallPrivate(board, "RebuildGrid");

            var ctrlGo = new GameObject("TurnController");
            var spellCtrl = ctrlGo.AddComponent<BattleSpellController>();
            SetPrivate(spellCtrl, "_board", board);

            var ctrl = ctrlGo.AddComponent<SimpleTurnOrderController>();
            SetPrivate(ctrl, "_board", board);

            var playerDef = ScriptableObject.CreateInstance<UnitDefinition>();
            var enemyDef = ScriptableObject.CreateInstance<UnitDefinition>();

            var playerGo = new GameObject("PlayerUnit");
            var playerStats = playerGo.AddComponent<UnitStats>();
            playerStats.ApplyBase(new UnitStatsData { Life = 10, ActionPoints = 3, Speed = 1, Initiative = 10, Spell = 0 });
            UnitBattleMetadata.Ensure(playerGo, true, playerDef, new Vector2Int(2, 2));

            var enemyGo = new GameObject("EnemyUnit");
            var enemyStats = enemyGo.AddComponent<UnitStats>();
            enemyStats.ApplyBase(new UnitStatsData { Life = 10, ActionPoints = 1, Speed = 1, Initiative = 1 });
            UnitBattleMetadata.Ensure(enemyGo, false, enemyDef, new Vector2Int(4, 2));

            var spell = ScriptableObject.CreateInstance<SpellDefinition>();
            spell.IsEphemeral = true;
            spell.ActionPointCost = 1;
            spell.TargetFilter = SpellTargetFilter.EnemyUnit;
            spell.MinCastRange = 1;
            spell.MaxCastRange = 4;
            spell.PrimaryAmountKind = SpellPrimaryAmountKind.Damage;
            spell.PrimaryBaseAmount = 1;
            spell.PrimarySpellStatScaling = 0f;

            UnitSpellDeck.Ensure(playerGo).Configure(new[] { spell }, deckCapacity: 0, drawCapacity: 0);

            CallPrivate(ctrl, "BeginBattle");
            Assert.IsTrue(System.Array.Exists(ctrl.ActiveUnitSpells, s => ReferenceEquals(s, spell)));

            CallPrivate(ctrl, "TryExecuteSpellCast", spell, new Vector2Int(4, 2));

            Assert.IsFalse(System.Array.Exists(ctrl.ActiveUnitSpells, s => ReferenceEquals(s, spell)));
            var nextDraw = playerGo.GetComponent<UnitSpellDeck>().DrawForTurn();
            Assert.IsFalse(System.Array.Exists(nextDraw, s => ReferenceEquals(s, spell)));

            Object.DestroyImmediate(spell);
            Object.DestroyImmediate(ctrlGo);
            Object.DestroyImmediate(boardGo);
            Object.DestroyImmediate(playerGo);
            Object.DestroyImmediate(enemyGo);
            Object.DestroyImmediate(playerDef);
            Object.DestroyImmediate(enemyDef);
        }

        private static void SetPrivate(object obj, string field, object value)
        {
            var f = obj.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(f, $"Field '{field}' not found on {obj.GetType().Name}");
            f.SetValue(obj, value);
        }

        private static object CallPrivate(object obj, string method, params object[] args)
        {
            var m = obj.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(m, $"Method '{method}' not found on {obj.GetType().Name}");
            return m.Invoke(obj, args);
        }
    }
}
