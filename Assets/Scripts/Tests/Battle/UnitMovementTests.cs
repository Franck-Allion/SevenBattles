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

        [Test]
        public void SortingOrder_LowerRowsHaveHigherOrder()
        {
            // Test that units on lower rows (closer to camera) have higher sorting orders
            // This ensures correct visual layering in isometric perspective
            var boardGo = new GameObject("Board");
            var board = boardGo.AddComponent<WorldPerspectiveBoard>();

            SetPrivate(board, "_columns", 7);
            SetPrivate(board, "_rows", 7);
            SetPrivate(board, "_topLeft", new Vector2(0, 7));
            SetPrivate(board, "_topRight", new Vector2(7, 7));
            SetPrivate(board, "_bottomRight", new Vector2(7, 0));
            SetPrivate(board, "_bottomLeft", new Vector2(0, 0));
            CallPrivate(board, "RebuildGrid");

            int baseSortingOrder = 0;
            int rowStride = 10;

            // Row 0 (front/bottom) should have highest sorting order
            int row0Order = board.ComputeSortingOrder(3, 0, baseSortingOrder, rowStride, 0);
            
            // Row 2 (middle) should have lower sorting order than row 0
            int row2Order = board.ComputeSortingOrder(3, 2, baseSortingOrder, rowStride, 0);
            
            // Row 6 (back/top) should have lowest sorting order
            int row6Order = board.ComputeSortingOrder(3, 6, baseSortingOrder, rowStride, 0);

            // Assert that lower row numbers (closer to camera) have higher sorting orders
            Assert.Greater(row0Order, row2Order, "Row 0 should render above row 2");
            Assert.Greater(row2Order, row6Order, "Row 2 should render above row 6");
            Assert.Greater(row0Order, row6Order, "Row 0 should render above row 6");

            Object.DestroyImmediate(boardGo);
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
