using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using SevenBattles.Core.Items;
using SevenBattles.Core.Players;
using SevenBattles.Core.Save;

namespace SevenBattles.Tests.Core
{
    public class SaveGameServiceTests
    {
        private sealed class FakeGameStateProvider : IGameStateSaveProvider
        {
            public string[] WizardIds;
            public string BattlefieldId;
            public int Gold;
            public int Gems;
            public InventoryEntrySaveData[] InventoryEntries;
            public OwnedUnitSaveData[] OwnedUnits;
            public string[] ActiveSquadOwnedUnitIds;
            public int TournamentCurrentRound = 1;
            public bool[] TournamentCompletedBattles;

            public void PopulateGameState(SaveGameData data)
            {
                data.PlayerResources = new PlayerResourcesSaveData
                {
                    Gold = Gold,
                    Gems = Gems
                };

                data.PlayerInventory = new PlayerInventorySaveData
                {
                    Entries = InventoryEntries ?? Array.Empty<InventoryEntrySaveData>()
                };

                data.PlayerOwnedUnits = new PlayerOwnedUnitsSaveData
                {
                    Units = OwnedUnits ?? Array.Empty<OwnedUnitSaveData>(),
                    ActiveSquadOwnedUnitIds = ActiveSquadOwnedUnitIds ?? Array.Empty<string>()
                };

                data.TournamentProgress = new TournamentProgressSaveData
                {
                    CurrentRoundIndex = TournamentCurrentRound,
                    CompletedBattles = TournamentCompletedBattles ?? Array.Empty<bool>()
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
                            Level = 3,
                            Xp = 42
                        }
                    },
                    EnemySquadUnits = Array.Empty<UnitSpellLoadoutSaveData>(),
                    BattleType = "test",
                    Difficulty = 0
                };
            }
        }

        private sealed class ShootStatsGameStateProvider : IGameStateSaveProvider
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
                            ShootRange = 4,
                            ShootDefense = 2
                        }
                    }
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
            Assert.IsNotNull(data.UnitPlacements, "UnitPlacements should be initialized even if provider left it null.");
            Assert.IsNotNull(data.BattleTurn, "BattleTurn should be initialized even if provider left it null.");
            Assert.IsNotNull(data.BattleEnchantments, "BattleEnchantments should be initialized even if provider left it null.");
            Assert.IsNotNull(data.PlayerResources, "PlayerResources should be initialized even if provider left it null.");
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
        public async Task Save_IncludesPlayerResources_WhenProvided()
        {
            string dir = CreateTestDirectory();
            string saveDir = Path.Combine(dir, "Saves");
            Directory.CreateDirectory(saveDir);

            var provider = new FakeGameStateProvider
            {
                WizardIds = new[] { "WizA" },
                Gold = 1234,
                Gems = 56
            };

            var service = new SaveGameService(provider, dir);
            await service.SaveSlotAsync(1);

            string path = Path.Combine(saveDir, "save_slot_01.json");
            string json = File.ReadAllText(path);
            var data = JsonUtility.FromJson<SaveGameData>(json);

            Assert.IsNotNull(data.PlayerResources);
            Assert.AreEqual(1234, data.PlayerResources.Gold);
            Assert.AreEqual(56, data.PlayerResources.Gems);
        }

        [Test]
        public async Task Save_IncludesPlayerInventory_WhenProvided()
        {
            string dir = CreateTestDirectory();
            string saveDir = Path.Combine(dir, "Saves");
            Directory.CreateDirectory(saveDir);

            var provider = new FakeGameStateProvider
            {
                WizardIds = new[] { "WizA" },
                InventoryEntries = new[]
                {
                    new InventoryEntrySaveData
                    {
                        Kind = "Equipment",
                        DefinitionId = "eq.sword",
                        Quantity = 1
                    },
                    new InventoryEntrySaveData
                    {
                        Kind = "Item",
                        DefinitionId = "item.potion",
                        Quantity = 3
                    }
                }
            };

            var service = new SaveGameService(provider, dir);
            await service.SaveSlotAsync(1);

            string path = Path.Combine(saveDir, "save_slot_01.json");
            string json = File.ReadAllText(path);
            var data = JsonUtility.FromJson<SaveGameData>(json);

            Assert.IsNotNull(data.PlayerInventory);
            Assert.IsNotNull(data.PlayerInventory.Entries);
            Assert.AreEqual(2, data.PlayerInventory.Entries.Length);
            Assert.AreEqual("Equipment", data.PlayerInventory.Entries[0].Kind);
            Assert.AreEqual("eq.sword", data.PlayerInventory.Entries[0].DefinitionId);
            Assert.AreEqual(1, data.PlayerInventory.Entries[0].Quantity);
            Assert.AreEqual("Item", data.PlayerInventory.Entries[1].Kind);
            Assert.AreEqual("item.potion", data.PlayerInventory.Entries[1].DefinitionId);
            Assert.AreEqual(3, data.PlayerInventory.Entries[1].Quantity);
        }

        [Test]
        public async Task Save_IncludesTournamentProgress_WhenProvided()
        {
            string dir = CreateTestDirectory();
            string saveDir = Path.Combine(dir, "Saves");
            Directory.CreateDirectory(saveDir);

            var provider = new FakeGameStateProvider
            {
                WizardIds = new[] { "WizA" },
                TournamentCurrentRound = 3,
                TournamentCompletedBattles = new[] { true, true, false, false, false, false, false }
            };

            var service = new SaveGameService(provider, dir);
            await service.SaveSlotAsync(1);

            string path = Path.Combine(saveDir, "save_slot_01.json");
            string json = File.ReadAllText(path);
            var data = JsonUtility.FromJson<SaveGameData>(json);

            Assert.IsNotNull(data.TournamentProgress);
            Assert.AreEqual(3, data.TournamentProgress.CurrentRoundIndex);
            Assert.IsNotNull(data.TournamentProgress.CompletedBattles);
            Assert.AreEqual(7, data.TournamentProgress.CompletedBattles.Length);
            Assert.IsTrue(data.TournamentProgress.CompletedBattles[0]);
            Assert.IsTrue(data.TournamentProgress.CompletedBattles[1]);
            Assert.IsFalse(data.TournamentProgress.CompletedBattles[2]);
        }

        [Test]
        public async Task Save_IncludesPlayerOwnedUnits_WhenProvided()
        {
            string dir = CreateTestDirectory();
            string saveDir = Path.Combine(dir, "Saves");
            Directory.CreateDirectory(saveDir);

            var provider = new FakeGameStateProvider
            {
                WizardIds = new[] { "WizA" },
                OwnedUnits = new[]
                {
                    new OwnedUnitSaveData
                    {
                        OwnedUnitId = "owned_1",
                        CustomName = "  Archmage  ",
                        UnitId = "WizA",
                        Level = 3,
                        Xp = 12,
                        SpellIds = new[] { "SpellA" },
                        EquippedItems = new[]
                        {
                            new EquipmentSlotEntry
                            {
                                SlotType = EquipmentSlotType.Weapon,
                                EquipmentDefinitionId = "eq.staff"
                            }
                        }
                    }
                },
                ActiveSquadOwnedUnitIds = new[] { "owned_1" }
            };

            var service = new SaveGameService(provider, dir);
            await service.SaveSlotAsync(1);

            string path = Path.Combine(saveDir, "save_slot_01.json");
            string json = File.ReadAllText(path);
            var data = JsonUtility.FromJson<SaveGameData>(json);

            Assert.IsNotNull(data.PlayerOwnedUnits);
            Assert.IsNotNull(data.PlayerOwnedUnits.Units);
            Assert.AreEqual(1, data.PlayerOwnedUnits.Units.Length);
            Assert.AreEqual("owned_1", data.PlayerOwnedUnits.Units[0].OwnedUnitId);
            Assert.AreEqual("Archmage", data.PlayerOwnedUnits.Units[0].CustomName);
            Assert.AreEqual("WizA", data.PlayerOwnedUnits.Units[0].UnitId);
            Assert.IsNotNull(data.PlayerOwnedUnits.Units[0].EquippedItems);
            Assert.AreEqual(1, data.PlayerOwnedUnits.Units[0].EquippedItems.Length);
            Assert.AreEqual(EquipmentSlotType.Weapon, data.PlayerOwnedUnits.Units[0].EquippedItems[0].SlotType);
            Assert.AreEqual("eq.staff", data.PlayerOwnedUnits.Units[0].EquippedItems[0].EquipmentDefinitionId);
            Assert.IsNotNull(data.PlayerOwnedUnits.ActiveSquadOwnedUnitIds);
            Assert.AreEqual("owned_1", data.PlayerOwnedUnits.ActiveSquadOwnedUnitIds[0]);
            StringAssert.Contains("\"PlayerOwnedUnits\"", json);
            StringAssert.Contains("\"CustomName\": \"Archmage\"", json);
            StringAssert.Contains("\"EquippedItems\"", json);
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
            Assert.AreEqual(42, data.BattleSession.PlayerSquadUnits[0].Xp);
            StringAssert.Contains("\"Xp\": 42", json);
        }

        [Test]
        public async Task Save_IncludesShootRangeAndDefense_WhenProvided()
        {
            string dir = CreateTestDirectory();
            string saveDir = Path.Combine(dir, "Saves");
            Directory.CreateDirectory(saveDir);

            var provider = new ShootStatsGameStateProvider();
            var service = new SaveGameService(provider, dir);

            await service.SaveSlotAsync(1);

            string path = Path.Combine(saveDir, "save_slot_01.json");
            string json = File.ReadAllText(path);
            var data = JsonUtility.FromJson<SaveGameData>(json);

            Assert.IsNotNull(data.UnitPlacements);
            Assert.AreEqual(4, data.UnitPlacements[0].Stats.ShootRange);
            Assert.AreEqual(2, data.UnitPlacements[0].Stats.ShootDefense);
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
            Assert.IsNotNull(data.UnitPlacements, "UnitPlacements should be non-null after load.");
            Assert.IsNotNull(data.BattleTurn, "BattleTurn should be non-null after load.");
            Assert.IsNotNull(data.BattleEnchantments, "BattleEnchantments should be non-null after load.");
        }

        [Test]
        public async Task LoadSlotDataAsync_MissingBattleEnchantments_DefaultsToEmpty()
        {
            string dir = CreateTestDirectory();
            string saveDir = Path.Combine(dir, "Saves");
            Directory.CreateDirectory(saveDir);

            string path = Path.Combine(saveDir, "save_slot_01.json");
            File.WriteAllText(path, "{ \"Timestamp\": \"2025-01-01 00:00:00\", \"RunNumber\": 1 }");

            var provider = new FakeGameStateProvider
            {
                WizardIds = new[] { "Any" }
            };
            var service = new SaveGameService(provider, dir);

            var data = await service.LoadSlotDataAsync(1);

            Assert.IsNotNull(data, "LoadSlotDataAsync should return a SaveGameData instance for valid JSON.");
            Assert.IsNotNull(data.BattleEnchantments, "BattleEnchantments should default to an empty array when missing.");
            Assert.AreEqual(0, data.BattleEnchantments.Length);
        }

        [Test]
        public async Task LoadSlotDataAsync_MissingPlayerResources_DefaultsToZero()
        {
            string dir = CreateTestDirectory();
            string saveDir = Path.Combine(dir, "Saves");
            Directory.CreateDirectory(saveDir);

            string path = Path.Combine(saveDir, "save_slot_01.json");
            File.WriteAllText(path, "{ \"Timestamp\": \"2025-01-01 00:00:00\", \"RunNumber\": 1 }");

            var provider = new FakeGameStateProvider
            {
                WizardIds = new[] { "Any" }
            };
            var service = new SaveGameService(provider, dir);

            var data = await service.LoadSlotDataAsync(1);

            Assert.IsNotNull(data, "LoadSlotDataAsync should return a SaveGameData instance for valid JSON.");
            Assert.IsNotNull(data.PlayerResources, "PlayerResources should default to a non-null DTO when missing.");
            Assert.AreEqual(0, data.PlayerResources.Gold);
            Assert.AreEqual(0, data.PlayerResources.Gems);
        }

        [Test]
        public async Task LoadSlotDataAsync_MissingPlayerInventory_DefaultsToEmpty()
        {
            string dir = CreateTestDirectory();
            string saveDir = Path.Combine(dir, "Saves");
            Directory.CreateDirectory(saveDir);

            string path = Path.Combine(saveDir, "save_slot_01.json");
            File.WriteAllText(path, "{ \"Timestamp\": \"2025-01-01 00:00:00\", \"RunNumber\": 1 }");

            var provider = new FakeGameStateProvider
            {
                WizardIds = new[] { "Any" }
            };
            var service = new SaveGameService(provider, dir);

            var data = await service.LoadSlotDataAsync(1);

            Assert.IsNotNull(data, "LoadSlotDataAsync should return a SaveGameData instance for valid JSON.");
            Assert.IsNotNull(data.PlayerInventory, "PlayerInventory should default to a non-null DTO when missing.");
            Assert.IsNotNull(data.PlayerInventory.Entries, "PlayerInventory.Entries should default to a non-null array when missing.");
            Assert.AreEqual(0, data.PlayerInventory.Entries.Length);
        }

        [Test]
        public async Task LoadSlotDataAsync_MissingPlayerOwnedUnits_DefaultsToEmpty()
        {
            string dir = CreateTestDirectory();
            string saveDir = Path.Combine(dir, "Saves");
            Directory.CreateDirectory(saveDir);

            string path = Path.Combine(saveDir, "save_slot_01.json");
            File.WriteAllText(path, "{ \"Timestamp\": \"2025-01-01 00:00:00\", \"RunNumber\": 1 }");

            var provider = new FakeGameStateProvider
            {
                WizardIds = new[] { "Any" }
            };
            var service = new SaveGameService(provider, dir);

            var data = await service.LoadSlotDataAsync(1);

            Assert.IsNotNull(data);
            Assert.IsNotNull(data.PlayerOwnedUnits);
            Assert.IsNotNull(data.PlayerOwnedUnits.Units);
            Assert.AreEqual(0, data.PlayerOwnedUnits.Units.Length);
            Assert.IsNotNull(data.PlayerOwnedUnits.ActiveSquadOwnedUnitIds);
            Assert.AreEqual(0, data.PlayerOwnedUnits.ActiveSquadOwnedUnitIds.Length);
        }

        [Test]
        public async Task LoadSlotDataAsync_CorruptPlayerOwnedUnits_AreSanitized()
        {
            string dir = CreateTestDirectory();
            string saveDir = Path.Combine(dir, "Saves");
            Directory.CreateDirectory(saveDir);

            string path = Path.Combine(saveDir, "save_slot_01.json");
            File.WriteAllText(
                path,
                "{ \"Timestamp\": \"2025-01-01 00:00:00\", \"RunNumber\": 1, \"PlayerOwnedUnits\": { \"Units\": [ { \"OwnedUnitId\": \"\", \"UnitId\": \"WizA\", \"CustomName\": \"\", \"Level\": -1, \"Xp\": -3, \"SpellIds\": [\"SpellA\"] }, { \"OwnedUnitId\": \"owned_ok\", \"UnitId\": \"WizB\", \"CustomName\": \"  NameTooLong_12345678901234567890  \", \"Level\": 0, \"Xp\": -7, \"SpellIds\": [null, \"SpellB\", \"\"], \"EquippedItems\": [ { \"SlotType\": 0, \"EquipmentDefinitionId\": \"eq.staff\" }, { \"SlotType\": 0, \"EquipmentDefinitionId\": \"eq.duplicate\" }, { \"SlotType\": 999, \"EquipmentDefinitionId\": \"eq.invalid\" }, { \"SlotType\": 1, \"EquipmentDefinitionId\": \"\" } ] } ], \"ActiveSquadOwnedUnitIds\": [null, \"\", \"owned_ok\"] } }");

            var provider = new FakeGameStateProvider
            {
                WizardIds = new[] { "Any" }
            };
            var service = new SaveGameService(provider, dir);

            var data = await service.LoadSlotDataAsync(1);

            Assert.IsNotNull(data);
            Assert.IsNotNull(data.PlayerOwnedUnits);
            Assert.IsNotNull(data.PlayerOwnedUnits.Units);
            Assert.AreEqual(1, data.PlayerOwnedUnits.Units.Length);
            Assert.AreEqual("owned_ok", data.PlayerOwnedUnits.Units[0].OwnedUnitId);
            Assert.AreEqual("WizB", data.PlayerOwnedUnits.Units[0].UnitId);
            Assert.AreEqual("NameTooLong_12345678", data.PlayerOwnedUnits.Units[0].CustomName);
            Assert.AreEqual(1, data.PlayerOwnedUnits.Units[0].Level);
            Assert.AreEqual(0, data.PlayerOwnedUnits.Units[0].Xp);
            Assert.IsNotNull(data.PlayerOwnedUnits.Units[0].SpellIds);
            Assert.AreEqual(1, data.PlayerOwnedUnits.Units[0].SpellIds.Length);
            Assert.AreEqual("SpellB", data.PlayerOwnedUnits.Units[0].SpellIds[0]);
            Assert.IsNotNull(data.PlayerOwnedUnits.Units[0].EquippedItems);
            Assert.AreEqual(1, data.PlayerOwnedUnits.Units[0].EquippedItems.Length);
            Assert.AreEqual(EquipmentSlotType.Weapon, data.PlayerOwnedUnits.Units[0].EquippedItems[0].SlotType);
            Assert.AreEqual("eq.staff", data.PlayerOwnedUnits.Units[0].EquippedItems[0].EquipmentDefinitionId);
            Assert.IsNotNull(data.PlayerOwnedUnits.ActiveSquadOwnedUnitIds);
            Assert.AreEqual(1, data.PlayerOwnedUnits.ActiveSquadOwnedUnitIds.Length);
            Assert.AreEqual("owned_ok", data.PlayerOwnedUnits.ActiveSquadOwnedUnitIds[0]);
        }

        [Test]
        public async Task LoadSlotDataAsync_MissingTournamentProgress_DefaultsSafely()
        {
            string dir = CreateTestDirectory();
            string saveDir = Path.Combine(dir, "Saves");
            Directory.CreateDirectory(saveDir);

            string path = Path.Combine(saveDir, "save_slot_01.json");
            File.WriteAllText(path, "{ \"Timestamp\": \"2025-01-01 00:00:00\", \"RunNumber\": 1 }");

            var provider = new FakeGameStateProvider
            {
                WizardIds = new[] { "Any" }
            };
            var service = new SaveGameService(provider, dir);

            var data = await service.LoadSlotDataAsync(1);

            Assert.IsNotNull(data);
            Assert.IsNotNull(data.TournamentProgress);
            Assert.AreEqual(1, data.TournamentProgress.CurrentRoundIndex);
            Assert.IsNotNull(data.TournamentProgress.CompletedBattles);
            Assert.AreEqual(7, data.TournamentProgress.CompletedBattles.Length);
        }

        [Test]
        public async Task LoadSlotDataAsync_CorruptTournamentProgress_IsSanitized()
        {
            string dir = CreateTestDirectory();
            string saveDir = Path.Combine(dir, "Saves");
            Directory.CreateDirectory(saveDir);

            string path = Path.Combine(saveDir, "save_slot_01.json");
            File.WriteAllText(
                path,
                "{ \"Timestamp\": \"2025-01-01 00:00:00\", \"RunNumber\": 1, \"TournamentProgress\": { \"CurrentRoundIndex\": -15, \"CompletedBattles\": [ true, true, true, true, true, true, true, true, true ] } }");

            var provider = new FakeGameStateProvider
            {
                WizardIds = new[] { "Any" }
            };
            var service = new SaveGameService(provider, dir);

            var data = await service.LoadSlotDataAsync(1);

            Assert.IsNotNull(data);
            Assert.IsNotNull(data.TournamentProgress);
            Assert.AreEqual(7, data.TournamentProgress.CurrentRoundIndex);
            Assert.IsNotNull(data.TournamentProgress.CompletedBattles);
            Assert.AreEqual(7, data.TournamentProgress.CompletedBattles.Length);
            for (int i = 0; i < data.TournamentProgress.CompletedBattles.Length; i++)
            {
                Assert.IsTrue(data.TournamentProgress.CompletedBattles[i]);
            }
        }

        [Test]
        public async Task LoadSlotDataAsync_CorruptPlayerInventoryEntries_AreSanitized()
        {
            string dir = CreateTestDirectory();
            string saveDir = Path.Combine(dir, "Saves");
            Directory.CreateDirectory(saveDir);

            string path = Path.Combine(saveDir, "save_slot_01.json");
            File.WriteAllText(
                path,
                "{ \"Timestamp\": \"2025-01-01 00:00:00\", \"RunNumber\": 1, \"PlayerInventory\": { \"Entries\": [ { \"Kind\": \"Unknown\", \"DefinitionId\": \"bad\", \"Quantity\": -8 }, { \"Kind\": \"Item\", \"DefinitionId\": \"item.potion\", \"Quantity\": 0 }, { \"Kind\": \"Spell\", \"DefinitionId\": \"\", \"Quantity\": 10 }, { \"Kind\": \"Equipment\", \"DefinitionId\": \"eq.sword\", \"Quantity\": 99 } ] } }");

            var provider = new FakeGameStateProvider
            {
                WizardIds = new[] { "Any" }
            };
            var service = new SaveGameService(provider, dir);

            var data = await service.LoadSlotDataAsync(1);

            Assert.IsNotNull(data);
            Assert.IsNotNull(data.PlayerInventory);
            Assert.IsNotNull(data.PlayerInventory.Entries);
            Assert.AreEqual(2, data.PlayerInventory.Entries.Length);

            Assert.AreEqual("Item", data.PlayerInventory.Entries[0].Kind);
            Assert.AreEqual("item.potion", data.PlayerInventory.Entries[0].DefinitionId);
            Assert.AreEqual(1, data.PlayerInventory.Entries[0].Quantity);

            Assert.AreEqual("Equipment", data.PlayerInventory.Entries[1].Kind);
            Assert.AreEqual("eq.sword", data.PlayerInventory.Entries[1].DefinitionId);
            Assert.AreEqual(1, data.PlayerInventory.Entries[1].Quantity);
        }
    }
}
