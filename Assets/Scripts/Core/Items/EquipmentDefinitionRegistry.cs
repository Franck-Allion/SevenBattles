using System.Collections.Generic;
using SevenBattles.Core.Diagnostics;
using UnityEngine;

namespace SevenBattles.Core.Items
{
    [CreateAssetMenu(menuName = "SevenBattles/Items/Equipment Definition Registry", fileName = "EquipmentDefinitionRegistry")]
    public sealed class EquipmentDefinitionRegistry : ScriptableObject
    {
        [SerializeField, Tooltip("All equipment definitions indexed by ID.")]
        private EquipmentDefinition[] _definitions;

        private readonly Dictionary<string, EquipmentDefinition> _lookup = new Dictionary<string, EquipmentDefinition>();

        public EquipmentDefinition GetById(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            if (_lookup.Count == 0)
            {
                RebuildLookup();
            }

            _lookup.TryGetValue(id, out EquipmentDefinition definition);
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
                EquipmentDefinition definition = _definitions[i];
                if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
                {
                    continue;
                }

                if (_lookup.ContainsKey(definition.Id))
                {
                    SBLog.Warn($"EquipmentDefinitionRegistry: Duplicate equipment ID '{definition.Id}' found. Keeping first occurrence.", this);
                    continue;
                }

                _lookup.Add(definition.Id, definition);
            }
        }
    }
}
