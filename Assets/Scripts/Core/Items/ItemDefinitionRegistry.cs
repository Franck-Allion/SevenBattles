using System.Collections.Generic;
using SevenBattles.Core.Diagnostics;
using UnityEngine;

namespace SevenBattles.Core.Items
{
    [CreateAssetMenu(menuName = "SevenBattles/Items/Item Definition Registry", fileName = "ItemDefinitionRegistry")]
    public sealed class ItemDefinitionRegistry : ScriptableObject
    {
        [SerializeField, Tooltip("All item definitions indexed by ID.")]
        private ItemDefinition[] _definitions;

        private readonly Dictionary<string, ItemDefinition> _lookup = new Dictionary<string, ItemDefinition>();

        public ItemDefinition GetById(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            if (_lookup.Count == 0)
            {
                RebuildLookup();
            }

            _lookup.TryGetValue(id, out ItemDefinition definition);
            return definition;
        }

        private void OnEnable()
        {
            RebuildLookup();
        }

        private void OnValidate()
        {
            RebuildLookup();
        }

        private void RebuildLookup()
        {
            _lookup.Clear();

            if (_definitions == null)
            {
                return;
            }

            for (int i = 0; i < _definitions.Length; i++)
            {
                ItemDefinition definition = _definitions[i];
                if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
                {
                    continue;
                }

                if (_lookup.ContainsKey(definition.Id))
                {
                    SBLog.Warn($"ItemDefinitionRegistry: Duplicate item ID '{definition.Id}' found. Keeping first occurrence.", this);
                    continue;
                }

                _lookup.Add(definition.Id, definition);
            }
        }
    }
}
