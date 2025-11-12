using UnityEngine;
using SevenBattles.Battle.Board;
using SevenBattles.Core.Players;
using SevenBattles.Core.Wizards;

namespace SevenBattles.Battle.Start
{
    // Spawns a squad of wizards (world-space SpriteRenderer prefabs) on the first row of the board.
    // Default orientation of HeroEditor4D wizards faces the top of the screen, so no rotation is applied.
    public class WorldSquadStartController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private WorldPerspectiveBoard _board;

        [Header("Squad Prefabs")]
        [Tooltip("Optional: legacy prefabs list. If PlayerSquad is assigned, it is used instead.")]
        [SerializeField] private GameObject[] _wizardPrefabs = new GameObject[3];
        [Tooltip("Preferred: data-driven squad.")]
        [SerializeField] private PlayerSquad _playerSquad;

        [Header("Placement")]
        [Tooltip("Tile X indices for each wizard on row 0. If empty or shorter than prefabs, defaults to 0,1,2.")]
        [SerializeField] private int[] _tileXs;
        [SerializeField] private int _rowY = 0; // First row

        [Header("Rendering")]
        [SerializeField] private string _sortingLayer = "Characters";
        [SerializeField] private int _baseSortingOrder = 0; // Each subsequent wizard increments by 1 to avoid z-fighting
        [SerializeField, Tooltip("Uniform scale multiplier applied to each spawned wizard instance.")]
        private float _scaleMultiplier = 1f;

        [Header("Behavior")]
        [SerializeField] private bool _autoStartOnPlay = true;

        private void Start()
        {
            if (_autoStartOnPlay)
            {
                StartSquad();
            }
        }

        // Public entry to spawn all configured wizards.
        public void StartSquad()
        {
            if (_board == null)
            {
                Debug.LogWarning("WorldSquadStartController: Missing board reference.");
                return;
            }

            var defs = _playerSquad != null ? _playerSquad.Wizards : null;
            if ((defs == null || defs.Length == 0) && (_wizardPrefabs == null || _wizardPrefabs.Length == 0))
            {
                Debug.LogWarning("WorldSquadStartController: No wizard prefabs configured.");
                return;
            }

            if (defs != null && defs.Length > 0)
            {
                for (int i = 0; i < defs.Length; i++)
                {
                    var def = defs[i];
                    if (def == null || def.Prefab == null) continue;
                    int tileX = GetTileXForIndex(i);
                    var go = Object.Instantiate(def.Prefab);
                    SevenBattles.Battle.Wizards.WizardVisualUtil.ApplyScale(go, _scaleMultiplier);
                    int sortingOrder = _board != null ? _board.ComputeSortingOrder(tileX, _rowY, _baseSortingOrder, rowStride: 10, intraRowOffset: i % 10) : (_baseSortingOrder + i);
                    SevenBattles.Battle.Wizards.WizardVisualUtil.InitializeHero(go, _sortingLayer, sortingOrder, Vector2.up);
                    _board.PlaceHero(go.transform, tileX, _rowY, _sortingLayer, sortingOrder);
                }
            }
            else
            {
                for (int i = 0; i < _wizardPrefabs.Length; i++)
                {
                    var prefab = _wizardPrefabs[i];
                    if (prefab == null) continue;
                    int tileX = GetTileXForIndex(i);
                    var go = Object.Instantiate(prefab);
                    SevenBattles.Battle.Wizards.WizardVisualUtil.ApplyScale(go, _scaleMultiplier);
                    int sortingOrder = _board != null ? _board.ComputeSortingOrder(tileX, _rowY, _baseSortingOrder, rowStride: 10, intraRowOffset: i % 10) : (_baseSortingOrder + i);
                    SevenBattles.Battle.Wizards.WizardVisualUtil.InitializeHero(go, _sortingLayer, sortingOrder, Vector2.up);
                    _board.PlaceHero(go.transform, tileX, _rowY, _sortingLayer, sortingOrder);
                }
            }
        }

        private int GetTileXForIndex(int index)
        {
            if (_tileXs != null && index < _tileXs.Length)
                return _tileXs[index];
            // Default to first three columns: 0,1,2,...
            return index;
        }

        private enum DirectionFacing { Front, Back, Left, Right, Up = Back, Down = Front }
    }
}
