using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using SevenBattles.Core.Battle;
using SevenBattles.Core.Items;
using SevenBattles.Core.Players;

using SevenBattles.Core.Diagnostics;
namespace SevenBattles.Core.Save
{
    public interface ISaveGameService
    {
        int MaxSlots { get; }

        Task<SaveSlotMetadata[]> LoadAllSlotMetadataAsync();

        Task<SaveSlotMetadata> SaveSlotAsync(int slotIndex);

        /// <summary>
        /// Loads the full SaveGameData payload for the given slot index.
        /// Returns null if the slot does not exist, the JSON is invalid, or
        /// deserialization fails. Callers must treat a null result as a
        /// failed load and avoid applying partial state.
        /// </summary>
        Task<SaveGameData> LoadSlotDataAsync(int slotIndex);
    }

    public interface IGameStateSaveProvider
    {
        void PopulateGameState(SaveGameData data);
    }

    /// <summary>
    /// Abstraction for applying a loaded SaveGameData snapshot to the current game.
    /// Implementations live in gameplay domains (e.g., Battle) and are responsible
    /// for reconstructing units, controllers, and other state from the DTO.
    /// </summary>
    public interface IGameStateLoadHandler
    {
        void ApplyLoadedGame(SaveGameData data);
    }

    public sealed class SaveSlotMetadata
    {
        public int SlotIndex { get; }
        public bool HasSave { get; }
        public string TimestampString { get; }
        public int RunNumber { get; }

        public SaveSlotMetadata(int slotIndex, bool hasSave, string timestampString, int runNumber)
        {
            SlotIndex = slotIndex;
            HasSave = hasSave;
            TimestampString = timestampString;
            RunNumber = runNumber;
        }
    }

    [Serializable]
    public sealed class PlayerSquadSaveData
    {
        public string[] WizardIds;
    }

    [Serializable]
    public sealed class OwnedUnitSaveData
    {
        public string OwnedUnitId;
        public string CustomName;
        public string UnitId;
        public int Level;
        public int Xp;
        public string[] SpellIds;
        public EquipmentSlotEntry[] EquippedItems;
    }

    [Serializable]
    public sealed class PlayerOwnedUnitsSaveData
    {
        public OwnedUnitSaveData[] Units;
        public string[] ActiveSquadOwnedUnitIds;
    }

    [Serializable]
    public sealed class UnitStatsSaveData
    {
        public int Life;
        public int MaxLife;
        public int Level;
        public int Attack;
        public int Shoot;
        public int ShootRange;
        public int ShootDefense;
        public int Spell;
        public int Speed;
        public int Luck;
        public int Defense;
        public int Protection;
        public int Initiative;
        public int Morale;
        public int DeckCapacity;
        public int DrawCapacity;
    }

    [Serializable]
    public sealed class UnitPlacementSaveData
    {
        public string UnitId;
        public string InstanceId;
        public string[] SpellIds;
        public string Team;
        public int X;
        public int Y;
        public string Facing;
        public bool Dead;
        public UnitStatsSaveData Stats;
    }

    [Serializable]
    public sealed class BattleTurnSaveData
    {
        public string Phase;
        public int TurnIndex;
        public string ActiveUnitId;
        public string ActiveUnitInstanceId;
        public string ActiveUnitTeam;
        public int ActiveUnitCurrentActionPoints;
        public int ActiveUnitMaxActionPoints;
        public bool ActiveUnitHasMoved;
    }

    [Serializable]
    public sealed class BattleEnchantmentSaveData
    {
        public string SpellId;
        public int QuadIndex;
        public string CasterInstanceId;
        public string CasterUnitId;
        public string CasterTeam;
    }

    [Serializable]
    public sealed class UnitSpellLoadoutSaveData
    {
        public string UnitId;
        public string[] SpellIds;
        public int Level;
        public int Xp;
    }

    [Serializable]
    public sealed class BattleSessionSaveData
    {
        public string[] PlayerSquadIds;
        public string[] EnemySquadIds;
        public UnitSpellLoadoutSaveData[] PlayerSquadUnits;
        public UnitSpellLoadoutSaveData[] EnemySquadUnits;
        public string BattleType;
        public int Difficulty;
        public string CampaignMissionId;
        public string BattlefieldId;
    }

    [Serializable]
    public sealed class PlayerResourcesSaveData
    {
        public int Gold;
        public int Gems;
    }

    [Serializable]
    public sealed class InventoryEntrySaveData
    {
        public string Kind;
        public string DefinitionId;
        public int Quantity;
    }

