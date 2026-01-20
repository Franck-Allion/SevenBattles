using System;
using UnityEngine;
using SevenBattles.Core.Players;

namespace SevenBattles.Core.Battle
{
    [Serializable]
    public sealed class TournamentBattleDefinition
    {
        [Header("Battle Setup")]
        [SerializeField] private BattlefieldDefinition _battlefield;
        [SerializeField] private PlayerSquad _enemySquad;
        [SerializeField] private GameObject _enemyPrefab;

        [Header("Tournament View")]
        [SerializeField] private EllipseDefinition _ellipse;
        [SerializeField] private Vector2 _prefabOffset;
        [SerializeField, Min(0.01f)] private float _prefabScale = 1f;

        public BattlefieldDefinition Battlefield => _battlefield;
        public PlayerSquad EnemySquad => _enemySquad;
        public GameObject EnemyPrefab => _enemyPrefab;
        public EllipseDefinition Ellipse => _ellipse;
        public Vector2 PrefabOffset => _prefabOffset;
        public float PrefabScale => Mathf.Max(0.01f, _prefabScale);
    }
}
