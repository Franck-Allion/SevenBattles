using UnityEngine;
using SevenBattles.Core.Battle;

namespace SevenBattles.Preparation
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class TournamentPathPreviewRenderer : MonoBehaviour
    {
        [SerializeField] private TournamentDefinition _tournament;
        [SerializeField] private SpriteRenderer _spriteRenderer;

        private void Reset()
        {
            EnsureSpriteRenderer();
        }

        private void Awake()
        {
            EnsureSpriteRenderer();
            ApplyTournament();
        }

        private void OnEnable()
        {
            ApplyTournament();
        }

        private void OnValidate()
        {
            EnsureSpriteRenderer();
            ApplyTournament();
        }

        public void SetTournamentDefinition(TournamentDefinition tournament)
        {
            _tournament = tournament;
            ApplyTournament();
        }

        private void EnsureSpriteRenderer()
        {
            if (_spriteRenderer == null)
            {
                _spriteRenderer = GetComponent<SpriteRenderer>();
            }
        }

        private void ApplyTournament()
        {
            if (_spriteRenderer == null)
            {
                return;
            }

            _spriteRenderer.sprite = _tournament != null ? _tournament.TournamentPathImage : null;
        }
    }
}