    [Serializable]
    public sealed class PlayerInventorySaveData
    {
        public InventoryEntrySaveData[] Entries;
    }

    [Serializable]
    public sealed class TournamentProgressSaveData
    {
        public int CurrentRoundIndex;
        public bool[] CompletedBattles;
    }

    [Serializable]
    public sealed class SaveGameData
    {
        public string Timestamp;
        public int RunNumber;
        public PlayerSquadSaveData PlayerSquad; // DEPRECATED - use BattleSession.PlayerSquadIds
        public UnitPlacementSaveData[] UnitPlacements;
        public BattleTurnSaveData BattleTurn;
        public BattleSessionSaveData BattleSession; // NEW: Original battle configuration
        public BattleEnchantmentSaveData[] BattleEnchantments;
        public PlayerResourcesSaveData PlayerResources;
        public PlayerInventorySaveData PlayerInventory;
        public TournamentProgressSaveData TournamentProgress;
        public PlayerOwnedUnitsSaveData PlayerOwnedUnits;
    }

    public sealed class SaveGameService : ISaveGameService
    {
        public const int DefaultMaxSlots = 8;

        private readonly string _baseDirectory;
        private readonly IGameStateSaveProvider _gameStateProvider;

        public int MaxSlots => DefaultMaxSlots;

        public SaveGameService(IGameStateSaveProvider gameStateProvider, string baseDirectory)
        {
            _gameStateProvider = gameStateProvider ?? throw new ArgumentNullException(nameof(gameStateProvider));

            if (string.IsNullOrEmpty(baseDirectory))
            {
                throw new ArgumentException("Base directory must be a non-empty path string.", nameof(baseDirectory));
            }

            _baseDirectory = baseDirectory;
        }

        public Task<SaveSlotMetadata[]> LoadAllSlotMetadataAsync()
        {
            return Task.Run(() =>
            {
                var result = new SaveSlotMetadata[MaxSlots];
                string directory = GetSaveDirectory();
                SBLog.Info($"SaveGameService: Scanning save slots in '{directory}'.");

                try
                {
                    Directory.CreateDirectory(directory);
                }
                catch (Exception ex)
                {
                    SBLog.Warn($"SaveGameService: Failed to create save directory '{directory}'. {ex}");
                }

                for (int i = 0; i < MaxSlots; i++)
                {
                    int slotIndex = i + 1;
                    string path = GetSlotFilePath(directory, slotIndex);
                    SBLog.Info($"SaveGameService: Load metadata slot {slotIndex} path '{path}'.");

                    if (!File.Exists(path))
                    {
                        result[i] = new SaveSlotMetadata(slotIndex, false, null, 0);
                        continue;
                    }

                    try
                    {
                        string json = File.ReadAllText(path);
                        if (string.IsNullOrEmpty(json))
                        {
                            result[i] = new SaveSlotMetadata(slotIndex, false, null, 0);
                            continue;
                        }

                        var data = JsonUtility.FromJson<SaveGameData>(json);
                        if (data == null)
                        {
                            result[i] = new SaveSlotMetadata(slotIndex, false, null, 0);
                            continue;
                        }

                        bool hasSave = !string.IsNullOrEmpty(data.Timestamp) ||
                                       (data.PlayerOwnedUnits != null && data.PlayerOwnedUnits.Units != null && data.PlayerOwnedUnits.Units.Length > 0) ||
                                       (data.PlayerInventory != null && data.PlayerInventory.Entries != null && data.PlayerInventory.Entries.Length > 0) ||
                                       HasTournamentProgress(data.TournamentProgress);

                        string timestamp = data.Timestamp;
                        int runNumber = data.RunNumber;

                        result[i] = new SaveSlotMetadata(slotIndex, hasSave, timestamp, runNumber);
                    }
                    catch (Exception ex)
                    {
                        SBLog.Warn($"SaveGameService: Failed to read save slot {slotIndex} at '{path}'. Treating as empty. {ex}");
                        result[i] = new SaveSlotMetadata(slotIndex, false, null, 0);
                    }
                }

                return result;
            });
        }

