using NUnit.Framework;
using UnityEngine;
using SevenBattles.Battle.Turn;
using SevenBattles.Battle.Units;
using SevenBattles.Battle.Board;
using SevenBattles.Core.Units;

namespace SevenBattles.Tests.Battle
{
    public class UnitAttackTests
    {
        [Test]
        public void AttackEligibility_RequiresAttackGreaterThanZero()
        {
            var boardGo = new GameObject("Board");
            var board = boardGo.AddComponent<WorldPerspectiveBoard>();

            SetPrivate(board, "_columns", 5);
            SetPrivate(board, "_rows", 5);
            SetPrivate(board, "_topLeft", new Vector2(0, 5));
            SetPrivate(board, "_topRight", new Vector2(5, 5));
            SetPrivate(board, "_bottomRight", new Vector2(5, 0));
            SetPrivate(board, "_bottomLeft", new Vector2(0, 0));
            CallPrivate(board, "RebuildGrid");

            var attackerGo = new GameObject("Attacker");
            var attackerStats = attackerGo.AddComponent<UnitStats>();
            attackerStats.ApplyBase(new UnitStatsData { Attack = 0, ActionPoints = 2, Speed = 2, Initiative = 10 });

            var targetGo = new GameObject("Target");
            var targetStats = targetGo.AddComponent<UnitStats>();
            targetStats.ApplyBase(new UnitStatsData { Attack = 5, Defense = 5, Life = 50, ActionPoints = 2, Speed = 2, Initiative = 5 });

            var def = ScriptableObject.CreateInstance<UnitDefinition>();
            def.Portrait = null;

            UnitBattleMetadata.Ensure(attackerGo, true, def, new Vector2Int(2, 2));
            UnitBattleMetadata.Ensure(targetGo, false, def, new Vector2Int(2, 3)); // Adjacent north

            var ctrlGo = new GameObject("TurnController");
            var ctrl = ctrlGo.AddComponent<SimpleTurnOrderController>();

            SetPrivate(ctrl, "_board", board);
            CallPrivate(ctrl, "BeginBattle");

            // Verify attack cannot be executed
            bool canAttack = (bool)CallPrivate(ctrl, "CanActiveUnitAttack");
            Assert.IsFalse(canAttack, "Unit with Attack=0 should not be able to attack");

            Object.DestroyImmediate(ctrlGo);
            Object.DestroyImmediate(attackerGo);
            Object.DestroyImmediate(targetGo);
            Object.DestroyImmediate(boardGo);
            Object.DestroyImmediate(def);
        }

        [Test]
        public void AttackEligibility_RequiresAtLeastOneAP()
        {
            var boardGo = new GameObject("Board");
            var board = boardGo.AddComponent<WorldPerspectiveBoard>();

            SetPrivate(board, "_columns", 5);
            SetPrivate(board, "_rows", 5);
            SetPrivate(board, "_topLeft", new Vector2(0, 5));
            SetPrivate(board, "_topRight", new Vector2(5, 5));
            SetPrivate(board, "_bottomRight", new Vector2(5, 0));
            SetPrivate(board, "_bottomLeft", new Vector2(0, 0));
            CallPrivate(board, "RebuildGrid");

            var attackerGo = new GameObject("Attacker");
            var attackerStats = attackerGo.AddComponent<UnitStats>();
            attackerStats.ApplyBase(new UnitStatsData { Attack = 10, ActionPoints = 2, Speed = 2, Initiative = 10 });

            var targetGo = new GameObject("Target");
            var targetStats = targetGo.AddComponent<UnitStats>();
            targetStats.ApplyBase(new UnitStatsData { Attack = 5, Defense = 5, Life = 50, ActionPoints = 2, Speed = 2, Initiative = 5 });

            var def = ScriptableObject.CreateInstance<UnitDefinition>();
            def.Portrait = null;

            UnitBattleMetadata.Ensure(attackerGo, true, def, new Vector2Int(2, 2));
            UnitBattleMetadata.Ensure(targetGo, false, def, new Vector2Int(2, 3));

            var ctrlGo = new GameObject("TurnController");
            var ctrl = ctrlGo.AddComponent<SimpleTurnOrderController>();

            SetPrivate(ctrl, "_board", board);
            CallPrivate(ctrl, "BeginBattle");

            // Consume all AP
            SetPrivate(ctrl, "_activeUnitCurrentActionPoints", 0);

            bool canAttack = (bool)CallPrivate(ctrl, "CanActiveUnitAttack");
            Assert.IsFalse(canAttack, "Unit with AP=0 should not be able to attack");

            Object.DestroyImmediate(ctrlGo);
            Object.DestroyImmediate(attackerGo);
            Object.DestroyImmediate(targetGo);
            Object.DestroyImmediate(boardGo);
            Object.DestroyImmediate(def);
        }

