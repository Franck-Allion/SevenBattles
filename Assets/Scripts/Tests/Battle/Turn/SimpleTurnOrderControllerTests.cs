using System;
using NUnit.Framework;
using UnityEngine;
using SevenBattles.Battle.Turn;
using SevenBattles.Battle.Units;
using SevenBattles.Battle.Board;
using SevenBattles.Battle.Spells;
using SevenBattles.Core.Battle;
using SevenBattles.Core.Units;
using SevenBattles.Core;
using System.Collections;
using UnityEngine.TestTools;
using SevenBattles.Battle.Combat;
using SevenBattles.Battle.Movement;
using Object = UnityEngine.Object;

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
            // Active player unit (highest initiative)
            var playerGo = new GameObject("PlayerUnit");
            var playerStats = playerGo.AddComponent<UnitStats>();
            playerStats.ApplyBase(new UnitStatsData { Life = 10, ActionPoints = 1, Speed = 1, Initiative = 10, DeckCapacity = 4, DrawCapacity = 2 });
            UnitBattleMetadata.Ensure(playerGo, true, def, new Vector2Int(0, 0));
            UnitSpellDeck.Ensure(playerGo).Configure(new[] { s1, s2 }, playerStats.DeckCapacity, playerStats.DrawCapacity);

            // Enemy unit
            var enemyGo = new GameObject("EnemyUnit");
            var enemyStats = enemyGo.AddComponent<UnitStats>();
            enemyStats.ApplyBase(new UnitStatsData { Life = 10, ActionPoints = 1, Speed = 1, Initiative = 5 });
            UnitBattleMetadata.Ensure(enemyGo, false, def, new Vector2Int(1, 0));

            CallPrivate(ctrl, "BeginBattle");

            var spells = ctrl.ActiveUnitSpells;
            Assert.AreEqual(2, spells.Length);
            Assert.IsTrue(Array.Exists(spells, spell => spell != null && spell.ActionPointCost == 1));
            Assert.IsTrue(Array.Exists(spells, spell => spell != null && spell.ActionPointCost == 2));

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

        [UnityTest]
        public IEnumerator AiUnitMovesTowardNearestPlayer_WhenMovementPossible()
        {
            var boardGo = new GameObject("Board");
            var board = boardGo.AddComponent<WorldPerspectiveBoard>();
            SetPrivate(board, "_columns", 6);
            SetPrivate(board, "_rows", 6);
            SetPrivate(board, "_topLeft", new Vector2(0, 6));
            SetPrivate(board, "_topRight", new Vector2(6, 6));
            SetPrivate(board, "_bottomRight", new Vector2(6, 0));
            SetPrivate(board, "_bottomLeft", new Vector2(0, 0));
            CallPrivate(board, "RebuildGrid");

            var ctrlGo = new GameObject("TurnController");
            var movement = ctrlGo.AddComponent<BattleMovementController>();
            var ctrl = ctrlGo.AddComponent<SimpleTurnOrderController>();

            SetPrivate(movement, "_board", board);
            SetPrivate(ctrl, "_board", board);
            SetPrivate(ctrl, "_movementController", movement);

            var def = ScriptableObject.CreateInstance<UnitDefinition>();
            def.Portrait = null;

            var enemyGo = new GameObject("EnemyUnit");
            var enemyStats = enemyGo.AddComponent<UnitStats>();
            enemyStats.ApplyBase(new UnitStatsData { Life = 10, ActionPoints = 2, Speed = 2, Initiative = 20 });
            var enemyMeta = UnitBattleMetadata.Ensure(enemyGo, false, def, new Vector2Int(4, 4));

            var playerGo = new GameObject("PlayerUnit");
            var playerStats = playerGo.AddComponent<UnitStats>();
            playerStats.ApplyBase(new UnitStatsData { Life = 10, ActionPoints = 2, Speed = 2, Initiative = 10 });
            UnitBattleMetadata.Ensure(playerGo, true, def, new Vector2Int(1, 1));

            CallPrivate(ctrl, "BeginBattle");

            var expectedTile = new Vector2Int(2, 4);
            int frameBudget = 180;
            while (!ctrl.IsActiveUnitPlayerControlled && frameBudget-- > 0)
            {
                yield return null;
            }

            Assert.Greater(frameBudget, 0, "AI turn should have ended after attempting to move.");
            Assert.AreEqual(expectedTile, enemyMeta.Tile, "AI should move closer to the nearest player unit.");

            Object.DestroyImmediate(ctrlGo);
            Object.DestroyImmediate(boardGo);
            Object.DestroyImmediate(enemyGo);
            Object.DestroyImmediate(playerGo);
            Object.DestroyImmediate(def);
        }

        [UnityTest]
        public IEnumerator AiUnitSkipsMovement_WhenNoActionPoints()
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

            var ctrlGo = new GameObject("TurnController");
            var movement = ctrlGo.AddComponent<BattleMovementController>();
            var ctrl = ctrlGo.AddComponent<SimpleTurnOrderController>();

            SetPrivate(movement, "_board", board);
            SetPrivate(ctrl, "_board", board);
            SetPrivate(ctrl, "_movementController", movement);

            var def = ScriptableObject.CreateInstance<UnitDefinition>();
            def.Portrait = null;

            var enemyGo = new GameObject("EnemyUnit");
            var enemyStats = enemyGo.AddComponent<UnitStats>();
            enemyStats.ApplyBase(new UnitStatsData { Life = 10, ActionPoints = 0, Speed = 2, Initiative = 20 });
            var enemyMeta = UnitBattleMetadata.Ensure(enemyGo, false, def, new Vector2Int(3, 3));

            var playerGo = new GameObject("PlayerUnit");
            var playerStats = playerGo.AddComponent<UnitStats>();
            playerStats.ApplyBase(new UnitStatsData { Life = 10, ActionPoints = 2, Speed = 2, Initiative = 10 });
            UnitBattleMetadata.Ensure(playerGo, true, def, new Vector2Int(1, 1));

            CallPrivate(ctrl, "BeginBattle");

            var originalTile = enemyMeta.Tile;
            int frameBudget = 120;
            while (!ctrl.IsActiveUnitPlayerControlled && frameBudget-- > 0)
            {
                yield return null;
            }

            Assert.Greater(frameBudget, 0, "AI turn should end even when it cannot move.");
            Assert.AreEqual(originalTile, enemyMeta.Tile, "Enemy should remain in place without AP.");

            Object.DestroyImmediate(ctrlGo);
            Object.DestroyImmediate(boardGo);
            Object.DestroyImmediate(enemyGo);
            Object.DestroyImmediate(playerGo);
            Object.DestroyImmediate(def);
        }

        [UnityTest]
        public IEnumerator AiUnitAttacksAdjacentPlayerAndEndsTurn()
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

            var ctrlGo = new GameObject("TurnController");
            var movement = ctrlGo.AddComponent<BattleMovementController>();
            var combat = ctrlGo.AddComponent<BattleCombatController>();
            var ctrl = ctrlGo.AddComponent<SimpleTurnOrderController>();

            SetPrivate(movement, "_board", board);
            SetPrivate(combat, "_board", board);
            SetPrivate(ctrl, "_board", board);
            SetPrivate(ctrl, "_movementController", movement);
            SetPrivate(ctrl, "_combatController", combat);

            var def = ScriptableObject.CreateInstance<UnitDefinition>();
            def.Portrait = null;

            var enemyGo = new GameObject("EnemyUnit");
            var enemyStats = enemyGo.AddComponent<UnitStats>();
            enemyStats.ApplyBase(new UnitStatsData { Life = 10, ActionPoints = 1, Speed = 1, Initiative = 20, Attack = 5 });
            UnitBattleMetadata.Ensure(enemyGo, false, def, new Vector2Int(2, 2));

            var playerGo = new GameObject("PlayerUnit");
            var playerStats = playerGo.AddComponent<UnitStats>();
            playerStats.ApplyBase(new UnitStatsData { Life = 10, ActionPoints = 1, Speed = 1, Initiative = 10, Defense = 0 });
            UnitBattleMetadata.Ensure(playerGo, true, def, new Vector2Int(2, 3));

            CallPrivate(ctrl, "BeginBattle");

            int initialLife = playerStats.Life;
            int frameBudget = 240;
            while (!ctrl.IsActiveUnitPlayerControlled && frameBudget-- > 0)
            {
                yield return null;
            }

            Assert.Greater(frameBudget, 0, "AI turn should end after attacking.");
            Assert.Less(playerStats.Life, initialLife, "AI attack should reduce player life.");

            Object.DestroyImmediate(ctrlGo);
            Object.DestroyImmediate(boardGo);
            Object.DestroyImmediate(enemyGo);
            Object.DestroyImmediate(playerGo);
            Object.DestroyImmediate(def);
        }

        [Test]
        public void InspectEnemyAtTile_SetsInspectedEnemyStats()
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
            playerStats.ApplyBase(new UnitStatsData { Life = 10, ActionPoints = 1, Speed = 1, Initiative = 10 });
            UnitBattleMetadata.Ensure(playerGo, true, def, new Vector2Int(0, 0));

            var enemyGo = new GameObject("EnemyUnit");
            var enemyStats = enemyGo.AddComponent<UnitStats>();
            enemyStats.ApplyBase(new UnitStatsData { Life = 7, ActionPoints = 1, Speed = 1, Initiative = 5 });
            UnitBattleMetadata.Ensure(enemyGo, false, def, new Vector2Int(1, 0));

            CallPrivate(ctrl, "BeginBattle");

            bool inspected = (bool)CallPrivate(ctrl, "TryInspectEnemyAtTile", new Vector2Int(1, 0));

            Assert.IsTrue(inspected, "Inspecting an enemy tile should return true.");
            Assert.IsTrue(ctrl.HasInspectedEnemy, "Controller should report inspected enemy.");
            Assert.IsTrue(ctrl.TryGetInspectedEnemyStats(out var stats));
            Assert.AreEqual(7, stats.Life);

            Object.DestroyImmediate(ctrlGo);
            Object.DestroyImmediate(boardGo);
            Object.DestroyImmediate(playerGo);
            Object.DestroyImmediate(enemyGo);
            Object.DestroyImmediate(def);
        }

        [Test]
        public void InspectEnemyAtTile_IgnoresPlayerUnits()
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
            playerStats.ApplyBase(new UnitStatsData { Life = 10, ActionPoints = 1, Speed = 1, Initiative = 10 });
            UnitBattleMetadata.Ensure(playerGo, true, def, new Vector2Int(0, 0));

            CallPrivate(ctrl, "BeginBattle");

            bool inspected = (bool)CallPrivate(ctrl, "TryInspectEnemyAtTile", new Vector2Int(0, 0));

            Assert.IsFalse(inspected, "Inspecting a player tile should return false.");
            Assert.IsFalse(ctrl.HasInspectedEnemy);

            Object.DestroyImmediate(ctrlGo);
            Object.DestroyImmediate(boardGo);
            Object.DestroyImmediate(playerGo);
            Object.DestroyImmediate(def);
        }

        [Test]
        public void InspectFriendlyAtTile_AllowsPlayerUnits()
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
            playerStats.ApplyBase(new UnitStatsData { Life = 12, ActionPoints = 1, Speed = 1, Initiative = 10 });
            UnitBattleMetadata.Ensure(playerGo, true, def, new Vector2Int(0, 0));

            CallPrivate(ctrl, "BeginBattle");

            bool inspected = (bool)CallPrivate(ctrl, "TryInspectUnitAtTile", new Vector2Int(0, 0), true);

            Assert.IsTrue(inspected, "Inspecting a player tile should return true when player units are allowed.");
            Assert.IsTrue(ctrl.HasInspectedEnemy, "Controller should report inspected unit.");
            Assert.IsTrue(ctrl.TryGetInspectedEnemyStats(out var stats));
            Assert.AreEqual(12, stats.Life);

            Object.DestroyImmediate(ctrlGo);
            Object.DestroyImmediate(boardGo);
            Object.DestroyImmediate(playerGo);
            Object.DestroyImmediate(def);
        }

        [Test]
        public void ActiveUnitChange_ClearsInspectedEnemy()
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
            playerStats.ApplyBase(new UnitStatsData { Life = 10, ActionPoints = 1, Speed = 1, Initiative = 10 });
            UnitBattleMetadata.Ensure(playerGo, true, def, new Vector2Int(0, 0));

            var enemyGo = new GameObject("EnemyUnit");
            var enemyStats = enemyGo.AddComponent<UnitStats>();
            enemyStats.ApplyBase(new UnitStatsData { Life = 7, ActionPoints = 1, Speed = 1, Initiative = 5 });
            UnitBattleMetadata.Ensure(enemyGo, false, def, new Vector2Int(1, 0));

            CallPrivate(ctrl, "BeginBattle");

            CallPrivate(ctrl, "TryInspectEnemyAtTile", new Vector2Int(1, 0));
            Assert.IsTrue(ctrl.HasInspectedEnemy);

            CallPrivate(ctrl, "SetActiveIndex", 1);

            Assert.IsFalse(ctrl.HasInspectedEnemy, "Inspected enemy should be cleared when active unit changes.");

            Object.DestroyImmediate(ctrlGo);
            Object.DestroyImmediate(boardGo);
            Object.DestroyImmediate(playerGo);
            Object.DestroyImmediate(enemyGo);
            Object.DestroyImmediate(def);
        }

        [UnityTest]
        public IEnumerator AiUnitMovesThenAttacks_WhenEnoughActionPoints()
        {
            var boardGo = new GameObject("Board");
            var board = boardGo.AddComponent<WorldPerspectiveBoard>();
            SetPrivate(board, "_columns", 6);
            SetPrivate(board, "_rows", 6);
            SetPrivate(board, "_topLeft", new Vector2(0, 6));
            SetPrivate(board, "_topRight", new Vector2(6, 6));
            SetPrivate(board, "_bottomRight", new Vector2(6, 0));
            SetPrivate(board, "_bottomLeft", new Vector2(0, 0));
            CallPrivate(board, "RebuildGrid");

            var ctrlGo = new GameObject("TurnController");
            var movement = ctrlGo.AddComponent<BattleMovementController>();
            var combat = ctrlGo.AddComponent<BattleCombatController>();
            var ctrl = ctrlGo.AddComponent<SimpleTurnOrderController>();

            SetPrivate(movement, "_board", board);
            SetPrivate(movement, "_moveDurationSeconds", 0.01f);
            SetPrivate(combat, "_board", board);
            SetPrivate(ctrl, "_board", board);
            SetPrivate(ctrl, "_movementController", movement);
            SetPrivate(ctrl, "_combatController", combat);

            var def = ScriptableObject.CreateInstance<UnitDefinition>();
            def.Portrait = null;

            var enemyGo = new GameObject("EnemyUnit");
            var enemyStats = enemyGo.AddComponent<UnitStats>();
            enemyStats.ApplyBase(new UnitStatsData { Life = 10, ActionPoints = 2, Speed = 1, Initiative = 20, Attack = 5 });
            var enemyMeta = UnitBattleMetadata.Ensure(enemyGo, false, def, new Vector2Int(2, 2));

            var playerGo = new GameObject("PlayerUnit");
            var playerStats = playerGo.AddComponent<UnitStats>();
            playerStats.ApplyBase(new UnitStatsData { Life = 10, ActionPoints = 1, Speed = 1, Initiative = 10, Defense = 0 });
            UnitBattleMetadata.Ensure(playerGo, true, def, new Vector2Int(2, 4));

            CallPrivate(ctrl, "BeginBattle");

            int initialLife = playerStats.Life;
            int frameBudget = 300;
            while (!ctrl.IsActiveUnitPlayerControlled && frameBudget-- > 0)
            {
                yield return null;
            }

            Assert.Greater(frameBudget, 0, "AI turn should end after moving and attacking.");
            Assert.AreEqual(new Vector2Int(2, 3), enemyMeta.Tile, "AI should move adjacent before attacking.");
            Assert.Less(playerStats.Life, initialLife, "AI should attack after moving when AP remains.");

            Object.DestroyImmediate(ctrlGo);
            Object.DestroyImmediate(boardGo);
            Object.DestroyImmediate(enemyGo);
            Object.DestroyImmediate(playerGo);
            Object.DestroyImmediate(def);
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
