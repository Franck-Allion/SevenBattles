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
    }
}
