using NUnit.Framework;
using UnityEngine;
using SevenBattles.Battle.Turn;
using SevenBattles.Battle.Units;
using SevenBattles.Battle.Board;
using SevenBattles.Core.Units;

namespace SevenBattles.Tests.Battle
{
    public class UnitMovementTests
    {
        [Test]
        public void TileOutsideSpeedRange_IsIllegalDestination()
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

            var unitGo = new GameObject("Wizard");
            var stats = unitGo.AddComponent<UnitStats>();
            stats.ApplyBase(new UnitStatsData { Attack = 2, ActionPoints = 2, Speed = 2, Initiative = 5 });

            var def = ScriptableObject.CreateInstance<UnitDefinition>();
            def.Portrait = null;

            UnitBattleMetadata.Ensure(unitGo, true, def, new Vector2Int(3, 3));

            var ctrlGo = new GameObject("TurnController");
            var ctrl = ctrlGo.AddComponent<SimpleTurnOrderController>();

            SetPrivate(ctrl, "_board", board);
            CallPrivate(ctrl, "BeginBattle");

            var farTile = new Vector2Int(3, 6);
            bool isLegal = (bool)CallPrivate(ctrl, "IsTileLegalMoveDestination", farTile);
            Assert.IsFalse(isLegal);

            Object.DestroyImmediate(ctrlGo);
            Object.DestroyImmediate(unitGo);
            Object.DestroyImmediate(boardGo);
            Object.DestroyImmediate(def);
        }

        [Test]
        public void OccupiedTile_IsNotLegalDestination()
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

            var aGo = new GameObject("WizardA");
            var aStats = aGo.AddComponent<UnitStats>();
            aStats.ApplyBase(new UnitStatsData { Attack = 2, ActionPoints = 2, Speed = 3, Initiative = 10 });

            var bGo = new GameObject("WizardB");
            var bStats = bGo.AddComponent<UnitStats>();
            bStats.ApplyBase(new UnitStatsData { Attack = 2, ActionPoints = 2, Speed = 3, Initiative = 5 });

            var def = ScriptableObject.CreateInstance<UnitDefinition>();
            def.Portrait = null;

            UnitBattleMetadata.Ensure(aGo, true, def, new Vector2Int(1, 1));
            UnitBattleMetadata.Ensure(bGo, false, def, new Vector2Int(2, 2));

            var ctrlGo = new GameObject("TurnController");
            var ctrl = ctrlGo.AddComponent<SimpleTurnOrderController>();

            SetPrivate(ctrl, "_board", board);
            CallPrivate(ctrl, "BeginBattle");

            var occupiedTile = new Vector2Int(2, 2);
            bool isLegal = (bool)CallPrivate(ctrl, "IsTileLegalMoveDestination", occupiedTile);
            Assert.IsFalse(isLegal);

            Object.DestroyImmediate(ctrlGo);
            Object.DestroyImmediate(aGo);
            Object.DestroyImmediate(bGo);
            Object.DestroyImmediate(boardGo);
            Object.DestroyImmediate(def);
        }

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

        private static void CallPrivate(object obj, string method)
        {
            var mi = obj.GetType().GetMethod(
                method,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Public);
            mi.Invoke(obj, null);
        }
    }
}
