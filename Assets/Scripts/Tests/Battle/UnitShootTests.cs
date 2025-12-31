using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using SevenBattles.Battle.Combat;
using SevenBattles.Battle.Units;
using SevenBattles.Battle.Board;
using SevenBattles.Core.Units;
using Object = UnityEngine.Object;

namespace SevenBattles.Tests.Battle
{
    public class UnitShootTests
    {
        private struct TestUnit
        {
            public UnitBattleMetadata Metadata;
            public UnitStats Stats;
        }

        [Test]
        public void ShootEligibility_RequiresShootGreaterThanZero()
        {
            var ctrlGo = new GameObject("CombatController");
            var ctrl = ctrlGo.AddComponent<BattleCombatController>();

            var boardGo = new GameObject("Board");
            var board = boardGo.AddComponent<WorldPerspectiveBoard>();
            SetPrivate(ctrl, "_board", board);

            var shooterGo = new GameObject("Shooter");
            var stats = shooterGo.AddComponent<UnitStats>();
            stats.ApplyBase(new UnitStatsData { Shoot = 0, ShootRange = 3, ActionPoints = 2 });

            bool canShoot = ctrl.CanShoot(stats, 1, true, false, false, false);
            Assert.IsFalse(canShoot, "Unit with Shoot=0 should not be able to shoot.");

            Object.DestroyImmediate(ctrlGo);
            Object.DestroyImmediate(boardGo);
            Object.DestroyImmediate(shooterGo);
        }

        [Test]
        public void ShootEligibility_RequiresAtLeastOneAp()
        {
            var ctrlGo = new GameObject("CombatController");
            var ctrl = ctrlGo.AddComponent<BattleCombatController>();

            var boardGo = new GameObject("Board");
            var board = boardGo.AddComponent<WorldPerspectiveBoard>();
            SetPrivate(ctrl, "_board", board);

            var shooterGo = new GameObject("Shooter");
            var stats = shooterGo.AddComponent<UnitStats>();
            stats.ApplyBase(new UnitStatsData { Shoot = 5, ShootRange = 3, ActionPoints = 2 });

            bool canShoot = ctrl.CanShoot(stats, 0, true, false, false, false);
            Assert.IsFalse(canShoot, "Unit with AP=0 should not be able to shoot.");

            Object.DestroyImmediate(ctrlGo);
            Object.DestroyImmediate(boardGo);
            Object.DestroyImmediate(shooterGo);
        }

        [Test]
        public void ShootTargeting_AllowsAlignedWithinRange()
        {
            var ctrlGo = new GameObject("CombatController");
            var ctrl = ctrlGo.AddComponent<BattleCombatController>();

            var def = ScriptableObject.CreateInstance<UnitDefinition>();
            def.Portrait = null;

            var shooterGo = new GameObject("Shooter");
            var shooterStats = shooterGo.AddComponent<UnitStats>();
            shooterStats.ApplyBase(new UnitStatsData { Shoot = 5, ShootRange = 3, ActionPoints = 2 });
            var shooterMeta = UnitBattleMetadata.Ensure(shooterGo, true, def, new Vector2Int(2, 2));

            var targetGo = new GameObject("Target");
            var targetStats = targetGo.AddComponent<UnitStats>();
            targetStats.ApplyBase(new UnitStatsData { Life = 10, ShootDefense = 1 });
            var targetMeta = UnitBattleMetadata.Ensure(targetGo, false, def, new Vector2Int(2, 5));

            var units = new List<TestUnit>
            {
                new TestUnit { Metadata = shooterMeta, Stats = shooterStats },
                new TestUnit { Metadata = targetMeta, Stats = targetStats }
            };

            ctrl.RebuildShootableEnemyTiles(units[0], units, u => u.Metadata, u => u.Stats);

            Assert.IsTrue(ctrl.IsShootableEnemyTile(new Vector2Int(2, 5)), "Enemy in same column within range should be shootable.");
            Assert.IsFalse(ctrl.IsShootableEnemyTile(new Vector2Int(3, 3)), "Diagonal enemy should not be shootable.");

            Object.DestroyImmediate(ctrlGo);
            Object.DestroyImmediate(shooterGo);
            Object.DestroyImmediate(targetGo);
            Object.DestroyImmediate(def);
        }

        [Test]
        public void ShootTargeting_DisallowsAdjacentEnemy()
        {
            var ctrlGo = new GameObject("CombatController");
            var ctrl = ctrlGo.AddComponent<BattleCombatController>();

            var def = ScriptableObject.CreateInstance<UnitDefinition>();
            def.Portrait = null;

            var shooterGo = new GameObject("Shooter");
            var shooterStats = shooterGo.AddComponent<UnitStats>();
            shooterStats.ApplyBase(new UnitStatsData { Shoot = 5, ShootRange = 3, ActionPoints = 2 });
            var shooterMeta = UnitBattleMetadata.Ensure(shooterGo, true, def, new Vector2Int(2, 2));

            var targetGo = new GameObject("Target");
            var targetStats = targetGo.AddComponent<UnitStats>();
            targetStats.ApplyBase(new UnitStatsData { Life = 10, ShootDefense = 1 });
            var targetMeta = UnitBattleMetadata.Ensure(targetGo, false, def, new Vector2Int(2, 3));

            var units = new List<TestUnit>
            {
                new TestUnit { Metadata = shooterMeta, Stats = shooterStats },
                new TestUnit { Metadata = targetMeta, Stats = targetStats }
            };

            ctrl.RebuildShootableEnemyTiles(units[0], units, u => u.Metadata, u => u.Stats);

            Assert.IsFalse(ctrl.IsShootableEnemyTile(new Vector2Int(2, 3)), "Adjacent enemy should not be shootable.");

            Object.DestroyImmediate(ctrlGo);
            Object.DestroyImmediate(shooterGo);
            Object.DestroyImmediate(targetGo);
            Object.DestroyImmediate(def);
        }

