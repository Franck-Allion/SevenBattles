using System;
using System.Collections.Generic;
using SevenBattles.Core.Battle;
using UnityEngine;

namespace SevenBattles.Core.Items
{
    [CreateAssetMenu(menuName = "SevenBattles/Player Inventory", fileName = "PlayerInventory")]
    public sealed class PlayerInventory : ScriptableObject
    {
        [SerializeField, Tooltip("Runtime inventory entries for equipment, spells, and consumable items.")]
        private List<InventoryEntry> _entries = new List<InventoryEntry>();

        public List<InventoryEntry> Entries => _entries;

        public event Action InventoryChanged;

        public void AddEquipment(EquipmentDefinition def)
        {
            AddSingleEntry(InventoryEntry.EntryKind.Equipment, def != null ? def.Id : null);
        }

        public void AddSpell(SpellDefinition def)
        {
            AddSingleEntry(InventoryEntry.EntryKind.Spell, def != null ? def.Id : null);
        }

        public void AddItem(ItemDefinition def, int quantity = 1)
        {
            string definitionId = def != null ? def.Id : null;
            if (string.IsNullOrWhiteSpace(definitionId) || quantity <= 0)
            {
                return;
            }

            EnsureEntriesList();

            InventoryEntry entry = FindEntryByKind(InventoryEntry.EntryKind.Item, definitionId);
            if (entry == null)
            {
                _entries.Add(new InventoryEntry
                {
                    Kind = InventoryEntry.EntryKind.Item,
                    DefinitionId = definitionId,
                    Quantity = quantity
                });
            }
            else
            {
                entry.Quantity = Mathf.Max(0, entry.Quantity) + quantity;
            }

            InventoryChanged?.Invoke();
        }

        public bool RemoveItem(string definitionId, int quantity = 1)
        {
            if (string.IsNullOrWhiteSpace(definitionId) || quantity <= 0)
            {
                return false;
            }

            EnsureEntriesList();

            InventoryEntry entry = FindEntryByKind(InventoryEntry.EntryKind.Item, definitionId);
            if (entry == null || entry.Quantity < quantity)
            {
                return false;
            }

            entry.Quantity -= quantity;
            if (entry.Quantity <= 0)
            {
                _entries.Remove(entry);
            }

            InventoryChanged?.Invoke();
            return true;
        }

        public InventoryEntry FindEntry(string definitionId)
        {
            if (string.IsNullOrWhiteSpace(definitionId))
            {
                return null;
            }

            EnsureEntriesList();
            for (int i = 0; i < _entries.Count; i++)
            {
                InventoryEntry entry = _entries[i];
                if (entry != null && string.Equals(entry.DefinitionId, definitionId, StringComparison.Ordinal))
                {
                    return entry;
                }
            }

            return null;
        }

        public InventoryEntry[] GetAllOfKind(InventoryEntry.EntryKind kind)
        {
            EnsureEntriesList();

            var matches = new List<InventoryEntry>();
            for (int i = 0; i < _entries.Count; i++)
            {
                InventoryEntry entry = _entries[i];
                if (entry != null && entry.Kind == kind)
                {
                    matches.Add(entry);
                }
            }

            return matches.Count > 0 ? matches.ToArray() : Array.Empty<InventoryEntry>();
        }

        private void AddSingleEntry(InventoryEntry.EntryKind kind, string definitionId)
        {
            if (string.IsNullOrWhiteSpace(definitionId))
            {
                return;
            }

            EnsureEntriesList();
            _entries.Add(new InventoryEntry
            {
                Kind = kind,
                DefinitionId = definitionId,
                Quantity = 1
            });
            InventoryChanged?.Invoke();
        }

        private InventoryEntry FindEntryByKind(InventoryEntry.EntryKind kind, string definitionId)
        {
            if (string.IsNullOrWhiteSpace(definitionId))
            {
                return null;
            }

            for (int i = 0; i < _entries.Count; i++)
            {
                InventoryEntry entry = _entries[i];
                if (entry == null)
                {
                    continue;
                }

                if (entry.Kind == kind && string.Equals(entry.DefinitionId, definitionId, StringComparison.Ordinal))
                {
                    return entry;
                }
            }

            return null;
        }

        private void OnValidate()
        {
            EnsureEntriesList();
        }

        private void OnEnable()
        {
            EnsureEntriesList();
        }

        private void EnsureEntriesList()
        {
            _entries ??= new List<InventoryEntry>();
        }
    }
}
