using System;
using UnityEngine;

namespace SevenBattles.Core.Battle
{
    [CreateAssetMenu(menuName = "SevenBattles/Battle/Tournament Definition", fileName = "TournamentDefinition")]
    public sealed class TournamentDefinition : ScriptableObject
    {
        public const int BattlefieldCount = 7;

        [Header("Visuals")]
        [SerializeField] private Sprite _tournamentPathImage;

        [Header("Battlefields (in order)")]
        [SerializeField] private BattlefieldDefinition[] _battlefields = new BattlefieldDefinition[BattlefieldCount];

        public Sprite TournamentPathImage => _tournamentPathImage;

        public BattlefieldDefinition[] Battlefields
        {
            get
            {
                EnsureBattlefieldCount();
                return _battlefields;
            }
        }

        private void OnEnable()
        {
            EnsureBattlefieldCount();
        }

        private void OnValidate()
        {
            EnsureBattlefieldCount();
        }

        private void EnsureBattlefieldCount()
        {
            if (_battlefields == null)
            {
                _battlefields = new BattlefieldDefinition[BattlefieldCount];
                return;
            }

            if (_battlefields.Length == BattlefieldCount)
            {
                return;
            }

            var resized = new BattlefieldDefinition[BattlefieldCount];
            Array.Copy(_battlefields, resized, Mathf.Min(_battlefields.Length, resized.Length));
            _battlefields = resized;
        }
    }
}