        [Test]
        public void ShootTargeting_OnlyFirstEnemyPerDirection_IgnoresFriendlyUnits()
        {
            var ctrlGo = new GameObject("CombatController");
            var ctrl = ctrlGo.AddComponent<BattleCombatController>();

            var def = ScriptableObject.CreateInstance<UnitDefinition>();
            def.Portrait = null;

            var shooterGo = new GameObject("Shooter");
            var shooterStats = shooterGo.AddComponent<UnitStats>();
            shooterStats.ApplyBase(new UnitStatsData { Shoot = 5, ShootRange = 5, ActionPoints = 2 });
            var shooterMeta = UnitBattleMetadata.Ensure(shooterGo, true, def, new Vector2Int(2, 2));

            var allyGo = new GameObject("Ally");
            var allyStats = allyGo.AddComponent<UnitStats>();
            allyStats.ApplyBase(new UnitStatsData { Life = 10 });
            var allyMeta = UnitBattleMetadata.Ensure(allyGo, true, def, new Vector2Int(2, 3));

            var enemyNearGo = new GameObject("EnemyNear");
            var enemyNearStats = enemyNearGo.AddComponent<UnitStats>();
            enemyNearStats.ApplyBase(new UnitStatsData { Life = 10 });
            var enemyNearMeta = UnitBattleMetadata.Ensure(enemyNearGo, false, def, new Vector2Int(2, 4));

            var enemyFarGo = new GameObject("EnemyFar");
            var enemyFarStats = enemyFarGo.AddComponent<UnitStats>();
            enemyFarStats.ApplyBase(new UnitStatsData { Life = 10 });
            var enemyFarMeta = UnitBattleMetadata.Ensure(enemyFarGo, false, def, new Vector2Int(2, 5));

            var units = new List<TestUnit>
            {
                new TestUnit { Metadata = shooterMeta, Stats = shooterStats },
                new TestUnit { Metadata = allyMeta, Stats = allyStats },
                new TestUnit { Metadata = enemyNearMeta, Stats = enemyNearStats },
                new TestUnit { Metadata = enemyFarMeta, Stats = enemyFarStats }
            };

            ctrl.RebuildShootableEnemyTiles(units[0], units, u => u.Metadata, u => u.Stats);

            Assert.IsTrue(ctrl.IsShootableEnemyTile(new Vector2Int(2, 4)), "Closest enemy in line should be shootable.");
            Assert.IsFalse(ctrl.IsShootableEnemyTile(new Vector2Int(2, 5)), "Enemy behind the first enemy should not be shootable.");

            Object.DestroyImmediate(ctrlGo);
            Object.DestroyImmediate(shooterGo);
            Object.DestroyImmediate(allyGo);
            Object.DestroyImmediate(enemyNearGo);
            Object.DestroyImmediate(enemyFarGo);
            Object.DestroyImmediate(def);
        }

        [UnityTest]
        public IEnumerator SuccessfulShot_DealsDamage_AndConsumesAp()
        {
            var ctrlGo = new GameObject("CombatController");
            var ctrl = ctrlGo.AddComponent<BattleCombatController>();

            var def = ScriptableObject.CreateInstance<UnitDefinition>();
            def.Portrait = null;

            var shooterGo = new GameObject("Shooter");
            var shooterStats = shooterGo.AddComponent<UnitStats>();
            shooterStats.ApplyBase(new UnitStatsData { Shoot = 10, ShootRange = 3, ActionPoints = 2 });
            var shooterMeta = UnitBattleMetadata.Ensure(shooterGo, true, def, new Vector2Int(2, 2));

            var targetGo = new GameObject("Target");
            var targetStats = targetGo.AddComponent<UnitStats>();
            targetStats.ApplyBase(new UnitStatsData { Life = 20, ShootDefense = 0 });
            var targetMeta = UnitBattleMetadata.Ensure(targetGo, false, def, new Vector2Int(2, 4));

            var units = new List<TestUnit>
            {
                new TestUnit { Metadata = shooterMeta, Stats = shooterStats },
                new TestUnit { Metadata = targetMeta, Stats = targetStats }
            };

            ctrl.RebuildShootableEnemyTiles(units[0], units, u => u.Metadata, u => u.Stats);

            bool apConsumed = false;
            bool completed = false;
            int initialLife = targetStats.Life;

            ctrl.TryExecuteShoot(
                new Vector2Int(2, 4),
                units[0],
                units,
                u => u.Metadata,
                u => u.Stats,
                () => { },
                () => { apConsumed = true; },
                () => { },
                () => { completed = true; }
            );

            yield return new WaitForSeconds(0.6f);

            Assert.IsTrue(completed, "Shot should complete within the expected time window.");
            Assert.IsTrue(apConsumed, "Shot should consume exactly one AP.");
            Assert.Less(targetStats.Life, initialLife, "Shot should reduce target life.");

            Object.DestroyImmediate(ctrlGo);
            Object.DestroyImmediate(shooterGo);
            Object.DestroyImmediate(targetGo);
            Object.DestroyImmediate(def);
        }

        private static void SetPrivate(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' was not found on type '{target.GetType().FullName}'.");
            field.SetValue(target, value);
        }
    }
}
