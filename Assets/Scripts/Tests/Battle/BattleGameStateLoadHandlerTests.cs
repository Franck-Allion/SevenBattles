using NUnit.Framework;
using UnityEngine;
using SevenBattles.Battle.Board;
using SevenBattles.Battle.Save;
using SevenBattles.Battle.Turn;
using SevenBattles.Battle.Units;
using SevenBattles.Core.Players;
using SevenBattles.Core.Save;
using SevenBattles.Core.Units;

namespace SevenBattles.Tests.Battle
{
    public class BattleGameStateLoadHandlerTests
    {
        [Test]
        public void ApplyLoadedGame_SpawnsUnitsAndRestoresTurnState()
        {
            var boardGo = new GameObject("WorldBoard");
            var board = boardGo.AddComponent<WorldPerspectiveBoard>();
            SetPrivate(board, "_columns", 5);
            SetPrivate(board, "_rows", 5);
            SetPrivate(board, "_topLeft", new Vector2(-2f, 2f));
            SetPrivate(board, "_topRight", new Vector2(2f, 2f));
            SetPrivate(board, "_bottomRight", new Vector2(2f, -2f));
            SetPrivate(board, "_bottomLeft", new Vector2(-2f, -2f));
            board.RebuildGrid();

            var wizardPrefab = new GameObject("WizardPrefab");
            wizardPrefab.AddComponent<SpriteRenderer>();

            var def = ScriptableObject.CreateInstance<UnitDefinition>();
            def.Id = "UnitA";
            def.Prefab = wizardPrefab;
            def.BaseStats = new UnitStatsData { Life = 10, ActionPoints = 2, Speed = 2, Initiative = 5 };

            var squad = ScriptableObject.CreateInstance<PlayerSquad>();
            squad.Wizards = new[] { def };

            var ctrlGo = new GameObject("TurnController");
            var ctrl = ctrlGo.AddComponent<SimpleTurnOrderController>();
            SetPrivate(ctrl, "_board", board);

            var handlerGo = new GameObject("BattleGameStateLoadHandler");
            var handler = handlerGo.AddComponent<BattleGameStateLoadHandler>();
            SetPrivate(handler, "_board", board);
            SetPrivate(handler, "_playerSquad", squad);
            SetPrivate(handler, "_enemySquad", null);
            SetPrivate(handler, "_turnController", ctrl);

            var save = new SaveGameData
            {
                UnitPlacements = new[]
                {
                    new UnitPlacementSaveData
                    {
                        UnitId = "UnitA",
                        InstanceId = "instance-unit-a",
                        Team = "player",
                        X = 2,
                        Y = 1,
                        Facing = "up",
                        Dead = false,
                        Stats = new UnitStatsSaveData
                        {
                            Life = 23,
                            MaxLife = 30,
                            Attack = 5,
                            Shoot = 3,
                            Spell = 2,
                            Speed = 4,
                            Luck = 1,
                            Defense = 2,
                            Protection = 1,
                            Initiative = 10,
                            Morale = 0
                        }
                    }
                },
                BattleTurn = new BattleTurnSaveData
                {
                    Phase = "battle",
                    TurnIndex = 2,
                    ActiveUnitId = "UnitA",
                    ActiveUnitInstanceId = "instance-unit-a",
                    ActiveUnitTeam = "player",
                    ActiveUnitCurrentActionPoints = 1,
                    ActiveUnitMaxActionPoints = 3,
                    ActiveUnitHasMoved = true
                }
            };

            handler.ApplyLoadedGame(save);

            var metas = Object.FindObjectsByType<UnitBattleMetadata>(FindObjectsSortMode.None);
            Assert.AreEqual(1, metas.Length, "Exactly one unit should be spawned from save.");

            var meta = metas[0];
            Assert.AreEqual(new Vector2Int(2, 1), meta.Tile);
            Assert.IsTrue(meta.IsPlayerControlled);
            Assert.IsNotNull(meta.Definition);
            Assert.AreEqual("UnitA", meta.Definition.Id);

            var stats = meta.GetComponent<UnitStats>();
            Assert.IsNotNull(stats, "UnitStats should be attached to spawned unit.");
            Assert.AreEqual(23, stats.Life);
            Assert.AreEqual(10, stats.Initiative);

            Assert.IsTrue(ctrl.HasActiveUnit, "Turn controller should have an active unit after restore.");
            Assert.AreEqual(2, ctrl.TurnIndex);
            Assert.AreEqual(1, ctrl.ActiveUnitCurrentActionPoints);
            Assert.AreEqual(3, ctrl.ActiveUnitMaxActionPoints);
            Assert.IsTrue(ctrl.ActiveUnitHasMoved);

            Object.DestroyImmediate(handlerGo);
            Object.DestroyImmediate(ctrlGo);
            Object.DestroyImmediate(boardGo);
            Object.DestroyImmediate(wizardPrefab);
            Object.DestroyImmediate(def);
            Object.DestroyImmediate(squad);

            var spawned = GameObject.Find("WizardPrefab(Clone)");
            if (spawned != null)
            {
                Object.DestroyImmediate(spawned);
            }
        }

        [Test]
        public void ApplyLoadedGame_DoesNotSpawnDeadUnits()
        {
            var boardGo = new GameObject("WorldBoard");
            var board = boardGo.AddComponent<WorldPerspectiveBoard>();
            SetPrivate(board, "_columns", 3);
            SetPrivate(board, "_rows", 3);
            SetPrivate(board, "_topLeft", new Vector2(-1f, 1f));
            SetPrivate(board, "_topRight", new Vector2(1f, 1f));
            SetPrivate(board, "_bottomRight", new Vector2(1f, -1f));
            SetPrivate(board, "_bottomLeft", new Vector2(-1f, -1f));
            board.RebuildGrid();

            var handlerGo = new GameObject("BattleGameStateLoadHandler");
            var handler = handlerGo.AddComponent<BattleGameStateLoadHandler>();
            SetPrivate(handler, "_board", board);

            var save = new SaveGameData
            {
                UnitPlacements = new[]
                {
                    new UnitPlacementSaveData
                    {
                        UnitId = "UnitA",
                        Team = "player",
                        X = 1,
                        Y = 1,
                        Facing = "up",
                        Dead = true
                    }
                },
                BattleTurn = new BattleTurnSaveData
                {
                    Phase = "battle",
                    TurnIndex = 1
                }
            };

            handler.ApplyLoadedGame(save);

            var metas = Object.FindObjectsByType<UnitBattleMetadata>(FindObjectsSortMode.None);
            Assert.AreEqual(0, metas.Length, "Dead units should not be spawned when loading.");

            Object.DestroyImmediate(handlerGo);
            Object.DestroyImmediate(boardGo);
        }

        private static void SetPrivate(object target, string fieldName, object value)
        {
            var type = target.GetType();
            var field = type.GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' was not found on type '{type.FullName}'.");
            field.SetValue(target, value);
        }
    }
}

