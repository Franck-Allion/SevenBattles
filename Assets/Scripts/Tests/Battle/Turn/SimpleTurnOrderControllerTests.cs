using NUnit.Framework;
using UnityEngine;
using SevenBattles.Battle.Turn;
using SevenBattles.Battle.Units;
using SevenBattles.Battle.Board;
using SevenBattles.Core.Units;
using System.Collections;
using UnityEngine.TestTools;

namespace SevenBattles.Tests.Battle
{
    public class SimpleTurnOrderControllerTests
    {
        [Test]
        public void CanActiveUnitAttack_ReturnsTrue_WhenEnemyAdjacentAndApAvailable()
        {
            // Setup
            var boardGo = new GameObject("Board");
            var board = boardGo.AddComponent<WorldPerspectiveBoard>();
            SetPrivate(board, "_columns", 5);
            SetPrivate(board, "_rows", 5);
            CallPrivate(board, "RebuildGrid");

            var ctrlGo = new GameObject("TurnController");
            var ctrl = ctrlGo.AddComponent<SimpleTurnOrderController>();
            SetPrivate(ctrl, "_board", board);

            // Player Unit (Active)
            var playerGo = new GameObject("PlayerUnit");
            var playerStats = playerGo.AddComponent<UnitStats>();
            playerStats.ApplyBase(new UnitStatsData { Attack = 5, ActionPoints = 2, Speed = 3, Initiative = 10, Defense = 0, Life = 10 });
            var playerDef = ScriptableObject.CreateInstance<UnitDefinition>();
            playerDef.Portrait = null;
            UnitBattleMetadata.Ensure(playerGo, true, playerDef, new Vector2Int(2, 2));

            // Enemy Unit (Adjacent)
            var enemyGo = new GameObject("EnemyUnit");
            var enemyStats = enemyGo.AddComponent<UnitStats>();
            enemyStats.ApplyBase(new UnitStatsData { Attack = 2, ActionPoints = 2, Speed = 3, Initiative = 5, Defense = 0, Life = 10 });
            var enemyDef = ScriptableObject.CreateInstance<UnitDefinition>();
            enemyDef.Portrait = null;
            UnitBattleMetadata.Ensure(enemyGo, false, enemyDef, new Vector2Int(2, 3)); // North of player

            // Initialize Battle
            CallPrivate(ctrl, "BeginBattle");

            // Act
            bool canAttack = (bool)CallPrivate(ctrl, "CanActiveUnitAttack");
            bool isEnemyTargetable = (bool)CallPrivate(ctrl, "IsAttackableEnemyTile", new Vector2Int(2, 3));

            // Assert
            Assert.IsTrue(canAttack, "Active unit should be able to attack.");
            Assert.IsTrue(isEnemyTargetable, "Adjacent enemy should be targetable.");

            // Cleanup
            Object.DestroyImmediate(ctrlGo);
            Object.DestroyImmediate(playerGo);
            Object.DestroyImmediate(enemyGo);
            Object.DestroyImmediate(boardGo);
            Object.DestroyImmediate(playerDef);
            Object.DestroyImmediate(enemyDef);
        }