        public Task<SaveGameData> LoadSlotDataAsync(int slotIndex)
        {
            if (slotIndex < 1 || slotIndex > MaxSlots)
            {
                throw new ArgumentOutOfRangeException(nameof(slotIndex), $"Slot index must be between 1 and {MaxSlots}.");
            }

            string directory = GetSaveDirectory();

            return Task.Run(() =>
            {
                try
                {
                    Directory.CreateDirectory(directory);
                }
                catch (Exception ex)
                {
                    SBLog.Warn($"SaveGameService: Failed to create save directory '{directory}' for loading. {ex}");
                }

                string path = GetSlotFilePath(directory, slotIndex);
                SBLog.Info($"SaveGameService: Load slot {slotIndex} from '{path}'.");
                if (!File.Exists(path))
                {
                    SBLog.Info($"SaveGameService: Slot {slotIndex} file not found at '{path}'.");
                    return null;
                }

                try
                {
                    string json = File.ReadAllText(path);
                    if (string.IsNullOrEmpty(json))
                    {
                        return null;
                    }

                    var data = JsonUtility.FromJson<SaveGameData>(json);
                    if (data == null)
                    {
                        return null;
                    }

                    if (data.UnitPlacements == null)
                    {
                        data.UnitPlacements = Array.Empty<UnitPlacementSaveData>();
                    }

                    if (data.BattleTurn == null)
                    {
                        data.BattleTurn = new BattleTurnSaveData
                        {
                            Phase = "unknown",
                            TurnIndex = 0,
                            ActiveUnitId = null,
                            ActiveUnitTeam = null,
                            ActiveUnitCurrentActionPoints = 0,
                            ActiveUnitMaxActionPoints = 0,
                            ActiveUnitHasMoved = false
                        };
                    }

                    if (data.BattleEnchantments == null)
                    {
                        data.BattleEnchantments = Array.Empty<BattleEnchantmentSaveData>();
                    }

                    data.PlayerResources = SanitizePlayerResources(data.PlayerResources);
                    data.PlayerInventory = SanitizePlayerInventory(data.PlayerInventory);
                    data.TournamentProgress = SanitizeTournamentProgress(data.TournamentProgress);
                    data.PlayerOwnedUnits = SanitizePlayerOwnedUnits(data.PlayerOwnedUnits);

                    return data;
                }
                catch (Exception ex)
                {
                    SBLog.Error($"SaveGameService: Failed to load save slot {slotIndex} at '{path}'. {ex}");
                    return null;
                }
            });
        }

        public Task<SaveSlotMetadata> SaveSlotAsync(int slotIndex)
        {
            if (slotIndex < 1 || slotIndex > MaxSlots)
            {
                throw new ArgumentOutOfRangeException(nameof(slotIndex), $"Slot index must be between 1 and {MaxSlots}.");
            }

            string directory = GetSaveDirectory();

            try
            {
                Directory.CreateDirectory(directory);
            }
            catch (Exception ex)
            {
                SBLog.Warn($"SaveGameService: Failed to create save directory '{directory}' for saving. {ex}");
            }

            string path = GetSlotFilePath(directory, slotIndex);
            string tempPath = path + ".tmp";
            string backupPath = path + ".bak";

            int nextRunNumber = 1;

            if (File.Exists(path))
            {
                try
                {
                    string existingJson = File.ReadAllText(path);
                    var existingData = JsonUtility.FromJson<SaveGameData>(existingJson);
                    if (existingData != null && existingData.RunNumber > 0)
                    {
                        nextRunNumber = existingData.RunNumber + 1;
                    }
                    else
                    {
                        nextRunNumber = 2;
                    }
                }
                catch (Exception ex)
                {
                    SBLog.Warn($"SaveGameService: Failed to read existing save for slot {slotIndex} at '{path}' to compute run number. {ex}");
                }
            }

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            var data = BuildSaveGameData(_gameStateProvider, timestamp, nextRunNumber);
            string json = JsonUtility.ToJson(data, true);

            return Task.Run(() =>
            {
                try
                {
                    File.WriteAllText(tempPath, json);

                    if (File.Exists(path))
                    {
                        try
                        {
                            File.Replace(tempPath, path, backupPath, ignoreMetadataErrors: true);
                            SBLog.Info($"SaveGameService: Save slot {slotIndex} backup file created at '{backupPath}'.");
                            try
                            {
                                if (File.Exists(backupPath))
                                {
                                    File.Delete(backupPath);
                                }
                            }
                            catch (Exception backupEx)
                            {
                                SBLog.Warn($"SaveGameService: Failed to delete backup file '{backupPath}'. {backupEx}");
                            }
                        }
                        catch (Exception replaceEx)
                        {
                            SBLog.Warn($"SaveGameService: File.Replace failed for '{path}'. Falling back to overwrite. {replaceEx}");
                            File.Copy(tempPath, path, true);
                            File.Delete(tempPath);
                        }
                    }
                    else
                    {
                        File.Move(tempPath, path);
                    }
                }
                catch (Exception ex)
                {
                    SBLog.Error($"SaveGameService: Failed to write save slot {slotIndex} to '{path}'. {ex}");
                    try
                    {
                        if (File.Exists(tempPath))
                        {
                            File.Delete(tempPath);
                        }
                    }
                    catch
                    {
                        // Ignore cleanup failures.
                    }

                    throw;
                }

                return new SaveSlotMetadata(slotIndex, true, timestamp, nextRunNumber);
            });
        }

