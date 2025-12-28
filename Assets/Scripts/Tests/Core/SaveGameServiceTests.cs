using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using SevenBattles.Core.Save;

namespace SevenBattles.Tests.Core
{
    public class SaveGameServiceTests
    {
        private sealed class FakeGameStateProvider : IGameStateSaveProvider
        {
            public string[] WizardIds;
            public string BattlefieldId;

            public void PopulateGameState(SaveGameData data)
            {
                data.PlayerSquad = new PlayerSquadSaveData
                {
                    WizardIds = WizardIds ?? Array.Empty<string>()
                };

                if (!string.IsNullOrEmpty(BattlefieldId))
                {
                    data.BattleSession = new BattleSessionSaveData
                    {
                        BattlefieldId = BattlefieldId
                    };
                }
            }
        }

        private sealed class LevelGameStateProvider : IGameStateSaveProvider
        {
            public void PopulateGameState(SaveGameData data)
            {
                data.UnitPlacements = new[]
                {
                    new UnitPlacementSaveData
                    {
                        UnitId = "UnitA",
                        Stats = new UnitStatsSaveData
                        {
                            Life = 5,
                            MaxLife = 5,
                            Level = 2
                        }
                    }
                };

                data.BattleSession = new BattleSessionSaveData
                {
                    PlayerSquadIds = new[] { "UnitA" },
                    EnemySquadIds = Array.Empty<string>(),
                    PlayerSquadUnits = new[]
                    {
                        new UnitSpellLoadoutSaveData
                        {
                            UnitId = "UnitA",
                            SpellIds = Array.Empty<string>(),
                            Level = 3
                        }
                    },
                    EnemySquadUnits = Array.Empty<UnitSpellLoadoutSaveData>(),
                    BattleType = "test",
                    Difficulty = 0
                };
            }
        }

