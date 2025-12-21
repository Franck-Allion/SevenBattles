using NUnit.Framework;
using UnityEngine;
using SevenBattles.Battle.Turn;
using SevenBattles.Battle.Units;
using SevenBattles.Battle.Board;
using SevenBattles.Core.Battle;
using SevenBattles.Core.Units;
using SevenBattles.Core;
using System.Collections;
using UnityEngine.TestTools;

namespace SevenBattles.Tests.Battle
{
    public class SimpleTurnOrderControllerTests
    {
        [Test]
        public void ActiveUnitSpells_ReturnsEmptyArray_WhenNoActiveUnit()
        {
            var ctrlGo = new GameObject("TurnController");
            var ctrl = ctrlGo.AddComponent<SimpleTurnOrderController>();

            Assert.IsNotNull(ctrl.ActiveUnitSpells);
            Assert.AreEqual(0, ctrl.ActiveUnitSpells.Length);

            Object.DestroyImmediate(ctrlGo);
        }

        [Test]
        public void ActiveUnitSpells_ReturnsConfiguredSpells_ForActiveUnit()
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
            def.Portrait = null;
            var s1 = ScriptableObject.CreateInstance<SpellDefinition>();
            s1.Id = "spell.firebolt";
            s1.ActionPointCost = 1;
            var s2 = ScriptableObject.CreateInstance<SpellDefinition>();
            s2.Id = "spell.arcane_shield";
            s2.ActionPointCost = 2;
            def.Spells = new[] { s1, s2 };

            // Active player unit (highest initiative)
            var playerGo = new GameObject("PlayerUnit");
            var playerStats = playerGo.AddComponent<UnitStats>();
            playerStats.ApplyBase(new UnitStatsData { Life = 10, ActionPoints = 1, Speed = 1, Initiative = 10 });
            UnitBattleMetadata.Ensure(playerGo, true, def, new Vector2Int(0, 0));

            // Enemy unit
            var enemyGo = new GameObject("EnemyUnit");
            var enemyStats = enemyGo.AddComponent<UnitStats>();
            enemyStats.ApplyBase(new UnitStatsData { Life = 10, ActionPoints = 1, Speed = 1, Initiative = 5 });
            UnitBattleMetadata.Ensure(enemyGo, false, def, new Vector2Int(1, 0));

            CallPrivate(ctrl, "BeginBattle");

            var spells = ctrl.ActiveUnitSpells;
            Assert.AreEqual(2, spells.Length);
            Assert.AreEqual(1, spells[0].ActionPointCost);
            Assert.AreEqual(2, spells[1].ActionPointCost);

            Object.DestroyImmediate(ctrlGo);
            Object.DestroyImmediate(boardGo);
            Object.DestroyImmediate(playerGo);
            Object.DestroyImmediate(enemyGo);
            Object.DestroyImmediate(def);
            Object.DestroyImmediate(s1);
            Object.DestroyImmediate(s2);
        }

        [Test]
        public void BattleEndsWithPlayerDefeat_WhenAllPlayerUnitsAreDead_AndEnemyRemains()
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
            def.Portrait = null;

            // Dead player unit
            var playerGo = new GameObject("PlayerUnit");
            var playerStats = playerGo.AddComponent<UnitStats>();
            playerStats.ApplyBase(new UnitStatsData { Life = 0, ActionPoints = 1, Speed = 1, Initiative = 10 });
            UnitBattleMetadata.Ensure(playerGo, true, def, new Vector2Int(0, 0));

            // Alive enemy unit
            var enemyGo = new GameObject("EnemyUnit");
            var enemyStats = enemyGo.AddComponent<UnitStats>();
            enemyStats.ApplyBase(new UnitStatsData { Life = 10, ActionPoints = 1, Speed = 1, Initiative = 5 });
            UnitBattleMetadata.Ensure(enemyGo, false, def, new Vector2Int(1, 0));

            CallPrivate(ctrl, "BeginBattle");

            Assert.IsTrue(ctrl.HasBattleEnded, "Battle should have ended when all player units are dead.");
            Assert.AreEqual(BattleOutcome.PlayerDefeat, ctrl.Outcome);