        [Test]
        public void AttackEligibility_RequiresAdjacentEnemy()
        {
            var boardGo = new GameObject("Board");
            var board = boardGo.AddComponent<WorldPerspectiveBoard>();

            SetPrivate(board, "_columns", 7);
            SetPrivate(board, "_rows", 7);
            SetPrivate(board, "_topLeft", new Vector2(0, 7));
            SetPrivate(board, "_topRight", new Vector2(7, 7));
            SetPrivate(board, "_bottomRight", new Vector2(7, 0));
            SetPrivate(board, "_bottomLeft", new Vector2(0, 0));
            CallPrivate(board, "RebuildGrid");

            var attackerGo = new GameObject("Attacker");
            var attackerStats = attackerGo.AddComponent<UnitStats>();
            attackerStats.ApplyBase(new UnitStatsData { Attack = 10, ActionPoints = 2, Speed = 2, Initiative = 10 });

            var targetGo = new GameObject("Target");
            var targetStats = targetGo.AddComponent<UnitStats>();
            targetStats.ApplyBase(new UnitStatsData { Attack = 5, Defense = 5, Life = 50, ActionPoints = 2, Speed = 2, Initiative = 5 });

            var def = ScriptableObject.CreateInstance<UnitDefinition>();
            def.Portrait = null;

            UnitBattleMetadata.Ensure(attackerGo, true, def, new Vector2Int(2, 2));
            UnitBattleMetadata.Ensure(targetGo, false, def, new Vector2Int(2, 4)); // 2 tiles away (not adjacent)

            var ctrlGo = new GameObject("TurnController");
            var ctrl = ctrlGo.AddComponent<SimpleTurnOrderController>();

            SetPrivate(ctrl, "_board", board);
            CallPrivate(ctrl, "BeginBattle");

            bool isAttackable = (bool)CallPrivate(ctrl, "IsAttackableEnemyTile", new Vector2Int(2, 4));
            Assert.IsFalse(isAttackable, "Enemy 2 tiles away should not be attackable");

            Object.DestroyImmediate(ctrlGo);
            Object.DestroyImmediate(attackerGo);
            Object.DestroyImmediate(targetGo);
            Object.DestroyImmediate(boardGo);
            Object.DestroyImmediate(def);
        }

