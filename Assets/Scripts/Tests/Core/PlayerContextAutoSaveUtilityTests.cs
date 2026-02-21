using System.IO;
using NUnit.Framework;
using SevenBattles.Core.Battle;
using SevenBattles.Core.Players;
using SevenBattles.Core.Save;
using SevenBattles.Core.Units;
using UnityEngine;

namespace SevenBattles.Tests.Core
{
    public class PlayerContextAutoSaveUtilityTests
    {
        [Test]
        public void SaveThenLoad_RestoresPlayerContextProgression()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "SevenBattles_AutoSaveTests");
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, true);
            }

            var context = ScriptableObject.CreateInstance<PlayerContext>();
            var unitA = ScriptableObject.CreateInstance<UnitDefinition>();
            unitA.Id = "unit.a";
            var unitB = ScriptableObject.CreateInstance<UnitDefinition>();
            unitB.Id = "unit.b";

            context.SetOwnedUnits(new[]
            {
                new OwnedUnitData
                {
                    OwnedUnitId = "owned_a",
                    CustomName = "Alpha",
                    Definition = unitA,
                    Level = 3,
                    Xp = 12,
                    Spells = System.Array.Empty<SpellDefinition>()
                },
                new OwnedUnitData
                {
                    OwnedUnitId = "owned_b",
                    CustomName = "Beta",
                    Definition = unitB,
                    Level = 2,
                    Xp = 5,
                    Spells = System.Array.Empty<SpellDefinition>()
                }
            });
            context.SetActiveSquadOwnedUnitIds(new[] { "owned_a", "owned_b" });
            context.SetResources(321, 9);
            context.SetTournamentProgress(2, new[] { true, false, false, false, false, false, false });

            bool saved = PlayerContextAutoSaveUtility.TrySaveFromPlayerContext(context, out string savePath, tempRoot);
            Assert.IsTrue(saved);
            Assert.IsTrue(File.Exists(savePath));

            context.SetResources(0, 0);
            context.SetTournamentProgress(1, new[] { false, false, false, false, false, false, false });
            context.SetOwnedUnits(System.Array.Empty<OwnedUnitData>());
            context.SetActiveSquadOwnedUnitIds(System.Array.Empty<string>());

            bool loaded = PlayerContextAutoSaveUtility.TryLoadIntoPlayerContext(context, out string loadedPath, tempRoot);
            Assert.IsTrue(loaded);
            Assert.AreEqual(savePath, loadedPath);

            Assert.AreEqual(321, context.Gold);
            Assert.AreEqual(9, context.Gems);
            Assert.AreEqual(2, context.CurrentTournamentRoundIndex);
            Assert.IsTrue(context.IsTournamentBattleCompleted(1));
            Assert.IsFalse(context.IsTournamentBattleCompleted(2));
            Assert.AreEqual(2, context.OwnedUnits.Count);
            Assert.AreEqual(2, context.ActiveSquadOwnedUnitIds.Count);
            Assert.AreEqual("Alpha", context.OwnedUnits[0].CustomName);
            Assert.AreEqual("Beta", context.OwnedUnits[1].CustomName);

            var activeLoadouts = context.GetActiveSquadLoadoutsNonAlloc();
            Assert.AreEqual(2, activeLoadouts.Count);
            Assert.AreEqual(3, activeLoadouts[0].EffectiveLevel);
            Assert.AreEqual(12, activeLoadouts[0].EffectiveXp);
            Assert.AreEqual(2, activeLoadouts[1].EffectiveLevel);
            Assert.AreEqual(5, activeLoadouts[1].EffectiveXp);

            string loadedJson = File.ReadAllText(savePath);
            StringAssert.Contains("\"OwnedUnits\"", loadedJson);
            StringAssert.Contains("\"ActiveSquadOwnedUnitIds\"", loadedJson);
            StringAssert.Contains("\"CustomName\"", loadedJson);

            UnityEngine.Object.DestroyImmediate(unitB);
            UnityEngine.Object.DestroyImmediate(unitA);
            UnityEngine.Object.DestroyImmediate(context);
            Directory.Delete(tempRoot, true);
        }

        [Test]
        public void Load_LegacyAutosaveWithoutOwnedUnits_DoesNotWipeOwnedUnits()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "SevenBattles_AutoSaveTests_LegacyOwnedUnits");
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, true);
            }

            var context = ScriptableObject.CreateInstance<PlayerContext>();
            var unitA = ScriptableObject.CreateInstance<UnitDefinition>();
            unitA.Id = "unit.a";

            context.SetOwnedUnits(new[]
            {
                new OwnedUnitData
                {
                    OwnedUnitId = "owned_a",
                    CustomName = "LegacyName",
                    Definition = unitA,
                    Level = 2,
                    Xp = 10,
                    Spells = System.Array.Empty<SpellDefinition>()
                }
            });
            context.SetActiveSquadOwnedUnitIds(System.Array.Empty<string>());

            string autosavePath = PlayerContextAutoSaveUtility.GetAutoSavePath(tempRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(autosavePath));
            File.WriteAllText(autosavePath, "{ \"Timestamp\": \"2026-01-01T00:00:00.0000000Z\", \"Gold\": 50, \"Gems\": 5, \"CurrentRoundIndex\": 1, \"CompletedBattles\": [false,false,false,false,false,false,false] }");

            bool loaded = PlayerContextAutoSaveUtility.TryLoadIntoPlayerContext(context, out string loadedPath, tempRoot);
            Assert.IsTrue(loaded);
            Assert.AreEqual(autosavePath, loadedPath);

            Assert.AreEqual(1, context.OwnedUnits.Count);
            Assert.AreEqual("owned_a", context.OwnedUnits[0].OwnedUnitId);
            Assert.AreEqual("LegacyName", context.OwnedUnits[0].CustomName);
            Assert.AreEqual(1, context.ActiveSquadOwnedUnitIds.Count);
            Assert.AreEqual("owned_a", context.ActiveSquadOwnedUnitIds[0]);

            UnityEngine.Object.DestroyImmediate(unitA);
            UnityEngine.Object.DestroyImmediate(context);
            Directory.Delete(tempRoot, true);
        }
    }
}
