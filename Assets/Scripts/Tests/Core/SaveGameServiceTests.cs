using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using SevenBattles.Core.Save;

namespace SevenBattles.Tests.Core
{
    public class SaveGameServiceTests
    {
        private sealed class FakeGameStateProvider : IGameStateSaveProvider
        {
            public string[] WizardIds;

            public void PopulateGameState(SaveGameData data)
            {
                data.PlayerSquad = new PlayerSquadSaveData
                {
                    WizardIds = WizardIds ?? Array.Empty<string>()
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
    }
}