        [Test]
        public void AttackEligibility_DiagonalEnemyNotAttackable()
        {
            var boardGo = new GameObject("Board");
            var board = boardGo.AddComponent<WorldPerspectiveBoard>();

            SetPrivate(board, "_columns", 5);
            SetPrivate(board, "_rows", 5);
            SetPrivate(board, "_topLeft", new Vector2(0, 5));
            SetPrivate(board, "_topRight", new Vector2(5, 5));
            SetPrivate(board, "_bottomRight", new Vector2(5, 0));
            SetPrivate(board, "_bottomLeft", new Vector2(0, 0));
            CallPrivate(board, "RebuildGrid");

            var attackerGo = new GameObject("Attacker");
            var attackerStats = attackerGo.AddComponent<UnitStats>();
            attackerStats.ApplyBase(new UnitStatsData { Attack = 10, ActionPoints = 2, Speed = 2, Initiative = 10 });

            var targetGo = new GameObject("Target");
            var targetStats = targetGo.AddComponent<UnitStats>();
            targetStats.ApplyBase(new UnitStatsData { Attack = 5, Defense = 5, Life = 50, ActionPoints = 2, Speed = 2, Initiative = 5 });

            var def = ScriptableObject.CreateInstance<UnitDefinition>();
            def.Portrait = null;

            UnitBattleMetadata.Ensure(attackerGo, true, def, new Vector2Int(2, 2));
            UnitBattleMetadata.Ensure(targetGo, false, def, new Vector2Int(3, 3)); // Diagonal

            var ctrlGo = new GameObject("TurnController");
            var ctrl = ctrlGo.AddComponent<SimpleTurnOrderController>();

            SetPrivate(ctrl, "_board", board);
            CallPrivate(ctrl, "BeginBattle");

            bool isAttackable = (bool)CallPrivate(ctrl, "IsAttackableEnemyTile", new Vector2Int(3, 3));
            Assert.IsFalse(isAttackable, "Diagonal enemy should not be attackable");

            Object.DestroyImmediate(ctrlGo);
            Object.DestroyImmediate(attackerGo);
            Object.DestroyImmediate(targetGo);
            Object.DestroyImmediate(boardGo);
            Object.DestroyImmediate(def);
        }

        [Test]
        public void AttackEligibility_OnlyEnemyUnitsCanBeTargeted()
        {
            var boardGo = new GameObject("Board");
            var board = boardGo.AddComponent<WorldPerspectiveBoard>();

            SetPrivate(board, "_columns", 5);
            SetPrivate(board, "_rows", 5);
            SetPrivate(board, "_topLeft", new Vector2(0, 5));
            SetPrivate(board, "_topRight", new Vector2(5, 5));
            SetPrivate(board, "_bottomRight", new Vector2(5, 0));
            SetPrivate(board, "_bottomLeft", new Vector2(0, 0));
            CallPrivate(board, "RebuildGrid");

            var attackerGo = new GameObject("Attacker");
            var attackerStats = attackerGo.AddComponent<UnitStats>();
            attackerStats.ApplyBase(new UnitStatsData { Attack = 10, ActionPoints = 2, Speed = 2, Initiative = 10 });

            var allyGo = new GameObject("Ally");
            var allyStats = allyGo.AddComponent<UnitStats>();
            allyStats.ApplyBase(new UnitStatsData { Attack = 5, Defense = 5, Life = 50, ActionPoints = 2, Speed = 2, Initiative = 5 });

            var def = ScriptableObject.CreateInstance<UnitDefinition>();
            def.Portrait = null;

            UnitBattleMetadata.Ensure(attackerGo, true, def, new Vector2Int(2, 2));
            UnitBattleMetadata.Ensure(allyGo, true, def, new Vector2Int(2, 3)); // Same team (player)

            var ctrlGo = new GameObject("TurnController");
            var ctrl = ctrlGo.AddComponent<SimpleTurnOrderController>();

            SetPrivate(ctrl, "_board", board);
            CallPrivate(ctrl, "BeginBattle");

            bool isAttackable = (bool)CallPrivate(ctrl, "IsAttackableEnemyTile", new Vector2Int(2, 3));
            Assert.IsFalse(isAttackable, "Allied unit should not be attackable");

            Object.DestroyImmediate(ctrlGo);
            Object.DestroyImmediate(attackerGo);
            Object.DestroyImmediate(allyGo);
            Object.DestroyImmediate(boardGo);
            Object.DestroyImmediate(def);
        }

