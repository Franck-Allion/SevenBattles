using UnityEngine;

namespace SevenBattles.Battle.Start
{
    // Minimal controller to initialize a battle by spawning a hero UI prefab
    // and placing it at a given tile center on the UiPerspectiveBoard.
    public class BattleStartController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SevenBattles.Battle.Board.UiPerspectiveBoard _board;
        [SerializeField] private GameObject _heroPrefab; // Prefab root (must include a RectTransform)

        [Header("Params")]
        [SerializeField] private Vector2Int _spawnTile = new Vector2Int(0, 0);
        [SerializeField] private bool _autoStartOnPlay = true;

        private void Start()
        {
            if (_autoStartOnPlay)
            {
                StartBattleAt(_spawnTile);
            }
        }

        public void StartBattleAt(int tileX, int tileY)
        {
            if (_board == null || _heroPrefab == null)
            {
                Debug.LogWarning("BattleStartController: Missing board or hero prefab.");
                return;
            }

            var go = Instantiate(_heroPrefab);
            var rt = go.GetComponent<RectTransform>();
            if (rt == null)
            {
                // Wrap non-UI prefabs into a UI RectTransform container under the board
                var wrapper = new GameObject(go.name + "_UI", typeof(RectTransform));
                var boardRt = _board != null ? _board.GetComponent<RectTransform>() : null;
                if (boardRt != null) ((RectTransform)wrapper.transform).SetParent(boardRt, false);
                go.transform.SetParent(wrapper.transform, false);
                go.transform.localPosition = Vector3.zero;
                rt = (RectTransform)wrapper.transform;
            }
            _board.PlaceHero(rt, tileX, tileY);
        }

        // Convenience overload
        public void StartBattleAt(Vector2Int tile)
        {
            StartBattleAt(tile.x, tile.y);
        }
    }
}
