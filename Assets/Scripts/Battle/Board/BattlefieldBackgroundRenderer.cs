using UnityEngine;
using SevenBattles.Core;

namespace SevenBattles.Battle.Board
{
    [DefaultExecutionOrder(-450)]
    public sealed class BattlefieldBackgroundRenderer : MonoBehaviour
    {
        [SerializeField, Tooltip("SpriteRenderer that displays the battlefield background.")]
        private SpriteRenderer _spriteRenderer;

        [SerializeField, Tooltip("Battlefield service (MonoBehaviour implementing IBattlefieldService). If not assigned, will be auto-found at runtime.")]
        private MonoBehaviour _battlefieldServiceBehaviour;

        private IBattlefieldService _battlefieldService;

        private void Awake()
        {
            if (_spriteRenderer == null)
            {
                _spriteRenderer = GetComponent<SpriteRenderer>();
            }

            ResolveService();
            ApplyBackground();
        }

        private void OnEnable()
        {
            ResolveService();
            if (_battlefieldService != null)
            {
                _battlefieldService.BattlefieldChanged += HandleBattlefieldChanged;
            }

            ApplyBackground();
        }

        private void OnDisable()
        {
            if (_battlefieldService != null)
            {
                _battlefieldService.BattlefieldChanged -= HandleBattlefieldChanged;
            }
        }

        private void HandleBattlefieldChanged(Core.Battle.BattlefieldDefinition battlefield)
        {
            ApplyBackground();
        }

        private void ResolveService()
        {
            if (_battlefieldService != null)
            {
                return;
            }

            if (_battlefieldServiceBehaviour != null)
            {
                _battlefieldService = _battlefieldServiceBehaviour as IBattlefieldService;
            }

            if (_battlefieldService == null)
            {
                var behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
                for (int i = 0; i < behaviours.Length; i++)
                {
                    if (behaviours[i] is IBattlefieldService service)
                    {
                        _battlefieldService = service;
                        _battlefieldServiceBehaviour = behaviours[i];
                        break;
                    }
                }
            }
        }

        private void ApplyBackground()
        {
            if (_spriteRenderer == null || _battlefieldService == null)
            {
                return;
            }

            var battlefield = _battlefieldService.Current;
            if (battlefield != null && battlefield.BackgroundSprite != null)
            {
                _spriteRenderer.sprite = battlefield.BackgroundSprite;
            }
        }
    }
}
