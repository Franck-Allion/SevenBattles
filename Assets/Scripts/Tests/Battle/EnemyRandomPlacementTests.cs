using NUnit.Framework;
using UnityEngine;
using SevenBattles.Battle.Board;
using SevenBattles.Battle.Start;
using SevenBattles.Core.Battle;
using SevenBattles.Core.Players;
using SevenBattles.Core.Units;
using System.Reflection;
using System.Collections.Generic;

namespace SevenBattles.Tests.Battle
{
    public class EnemyRandomPlacementTests
    {
        [Test]
        public void PlacesEnemies_OnTopTwoRows_DistinctTiles()
        {
            // Arrange a 6x6 board
            var boardGo = new GameObject("WorldBoard");
            var board = boardGo.AddComponent<WorldPerspectiveBoard>();
            SetPrivate(board, "_columns", 6);
            SetPrivate(board, "_rows", 6);
            SetPrivate(board, "_topLeft",     new Vector2(-3f,  3f));
            SetPrivate(board, "_topRight",    new Vector2( 3f,  3f));
            SetPrivate(board, "_bottomRight", new Vector2( 3f, -3f));
            SetPrivate(board, "_bottomLeft",  new Vector2(-3f, -3f));
            board.RebuildGrid();

            // Enemy squad of 5 simple SpriteRenderer prefabs
            var defs = new List<UnitDefinition>();
            for (int i = 0; i < 5; i++)
            {
                var wizGo = new GameObject($"Enemy{i}");
                wizGo.AddComponent<SpriteRenderer>();
                var def = ScriptableObject.CreateInstance<UnitDefinition>();
                def.Prefab = wizGo;
                defs.Add(def);
            }
            var squad = ScriptableObject.CreateInstance<PlayerSquad>();
            var loadouts = new UnitSpellLoadout[defs.Count];
            for (int i = 0; i < defs.Count; i++)
            {
                loadouts[i] = new UnitSpellLoadout
                {
                    Definition = defs[i],
                    Spells = System.Array.Empty<SpellDefinition>()
                };
            }
            squad.UnitLoadouts = loadouts;

            // Controller
            var ctrlGo = new GameObject("EnemyStartController");
            var ctrl = ctrlGo.AddComponent<WorldEnemySquadStartController>();
            SetPrivate(ctrl, "_board", board);
            SetPrivate(ctrl, "_enemySquad", squad);
            SetPrivate(ctrl, "_autoStartOnPlay", false);

            // Act
            ctrl.StartEnemySquad();

            // Assert: Every spawned enemy is on row 5 or 4 (top two rows) and on a unique tile center
            int rows = board.Rows;
            int topY = rows - 1;
            int belowTopY = rows - 2;

            var found = new List<GameObject>();
            for (int i = 0; i < 5; i++)
            {
                var go = GameObject.Find($"Enemy{i}(Clone)");
                Assert.NotNull(go, $"Enemy{i} should be spawned.");
                found.Add(go);
            }

            var used = new HashSet<(int x, int y)>();
            foreach (var go in found)
            {
                var pos = go.transform.position;
                // Find matching tile index by nearest center among all tiles
                var (tx, ty) = FindNearestTile(board, pos);
                Assert.That(ty == topY || ty == belowTopY, $"Enemy at unexpected row {ty}");
                var key = (tx, ty);
                Assert.IsFalse(used.Contains(key), "Duplicate tile assignment detected.");
                used.Add(key);
                // Also assert position close to tile center
                var expected = board.TileCenterWorld(tx, ty);
                Assert.That(Vector3.Distance(pos, expected), Is.LessThan(1e-3f));
            }

            // Cleanup
            Object.DestroyImmediate(ctrlGo);
            foreach (var d in defs) Object.DestroyImmediate(d.Prefab);
            foreach (var d in defs) Object.DestroyImmediate(d);
            Object.DestroyImmediate(boardGo);
        }

        private static (int x, int y) FindNearestTile(WorldPerspectiveBoard board, Vector3 world)
        {
            int bestX = 0, bestY = 0;
            float bestDist = float.MaxValue;
            for (int y = 0; y < board.Rows; y++)
            {
                for (int x = 0; x < board.Columns; x++)
                {
                    var c = board.TileCenterWorld(x, y);
                    float d = Vector3.SqrMagnitude(world - c);
                    if (d < bestDist)
                    {
                        bestDist = d; bestX = x; bestY = y;
                    }
                }
            }
            return (bestX, bestY);
        }

        private static void SetPrivate(object obj, string field, object value)
        {
            obj.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(obj, value);
        }
    }
}
