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
        [SerializeField] private Vector2 _facing = Vector2.up;
        [SerializeField] private string _saveInstanceId;
        [SerializeField] private string _sortingLayer = "Characters";
        [SerializeField] private int _baseSortingOrder = 0;

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

        public Vector2 Facing
        {
            get => _facing;
            set => _facing = value;
        }

        /// <summary>
        /// Stable identifier for this unit within a single battle save.
        /// This value is generated once and persisted to the save file so that
        /// load logic can match the active unit and placements reliably, without
        /// relying on Unity's transient GetInstanceID values.
        /// </summary>
        public string SaveInstanceId
        {
            get
            {
                if (string.IsNullOrEmpty(_saveInstanceId))
                {
                    _saveInstanceId = System.Guid.NewGuid().ToString("N");
                }

                return _saveInstanceId;
            }
            set => _saveInstanceId = value;
        }

        public string SortingLayer
        {
            get => _sortingLayer;
            set => _sortingLayer = value;
        }

        public int BaseSortingOrder
        {
            get => _baseSortingOrder;
            set => _baseSortingOrder = value;
        }

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
            if (string.IsNullOrEmpty(meta._saveInstanceId))
            {
                meta._saveInstanceId = System.Guid.NewGuid().ToString("N");
            }
            return meta;
        }
    }
}

