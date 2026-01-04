using System;
using UnityEngine;

namespace SevenBattles.Core.Save
{
    /// <summary>
    /// Aggregates multiple IGameStateLoadHandler instances so different domains
    /// can apply portions of SaveGameData in a single load operation.
    /// </summary>
    public class CompositeGameStateLoadHandler : MonoBehaviour, IGameStateLoadHandler
    {
        [SerializeField, Tooltip("List of MonoBehaviours that implement IGameStateLoadHandler (e.g., BattleSessionLoadHandler, BattleGameStateLoadHandler).")]
        private MonoBehaviour[] _handlers;

        private IGameStateLoadHandler[] _cached;

        private void Awake()
        {
            CacheHandlers();
        }

        private void OnValidate()
        {
            CacheHandlers();
        }

        private void CacheHandlers()
        {
            if (_handlers == null || _handlers.Length == 0)
            {
                _cached = Array.Empty<IGameStateLoadHandler>();
                return;
            }

            var list = new IGameStateLoadHandler[_handlers.Length];
            int count = 0;
            for (int i = 0; i < _handlers.Length; i++)
            {
                var b = _handlers[i];
                if (b == null)
                {
                    continue;
                }

                if (b is IGameStateLoadHandler handler)
                {
                    list[count++] = handler;
                }
                else
                {
                    Debug.LogWarning($"CompositeGameStateLoadHandler: '{b.name}' does not implement IGameStateLoadHandler.", this);
                }
            }

            if (count == list.Length)
            {
                _cached = list;
                return;
            }

            _cached = new IGameStateLoadHandler[count];
            Array.Copy(list, _cached, count);
        }

        public void ApplyLoadedGame(SaveGameData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (_cached == null)
            {
                CacheHandlers();
            }

            for (int i = 0; i < _cached.Length; i++)
            {
                var handler = _cached[i];
                if (handler == null)
                {
                    continue;
                }

                try
                {
                    handler.ApplyLoadedGame(data);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"CompositeGameStateLoadHandler: Load handler '{handler.GetType().Name}' failed. {ex}", this);
                }
            }
        }
    }
}

