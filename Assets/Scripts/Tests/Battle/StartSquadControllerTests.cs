using NUnit.Framework;
using UnityEngine;
using SevenBattles.Battle.Board;
using SevenBattles.Battle.Start;
using SevenBattles.Core.Battle;
using SevenBattles.Core.Players;
using SevenBattles.Core.Units;
using System.Reflection;

namespace SevenBattles.Tests.Battle
{
    public class StartSquadControllerTests
    {
        [TearDown]
        public void TearDown()
        {
            PlayerContext.SetRuntimeInstance(null);
        }

        [Test]
        public void SpawnsThreeWizards_OnRowZero_DistinctTiles()
        {
            // Create a world board with a simple rectangular quad in local X/Y plane
            var boardGo = new GameObject("WorldBoard");
            var board = boardGo.AddComponent<WorldPerspectiveBoard>();

            // Configure a 5x5 grid over a 4x4 local square centered at origin
            SetPrivate(board, "_columns", 5);
            SetPrivate(board, "_rows", 5);
            SetPrivate(board, "_topLeft",     new Vector2(-2f,  2f));
            SetPrivate(board, "_topRight",    new Vector2( 2f,  2f));
            SetPrivate(board, "_bottomRight", new Vector2( 2f, -2f));
            SetPrivate(board, "_bottomLeft",  new Vector2(-2f, -2f));
            board.RebuildGrid();

            // Simple wizard prefabs: 3 GameObjects with SpriteRenderers
            var wizA = new GameObject("WizardA");
            wizA.AddComponent<SpriteRenderer>();
            var wizB = new GameObject("WizardB");
            wizB.AddComponent<SpriteRenderer>();
            var wizC = new GameObject("WizardC");
            wizC.AddComponent<SpriteRenderer>();

            // Controller
            var ctrlGo = new GameObject("SquadStartController");
            var ctrl = ctrlGo.AddComponent<WorldSquadStartController>();
            SetPrivate(ctrl, "_board", board);
            SetPrivate(ctrl, "_wizardPrefabs", new GameObject[] { wizA, wizB, wizC });
            SetPrivate(ctrl, "_tileXs", new int[] { 0, 1, 2 });
            SetPrivate(ctrl, "_rowY", 0);

            // Act
            ctrl.StartSquad();

            // Assert each spawned wizard is at the tile center
            var spawnedA = GameObject.Find("WizardA(Clone)");
            var spawnedB = GameObject.Find("WizardB(Clone)");
            var spawnedC = GameObject.Find("WizardC(Clone)");
            Assert.NotNull(spawnedA);
            Assert.NotNull(spawnedB);
            Assert.NotNull(spawnedC);

            var aPos = spawnedA.transform.position;
            var bPos = spawnedB.transform.position;
            var cPos = spawnedC.transform.position;

            var aExpected = board.TileCenterWorld(0, 0);
            var bExpected = board.TileCenterWorld(1, 0);
            var cExpected = board.TileCenterWorld(2, 0);

            Assert.That(Vector3.Distance(aPos, aExpected), Is.LessThan(1e-4f));
            Assert.That(Vector3.Distance(bPos, bExpected), Is.LessThan(1e-4f));
            Assert.That(Vector3.Distance(cPos, cExpected), Is.LessThan(1e-4f));

            // Cleanup
            Object.DestroyImmediate(ctrlGo);
            Object.DestroyImmediate(wizA);
            Object.DestroyImmediate(wizB);
            Object.DestroyImmediate(wizC);
            Object.DestroyImmediate(boardGo);
        }

