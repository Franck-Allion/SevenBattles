using NUnit.Framework;
using UnityEngine;
using SevenBattles.Battle.Board;
using SevenBattles.Battle.Start;
using SevenBattles.Core.Battle;
using SevenBattles.Core.Players;
using SevenBattles.Core.Units;
using SevenBattles.Battle.Units;

namespace SevenBattles.Tests.Battle
{
    public class UnitDefinitionControllerTests
    {
        [Test]
        public void Spawns_From_Definition_And_Applies_Stats()
        {
            // Setup board
            var boardGo = new GameObject("WorldBoard");
            var board = boardGo.AddComponent<WorldPerspectiveBoard>();
            SetPrivate(board, "_columns", 3);
            SetPrivate(board, "_rows", 3);
            SetPrivate(board, "_topLeft",     new Vector2(-1f,  1f));
            SetPrivate(board, "_topRight",    new Vector2( 1f,  1f));
            SetPrivate(board, "_bottomRight", new Vector2( 1f, -1f));
            SetPrivate(board, "_bottomLeft",  new Vector2(-1f, -1f));
            board.RebuildGrid();

            // Wizard prefab (runtime instance must get stats)
            var wizPrefab = new GameObject("WizardFromDef");
            wizPrefab.AddComponent<SpriteRenderer>();

            // ScriptableObject definition
            var def = ScriptableObject.CreateInstance<UnitDefinition>();
            def.Id = "wiz_def_test";
            def.Prefab = wizPrefab;
            def.BaseStats = new UnitStatsData { MaxHP = 42, ActionPoints = 7, Speed = 3, Initiative = 5 };

            var ctrlGo = new GameObject("SquadPlacementCtrl");
            var ctrl = ctrlGo.AddComponent<WorldSquadPlacementController>();
            SetPrivate(ctrl, "_board", board);
            var squad = ScriptableObject.CreateInstance<PlayerSquad>();
            squad.UnitLoadouts = new[]
            {
                new UnitSpellLoadout
                {
                    Definition = def,
                    Spells = System.Array.Empty<SpellDefinition>()
                }
            };
            SetPrivate(ctrl, "_playerSquad", squad);
            SetPrivate(ctrl, "_playerRows", 2);

            // Initialize controller (Start won't be called in EditMode tests, ensure model exists)
            // Try place at (0,0)
            var ok = ctrl.TryPlaceAt(0, new Vector2Int(0, 0));
            Assert.IsTrue(ok, "TryPlaceAt should succeed for definition-based wizard.")
                ;
            var spawned = GameObject.Find("WizardFromDef(Clone)");
            Assert.NotNull(spawned, "Spawned instance should exist.");

            var stats = spawned.GetComponent<UnitStats>();
            Assert.NotNull(stats, "WizardStats should be attached.");
            Assert.AreEqual(42, stats.MaxHP);
            Assert.AreEqual(7, stats.ActionPoints);
            Assert.AreEqual(3, stats.Speed);
            Assert.AreEqual(5, stats.Initiative);

            // Cleanup
            Object.DestroyImmediate(ctrlGo);
            Object.DestroyImmediate(wizPrefab);
            Object.DestroyImmediate(boardGo);
            Object.DestroyImmediate(def);
            Object.DestroyImmediate(squad);
        }

        private static void SetPrivate(object obj, string field, object value)
        {
            var f = obj.GetType().GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            f.SetValue(obj, value);
        }
    }
}