            Object.DestroyImmediate(ctrlGo);
            Object.DestroyImmediate(boardGo);
            Object.DestroyImmediate(playerGo);
            Object.DestroyImmediate(enemyGo);
            Object.DestroyImmediate(def);
        }

        [Test]
        public void BattleEndsWithPlayerVictory_WhenAllEnemyUnitsAreDead_AndPlayerRemains()
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
            def.Portrait = null;

            // Alive player unit
            var playerGo = new GameObject("PlayerUnit");
            var playerStats = playerGo.AddComponent<UnitStats>();
            playerStats.ApplyBase(new UnitStatsData { Life = 10, ActionPoints = 1, Speed = 1, Initiative = 10 });
            UnitBattleMetadata.Ensure(playerGo, true, def, new Vector2Int(0, 0));

            // Dead enemy unit
            var enemyGo = new GameObject("EnemyUnit");
            var enemyStats = enemyGo.AddComponent<UnitStats>();
            enemyStats.ApplyBase(new UnitStatsData { Life = 0, ActionPoints = 1, Speed = 1, Initiative = 5 });
            UnitBattleMetadata.Ensure(enemyGo, false, def, new Vector2Int(1, 0));

            CallPrivate(ctrl, "BeginBattle");

            Assert.IsTrue(ctrl.HasBattleEnded, "Battle should have ended when all enemy units are dead.");
            Assert.AreEqual(BattleOutcome.PlayerVictory, ctrl.Outcome);

            Object.DestroyImmediate(ctrlGo);
            Object.DestroyImmediate(boardGo);
            Object.DestroyImmediate(playerGo);
            Object.DestroyImmediate(enemyGo);
            Object.DestroyImmediate(def);
        }

        [Test]
        public void BattleTreatsSimultaneousZeroAsDefeat()
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
            def.Portrait = null;

            // Dead player unit
            var playerGo = new GameObject("PlayerUnit");
            var playerStats = playerGo.AddComponent<UnitStats>();
            playerStats.ApplyBase(new UnitStatsData { Life = 0, ActionPoints = 1, Speed = 1, Initiative = 10 });
            UnitBattleMetadata.Ensure(playerGo, true, def, new Vector2Int(0, 0));

            // Dead enemy unit
            var enemyGo = new GameObject("EnemyUnit");
            var enemyStats = enemyGo.AddComponent<UnitStats>();
            enemyStats.ApplyBase(new UnitStatsData { Life = 0, ActionPoints = 1, Speed = 1, Initiative = 5 });
            UnitBattleMetadata.Ensure(enemyGo, false, def, new Vector2Int(1, 0));

            CallPrivate(ctrl, "BeginBattle");

            Assert.IsTrue(ctrl.HasBattleEnded, "Battle should have ended when both squads are dead.");
            Assert.AreEqual(BattleOutcome.PlayerDefeat, ctrl.Outcome);

            Object.DestroyImmediate(ctrlGo);
            Object.DestroyImmediate(boardGo);
            Object.DestroyImmediate(playerGo);
            Object.DestroyImmediate(enemyGo);
            Object.DestroyImmediate(def);
        }
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

        [Test]
        public void HighlightIsHiddenWhileUnitMoves()
        {
            // Setup board with a basic grid and highlight material
            var boardGo = new GameObject("Board");
            var board = boardGo.AddComponent<WorldPerspectiveBoard>();
            SetPrivate(board, "_columns", 5);
            SetPrivate(board, "_rows", 5);

            var shader = Shader.Find("Sprites/Default");
            Assert.NotNull(shader, "Sprites/Default shader should be available in tests.");
            var highlightMat = new Material(shader);
            SetPrivate(board, "_highlightMaterial", highlightMat);

            CallPrivate(board, "RebuildGrid");

            var ctrlGo = new GameObject("TurnController");
            var ctrl = ctrlGo.AddComponent<SimpleTurnOrderController>();
            SetPrivate(ctrl, "_board", board);

            // Active player unit
            var playerGo = new GameObject("PlayerUnit");
            var playerStats = playerGo.AddComponent<UnitStats>();
            playerStats.ApplyBase(new UnitStatsData
            {
                Attack = 5,
                ActionPoints = 2,
                Speed = 3,
                Initiative = 10
            });
            var playerDef = ScriptableObject.CreateInstance<UnitDefinition>();
            playerDef.Portrait = null;
            UnitBattleMetadata.Ensure(playerGo, true, playerDef, new Vector2Int(2, 2));

            // Initialize battle and build movement grid
            CallPrivate(ctrl, "BeginBattle");
            CallPrivate(ctrl, "RebuildLegalMoveTilesForActiveUnit");

            // Force the board highlight to be visible before starting movement
            board.SetHighlightVisible(true);
            var highlightRendererBefore = GetPrivate<MeshRenderer>(board, "_highlightMr");
            Assert.NotNull(highlightRendererBefore, "Highlight renderer should exist after SetHighlightVisible(true).");
            Assert.IsTrue(highlightRendererBefore.gameObject.activeSelf, "Highlight should be active before movement starts.");

            // Execute a legal move to the right
            var destination = new Vector2Int(3, 2);
            CallPrivate(ctrl, "TryExecuteActiveUnitMove", destination);

            // After starting movement, the selection highlight (active tile marker) should be hidden
            var highlightRendererAfter = GetPrivate<MeshRenderer>(board, "_highlightMr");
            Assert.NotNull(highlightRendererAfter, "Highlight renderer should still exist after starting movement.");
            Assert.IsFalse(highlightRendererAfter.gameObject.activeSelf, "Highlight should be hidden while the unit is moving.");

            // Cleanup
            Object.DestroyImmediate(ctrlGo);
            Object.DestroyImmediate(playerGo);
            Object.DestroyImmediate(boardGo);
            Object.DestroyImmediate(playerDef);
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

        private static T GetPrivate<T>(object obj, string field) where T : class
        {
            var fi = obj.GetType().GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (fi != null)
            {
                return fi.GetValue(obj) as T;
            }

            Debug.LogError($"Field '{field}' not found on {obj.GetType().Name}");
            return null;
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
