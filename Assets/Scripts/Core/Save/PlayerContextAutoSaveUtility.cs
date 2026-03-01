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
            public OwnedUnitProgressData[] OwnedUnits;
            public string[] ActiveSquadOwnedUnitIds;
            public InventorySaveEntry[] InventoryEntries;
        }

        [Serializable]
        private sealed class OwnedUnitProgressData
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
                OwnedUnits = BuildOwnedUnits(context),
                ActiveSquadOwnedUnitIds = BuildActiveSquadOwnedUnitIds(context),
                InventoryEntries = BuildInventoryEntries(context.Inventory)
            };
            return data;
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

        private static OwnedUnitProgressData[] BuildOwnedUnits(PlayerContext context)
        {
            if (context == null || context.OwnedUnits == null || context.OwnedUnits.Count == 0)
            {
                return Array.Empty<OwnedUnitProgressData>();
            }

            var results = new List<OwnedUnitProgressData>(context.OwnedUnits.Count);
            for (int i = 0; i < context.OwnedUnits.Count; i++)
            {
                OwnedUnitData owned = context.OwnedUnits[i];
                if (owned == null || owned.Definition == null || string.IsNullOrWhiteSpace(owned.OwnedUnitId))
                {
                    continue;
                }

                SpellDefinition[] spells = owned.Spells ?? Array.Empty<SpellDefinition>();
                var spellIds = new List<string>(spells.Length);
                for (int j = 0; j < spells.Length; j++)
                {
                    SpellDefinition spell = spells[j];
                    if (spell != null && !string.IsNullOrWhiteSpace(spell.Id))
                    {
                        spellIds.Add(spell.Id);
                    }
                }

                results.Add(new OwnedUnitProgressData
                {
                    OwnedUnitId = owned.OwnedUnitId,
                    CustomName = OwnedUnitNamingPolicy.SanitizeCustomName(owned.CustomName),
                    UnitId = owned.Definition.Id,
                    Level = owned.EffectiveLevel,
                    Xp = owned.EffectiveXp,
                    SpellIds = spellIds.Count > 0 ? spellIds.ToArray() : Array.Empty<string>(),
                    EquippedItems = SanitizeEquippedItems(owned.EquippedItems)
                });
            }

            return results.Count == 0 ? Array.Empty<OwnedUnitProgressData>() : results.ToArray();
        }

        private static string[] BuildActiveSquadOwnedUnitIds(PlayerContext context)
        {
            if (context == null || context.ActiveSquadOwnedUnitIds == null || context.ActiveSquadOwnedUnitIds.Count == 0)
            {
                return Array.Empty<string>();
            }

            var ids = new List<string>(context.ActiveSquadOwnedUnitIds.Count);
            for (int i = 0; i < context.ActiveSquadOwnedUnitIds.Count; i++)
            {
                string id = context.ActiveSquadOwnedUnitIds[i];
                if (!string.IsNullOrWhiteSpace(id))
                {
                    ids.Add(id);
                }
            }

            return ids.Count == 0 ? Array.Empty<string>() : ids.ToArray();
        }

        private static void ApplyData(PlayerContext context, AutoSaveData data)
        {
            int gold = Mathf.Max(0, data != null ? data.Gold : 0);
            int gems = Mathf.Max(0, data != null ? data.Gems : 0);
            context.SetResources(gold, gems);

            int currentRound = data != null ? data.CurrentRoundIndex : 1;
            bool[] completed = data != null ? data.CompletedBattles : null;
            context.SetTournamentProgress(currentRound, completed, TournamentDefinition.BattleCount);

            // Backward compatibility: when legacy autosave JSON has no OwnedUnits field,
            // keep the current context-owned units instead of wiping progression.
            if (data != null && data.OwnedUnits != null)
            {
                ApplyOwnedUnits(context, data.OwnedUnits, data.ActiveSquadOwnedUnitIds);
            }
            else
            {
                EnsureActiveSquadFallback(context);
            }

            ApplyInventory(context.Inventory, data != null ? data.InventoryEntries : null);
        }

        private static void EnsureActiveSquadFallback(PlayerContext context)
        {
            if (context == null)
            {
                return;
            }

            if (context.ActiveSquadOwnedUnitIds != null && context.ActiveSquadOwnedUnitIds.Count > 0)
            {
                return;
            }

            if (context.OwnedUnits == null || context.OwnedUnits.Count == 0)
            {
                return;
            }

            var activeIds = new List<string>(Mathf.Min(context.MaxSquadSize, context.OwnedUnits.Count));
            for (int i = 0; i < context.OwnedUnits.Count && activeIds.Count < context.MaxSquadSize; i++)
            {
                OwnedUnitData unit = context.OwnedUnits[i];
                if (unit == null || string.IsNullOrWhiteSpace(unit.OwnedUnitId))
                {
                    continue;
                }

                activeIds.Add(unit.OwnedUnitId);
            }

            if (activeIds.Count > 0)
            {
                context.SetActiveSquadOwnedUnitIds(activeIds);
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

        private static void ApplyOwnedUnits(PlayerContext context, OwnedUnitProgressData[] ownedUnits, string[] activeOwnedUnitIds)
        {
            if (context == null)
            {
                return;
            }

            if (ownedUnits == null || ownedUnits.Length == 0)
            {
                context.SetOwnedUnits(Array.Empty<OwnedUnitData>());
                context.SetActiveSquadOwnedUnitIds(Array.Empty<string>());
                return;
            }

            var unitLookup = BuildUnitLookup();
            var spellLookup = BuildSpellLookup();
            var ownedList = new List<OwnedUnitData>(ownedUnits.Length);
            var seenIds = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < ownedUnits.Length; i++)
            {
                OwnedUnitProgressData saved = ownedUnits[i];
                if (saved == null || string.IsNullOrWhiteSpace(saved.UnitId))
                {
                    continue;
                }

                if (!unitLookup.TryGetValue(saved.UnitId, out UnitDefinition definition) || definition == null)
                {
                    continue;
                }

                string ownedId = string.IsNullOrWhiteSpace(saved.OwnedUnitId)
                    ? Guid.NewGuid().ToString("N")
                    : saved.OwnedUnitId;
                while (!seenIds.Add(ownedId))
                {
                    ownedId = Guid.NewGuid().ToString("N");
                }

                ownedList.Add(new OwnedUnitData
                {
                    OwnedUnitId = ownedId,
                    CustomName = saved.CustomName,
                    Definition = definition,
                    Level = Mathf.Max(UnitSpellLoadout.DefaultLevel, saved.Level),
                    Xp = Mathf.Max(0, saved.Xp),
                    Spells = ResolveSpells(saved.SpellIds, spellLookup),
                    EquippedItems = ResolveEquippedItemsForLoad(saved.EquippedItems)
                });
            }

            OwnedUnitNamingPolicy.NormalizeAllInPlace(ownedList);
            context.SetOwnedUnits(ownedList);

            var activeIds = new List<string>();
            if (activeOwnedUnitIds != null && activeOwnedUnitIds.Length > 0)
            {
                var ownedSet = new HashSet<string>(StringComparer.Ordinal);
                var selectedSet = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < ownedList.Count; i++)
                {
                    ownedSet.Add(ownedList[i].OwnedUnitId);
                }

                for (int i = 0; i < activeOwnedUnitIds.Length && activeIds.Count < context.MaxSquadSize; i++)
                {
                    string id = activeOwnedUnitIds[i];
                    if (string.IsNullOrWhiteSpace(id) || !ownedSet.Contains(id) || !selectedSet.Add(id))
                    {
                        continue;
                    }

                    activeIds.Add(id);
                }
            }

            if (activeIds.Count == 0)
            {
                for (int i = 0; i < ownedList.Count && activeIds.Count < context.MaxSquadSize; i++)
                {
                    activeIds.Add(ownedList[i].OwnedUnitId);
                }
            }

            context.SetActiveSquadOwnedUnitIds(activeIds);
        }

        private static Dictionary<string, UnitDefinition> BuildUnitLookup()
        {
            var lookup = new Dictionary<string, UnitDefinition>(StringComparer.Ordinal);
            var units = Resources.FindObjectsOfTypeAll<UnitDefinition>();
            for (int i = 0; i < units.Length; i++)
            {
                UnitDefinition definition = units[i];
                if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
                {
                    continue;
                }

                if (!lookup.ContainsKey(definition.Id))
                {
                    lookup.Add(definition.Id, definition);
                }
            }

            return lookup;
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

        private static EquipmentSlotEntry[] SanitizeEquippedItems(EquipmentSlotEntry[] value)
        {
            if (value == null || value.Length == 0)
            {
                return Array.Empty<EquipmentSlotEntry>();
            }

            var seenSlots = new HashSet<EquipmentSlotType>();
            var sanitized = new List<EquipmentSlotEntry>(value.Length);
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

            return sanitized.Count == 0 ? Array.Empty<EquipmentSlotEntry>() : sanitized.ToArray();
        }

        private static EquipmentSlotEntry[] ResolveEquippedItemsForLoad(EquipmentSlotEntry[] value)
        {
            if (value == null || value.Length == 0)
            {
                return OwnedUnitData.CreateDefaultEquippedItems();
            }

            return SanitizeEquippedItems(value);
        }
    }
}