        [Test]
        public void SuccessfulAttack_DealsDamageUsingFormula()
        {
            var boardGo = new GameObject("Board");
            var board = boardGo.AddComponent<WorldPerspectiveBoard>();

            SetPrivate(board, "_columns", 5);
            SetPrivate(board, "_rows", 5);
            SetPrivate(board, "_topLeft", new Vector2(0, 5));
            SetPrivate(board, "_topRight", new Vector2(5, 5));
            SetPrivate(board, "_bottomRight", new Vector2(5, 0));
            SetPrivate(board, "_bottomLeft", new Vector2(0, 0));
            CallPrivate(board, "RebuildGrid");

            var attackerGo = new GameObject("Attacker");
            var attackerStats = attackerGo.AddComponent<UnitStats>();
            attackerStats.ApplyBase(new UnitStatsData { Attack = 10, Defense = 5, ActionPoints = 2, Speed = 2, Initiative = 10 });

            var targetGo = new GameObject("Target");
            var targetStats = targetGo.AddComponent<UnitStats>();
            targetStats.ApplyBase(new UnitStatsData { Attack = 5, Defense = 8, Life = 50, ActionPoints = 2, Speed = 2, Initiative = 5 });

            var def = ScriptableObject.CreateInstance<UnitDefinition>();
            def.Portrait = null;

            UnitBattleMetadata.Ensure(attackerGo, true, def, new Vector2Int(2, 2));
            UnitBattleMetadata.Ensure(targetGo, false, def, new Vector2Int(2, 3));

            var ctrlGo = new GameObject("TurnController");
            var ctrl = ctrlGo.AddComponent<SimpleTurnOrderController>();

            SetPrivate(ctrl, "_board", board);
            CallPrivate(ctrl, "BeginBattle");

            int initialLife = targetStats.Life;

            // Execute attack
            CallPrivate(ctrl, "TryExecuteAttack", new Vector2Int(2, 3));

            int finalLife = targetStats.Life;
            int damageTaken = initialLife - finalLife;

            // Expected damage calculation (with variance 0.95-1.05):
            // RawDamage = 10 * variance (9.5 to 10.5)
            // Mitigation = 10 / (10 + 8) = 0.555...
            // Damage = RawDamage * 0.555 ≈ 5.27 to 5.83
            // Floor to int = 5
            Assert.That(damageTaken, Is.InRange(5, 6), "Damage should be in expected range based on formula");
            Assert.That(finalLife, Is.GreaterThanOrEqualTo(0), "Life should not go negative");

            Object.DestroyImmediate(ctrlGo);
            Object.DestroyImmediate(attackerGo);
            Object.DestroyImmediate(targetGo);
            Object.DestroyImmediate(boardGo);
            Object.DestroyImmediate(def);
        }

        [Test]
        public void SuccessfulAttack_ConsumesOneAP()
        {
            var boardGo = new GameObject("Board");
            var board = boardGo.AddComponent<WorldPerspectiveBoard>();

            SetPrivate(board, "_columns", 5);
            SetPrivate(board, "_rows", 5);
            SetPrivate(board, "_topLeft", new Vector2(0, 5));
            SetPrivate(board, "_topRight", new Vector2(5, 5));
            SetPrivate(board, "_bottomRight", new Vector2(5, 0));
            SetPrivate(board, "_bottomLeft", new Vector2(0, 0));
            CallPrivate(board, "RebuildGrid");

            var attackerGo = new GameObject("Attacker");
            var attackerStats = attackerGo.AddComponent<UnitStats>();
            attackerStats.ApplyBase(new UnitStatsData { Attack = 10, ActionPoints = 3, Speed = 2, Initiative = 10 });

            var targetGo = new GameObject("Target");
            var targetStats = targetGo.AddComponent<UnitStats>();
            targetStats.ApplyBase(new UnitStatsData { Attack = 5, Defense = 5, Life = 50, ActionPoints = 2, Speed = 2, Initiative = 5 });

            var def = ScriptableObject.CreateInstance<UnitDefinition>();
            def.Portrait = null;

            UnitBattleMetadata.Ensure(attackerGo, true, def, new Vector2Int(2, 2));
            UnitBattleMetadata.Ensure(targetGo, false, def, new Vector2Int(2, 3));

            var ctrlGo = new GameObject("TurnController");
            var ctrl = ctrlGo.AddComponent<SimpleTurnOrderController>();

            SetPrivate(ctrl, "_board", board);
            CallPrivate(ctrl, "BeginBattle");

            int initialAP = ctrl.ActiveUnitCurrentActionPoints;

            // Execute attack
            CallPrivate(ctrl, "TryExecuteAttack", new Vector2Int(2, 3));

            int finalAP = ctrl.ActiveUnitCurrentActionPoints;

            Assert.AreEqual(initialAP - 1, finalAP, "Attack should consume exactly 1 AP");

            Object.DestroyImmediate(ctrlGo);
            Object.DestroyImmediate(attackerGo);
            Object.DestroyImmediate(targetGo);
            Object.DestroyImmediate(boardGo);
            Object.DestroyImmediate(def);
        }

