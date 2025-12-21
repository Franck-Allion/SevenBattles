using NUnit.Framework;
using UnityEngine;
using SevenBattles.Battle.Turn;
using SevenBattles.Battle.Units;
using SevenBattles.Battle.Board;
using SevenBattles.Battle.Spells;
using SevenBattles.Core.Battle;
using SevenBattles.Core.Units;

namespace SevenBattles.Tests.Battle
{
    public class SpellAmountPreviewTests
    {
        [Test]
        public void TryGetActiveUnitSpellAmountPreview_AppliesScalingAndModifiers()
        {
            var boardGo = new GameObject("Board");
            var board = boardGo.AddComponent<WorldPerspectiveBoard>();
            SetPrivate(board, "_columns", 3);
            SetPrivate(board, "_rows", 3);
            CallPrivate(board, "RebuildGrid");

            var ctrlGo = new GameObject("TurnController");
            var ctrl = ctrlGo.AddComponent<SimpleTurnOrderController>();
            SetPrivate(ctrl, "_board", board);

            var def = ScriptableObject.CreateInstance<UnitDefinition>();

            var playerGo = new GameObject("PlayerUnit");
            var playerStats = playerGo.AddComponent<UnitStats>();
            playerStats.ApplyBase(new UnitStatsData { Life = 10, ActionPoints = 1, Spell = 2, Speed = 1, Initiative = 10 });
            UnitBattleMetadata.Ensure(playerGo, true, def, new Vector2Int(0, 0));

            var enemyGo = new GameObject("EnemyUnit");
            var enemyStats = enemyGo.AddComponent<UnitStats>();
            enemyStats.ApplyBase(new UnitStatsData { Life = 10, ActionPoints = 1, Spell = 0, Speed = 1, Initiative = 5 });
            UnitBattleMetadata.Ensure(enemyGo, false, def, new Vector2Int(1, 0));

            CallPrivate(ctrl, "BeginBattle");

            var spell = ScriptableObject.CreateInstance<SpellDefinition>();
            spell.Id = "spell.firebolt";
            spell.PrimaryAmountKind = SpellPrimaryAmountKind.Damage;
            spell.PrimaryDamageElement = DamageElement.Fire;
            spell.PrimaryBaseAmount = 5;
            spell.PrimarySpellStatScaling = 1f;

            Assert.IsTrue(ctrl.TryGetActiveUnitSpellAmountPreview(spell, out var preview));
            Assert.AreEqual(5, preview.BaseAmount);
            Assert.AreEqual(7, preview.ModifiedAmount);

            var modifier = playerGo.AddComponent<SpellAmountModifierSource>();
            SetPrivate(modifier, "_flatBonus", -4);
            SetPrivate(modifier, "_multiplier", 1f);

            Assert.IsTrue(ctrl.TryGetActiveUnitSpellAmountPreview(spell, out preview));
            Assert.AreEqual(5, preview.BaseAmount);
            Assert.AreEqual(3, preview.ModifiedAmount);

            Object.DestroyImmediate(ctrlGo);
            Object.DestroyImmediate(boardGo);
            Object.DestroyImmediate(playerGo);
            Object.DestroyImmediate(enemyGo);
            Object.DestroyImmediate(def);
            Object.DestroyImmediate(spell);
        }

        private static void SetPrivate(object target, string fieldName, object value)
        {
            var type = target.GetType();
            var field = type.GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' was not found on type '{type.FullName}'.");
            field.SetValue(target, value);
        }

        private static void CallPrivate(object target, string methodName)
        {
            var type = target.GetType();
            var method = type.GetMethod(methodName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Method '{methodName}' was not found on type '{type.FullName}'.");
            method.Invoke(target, null);
        }
    }
}

