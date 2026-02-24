using System;

namespace SevenBattles.Core.Items
{
    [Serializable]
    public sealed class InventoryEntry
    {
        public enum EntryKind
        {
            Equipment,
            Spell,
            Item
        }

        public EntryKind Kind;
        public string DefinitionId;
        public int Quantity;

        /// <summary>
        /// Stable runtime key for this entry category + definition.
        /// </summary>
        public string EntryKey => BuildEntryKey(Kind, DefinitionId);

        public static string BuildEntryKey(EntryKind kind, string definitionId)
        {
            return $"{kind}:{definitionId ?? string.Empty}";
        }
    }
}