        [Test]
        public void SuccessfulAttack_ClampsEnemyLifeToZero()
        {
            var boardGo = new GameObject("Board");
            var board = boardGo.AddComponent<WorldPerspectiveBoard>();

            SetPrivate(board, "_columns", 5);
            SetPrivate(board, "_rows", 5);
            SetPrivate(board, "_topLeft", new Vector2(0, 5));
            SetPrivate(board, "_topRight", new Vector2(5, 5));
            SetPrivate(board, "_bottomRight", new Vector2(5, 0));
            SetPrivate(board, "_bottomLeft", new Vector2(0, 0));
            CallPrivate(board, "RebuildGrid");

            var attackerGo = new GameObject("Attacker");
            var attackerStats = attackerGo.AddComponent<UnitStats>();
            attackerStats.ApplyBase(new UnitStatsData { Attack = 100, ActionPoints = 2, Speed = 2, Initiative = 10 });

            var targetGo = new GameObject("Target");
            var targetStats = targetGo.AddComponent<UnitStats>();
            targetStats.ApplyBase(new UnitStatsData { Attack = 5, Defense = 1, Life = 3, ActionPoints = 2, Speed = 2, Initiative = 5 });

            var def = ScriptableObject.CreateInstance<UnitDefinition>();
            def.Portrait = null;

            UnitBattleMetadata.Ensure(attackerGo, true, def, new Vector2Int(2, 2));
            UnitBattleMetadata.Ensure(targetGo, false, def, new Vector2Int(2, 3));

            var ctrlGo = new GameObject("TurnController");
            var ctrl = ctrlGo.AddComponent<SimpleTurnOrderController>();

            SetPrivate(ctrl, "_board", board);
            CallPrivate(ctrl, "BeginBattle");

            // Execute attack (should deal massive damage)
            CallPrivate(ctrl, "TryExecuteAttack", new Vector2Int(2, 3));

            Assert.AreEqual(0, targetStats.Life, "Life should be clamped to 0, not negative");

            Object.DestroyImmediate(ctrlGo);
            Object.DestroyImmediate(attackerGo);
            Object.DestroyImmediate(targetGo);
            Object.DestroyImmediate(boardGo);
            Object.DestroyImmediate(def);
        }

        [Test]
        public void DamageCalculation_HandlesDefenseZero()
        {
            var boardGo = new GameObject("Board");
            var board = boardGo.AddComponent<WorldPerspectiveBoard>();

            SetPrivate(board, "_columns", 5);
            SetPrivate(board, "_rows", 5);
            SetPrivate(board, "_topLeft", new Vector2(0, 5));
            SetPrivate(board, "_topRight", new Vector2(5, 5));
            SetPrivate(board, "_bottomRight", new Vector2(5, 0));
            SetPrivate(board, "_bottomLeft", new Vector2(0, 0));
            CallPrivate(board, "RebuildGrid");

            var attackerGo = new GameObject("Attacker");
            var attackerStats = attackerGo.AddComponent<UnitStats>();
            attackerStats.ApplyBase(new UnitStatsData { Attack = 10, ActionPoints = 2, Speed = 2, Initiative = 10 });

            var targetGo = new GameObject("Target");
            var targetStats = targetGo.AddComponent<UnitStats>();
            targetStats.ApplyBase(new UnitStatsData { Attack = 5, Defense = 0, Life = 50, ActionPoints = 2, Speed = 2, Initiative = 5 });

            var def = ScriptableObject.CreateInstance<UnitDefinition>();
            def.Portrait = null;

            UnitBattleMetadata.Ensure(attackerGo, true, def, new Vector2Int(2, 2));
            UnitBattleMetadata.Ensure(targetGo, false, def, new Vector2Int(2, 3));

            var ctrlGo = new GameObject("TurnController");
            var ctrl = ctrlGo.AddComponent<SimpleTurnOrderController>();

            SetPrivate(ctrl, "_board", board);
            CallPrivate(ctrl, "BeginBattle");

            int initialLife = targetStats.Life;

            // Execute attack (should not crash with Defense=0)
            CallPrivate(ctrl, "TryExecuteAttack", new Vector2Int(2, 3));

            int finalLife = targetStats.Life;

            // With Defense=0, mitigation = 10/(10+0) = 1.0, so full damage
            Assert.That(finalLife, Is.LessThan(initialLife), "Damage should be dealt even with Defense=0");
            Assert.That(finalLife, Is.GreaterThanOrEqualTo(0), "Life should not go negative");

            Object.DestroyImmediate(ctrlGo);
            Object.DestroyImmediate(attackerGo);
            Object.DestroyImmediate(targetGo);
            Object.DestroyImmediate(boardGo);
            Object.DestroyImmediate(def);
        }

