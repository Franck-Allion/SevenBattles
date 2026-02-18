using System;
using System.Collections.Generic;
using SevenBattles.Core.Items;
using SevenBattles.Core.Players;
using UnityEngine;

namespace SevenBattles.Core.Save
{
    /// <summary>
    /// Captures the runtime PlayerInventory into SaveGameData.
    /// </summary>
    public sealed class PlayerInventoryGameStateSaveProvider : MonoBehaviour, IGameStateSaveProvider
    {
        [SerializeField, Tooltip("Player context containing the runtime inventory to serialize.")]
        private PlayerContext _playerContext;

        public void PopulateGameState(SaveGameData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            var inventory = _playerContext != null ? _playerContext.Inventory : null;
            var sourceEntries = inventory != null ? inventory.Entries : null;

            if (sourceEntries == null || sourceEntries.Count == 0)
            {
                data.PlayerInventory = new PlayerInventorySaveData
                {
                    Entries = Array.Empty<InventoryEntrySaveData>()
                };
                return;
            }

            var entries = new List<InventoryEntrySaveData>(sourceEntries.Count);
            for (int i = 0; i < sourceEntries.Count; i++)
            {
                var entry = sourceEntries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.DefinitionId))
                {
                    continue;
                }

                int quantity = entry.Kind == InventoryEntry.EntryKind.Item
                    ? Mathf.Max(1, entry.Quantity)
                    : 1;

                entries.Add(new InventoryEntrySaveData
                {
                    Kind = entry.Kind.ToString(),
                    DefinitionId = entry.DefinitionId,
                    Quantity = quantity
                });
            }

            data.PlayerInventory = new PlayerInventorySaveData
            {
                Entries = entries.Count > 0 ? entries.ToArray() : Array.Empty<InventoryEntrySaveData>()
            };
        }
    }
}
