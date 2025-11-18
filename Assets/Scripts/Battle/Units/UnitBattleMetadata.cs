using UnityEngine;
using SevenBattles.Core.Units;

namespace SevenBattles.Battle.Units
{
    // Runtime metadata describing a wizard in battle (team, portrait, board tile).
    public class UnitBattleMetadata : MonoBehaviour
    {
        [SerializeField] private bool _isPlayerControlled = true;
        [SerializeField] private Sprite _portrait;
        [SerializeField] private Vector2Int _tile;
        [SerializeField] private bool _hasTile;

        public bool IsPlayerControlled
        {
            get => _isPlayerControlled;
            set => _isPlayerControlled = value;
        }

        public Sprite Portrait
        {
            get => _portrait;
            set => _portrait = value;
        }

        public bool HasTile => _hasTile;

        public Vector2Int Tile
        {
            get => _tile;
            set
            {
                _tile = value;
                _hasTile = true;
            }
        }

        public UnitDefinition Definition { get; private set; }

        public static UnitBattleMetadata Ensure(GameObject instance, bool isPlayerControlled, UnitDefinition definition, Vector2Int tile)
        {
            if (instance == null) return null;
            var meta = instance.GetComponent<UnitBattleMetadata>();
            if (meta == null)
            {
                meta = instance.AddComponent<UnitBattleMetadata>();
            }

            meta._isPlayerControlled = isPlayerControlled;
            meta.Definition = definition;
            meta._portrait = definition != null ? definition.Portrait : null;
            meta.Tile = tile;
            return meta;
        }
    }
}

