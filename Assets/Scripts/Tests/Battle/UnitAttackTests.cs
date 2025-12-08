using NUnit.Framework;
using UnityEngine;
using SevenBattles.Battle.Turn;
using SevenBattles.Battle.Units;
using SevenBattles.Battle.Board;
using SevenBattles.Core.Units;
using UnityEngine.TestTools;
using System.Collections;

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
            attackerGo.AddComponent<SpriteRenderer>();
            attackerStats.ApplyBase(new UnitStatsData { Attack = 0, ActionPoints = 2, Speed = 2, Initiative = 10 });

            var targetGo = new GameObject("Target");
            var targetStats = targetGo.AddComponent<UnitStats>();
            targetGo.AddComponent<SpriteRenderer>();
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
            attackerGo.AddComponent<SpriteRenderer>();
            attackerStats.ApplyBase(new UnitStatsData { Attack = 10, ActionPoints = 2, Speed = 2, Initiative = 10 });

            var targetGo = new GameObject("Target");
            var targetStats = targetGo.AddComponent<UnitStats>();
            targetGo.AddComponent<SpriteRenderer>();
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
            attackerGo.AddComponent<SpriteRenderer>();
            attackerStats.ApplyBase(new UnitStatsData { Attack = 10, ActionPoints = 2, Speed = 2, Initiative = 10 });

            var targetGo = new GameObject("Target");
            var targetStats = targetGo.AddComponent<UnitStats>();
            targetGo.AddComponent<SpriteRenderer>();
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
            attackerGo.AddComponent<SpriteRenderer>();
            attackerStats.ApplyBase(new UnitStatsData { Attack = 10, ActionPoints = 2, Speed = 2, Initiative = 10 });

            var targetGo = new GameObject("Target");
            var targetStats = targetGo.AddComponent<UnitStats>();
            targetGo.AddComponent<SpriteRenderer>();
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
            attackerGo.AddComponent<SpriteRenderer>();
            attackerStats.ApplyBase(new UnitStatsData { Attack = 10, ActionPoints = 2, Speed = 2, Initiative = 10 });

            var allyGo = new GameObject("Ally");
            var allyStats = allyGo.AddComponent<UnitStats>();
            allyGo.AddComponent<SpriteRenderer>();
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
            // This test validates that the damage formula for a normal
            // attack (Attack = 10, Defense = 8) yields the expected range.

            var ctrlGo = new GameObject("TurnController");
            var ctrl = ctrlGo.AddComponent<SimpleTurnOrderController>();

            int attack = 10;
            int defense = 8;

            var mi = typeof(SimpleTurnOrderController).GetMethod(
                "CalculateDamage",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);

            Assert.IsNotNull(mi, "CalculateDamage method should exist on SimpleTurnOrderController.");

            int damage = (int)mi.Invoke(ctrl, new object[] { attack, defense });

            // Expected damage calculation (with variance 0.95-1.05):
            // RawDamage = 10 * variance (9.5 to 10.5)
            // Mitigation = 10 / (10 + 8) = 0.555...
            // Damage = RawDamage * 0.555 ≈ 5.27 to 5.83
            // Floor to int = 5
            Assert.That(damage, Is.InRange(5, 6), "Damage should be in expected range based on formula");

            Object.DestroyImmediate(ctrlGo);
        }

        [Test]
        public void SuccessfulAttack_ConsumesOneAP()
        {
            // This test verifies that consuming an action point for an attack
            // reduces the active unit's current AP by exactly 1.

            var ctrlGo = new GameObject("TurnController");
            var ctrl = ctrlGo.AddComponent<SimpleTurnOrderController>();

            // Simulate an active unit with 3 AP.
            SetPrivate(ctrl, "_activeUnitCurrentActionPoints", 3);
            SetPrivate(ctrl, "_hasActiveUnit", true);

            int initialAP = ctrl.ActiveUnitCurrentActionPoints;

            var mi = typeof(SimpleTurnOrderController).GetMethod(
                "ConsumeActiveUnitActionPoint",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);

            Assert.IsNotNull(mi, "ConsumeActiveUnitActionPoint method should exist on SimpleTurnOrderController.");

            mi.Invoke(ctrl, null);

            int finalAP = ctrl.ActiveUnitCurrentActionPoints;

            Assert.AreEqual(initialAP - 1, finalAP, "Attack should consume exactly 1 AP");

            Object.DestroyImmediate(ctrlGo);
        }

        [Test]
        public void SuccessfulAttack_ClampsEnemyLifeToZero()
        {
            // This test focuses on the interaction between the damage
            // formula and UnitStats.TakeDamage: a hit that would reduce
            // life below 0 must clamp to exactly 0.

            var ctrlGo = new GameObject("TurnController");
            var ctrl = ctrlGo.AddComponent<SimpleTurnOrderController>();

            var targetGo = new GameObject("Target");
            var targetStats = targetGo.AddComponent<UnitStats>();
            targetStats.ApplyBase(new UnitStatsData { Life = 3, Defense = 1 });

            var mi = typeof(SimpleTurnOrderController).GetMethod(
                "CalculateDamage",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);

            Assert.IsNotNull(mi, "CalculateDamage method should exist on SimpleTurnOrderController.");

            int damage = (int)mi.Invoke(ctrl, new object[] { 100, targetStats.Defense });

            // Apply damage using the runtime stats component.
            targetStats.TakeDamage(damage);

            Assert.AreEqual(0, targetStats.Life, "Life should be clamped to 0, not negative");

            Object.DestroyImmediate(ctrlGo);
            Object.DestroyImmediate(targetGo);
        }

        [Test]
        public void DamageCalculation_HandlesDefenseZero()
        {
            // This test focuses on the pure damage formula to ensure that
            // a target with Defense = 0 still takes damage when attacked.
            var ctrlGo = new GameObject("TurnController");
            var ctrl = ctrlGo.AddComponent<SimpleTurnOrderController>();

            int attack = 10;
            int defense = 0;

            var mi = typeof(SimpleTurnOrderController).GetMethod(
                "CalculateDamage",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);

            Assert.IsNotNull(mi, "CalculateDamage method should exist on SimpleTurnOrderController.");

            int damage = (int)mi.Invoke(ctrl, new object[] { attack, defense });

            Assert.That(damage, Is.GreaterThan(0), "Damage should be dealt even with Defense=0");

            Object.DestroyImmediate(ctrlGo);
        }

        [UnityTest]
        public IEnumerator DamageCalculation_HandlesVeryHighDefense()
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
            attackerGo.AddComponent<SpriteRenderer>();
            attackerStats.ApplyBase(new UnitStatsData { Attack = 10, ActionPoints = 2, Speed = 2, Initiative = 10 });

            var targetGo = new GameObject("Target");
            var targetStats = targetGo.AddComponent<UnitStats>();
            targetGo.AddComponent<SpriteRenderer>();
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
            yield return new WaitForSeconds(0.5f);

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

        [UnityTest]
        public IEnumerator SuccessfulAttack_BothUnitsFaceEachOther()
        {
            // This test verifies that when one unit attacks another, both units
            // turn to face each other before the attack executes.
            var boardGo = new GameObject("Board");
            var board = boardGo.AddComponent<WorldPerspectiveBoard>();

            SetPrivate(board, "_columns", 5);
            SetPrivate(board, "_rows", 5);
            SetPrivate(board, "_topLeft", new Vector2(0, 5));
            SetPrivate(board, "_topRight", new Vector2(5, 5));
            SetPrivate(board, "_bottomRight", new Vector2(5, 0));
            SetPrivate(board, "_bottomLeft", new Vector2(0, 0));
            CallPrivate(board, "RebuildGrid");

            // Setup: Attacker at (2,2) initially facing UP, Target at (3,2) initially facing UP
            var attackerGo = new GameObject("Attacker");
            var attackerStats = attackerGo.AddComponent<UnitStats>();
            attackerGo.AddComponent<SpriteRenderer>();
            attackerStats.ApplyBase(new UnitStatsData { Attack = 10, ActionPoints = 2, Speed = 2, Initiative = 10 });

            var targetGo = new GameObject("Target");
            var targetStats = targetGo.AddComponent<UnitStats>();
            targetGo.AddComponent<SpriteRenderer>();
            targetStats.ApplyBase(new UnitStatsData { Attack = 5, Defense = 5, Life = 50, ActionPoints = 2, Speed = 2, Initiative = 5 });

            var def = ScriptableObject.CreateInstance<UnitDefinition>();
            def.Portrait = null;

            var attackerMeta = UnitBattleMetadata.Ensure(attackerGo, true, def, new Vector2Int(2, 2));
            var targetMeta = UnitBattleMetadata.Ensure(targetGo, false, def, new Vector2Int(3, 2)); // East of attacker

            // Set initial facing direction (both facing up initially)
            attackerMeta.Facing = Vector2.up;
            targetMeta.Facing = Vector2.up;

            var ctrlGo = new GameObject("TurnController");
            var ctrl = ctrlGo.AddComponent<SimpleTurnOrderController>();

            SetPrivate(ctrl, "_board", board);
            CallPrivate(ctrl, "BeginBattle");

            // Execute: Attack target from the west (attacker is west of target)
            CallPrivate(ctrl, "TryExecuteAttack", new Vector2Int(3, 2));
            yield return new WaitForSeconds(0.5f);

            // Assert: Attacker should face RIGHT (east towards target)
            Assert.AreEqual(Vector2.right, attackerMeta.Facing, "Attacker should face right (towards target to the east)");

            // Assert: Target should face LEFT (west towards attacker)
            Assert.AreEqual(Vector2.left, targetMeta.Facing, "Target should face left (towards attacker to the west)");

            Object.DestroyImmediate(ctrlGo);
            Object.DestroyImmediate(attackerGo);
            Object.DestroyImmediate(targetGo);
            Object.DestroyImmediate(boardGo);
            Object.DestroyImmediate(def);
        }

        [UnityTest]
        public IEnumerator LethalAttack_PlaysDeathAndRemovesUnitFromBoard()
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
            attackerGo.AddComponent<SpriteRenderer>();
            attackerStats.ApplyBase(new UnitStatsData { Attack = 20, ActionPoints = 2, Speed = 2, Initiative = 10 });

            var targetGo = new GameObject("Target");
            var targetStats = targetGo.AddComponent<UnitStats>();
            targetGo.AddComponent<SpriteRenderer>();
            targetStats.ApplyBase(new UnitStatsData { Life = 10, Defense = 0, ActionPoints = 2, Speed = 2, Initiative = 5 });

            var def = ScriptableObject.CreateInstance<UnitDefinition>();
            def.Portrait = null;

            UnitBattleMetadata.Ensure(attackerGo, true, def, new Vector2Int(2, 2));
            var targetMeta = UnitBattleMetadata.Ensure(targetGo, false, def, new Vector2Int(2, 3)); // Adjacent north

            var ctrlGo = new GameObject("TurnController");
            var ctrl = ctrlGo.AddComponent<SimpleTurnOrderController>();

            SetPrivate(ctrl, "_board", board);

            // Ensure deterministic death behavior for this test (no VFX needed here).
            CallPrivate(ctrl, "BeginBattle");

            // Execute attack; with Attack=20 and Defense=0 the damage is guaranteed to be lethal.
            CallPrivate(ctrl, "TryExecuteAttack", new Vector2Int(2, 3));
            yield return new WaitForSeconds(1.0f);

            Assert.AreEqual(0, targetStats.Life, "Lethal attack should clamp target life to 0");

            // The unit GameObject should have been destroyed; metadata reference becomes null.
            Assert.IsTrue(targetMeta == null || targetMeta.Equals(null), "Dead unit GameObject should be destroyed so it disappears from the board");

            var occupied = (bool)CallPrivate(ctrl, "IsTileOccupiedByAnyUnit", new Vector2Int(2, 3));
            Assert.IsFalse(occupied, "Dead unit tile should be considered empty for movement and attacks");

            Object.DestroyImmediate(ctrlGo);
            Object.DestroyImmediate(attackerGo);
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
