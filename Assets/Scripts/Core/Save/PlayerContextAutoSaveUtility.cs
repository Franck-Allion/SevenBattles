using System;
using System.Collections.Generic;
using System.IO;
using SevenBattles.Core.Battle;
using SevenBattles.Core.Diagnostics;
using SevenBattles.Core.Items;
using SevenBattles.Core.Players;
using SevenBattles.Core.Units;
using UnityEngine;

namespace SevenBattles.Core.Save
{
    /// <summary>
    /// JSON autosave helper for PlayerContext runtime progression.
    /// </summary>
    public static class PlayerContextAutoSaveUtility
    {
        public const string AutoSaveFileName = "autosave_player_context.json";

        [Serializable]
        private sealed class AutoSaveData
        {
            public string Timestamp;
            public int Gold;
            public int Gems;
            public int CurrentRoundIndex;
            public bool[] CompletedBattles;
            public UnitProgressData[] Units;
            public InventorySaveEntry[] InventoryEntries;
        }

        [Serializable]
        private sealed class UnitProgressData
        {
            public int SlotIndex;
            public string UnitId;
            public int Level;
            public int Xp;
            public string[] SpellIds;
        }

        [Serializable]
        private sealed class InventorySaveEntry
        {
            public string Kind;
            public string DefinitionId;
            public int Quantity;
        }

        public static bool TrySaveFromPlayerContext(PlayerContext context, out string path, string baseDirectoryOverride = null)
        {
            path = GetAutoSavePath(baseDirectoryOverride);
            if (context == null)
            {
                SBLog.Warn($"PlayerContextAutoSaveUtility: Save skipped. PlayerContext is null. Path='{path}'.");
                return false;
            }

            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var data = BuildData(context);
                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(path, json);
                SBLog.Info($"PlayerContextAutoSaveUtility: Autosave written to '{path}'.");
                return true;
            }
            catch (Exception ex)
            {
                SBLog.Error($"PlayerContextAutoSaveUtility: Failed to save autosave at '{path}'. {ex}");
                return false;
            }
        }

        public static bool TryLoadIntoPlayerContext(PlayerContext context, out string path, string baseDirectoryOverride = null)
        {
            path = GetAutoSavePath(baseDirectoryOverride);
            if (context == null)
            {
                SBLog.Warn($"PlayerContextAutoSaveUtility: Load skipped. PlayerContext is null. Path='{path}'.");
                return false;
            }

            if (!File.Exists(path))
            {
                SBLog.Info($"PlayerContextAutoSaveUtility: No autosave file found at '{path}'.");
                return false;
            }

            try
            {
                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                {
                    SBLog.Warn($"PlayerContextAutoSaveUtility: Autosave file is empty at '{path}'.");
                    return false;
                }

                var data = JsonUtility.FromJson<AutoSaveData>(json);
                if (data == null)
                {
                    SBLog.Warn($"PlayerContextAutoSaveUtility: Autosave JSON is invalid at '{path}'.");
                    return false;
                }

                ApplyData(context, data);
                SBLog.Info($"PlayerContextAutoSaveUtility: Autosave loaded from '{path}'.");
                return true;
            }
            catch (Exception ex)
            {
                SBLog.Error($"PlayerContextAutoSaveUtility: Failed to load autosave at '{path}'. {ex}");
                return false;
            }
        }

        public static string GetAutoSavePath(string baseDirectoryOverride = null)
        {
            string baseDirectory = string.IsNullOrWhiteSpace(baseDirectoryOverride)
                ? Application.persistentDataPath
                : baseDirectoryOverride;
            return Path.Combine(baseDirectory, "Saves", AutoSaveFileName);
        }

        private static AutoSaveData BuildData(PlayerContext context)
        {
            var data = new AutoSaveData
            {
                Timestamp = DateTime.UtcNow.ToString("o"),
                Gold = context.Gold,
                Gems = context.Gems,
                CurrentRoundIndex = context.CurrentTournamentRoundIndex,
                CompletedBattles = context.TournamentProgress != null
                    ? context.TournamentProgress.GetCompletedFlagsCopy()
                    : Array.Empty<bool>(),
                Units = BuildUnitProgress(context.PlayerSquad),
                InventoryEntries = BuildInventoryEntries(context.Inventory)
            };
            return data;
        }

        private static UnitProgressData[] BuildUnitProgress(PlayerSquad squad)
        {
            var loadouts = squad != null ? squad.GetLoadouts() : Array.Empty<UnitSpellLoadout>();
            if (loadouts == null || loadouts.Length == 0)
            {
                return Array.Empty<UnitProgressData>();
            }

            var results = new List<UnitProgressData>(loadouts.Length);
            for (int i = 0; i < loadouts.Length; i++)
            {
                var loadout = loadouts[i];
                if (loadout == null)
                {
                    continue;
                }

                var spells = loadout.Spells ?? Array.Empty<SpellDefinition>();
                var spellIds = new string[spells.Length];
                for (int j = 0; j < spells.Length; j++)
                {
                    spellIds[j] = spells[j] != null ? spells[j].Id : null;
                }

                results.Add(new UnitProgressData
                {
                    SlotIndex = i,
                    UnitId = loadout.Definition != null ? loadout.Definition.Id : null,
                    Level = loadout.EffectiveLevel,
                    Xp = loadout.EffectiveXp,
                    SpellIds = spellIds
                });
            }

            return results.Count == 0 ? Array.Empty<UnitProgressData>() : results.ToArray();
        }

