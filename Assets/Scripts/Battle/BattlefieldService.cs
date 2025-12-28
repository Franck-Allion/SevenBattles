using System;
using UnityEngine;
using SevenBattles.Core;
using SevenBattles.Core.Battle;

namespace SevenBattles.Battle
{
    public sealed class BattlefieldService : MonoBehaviour, IBattlefieldService
    {
        [Header("References")]
        [SerializeField, Tooltip("Battle session service to read battlefield selection from.")]
        private MonoBehaviour _sessionServiceBehaviour;
        [SerializeField, Tooltip("Registry used to resolve battlefield IDs.")]
        private BattlefieldDefinitionRegistry _registry;

        [Header("Fallback")]
        [SerializeField, Tooltip("Default battlefield used when session does not specify one.")]
        private BattlefieldDefinition _inspectorDefaultBattlefield;

        private IBattleSessionService _sessionService;
        private BattlefieldDefinition _current;

        public BattlefieldDefinition Current => _current;
        public event Action<BattlefieldDefinition> BattlefieldChanged;

        private void Awake()
        {
            ResolveSessionService();
            RefreshBattlefield();
        }

        public void RefreshBattlefield()
        {
            ResolveSessionService();
            var resolved = ResolveBattlefield();
            if (!ReferenceEquals(_current, resolved))
            {
                _current = resolved;
                BattlefieldChanged?.Invoke(_current);
            }
        }

        public bool TryGetTileColor(Vector2Int tile, out BattlefieldTileColor color)
        {
            if (_current == null)
            {
                color = BattlefieldTileColor.None;
                return false;
            }

            return _current.TryGetTileColor(tile, out color);
        }

        private BattlefieldDefinition ResolveBattlefield()
        {
            var session = _sessionService?.CurrentSession;
            if (session != null)
            {
                if (session.Battlefield != null)
                {
                    return session.Battlefield;
                }

                var id = session.BattlefieldId;
                if (!string.IsNullOrEmpty(id))
                {
                    var resolved = _registry != null ? _registry.GetById(id) : null;
                    if (resolved != null)
                    {
                        return resolved;
                    }

                    Debug.LogWarning($"BattlefieldService: BattlefieldId '{id}' could not be resolved. Falling back to inspector default.", this);
                }
            }

            if (_inspectorDefaultBattlefield == null)
            {
                Debug.LogWarning("BattlefieldService: No battlefield resolved. Assign a default battlefield or set BattleSessionConfig.BattlefieldId.", this);
            }

            return _inspectorDefaultBattlefield;
        }

        private void ResolveSessionService()
        {
            if (_sessionService == null)
            {
                if (_sessionServiceBehaviour != null)
                {
                    _sessionService = _sessionServiceBehaviour as IBattleSessionService;
                }

                if (_sessionService == null)
                {
                    var behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
                    for (int i = 0; i < behaviours.Length; i++)
                    {
                        if (behaviours[i] is IBattleSessionService service)
                        {
                            _sessionService = service;
                            _sessionServiceBehaviour = behaviours[i];
                            break;
                        }
                    }
                }
            }
        }
    }
}
