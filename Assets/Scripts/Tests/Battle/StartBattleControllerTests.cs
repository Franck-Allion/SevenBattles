using NUnit.Framework;
using UnityEngine;
using SevenBattles.Battle.Board;
using SevenBattles.Battle.Start;
using System.Reflection;

namespace SevenBattles.Tests.Battle
{
    public class StartBattleControllerTests
    {
        [Test]
        public void SpawnsHero_AtRequestedTileCenter()
        {
            // Board setup
            var boardGo = new GameObject("Board", typeof(RectTransform));
            var boardRt = boardGo.GetComponent<RectTransform>();
            boardRt.sizeDelta = new Vector2(400, 400);

            var board = boardGo.AddComponent<UiPerspectiveBoard>();

            // Set a simple rectangular quad equal to rect bounds and a 5x5 grid
            SetPrivate(board, "_columns", 5);
            SetPrivate(board, "_rows", 5);
            var r = boardRt.rect;
            SetPrivate(board, "_topLeft", new Vector2(r.xMin, r.yMax));
            SetPrivate(board, "_topRight", new Vector2(r.xMax, r.yMax));
            SetPrivate(board, "_bottomRight", new Vector2(r.xMax, r.yMin));
            SetPrivate(board, "_bottomLeft", new Vector2(r.xMin, r.yMin));

            board.RebuildGrid();

            // Hero prefab (simple RectTransform under root)
            var heroPrefabGo = new GameObject("HeroPrefab", typeof(RectTransform));

            // Controller
            var ctrlGo = new GameObject("StartController");
            var ctrl = ctrlGo.AddComponent<BattleStartController>();
            SetPrivate(ctrl, "_board", board);
            SetPrivate(ctrl, "_heroPrefab", heroPrefabGo);

            // Place at tile (2,3)
            ctrl.StartBattleAt(2, 3);

            // Find the spawned hero (it will be a clone named with (Clone))
            var spawned = GameObject.Find("HeroPrefab(Clone)");
            Assert.IsNotNull(spawned);
            var spawnedRt = spawned.GetComponent<RectTransform>();

            // Expected center
            var expected = GetPrivate<SevenBattles.Core.Math.PerspectiveGrid>(board, "_grid").TileCenterLocal(2, 3);
            Assert.That(Vector2.Distance(spawnedRt.anchoredPosition, expected), Is.LessThan(1e-3f));

            // Cleanup
            Object.DestroyImmediate(ctrlGo);
            Object.DestroyImmediate(heroPrefabGo);
            Object.DestroyImmediate(boardGo);
        }

        private static void SetPrivate(object obj, string field, object value)
        {
            obj.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(obj, value);
        }

        private static T GetPrivate<T>(object obj, string field)
        {
            return (T)obj.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(obj);
        }
    }
}
