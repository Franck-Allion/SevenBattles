using System.Collections.Generic;
using UnityEngine;

namespace SevenBattles.Core.Battle
{
    /// <summary>
    /// Registry that maps SpellDefinition IDs to SpellDefinition ScriptableObjects.
    /// Used by load handlers to resolve saved spell IDs back to live objects.
    /// </summary>
    [CreateAssetMenu(menuName = "SevenBattles/Spell Definition Registry", fileName = "SpellDefinitionRegistry")]
    public class SpellDefinitionRegistry : ScriptableObject
    {
        [SerializeField, Tooltip("All available spell definitions in the game.")]
        private SpellDefinition[] _definitions;

        private Dictionary<string, SpellDefinition> _lookup;

        private void OnEnable()
        {
            RebuildLookup();
        }

        private void OnValidate()
        {
            RebuildLookup();
        }

        public SpellDefinition GetById(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            if (_lookup == null)
            {
                RebuildLookup();
            }

            _lookup.TryGetValue(id, out var definition);
            return definition;
        }

        private void RebuildLookup()
        {
            _lookup = new Dictionary<string, SpellDefinition>();

            if (_definitions == null)
            {
                return;
            }

            foreach (var def in _definitions)
            {
                if (def == null || string.IsNullOrEmpty(def.Id))
                {
                    continue;
                }

                if (_lookup.ContainsKey(def.Id))
                {
                    Debug.LogWarning($"SpellDefinitionRegistry: Duplicate spell ID '{def.Id}' found. Only the first occurrence will be used.", this);
                    continue;
                }

                _lookup[def.Id] = def;
            }
        }
    }
}