        [Test]
        public void AppliesScaleMultiplier_OnSpawn()
        {
            // Board setup
            var boardGo = new GameObject("WorldBoard");
            var board = boardGo.AddComponent<WorldPerspectiveBoard>();
            SetPrivate(board, "_columns", 3);
            SetPrivate(board, "_rows", 3);
            SetPrivate(board, "_topLeft",     new Vector2(-1f,  1f));
            SetPrivate(board, "_topRight",    new Vector2( 1f,  1f));
            SetPrivate(board, "_bottomRight", new Vector2( 1f, -1f));
            SetPrivate(board, "_bottomLeft",  new Vector2(-1f, -1f));
            board.RebuildGrid();

            // Prefab with default scale (1,1,1)
            var wiz = new GameObject("WizardScale");
            wiz.AddComponent<SpriteRenderer>();

            var ctrlGo = new GameObject("SquadStartController");
            var ctrl = ctrlGo.AddComponent<WorldSquadStartController>();
            SetPrivate(ctrl, "_board", board);
            SetPrivate(ctrl, "_wizardPrefabs", new GameObject[] { wiz });
            SetPrivate(ctrl, "_tileXs", new int[] { 0 });
            SetPrivate(ctrl, "_rowY", 0);
            SetPrivate(ctrl, "_scaleMultiplier", 2f);

            ctrl.StartSquad();

            var spawned = GameObject.Find("WizardScale(Clone)");
            Assert.NotNull(spawned);
            Assert.That(spawned.transform.localScale, Is.EqualTo(new Vector3(2f, 2f, 2f)).Within(1e-6f));

            // Cleanup
            Object.DestroyImmediate(ctrlGo);
            Object.DestroyImmediate(wiz);
            Object.DestroyImmediate(boardGo);
        }

        [Test]
        public void StartSquad_PrefersRuntimePlayerContextLoadoutLevel()
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

            var prefab = new GameObject("RuntimeLevelUnit");
            prefab.AddComponent<SpriteRenderer>();

            var unitDef = ScriptableObject.CreateInstance<UnitDefinition>();
            unitDef.Id = "unit.runtime.test";
            unitDef.Prefab = prefab;

            var assetSquad = ScriptableObject.CreateInstance<PlayerSquad>();
            assetSquad.UnitLoadouts = new[]
            {
                new UnitSpellLoadout { Definition = unitDef, Level = 1, Xp = 0 }
            };

            var runtimeSquad = ScriptableObject.CreateInstance<PlayerSquad>();
            runtimeSquad.UnitLoadouts = new[]
            {
                new UnitSpellLoadout { Definition = unitDef, Level = 4, Xp = 0 }
            };

            var assetContext = ScriptableObject.CreateInstance<PlayerContext>();
            assetContext.PlayerSquad = assetSquad;

            var runtimeContext = ScriptableObject.CreateInstance<PlayerContext>();
            runtimeContext.PlayerSquad = runtimeSquad;
            PlayerContext.SetRuntimeInstance(runtimeContext);

            var ctrlGo = new GameObject("SquadStartController");
            var ctrl = ctrlGo.AddComponent<WorldSquadStartController>();
            SetPrivate(ctrl, "_board", board);
            SetPrivate(ctrl, "_playerContext", assetContext);
            SetPrivate(ctrl, "_wizardPrefabs", new GameObject[0]);
            SetPrivate(ctrl, "_tileXs", new[] { 0 });
            SetPrivate(ctrl, "_rowY", 0);

            ctrl.StartSquad();

            var spawned = GameObject.Find("RuntimeLevelUnit(Clone)");
            Assert.NotNull(spawned);
            var stats = spawned.GetComponent<SevenBattles.Battle.Units.UnitStats>();
            Assert.NotNull(stats);
            Assert.AreEqual(4, stats.Level, "Spawned unit should use runtime context loadout level.");

            Object.DestroyImmediate(ctrlGo);
            Object.DestroyImmediate(runtimeContext);
            Object.DestroyImmediate(assetContext);
            Object.DestroyImmediate(runtimeSquad);
            Object.DestroyImmediate(assetSquad);
            Object.DestroyImmediate(unitDef);
            Object.DestroyImmediate(prefab);
            Object.DestroyImmediate(boardGo);
        }

        // The initialization now relies solely on Character4D.SetDirection via reflection.
        // No fallback toggling is performed anymore.

        private static void SetPrivate(object obj, string field, object value)
        {
            obj.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(obj, value);
        }
    }
}
