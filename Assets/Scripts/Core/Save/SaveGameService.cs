using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using SevenBattles.Core.Players;

namespace SevenBattles.Core.Save
{
    public interface ISaveGameService
    {
        int MaxSlots { get; }

        Task<SaveSlotMetadata[]> LoadAllSlotMetadataAsync();

        Task<SaveSlotMetadata> SaveSlotAsync(int slotIndex);
    }

    public interface IGameStateSaveProvider
    {
        void PopulateGameState(SaveGameData data);
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
    public sealed class UnitStatsSaveData
    {
        public int Life;
        public int MaxLife;
        public int Attack;
        public int Shoot;
        public int Spell;
        public int Speed;
        public int Luck;
        public int Defense;
        public int Protection;
        public int Initiative;
        public int Morale;
    }

    [Serializable]
    public sealed class UnitPlacementSaveData
    {
        public string UnitId;
        public string InstanceId;
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
    }

    [Serializable]
    public sealed class SaveGameData
    {
        public string Timestamp;
        public int RunNumber;
        public PlayerSquadSaveData PlayerSquad;
        public UnitPlacementSaveData[] UnitPlacements;
        public BattleTurnSaveData BattleTurn;
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

                try
                {
                    Directory.CreateDirectory(directory);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"SaveGameService: Failed to create save directory '{directory}'. {ex}");
                }

                for (int i = 0; i < MaxSlots; i++)
                {
                    int slotIndex = i + 1;
                    string path = GetSlotFilePath(directory, slotIndex);

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
                                       (data.PlayerSquad != null && data.PlayerSquad.WizardIds != null && data.PlayerSquad.WizardIds.Length > 0);

                        string timestamp = data.Timestamp;
                        int runNumber = data.RunNumber;

                        result[i] = new SaveSlotMetadata(slotIndex, hasSave, timestamp, runNumber);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"SaveGameService: Failed to read save slot {slotIndex} at '{path}'. Treating as empty. {ex}");
                        result[i] = new SaveSlotMetadata(slotIndex, false, null, 0);
                    }
                }

                return result;
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
                Debug.LogWarning($"SaveGameService: Failed to create save directory '{directory}' for saving. {ex}");
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
                    Debug.LogWarning($"SaveGameService: Failed to read existing save for slot {slotIndex} at '{path}' to compute run number. {ex}");
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
                            Debug.Log($"SaveGameService: Save slot {slotIndex} backup file created at '{backupPath}'.");
                            try
                            {
                                if (File.Exists(backupPath))
                                {
                                    File.Delete(backupPath);
                                }
                            }
                            catch (Exception backupEx)
                            {
                                Debug.LogWarning($"SaveGameService: Failed to delete backup file '{backupPath}'. {backupEx}");
                            }
                        }
                        catch (Exception replaceEx)
                        {
                            Debug.LogWarning($"SaveGameService: File.Replace failed for '{path}'. Falling back to overwrite. {replaceEx}");
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
                    Debug.LogError($"SaveGameService: Failed to write save slot {slotIndex} to '{path}'. {ex}");
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
                Debug.LogError($"SaveGameService: Game state provider threw during PopulateGameState. {ex}");
            }

            if (data.PlayerSquad == null)
            {
                data.PlayerSquad = new PlayerSquadSaveData
                {
                    WizardIds = Array.Empty<string>()
                };
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
                    ActiveUnitMaxActionPoints = 0
                };
            }

            return data;
        }
    }
}
