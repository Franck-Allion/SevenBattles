using System;
using System.Collections.Generic;
using UnityEngine;

namespace SevenBattles.Core.Save
{
    /// <summary>
    /// Aggregates multiple IGameStateSaveProvider instances so that different domains
    /// (Core, Battle, etc.) can contribute to a single SaveGameData snapshot.
    /// </summary>
    public class CompositeGameStateSaveProvider : MonoBehaviour, IGameStateSaveProvider
    {
        [SerializeField, Tooltip("List of MonoBehaviours that implement IGameStateSaveProvider (e.g., PlayerSquadGameStateSaveProvider, BattleBoardGameStateSaveProvider).")]
        private MonoBehaviour[] _providers;

        private IGameStateSaveProvider[] _typedProviders;

        private void Awake()
        {
            CacheProviders();
        }

        private void OnValidate()
        {
            CacheProviders();
        }

        private void CacheProviders()
        {
            if (_providers == null || _providers.Length == 0)
            {
                _typedProviders = Array.Empty<IGameStateSaveProvider>();
                return;
            }

            var list = new List<IGameStateSaveProvider>(_providers.Length);
            for (int i = 0; i < _providers.Length; i++)
            {
                var mb = _providers[i];
                if (mb == null)
                {
                    continue;
                }

                if (mb is IGameStateSaveProvider provider)
                {
                    list.Add(provider);
                }
                else
                {
                    Debug.LogWarning($"CompositeGameStateSaveProvider on '{name}': Assigned object '{mb.name}' does not implement IGameStateSaveProvider.", this);
                }
            }

            _typedProviders = list.ToArray();
        }

        public void PopulateGameState(SaveGameData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (_typedProviders == null)
            {
                CacheProviders();
            }

            for (int i = 0; i < _typedProviders.Length; i++)
            {
                var provider = _typedProviders[i];
                if (provider == null)
                {
                    continue;
                }

                try
                {
                    provider.PopulateGameState(data);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"CompositeGameStateSaveProvider: Provider '{provider.GetType().FullName}' threw during PopulateGameState. {ex}");
                }
            }
        }
    }
}