        private static string CreateTestDirectory()
        {
            string root = Path.Combine(Path.GetTempPath(), "SevenBattlesTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }

        [Test]
        public async Task SaveAndLoadMetadata_CreatesFileAndMetadata()
        {
            string dir = CreateTestDirectory();
            var provider = new FakeGameStateProvider
            {
                WizardIds = new[] { "WizA", "WizB" }
            };
            var service = new SaveGameService(provider, dir);

            var metadata = await service.SaveSlotAsync(1);

            Assert.AreEqual(1, metadata.SlotIndex);
            Assert.IsTrue(metadata.HasSave);
            Assert.IsFalse(string.IsNullOrEmpty(metadata.TimestampString));
            Assert.GreaterOrEqual(metadata.RunNumber, 1);

            var all = await service.LoadAllSlotMetadataAsync();
            Assert.AreEqual(service.MaxSlots, all.Length);
            Assert.IsTrue(all[0].HasSave);
            Assert.AreEqual(metadata.RunNumber, all[0].RunNumber);
            Assert.AreEqual(metadata.TimestampString, all[0].TimestampString);

            string saveDir = Path.Combine(dir, "Saves");
            string expectedPath = Path.Combine(saveDir, "save_slot_01.json");
            Assert.IsTrue(File.Exists(expectedPath), "Expected save file was not created.");
        }

        [Test]
        public async Task Overwrite_IncrementsRunNumber()
        {
            string dir = CreateTestDirectory();
            var provider = new FakeGameStateProvider
            {
                WizardIds = new[] { "WizA" }
            };
            var service = new SaveGameService(provider, dir);

            var first = await service.SaveSlotAsync(2);
            var second = await service.SaveSlotAsync(2);

            Assert.IsTrue(second.RunNumber > 0);
            Assert.AreEqual(first.RunNumber + 1, second.RunNumber);

            var all = await service.LoadAllSlotMetadataAsync();
            Assert.IsTrue(all[1].HasSave);
            Assert.AreEqual(second.RunNumber, all[1].RunNumber);
        }

        [Test]
        public async Task LoadMetadata_InvalidJson_HandledGracefully()
        {
            string dir = CreateTestDirectory();
            string saveDir = Path.Combine(dir, "Saves");
            Directory.CreateDirectory(saveDir);

            string path = Path.Combine(saveDir, "save_slot_03.json");
            File.WriteAllText(path, "{ this is not valid json");

            var provider = new FakeGameStateProvider
            {
                WizardIds = new[] { "Any" }
            };
            var service = new SaveGameService(provider, dir);
            var all = await service.LoadAllSlotMetadataAsync();

            Assert.AreEqual(service.MaxSlots, all.Length);
            Assert.IsFalse(all[2].HasSave, "Invalid JSON should be treated as empty slot.");
        }

        [Test]
        public async Task Save_IncludesUnitPlacements_FromProvider()
        {
            string dir = CreateTestDirectory();
            string saveDir = Path.Combine(dir, "Saves");
            Directory.CreateDirectory(saveDir);

            var provider = new FakeGameStateProvider
            {
                WizardIds = new[] { "WizA" }
            };

            var service = new SaveGameService(provider, dir);
            await service.SaveSlotAsync(1);

            string path = Path.Combine(saveDir, "save_slot_01.json");
            Assert.IsTrue(File.Exists(path), "Save file should exist after saving.");

            string json = File.ReadAllText(path);
            var data = JsonUtility.FromJson<SaveGameData>(json);
            Assert.IsNotNull(data, "Deserialized SaveGameData should not be null.");
            Assert.IsNotNull(data.PlayerSquad, "PlayerSquad should be populated by provider.");
            Assert.IsNotNull(data.UnitPlacements, "UnitPlacements should be initialized even if provider left it null.");
            Assert.IsNotNull(data.BattleTurn, "BattleTurn should be initialized even if provider left it null.");
        }

        [Test]
        public async Task Save_IncludesBattlefieldId_WhenProvided()
        {
            string dir = CreateTestDirectory();
            string saveDir = Path.Combine(dir, "Saves");
            Directory.CreateDirectory(saveDir);

            var provider = new FakeGameStateProvider
            {
                WizardIds = new[] { "WizA" },
                BattlefieldId = "battlefield.test"
            };

            var service = new SaveGameService(provider, dir);
            await service.SaveSlotAsync(1);

            string path = Path.Combine(saveDir, "save_slot_01.json");
            Assert.IsTrue(File.Exists(path), "Save file should exist after saving.");

            string json = File.ReadAllText(path);
            var data = JsonUtility.FromJson<SaveGameData>(json);
            Assert.IsNotNull(data.BattleSession, "BattleSession should be populated when provider supplies it.");
            Assert.AreEqual("battlefield.test", data.BattleSession.BattlefieldId);
        }

        [Test]
        public async Task Save_IncludesLevelFields_WhenProvided()
        {
            string dir = CreateTestDirectory();
            string saveDir = Path.Combine(dir, "Saves");
            Directory.CreateDirectory(saveDir);

            var provider = new LevelGameStateProvider();
            var service = new SaveGameService(provider, dir);

            await service.SaveSlotAsync(1);

            string path = Path.Combine(saveDir, "save_slot_01.json");
            string json = File.ReadAllText(path);
            var data = JsonUtility.FromJson<SaveGameData>(json);

            Assert.IsNotNull(data.UnitPlacements);
            Assert.AreEqual(2, data.UnitPlacements[0].Stats.Level);
            Assert.IsNotNull(data.BattleSession);
            Assert.AreEqual(3, data.BattleSession.PlayerSquadUnits[0].Level);
        }

        [Test]
        public async Task LoadSlotDataAsync_MissingFile_ReturnsNull()
        {
            string dir = CreateTestDirectory();
            var provider = new FakeGameStateProvider
            {
                WizardIds = new[] { "WizA" }
            };
            var service = new SaveGameService(provider, dir);

            var data = await service.LoadSlotDataAsync(1);

            Assert.IsNull(data, "LoadSlotDataAsync should return null when no save file exists.");
        }

        [Test]
        public async Task LoadSlotDataAsync_InvalidJson_ReturnsNull()
        {
            string dir = CreateTestDirectory();
            string saveDir = Path.Combine(dir, "Saves");
            Directory.CreateDirectory(saveDir);

            string path = Path.Combine(saveDir, "save_slot_01.json");
            File.WriteAllText(path, "{ this is not valid json");

            var provider = new FakeGameStateProvider
            {
                WizardIds = new[] { "Any" }
            };
            var service = new SaveGameService(provider, dir);

            // Expect the error log from invalid JSON parsing
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("SaveGameService: Failed to load save slot 1.*"));

            var data = await service.LoadSlotDataAsync(1);

            Assert.IsNull(data, "LoadSlotDataAsync should return null when JSON is invalid.");
        }

        [Test]
        public async Task LoadSlotDataAsync_ValidJson_PopulatesDefaults()
        {
            string dir = CreateTestDirectory();
            var provider = new FakeGameStateProvider
            {
                WizardIds = new[] { "WizA" }
            };
            var service = new SaveGameService(provider, dir);

            await service.SaveSlotAsync(1);

            var data = await service.LoadSlotDataAsync(1);

            Assert.IsNotNull(data, "LoadSlotDataAsync should return a SaveGameData instance for a valid save.");
            Assert.IsNotNull(data.PlayerSquad, "PlayerSquad should be non-null after load.");
            Assert.IsNotNull(data.UnitPlacements, "UnitPlacements should be non-null after load.");
            Assert.IsNotNull(data.BattleTurn, "BattleTurn should be non-null after load.");
        }
    }
}
