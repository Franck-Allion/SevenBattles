using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SevenBattles.Core.Units
{
    /// <summary>
    /// Registry that maps UnitDefinition IDs to UnitDefinition ScriptableObjects.
    /// Used by load handlers to resolve saved unit IDs back to live objects.
    /// Avoids fragile Resources.Load or hardcoded paths.
    /// </summary>
    [CreateAssetMenu(menuName = "SevenBattles/Unit Definition Registry", fileName = "UnitDefinitionRegistry")]
    public class UnitDefinitionRegistry : ScriptableObject
    {
        [SerializeField, Tooltip("All available unit definitions in the game.")]
        private UnitDefinition[] _definitions;

        private Dictionary<string, UnitDefinition> _lookup;

        private void OnEnable()
        {
            RebuildLookup();
        }

        private void OnValidate()
        {
            RebuildLookup();
        }

        /// <summary>
        /// Gets a UnitDefinition by its ID.
        /// Returns null if not found.
        /// </summary>
        public UnitDefinition GetById(string id)
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

        /// <summary>
        /// Gets all registered unit definitions.
        /// </summary>
        public UnitDefinition[] GetAll()
        {
            return _definitions ?? System.Array.Empty<UnitDefinition>();
        }

        private void RebuildLookup()
        {
            _lookup = new Dictionary<string, UnitDefinition>();

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
                    Debug.LogWarning($"UnitDefinitionRegistry: Duplicate unit ID '{def.Id}' found. Only the first occurrence will be used.", this);
                    continue;
                }

                _lookup[def.Id] = def;
            }
        }
    }
}
