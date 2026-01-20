using System;
using UnityEngine;

namespace SevenBattles.Core.Battle
{
    [CreateAssetMenu(menuName = "SevenBattles/Battle/Tournament Definition", fileName = "TournamentDefinition")]
    public sealed class TournamentDefinition : ScriptableObject
    {
        public const int BattleCount = 7;
        public const int BattlefieldCount = BattleCount;

        [Header("Visuals")]
        [SerializeField] private Sprite _tournamentPathImage;

        [Header("Battles (in order)")]
        [SerializeField] private TournamentBattleDefinition[] _battles = new TournamentBattleDefinition[BattleCount];

        [Header("Battlefields (legacy order)")]
        [SerializeField] private BattlefieldDefinition[] _battlefields = new BattlefieldDefinition[BattleCount];

        public Sprite TournamentPathImage => _tournamentPathImage;

        public BattlefieldDefinition[] Battlefields
        {
            get
            {
                EnsureBattleCount();
                return _battlefields;
            }
        }

        public TournamentBattleDefinition[] Battles
        {
            get
            {
                EnsureBattleCount();
                return _battles;
            }
        }

        private void OnEnable()
        {
            EnsureBattleCount();
        }

        private void OnValidate()
        {
            EnsureBattleCount();
        }

        private void EnsureBattleCount()
        {
            if (_battles == null)
            {
                _battles = new TournamentBattleDefinition[BattleCount];
            }

            if (_battles.Length != BattleCount)
            {
                var resizedBattles = new TournamentBattleDefinition[BattleCount];
                Array.Copy(_battles, resizedBattles, Mathf.Min(_battles.Length, resizedBattles.Length));
                _battles = resizedBattles;
            }

            for (int i = 0; i < _battles.Length; i++)
            {
                if (_battles[i] == null)
                {
                    _battles[i] = new TournamentBattleDefinition();
                }
            }

            if (_battlefields == null)
            {
                _battlefields = new BattlefieldDefinition[BattleCount];
            }

            if (_battlefields.Length == BattleCount)
            {
                return;
            }

            var resized = new BattlefieldDefinition[BattleCount];
            Array.Copy(_battlefields, resized, Mathf.Min(_battlefields.Length, resized.Length));
            _battlefields = resized;
        }
    }
}