        private static InventorySaveEntry[] BuildInventoryEntries(PlayerInventory inventory)
        {
            var entries = inventory != null ? inventory.Entries : null;
            if (entries == null || entries.Count == 0)
            {
                return Array.Empty<InventorySaveEntry>();
            }

            var results = new List<InventorySaveEntry>(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.DefinitionId))
                {
                    continue;
                }

                int quantity = entry.Kind == InventoryEntry.EntryKind.Item ? Mathf.Max(1, entry.Quantity) : 1;
                results.Add(new InventorySaveEntry
                {
                    Kind = entry.Kind.ToString(),
                    DefinitionId = entry.DefinitionId,
                    Quantity = quantity
                });
            }

            return results.Count == 0 ? Array.Empty<InventorySaveEntry>() : results.ToArray();
        }

        private static void ApplyData(PlayerContext context, AutoSaveData data)
        {
            int gold = Mathf.Max(0, data != null ? data.Gold : 0);
            int gems = Mathf.Max(0, data != null ? data.Gems : 0);
            context.SetResources(gold, gems);

            int currentRound = data != null ? data.CurrentRoundIndex : 1;
            bool[] completed = data != null ? data.CompletedBattles : null;
            context.SetTournamentProgress(currentRound, completed, TournamentDefinition.BattleCount);

            ApplyUnitProgress(context.PlayerSquad, data != null ? data.Units : null);
            ApplyInventory(context.Inventory, data != null ? data.InventoryEntries : null);
        }

        private static void ApplyUnitProgress(PlayerSquad squad, UnitProgressData[] units)
        {
            if (squad == null || units == null || units.Length == 0)
            {
                return;
            }

            var loadouts = squad.GetLoadouts();
            if (loadouts == null || loadouts.Length == 0)
            {
                return;
            }

            var spellLookup = BuildSpellLookup();
            for (int i = 0; i < units.Length; i++)
            {
                var saved = units[i];
                if (saved == null)
                {
                    continue;
                }

                int slotIndex = saved.SlotIndex;
                if (slotIndex < 0 || slotIndex >= loadouts.Length)
                {
                    continue;
                }

                var target = loadouts[slotIndex];
                if (target == null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(saved.UnitId) &&
                    target.Definition != null &&
                    !string.Equals(target.Definition.Id, saved.UnitId, StringComparison.Ordinal))
                {
                    continue;
                }

                target.Level = Mathf.Max(UnitSpellLoadout.DefaultLevel, saved.Level);
                target.Xp = Mathf.Max(0, saved.Xp);
                target.Spells = ResolveSpells(saved.SpellIds, spellLookup);
            }
        }

        private static void ApplyInventory(PlayerInventory inventory, InventorySaveEntry[] entries)
        {
            if (inventory == null)
            {
                return;
            }

            var runtimeEntries = inventory.Entries;
            runtimeEntries.Clear();

            if (entries == null || entries.Length == 0)
            {
                return;
            }

            for (int i = 0; i < entries.Length; i++)
            {
                var saved = entries[i];
                if (saved == null || string.IsNullOrWhiteSpace(saved.DefinitionId))
                {
                    continue;
                }

                if (!Enum.TryParse(saved.Kind, true, out InventoryEntry.EntryKind kind))
                {
                    continue;
                }

                int quantity = kind == InventoryEntry.EntryKind.Item ? Mathf.Max(1, saved.Quantity) : 1;
                runtimeEntries.Add(new InventoryEntry
                {
                    Kind = kind,
                    DefinitionId = saved.DefinitionId,
                    Quantity = quantity
                });
            }
        }

        private static Dictionary<string, SpellDefinition> BuildSpellLookup()
        {
            var lookup = new Dictionary<string, SpellDefinition>(StringComparer.Ordinal);
            var spells = Resources.FindObjectsOfTypeAll<SpellDefinition>();
            for (int i = 0; i < spells.Length; i++)
            {
                var spell = spells[i];
                if (spell == null || string.IsNullOrWhiteSpace(spell.Id))
                {
                    continue;
                }

                if (!lookup.ContainsKey(spell.Id))
                {
                    lookup.Add(spell.Id, spell);
                }
            }

            return lookup;
        }

        private static SpellDefinition[] ResolveSpells(string[] spellIds, Dictionary<string, SpellDefinition> spellLookup)
        {
            if (spellIds == null || spellIds.Length == 0 || spellLookup == null || spellLookup.Count == 0)
            {
                return Array.Empty<SpellDefinition>();
            }

            var results = new List<SpellDefinition>(spellIds.Length);
            for (int i = 0; i < spellIds.Length; i++)
            {
                string id = spellIds[i];
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                if (spellLookup.TryGetValue(id, out var spell) && spell != null)
                {
                    results.Add(spell);
                }
            }

            return results.Count == 0 ? Array.Empty<SpellDefinition>() : results.ToArray();
        }
    }
}