        [Test]
        public void DamageCalculation_HandlesVeryHighDefense()
        {
            var boardGo = new GameObject("Board");
            var board = boardGo.AddComponent<WorldPerspectiveBoard>();

            SetPrivate(board, "_columns", 5);
            SetPrivate(board, "_rows", 5);
            SetPrivate(board, "_topLeft", new Vector2(0, 5));
            SetPrivate(board, "_topRight", new Vector2(5, 5));
            SetPrivate(board, "_bottomRight", new Vector2(5, 0));
            SetPrivate(board, "_bottomLeft", new Vector2(0, 0));
            CallPrivate(board, "RebuildGrid");

            var attackerGo = new GameObject("Attacker");
            var attackerStats = attackerGo.AddComponent<UnitStats>();
            attackerStats.ApplyBase(new UnitStatsData { Attack = 10, ActionPoints = 2, Speed = 2, Initiative = 10 });

            var targetGo = new GameObject("Target");
            var targetStats = targetGo.AddComponent<UnitStats>();
            targetStats.ApplyBase(new UnitStatsData { Attack = 5, Defense = 1000, Life = 50, ActionPoints = 2, Speed = 2, Initiative = 5 });

            var def = ScriptableObject.CreateInstance<UnitDefinition>();
            def.Portrait = null;

            UnitBattleMetadata.Ensure(attackerGo, true, def, new Vector2Int(2, 2));
            UnitBattleMetadata.Ensure(targetGo, false, def, new Vector2Int(2, 3));

            var ctrlGo = new GameObject("TurnController");
            var ctrl = ctrlGo.AddComponent<SimpleTurnOrderController>();

            SetPrivate(ctrl, "_board", board);
            CallPrivate(ctrl, "BeginBattle");

            int initialLife = targetStats.Life;

            // Execute attack (should deal minimal damage but not crash)
            CallPrivate(ctrl, "TryExecuteAttack", new Vector2Int(2, 3));

            int finalLife = targetStats.Life;

            // Mitigation = 10/(10+1000) ≈ 0.0099, so very low damage (likely 0 after floor)
            Assert.That(finalLife, Is.LessThanOrEqualTo(initialLife), "Life should not increase");
            Assert.That(finalLife, Is.GreaterThanOrEqualTo(0), "Life should not go negative");

            Object.DestroyImmediate(ctrlGo);
            Object.DestroyImmediate(attackerGo);
            Object.DestroyImmediate(targetGo);
            Object.DestroyImmediate(boardGo);
            Object.DestroyImmediate(def);
        }

        // Test helper methods (reused from UnitMovementTests pattern)
        private static void SetPrivate(object obj, string field, object value)
        {
            var fi = obj.GetType().GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            fi.SetValue(obj, value);
        }

        private static object CallPrivate(object obj, string method, object arg)
        {
            var mi = obj.GetType().GetMethod(
                method,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Public);
            return mi.Invoke(obj, new[] { arg });
        }

        private static object CallPrivate(object obj, string method)
        {
            var mi = obj.GetType().GetMethod(
                method,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Public);
            return mi.Invoke(obj, null);
        }
    }
}