        private string GetSaveDirectory()
        {
            return Path.Combine(_baseDirectory, "Saves");
        }

        private static string GetSlotFilePath(string directory, int slotIndex)
        {
            string fileName = $"save_slot_{slotIndex:00}.json";
            return Path.Combine(directory, fileName);
        }

        private static SaveGameData BuildSaveGameData(IGameStateSaveProvider provider, string timestamp, int runNumber)
        {
            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            var data = new SaveGameData
            {
                Timestamp = timestamp,
                RunNumber = runNumber
            };

            try
            {
                provider.PopulateGameState(data);
            }
            catch (Exception ex)
            {
                SBLog.Error($"SaveGameService: Game state provider threw during PopulateGameState. {ex}");
            }

            if (data.UnitPlacements == null)
            {
                data.UnitPlacements = Array.Empty<UnitPlacementSaveData>();
            }

            if (data.BattleTurn == null)
            {
                data.BattleTurn = new BattleTurnSaveData
                {
                    Phase = "unknown",
                    TurnIndex = 0,
                    ActiveUnitId = null,
                    ActiveUnitTeam = null,
                    ActiveUnitCurrentActionPoints = 0,
                    ActiveUnitMaxActionPoints = 0,
                    ActiveUnitHasMoved = false
                };
            }

            if (data.BattleEnchantments == null)
            {
                data.BattleEnchantments = Array.Empty<BattleEnchantmentSaveData>();
            }

            data.PlayerResources = SanitizePlayerResources(data.PlayerResources);
            data.PlayerInventory = SanitizePlayerInventory(data.PlayerInventory);
            data.TournamentProgress = SanitizeTournamentProgress(data.TournamentProgress);
            data.PlayerOwnedUnits = SanitizePlayerOwnedUnits(data.PlayerOwnedUnits);

            return data;
        }

        private static PlayerResourcesSaveData SanitizePlayerResources(PlayerResourcesSaveData value)
        {
            int gold = 0;
            int gems = 0;

            if (value != null)
            {
                if (value.Gold > 0)
                {
                    gold = value.Gold;
                }

                if (value.Gems > 0)
                {
                    gems = value.Gems;
                }
            }

            return new PlayerResourcesSaveData
            {
                Gold = gold,
                Gems = gems
            };
        }

        private static PlayerInventorySaveData SanitizePlayerInventory(PlayerInventorySaveData value)
        {
            if (value == null || value.Entries == null || value.Entries.Length == 0)
            {
                return new PlayerInventorySaveData
                {
                    Entries = Array.Empty<InventoryEntrySaveData>()
                };
            }

            var entries = new System.Collections.Generic.List<InventoryEntrySaveData>(value.Entries.Length);
            for (int i = 0; i < value.Entries.Length; i++)
            {
                var entry = value.Entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.DefinitionId))
                {
                    continue;
                }

                if (!Enum.TryParse(entry.Kind, ignoreCase: true, out InventoryEntry.EntryKind parsedKind))
                {
                    continue;
                }

                int quantity = parsedKind == InventoryEntry.EntryKind.Item
                    ? Mathf.Max(1, entry.Quantity)
                    : 1;

                entries.Add(new InventoryEntrySaveData
                {
                    Kind = parsedKind.ToString(),
                    DefinitionId = entry.DefinitionId,
                    Quantity = quantity
                });
            }

