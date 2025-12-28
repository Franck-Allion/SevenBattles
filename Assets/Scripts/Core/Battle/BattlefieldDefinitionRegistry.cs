using System.Collections.Generic;
using UnityEngine;

namespace SevenBattles.Core.Battle
{
    [CreateAssetMenu(menuName = "SevenBattles/Battle/Battlefield Definition Registry", fileName = "BattlefieldDefinitionRegistry")]
    public sealed class BattlefieldDefinitionRegistry : ScriptableObject
    {
        [SerializeField] private BattlefieldDefinition[] _definitions;

        private Dictionary<string, BattlefieldDefinition> _lookup;

        private void OnEnable()
        {
            RebuildLookup();
        }

        private void OnValidate()
        {
            RebuildLookup();
        }

        public BattlefieldDefinition GetById(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            if (_lookup == null)
            {
                RebuildLookup();
            }

            _lookup.TryGetValue(id, out var def);
            return def;
        }

        public BattlefieldDefinition[] GetAll()
        {
            return _definitions ?? System.Array.Empty<BattlefieldDefinition>();
        }

        private void RebuildLookup()
        {
            _lookup = new Dictionary<string, BattlefieldDefinition>(System.StringComparer.Ordinal);

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
                    Debug.LogWarning($"BattlefieldDefinitionRegistry: Duplicate battlefield ID '{def.Id}' found. Only the first occurrence will be used.", this);
                    continue;
                }

                _lookup[def.Id] = def;
            }
        }
    }
}
