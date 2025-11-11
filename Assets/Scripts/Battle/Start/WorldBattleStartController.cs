using UnityEngine;
using SevenBattles.Battle.Board;

namespace SevenBattles.Battle.Start
{
    // World-space variant: spawns a world prefab (SpriteRenderers) and places it on the board.
    public class WorldBattleStartController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private WorldPerspectiveBoard _board;
        [SerializeField] private GameObject _heroPrefab; // World prefab using SpriteRenderer/Animator

        [Header("Params")]
        [SerializeField] private Vector2Int _spawnTile = new Vector2Int(0, 0);
        [SerializeField] private string _sortingLayer = "Characters";
        [SerializeField] private int _sortingOrder = 0;
        [SerializeField] private bool _autoStartOnPlay = true;

        private void Start()
        {
            if (_autoStartOnPlay) StartBattleAt(_spawnTile);
        }

        public void StartBattleAt(Vector2Int tile)
        {
            StartBattleAt(tile.x, tile.y);
        }

        public void StartBattleAt(int tileX, int tileY)
        {
            if (_board == null || _heroPrefab == null)
            {
                Debug.LogWarning("WorldBattleStartController: Missing board or hero prefab.");
                return;
            }

            var go = Instantiate(_heroPrefab);
            _board.PlaceHero(go.transform, tileX, tileY, _sortingLayer, _sortingOrder);
        }
    }
}

