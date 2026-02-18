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
            var squad = ScriptableObject.CreateInstance<PlayerSquad>();
            var unitA = ScriptableObject.CreateInstance<UnitDefinition>();
            unitA.Id = "unit.a";
            var unitB = ScriptableObject.CreateInstance<UnitDefinition>();
            unitB.Id = "unit.b";

            squad.UnitLoadouts = new[]
            {
                new UnitSpellLoadout
                {
                    Definition = unitA,
                    Level = 3,
                    Xp = 12,
                    Spells = System.Array.Empty<SpellDefinition>()
                },
                new UnitSpellLoadout
                {
                    Definition = unitB,
                    Level = 2,
                    Xp = 5,
                    Spells = System.Array.Empty<SpellDefinition>()
                }
            };

            context.PlayerSquad = squad;
            context.SetResources(321, 9);
            context.SetTournamentProgress(2, new[] { true, false, false, false, false, false, false });

            bool saved = PlayerContextAutoSaveUtility.TrySaveFromPlayerContext(context, out string savePath, tempRoot);
            Assert.IsTrue(saved);
            Assert.IsTrue(File.Exists(savePath));

            context.SetResources(0, 0);
            context.SetTournamentProgress(1, new[] { false, false, false, false, false, false, false });
            squad.UnitLoadouts[0].Level = 1;
            squad.UnitLoadouts[0].Xp = 0;
            squad.UnitLoadouts[1].Level = 1;
            squad.UnitLoadouts[1].Xp = 0;

            bool loaded = PlayerContextAutoSaveUtility.TryLoadIntoPlayerContext(context, out string loadedPath, tempRoot);
            Assert.IsTrue(loaded);
            Assert.AreEqual(savePath, loadedPath);

            Assert.AreEqual(321, context.Gold);
            Assert.AreEqual(9, context.Gems);
            Assert.AreEqual(2, context.CurrentTournamentRoundIndex);
            Assert.IsTrue(context.IsTournamentBattleCompleted(1));
            Assert.IsFalse(context.IsTournamentBattleCompleted(2));
            Assert.AreEqual(3, squad.UnitLoadouts[0].EffectiveLevel);
            Assert.AreEqual(12, squad.UnitLoadouts[0].EffectiveXp);
            Assert.AreEqual(2, squad.UnitLoadouts[1].EffectiveLevel);
            Assert.AreEqual(5, squad.UnitLoadouts[1].EffectiveXp);

            UnityEngine.Object.DestroyImmediate(unitB);
            UnityEngine.Object.DestroyImmediate(unitA);
            UnityEngine.Object.DestroyImmediate(squad);
            UnityEngine.Object.DestroyImmediate(context);
            Directory.Delete(tempRoot, true);
        }
    }
}
