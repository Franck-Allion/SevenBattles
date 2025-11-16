using UnityEngine;
using SevenBattles.Core.Wizards;

namespace SevenBattles.Battle.Wizards
{
    // Runtime metadata describing a wizard in battle (team, portrait, board tile).
    public class WizardBattleMetadata : MonoBehaviour
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

        public WizardDefinition Definition { get; private set; }

        public static WizardBattleMetadata Ensure(GameObject instance, bool isPlayerControlled, WizardDefinition definition, Vector2Int tile)
        {
            if (instance == null) return null;
            var meta = instance.GetComponent<WizardBattleMetadata>();
            if (meta == null)
            {
                meta = instance.AddComponent<WizardBattleMetadata>();
            }

            meta._isPlayerControlled = isPlayerControlled;
            meta.Definition = definition;
            meta._portrait = definition != null ? definition.Portrait : null;
            meta.Tile = tile;
            return meta;
        }
    }
}