        [Test]
        public void CanActiveUnitAttack_ReturnsFalse_WhenNoAp()
        {
            // Setup
            var boardGo = new GameObject("Board");
            var board = boardGo.AddComponent<WorldPerspectiveBoard>();
            SetPrivate(board, "_columns", 5);
            SetPrivate(board, "_rows", 5);
            CallPrivate(board, "RebuildGrid");

            var ctrlGo = new GameObject("TurnController");
            var ctrl = ctrlGo.AddComponent<SimpleTurnOrderController>();
            SetPrivate(ctrl, "_board", board);

            // Player Unit (Active) - 0 AP
            var playerGo = new GameObject("PlayerUnit");
            var playerStats = playerGo.AddComponent<UnitStats>();
            playerStats.ApplyBase(new UnitStatsData { Attack = 5, ActionPoints = 0, Speed = 3, Initiative = 10 });
            var playerDef = ScriptableObject.CreateInstance<UnitDefinition>();
            UnitBattleMetadata.Ensure(playerGo, true, playerDef, new Vector2Int(2, 2));

            // Enemy Unit
            var enemyGo = new GameObject("EnemyUnit");
            var enemyStats = enemyGo.AddComponent<UnitStats>();
            enemyStats.ApplyBase(new UnitStatsData { Attack = 2, ActionPoints = 2, Speed = 3, Initiative = 5 });
            var enemyDef = ScriptableObject.CreateInstance<UnitDefinition>();
            UnitBattleMetadata.Ensure(enemyGo, false, enemyDef, new Vector2Int(2, 3));

            // Initialize Battle
            CallPrivate(ctrl, "BeginBattle");

            // Act
            bool canAttack = (bool)CallPrivate(ctrl, "CanActiveUnitAttack");

            // Assert
            Assert.IsFalse(canAttack, "Unit with 0 AP should not be able to attack.");

            // Cleanup
            Object.DestroyImmediate(ctrlGo);
            Object.DestroyImmediate(playerGo);
            Object.DestroyImmediate(enemyGo);
            Object.DestroyImmediate(boardGo);
            Object.DestroyImmediate(playerDef);
            Object.DestroyImmediate(enemyDef);
        }

        [Test]
        public void IsAttackableEnemyTile_ReturnsFalse_WhenEnemyOutOfRange()
        {
            // Setup
            var boardGo = new GameObject("Board");
            var board = boardGo.AddComponent<WorldPerspectiveBoard>();
            SetPrivate(board, "_columns", 5);
            SetPrivate(board, "_rows", 5);
            CallPrivate(board, "RebuildGrid");

            var ctrlGo = new GameObject("TurnController");
            var ctrl = ctrlGo.AddComponent<SimpleTurnOrderController>();
            SetPrivate(ctrl, "_board", board);

            // Player Unit (Active)
            var playerGo = new GameObject("PlayerUnit");
            var playerStats = playerGo.AddComponent<UnitStats>();
            playerStats.ApplyBase(new UnitStatsData { Attack = 5, ActionPoints = 2, Speed = 3, Initiative = 10 });
            var playerDef = ScriptableObject.CreateInstance<UnitDefinition>();
            UnitBattleMetadata.Ensure(playerGo, true, playerDef, new Vector2Int(2, 2));

            // Enemy Unit (Far away)
            var enemyGo = new GameObject("EnemyUnit");
            var enemyStats = enemyGo.AddComponent<UnitStats>();
            enemyStats.ApplyBase(new UnitStatsData { Attack = 2, ActionPoints = 2, Speed = 3, Initiative = 5 });
            var enemyDef = ScriptableObject.CreateInstance<UnitDefinition>();
            UnitBattleMetadata.Ensure(enemyGo, false, enemyDef, new Vector2Int(4, 4));

            // Initialize Battle
            CallPrivate(ctrl, "BeginBattle");

            // Act
            bool isEnemyTargetable = (bool)CallPrivate(ctrl, "IsAttackableEnemyTile", new Vector2Int(4, 4));

            // Assert
            Assert.IsFalse(isEnemyTargetable, "Enemy out of range should not be targetable.");

            // Cleanup
            Object.DestroyImmediate(ctrlGo);
            Object.DestroyImmediate(playerGo);
            Object.DestroyImmediate(enemyGo);
            Object.DestroyImmediate(boardGo);
            Object.DestroyImmediate(playerDef);
            Object.DestroyImmediate(enemyDef);
        }

        private static void SetPrivate(object obj, string field, object value)
        {
            var fi = obj.GetType().GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (fi != null)
            {
                fi.SetValue(obj, value);
            }
            else
            {
                Debug.LogError($"Field '{field}' not found on {obj.GetType().Name}");
            }
        }

        private static object CallPrivate(object obj, string method, params object[] args)
        {
            var mi = obj.GetType().GetMethod(
                method,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Public);
            if (mi != null)
            {
                return mi.Invoke(obj, args);
            }
            else
            {
                Debug.LogError($"Method '{method}' not found on {obj.GetType().Name}");
                return null;
            }
        }
    }
}
