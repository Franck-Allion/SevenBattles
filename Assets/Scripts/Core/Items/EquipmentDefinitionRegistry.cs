using System;
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

        private readonly Dictionary<string, EquipmentDefinition> _lookup = new Dictionary<string, EquipmentDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, EquipmentDefinition> _fallbackLookup = new Dictionary<string, EquipmentDefinition>(StringComparer.Ordinal);
        private readonly HashSet<string> _fallbackWarnedIds = new HashSet<string>(StringComparer.Ordinal);
        private bool _fallbackLookupBuilt;

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

            if (_lookup.TryGetValue(id, out EquipmentDefinition definition))
            {
                return definition;
            }

            if (!TryResolveFromFallbackLookup(id, out definition))
            {
                return null;
            }

            if (_fallbackWarnedIds.Add(id))
            {
                SBLog.Warn(
                    $"EquipmentDefinitionRegistry: ID '{id}' was not found in serialized definitions and was resolved from runtime-loaded assets. Add it to '{name}' definitions.",
                    this);
            }

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
            _fallbackLookup.Clear();
            _fallbackWarnedIds.Clear();
            _fallbackLookupBuilt = false;

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

        private bool TryResolveFromFallbackLookup(string id, out EquipmentDefinition definition)
        {
            definition = null;
            if (string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            if (!_fallbackLookupBuilt)
            {
                RebuildFallbackLookup();
            }

            if (_fallbackLookup.TryGetValue(id, out definition))
            {
                return definition != null;
            }

            // Assets can load later in the editor; retry once with a rebuilt lookup.
            RebuildFallbackLookup();
            return _fallbackLookup.TryGetValue(id, out definition) && definition != null;
        }

        private void RebuildFallbackLookup()
        {
            _fallbackLookupBuilt = true;
            _fallbackLookup.Clear();

            EquipmentDefinition[] definitions = Resources.FindObjectsOfTypeAll<EquipmentDefinition>();
            for (int i = 0; i < definitions.Length; i++)
            {
                EquipmentDefinition definition = definitions[i];
                if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
                {
                    continue;
                }

                if (_fallbackLookup.ContainsKey(definition.Id))
                {
                    continue;
                }

                _fallbackLookup.Add(definition.Id, definition);
            }
        }
    }
}