            return new PlayerInventorySaveData
            {
                Entries = entries.Count > 0 ? entries.ToArray() : Array.Empty<InventoryEntrySaveData>()
            };
        }

        private static TournamentProgressSaveData SanitizeTournamentProgress(TournamentProgressSaveData value)
        {
            int totalBattles = TournamentDefinition.BattleCount;
            bool[] completed = new bool[totalBattles];

            if (value != null && value.CompletedBattles != null && value.CompletedBattles.Length > 0)
            {
                Array.Copy(value.CompletedBattles, completed, Mathf.Min(totalBattles, value.CompletedBattles.Length));
            }

            int currentRound = value != null ? value.CurrentRoundIndex : 1;
            currentRound = Mathf.Clamp(currentRound, 1, totalBattles);
            if (completed[currentRound - 1])
            {
                currentRound = ResolveFirstIncompleteOrLast(completed);
            }

            return new TournamentProgressSaveData
            {
                CurrentRoundIndex = currentRound,
                CompletedBattles = completed
            };
        }

        private static PlayerOwnedUnitsSaveData SanitizePlayerOwnedUnits(PlayerOwnedUnitsSaveData value)
        {
            if (value == null)
            {
                return new PlayerOwnedUnitsSaveData
                {
                    Units = Array.Empty<OwnedUnitSaveData>(),
                    ActiveSquadOwnedUnitIds = Array.Empty<string>()
                };
            }

            var units = new System.Collections.Generic.List<OwnedUnitSaveData>();
            if (value.Units != null)
            {
                for (int i = 0; i < value.Units.Length; i++)
                {
                    OwnedUnitSaveData unit = value.Units[i];
                    if (unit == null || string.IsNullOrWhiteSpace(unit.OwnedUnitId) || string.IsNullOrWhiteSpace(unit.UnitId))
                    {
                        continue;
                    }

                    var sanitizedSpellIds = new System.Collections.Generic.List<string>();
                    if (unit.SpellIds != null)
                    {
                        for (int j = 0; j < unit.SpellIds.Length; j++)
                        {
                            string spellId = unit.SpellIds[j];
                            if (!string.IsNullOrWhiteSpace(spellId))
                            {
                                sanitizedSpellIds.Add(spellId);
                            }
                        }
                    }

                    units.Add(new OwnedUnitSaveData
                    {
                        OwnedUnitId = unit.OwnedUnitId,
                        CustomName = OwnedUnitNamingPolicy.SanitizeCustomName(unit.CustomName),
                        UnitId = unit.UnitId,
                        Level = unit.Level > 0 ? unit.Level : UnitSpellLoadout.DefaultLevel,
                        Xp = unit.Xp > 0 ? unit.Xp : 0,
                        SpellIds = sanitizedSpellIds.Count > 0 ? sanitizedSpellIds.ToArray() : Array.Empty<string>(),
                        EquippedItems = SanitizeEquippedItems(unit.EquippedItems)
                    });
                }
            }

            var activeIds = new System.Collections.Generic.List<string>();
            if (value.ActiveSquadOwnedUnitIds != null)
            {
                for (int i = 0; i < value.ActiveSquadOwnedUnitIds.Length; i++)
                {
                    string id = value.ActiveSquadOwnedUnitIds[i];
                    if (!string.IsNullOrWhiteSpace(id))
                    {
                        activeIds.Add(id);
                    }
                }
            }

            return new PlayerOwnedUnitsSaveData
            {
                Units = units.Count > 0 ? units.ToArray() : Array.Empty<OwnedUnitSaveData>(),
                ActiveSquadOwnedUnitIds = activeIds.Count > 0 ? activeIds.ToArray() : Array.Empty<string>()
            };
        }

        private static int ResolveFirstIncompleteOrLast(bool[] completed)
        {
            if (completed == null || completed.Length == 0)
            {
                return 1;
            }

            for (int i = 0; i < completed.Length; i++)
            {
                if (!completed[i])
                {
                    return i + 1;
                }
            }

            return completed.Length;
        }

        private static EquipmentSlotEntry[] SanitizeEquippedItems(EquipmentSlotEntry[] value)
        {
            if (value == null || value.Length == 0)
            {
                return Array.Empty<EquipmentSlotEntry>();
            }

            var seenSlots = new System.Collections.Generic.HashSet<EquipmentSlotType>();
            var sanitized = new System.Collections.Generic.List<EquipmentSlotEntry>(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                EquipmentSlotEntry entry = value[i];
                if (!Enum.IsDefined(typeof(EquipmentSlotType), entry.SlotType))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(entry.EquipmentDefinitionId))
                {
                    continue;
                }

                if (!seenSlots.Add(entry.SlotType))
                {
                    continue;
                }

                sanitized.Add(new EquipmentSlotEntry
                {
                    SlotType = entry.SlotType,
                    EquipmentDefinitionId = entry.EquipmentDefinitionId
                });
            }

            return sanitized.Count > 0 ? sanitized.ToArray() : Array.Empty<EquipmentSlotEntry>();
        }

        private static bool HasTournamentProgress(TournamentProgressSaveData value)
        {
            if (value == null)
            {
                return false;
            }

            if (value.CurrentRoundIndex > 1)
            {
                return true;
            }

            if (value.CompletedBattles == null || value.CompletedBattles.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < value.CompletedBattles.Length; i++)
            {
                if (value.CompletedBattles[i])
                {
                    return true;
                }
            }

            return false;
        }
    }
}
