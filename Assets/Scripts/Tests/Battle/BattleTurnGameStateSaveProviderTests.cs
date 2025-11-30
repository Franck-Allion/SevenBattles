using NUnit.Framework;
using UnityEngine;
using SevenBattles.Battle.Board;
using SevenBattles.Battle.Save;
using SevenBattles.Battle.Turn;
using SevenBattles.Battle.Units;
using SevenBattles.Battle.Start;
using SevenBattles.Core.Save;
using SevenBattles.Core.Units;

namespace SevenBattles.Tests.Battle
{
    public class BattleTurnGameStateSaveProviderTests
    {
        private static void SetPrivate(object target, string fieldName, object value)
        {
            var type = target.GetType();
            var field = type.GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' was not found on type '{type.FullName}'.");
            field.SetValue(target, value);
        }

        private static object CallPrivate(object target, string methodName, params object[] args)
        {
            var type = target.GetType();
            var method = type.GetMethod(
                methodName,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Public);
            Assert.IsNotNull(method, $"Method '{methodName}' was not found on type '{type.FullName}'.");
            return method.Invoke(target, args);
        }

        [Test]
        public void PopulateGameState_SetsBattlePhaseAndActiveUnit()
        {
            var boardGo = new GameObject("Board");
            var board = boardGo.AddComponent<WorldPerspectiveBoard>();
            SetPrivate(board, "_columns", 5);
            SetPrivate(board, "_rows", 5);
            CallPrivate(board, "RebuildGrid");

            var unitGo = new GameObject("Wizard");
            var stats = unitGo.AddComponent<UnitStats>();
            stats.ApplyBase(new UnitStatsData { Life = 20, ActionPoints = 2, Speed = 2, Initiative = 5 });

            var def = ScriptableObject.CreateInstance<UnitDefinition>();
            def.Id = "UnitA";

            var meta = UnitBattleMetadata.Ensure(unitGo, true, def, new Vector2Int(1, 1));
            Assert.IsNotNull(meta);

            var ctrlGo = new GameObject("TurnController");
            var ctrl = ctrlGo.AddComponent<SimpleTurnOrderController>();
            SetPrivate(ctrl, "_board", board);
            CallPrivate(ctrl, "BeginBattle");

            Assert.IsTrue(ctrl.HasActiveUnit, "There should be an active unit after BeginBattle.");

            var providerGo = new GameObject("BattleTurnProvider");
            var provider = providerGo.AddComponent<BattleTurnGameStateSaveProvider>();

            var data = new SaveGameData();
            provider.PopulateGameState(data);

            Assert.IsNotNull(data.BattleTurn, "BattleTurn should be populated.");
            Assert.AreEqual("battle", data.BattleTurn.Phase);
            Assert.GreaterOrEqual(data.BattleTurn.TurnIndex, 1);
            Assert.AreEqual("UnitA", data.BattleTurn.ActiveUnitId);
            Assert.IsFalse(string.IsNullOrEmpty(data.BattleTurn.ActiveUnitInstanceId));
            Assert.AreEqual("player", data.BattleTurn.ActiveUnitTeam);
            Assert.AreEqual(ctrl.ActiveUnitCurrentActionPoints, data.BattleTurn.ActiveUnitCurrentActionPoints);
            Assert.AreEqual(ctrl.ActiveUnitMaxActionPoints, data.BattleTurn.ActiveUnitMaxActionPoints);
            Assert.AreEqual(ctrl.ActiveUnitHasMoved, data.BattleTurn.ActiveUnitHasMoved);

            Object.DestroyImmediate(providerGo);
            Object.DestroyImmediate(ctrlGo);
            Object.DestroyImmediate(unitGo);
            Object.DestroyImmediate(boardGo);
            Object.DestroyImmediate(def);
        }

        [Test]
        public void PopulateGameState_WithoutTurnController_ReportsPlacementPhaseWhenUnlocked()
        {
            var placementGo = new GameObject("Placement");
            placementGo.AddComponent<WorldSquadPlacementController>();

            var providerGo = new GameObject("BattleTurnProvider");
            var provider = providerGo.AddComponent<BattleTurnGameStateSaveProvider>();

            var data = new SaveGameData();
            provider.PopulateGameState(data);

            Assert.IsNotNull(data.BattleTurn);
            Assert.AreEqual("placement", data.BattleTurn.Phase);
            Assert.AreEqual(0, data.BattleTurn.TurnIndex);

            Object.DestroyImmediate(providerGo);
            Object.DestroyImmediate(placementGo);
        }
    }
}
