using UnityEngine;
using SevenBattles.Battle.Board;
using SevenBattles.Battle.Spells;
using SevenBattles.Core.Players;
using SevenBattles.Core.Battle;
using SevenBattles.Core.Units;

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
        [SerializeField] private PlayerContext _playerContext;

        [Header("Placement")]
        [Tooltip("Tile X indices for each wizard on row 0. If empty or shorter than prefabs, defaults to 0,1,2.")]
        [SerializeField] private int[] _tileXs;
        [SerializeField] private int _rowY = 0; // First row

        [Header("Rendering")]
        [SerializeField] private string _sortingLayer = "Characters";
        [SerializeField] private int _baseSortingOrder = 100; // Shared base with enemies for consistent row-based depth ordering
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

            var playerSquad = _playerContext != null ? _playerContext.PlayerSquad : null;
            var loadouts = playerSquad != null ? playerSquad.GetLoadouts() : null;
            if ((loadouts == null || loadouts.Length == 0) && (_wizardPrefabs == null || _wizardPrefabs.Length == 0))
            {
                Debug.LogWarning("WorldSquadStartController: No wizard prefabs configured.");
                return;
            }

            if (loadouts != null && loadouts.Length > 0)
            {
                for (int i = 0; i < loadouts.Length; i++)
                {
                    var loadout = loadouts[i];
                    var def = loadout != null ? loadout.Definition : null;
                    if (def == null || def.Prefab == null) continue;
                    int tileX = GetTileXForIndex(i);
                    var go = Object.Instantiate(def.Prefab);
                    SevenBattles.Battle.Units.UnitVisualUtil.ApplyScale(go, _scaleMultiplier);
                    int sortingOrder = _board != null ? _board.ComputeSortingOrder(tileX, _rowY, _baseSortingOrder, rowStride: 10, intraRowOffset: i % 10) : (_baseSortingOrder + i);
                    var meta = SevenBattles.Battle.Units.UnitBattleMetadata.Ensure(go, true, def, new Vector2Int(tileX, _rowY));
                    if (meta != null)
                    {
                        meta.SortingLayer = _sortingLayer;
                        meta.BaseSortingOrder = _baseSortingOrder;
                    }
                    SevenBattles.Battle.Units.UnitVisualUtil.InitializeHero(go, _sortingLayer, sortingOrder, Vector2.up);
                    _board.PlaceHero(go.transform, tileX, _rowY, _sortingLayer, sortingOrder);
                    ApplyStatsIfAny(go, def);
                    ApplySpellsIfAny(go, loadout);
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
                    SevenBattles.Battle.Units.UnitVisualUtil.ApplyScale(go, _scaleMultiplier);
                    int sortingOrder = _board != null ? _board.ComputeSortingOrder(tileX, _rowY, _baseSortingOrder, rowStride: 10, intraRowOffset: i % 10) : (_baseSortingOrder + i);
                    SevenBattles.Battle.Units.UnitVisualUtil.InitializeHero(go, _sortingLayer, sortingOrder, Vector2.up);
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

        private void ApplyStatsIfAny(GameObject go, UnitDefinition def)
        {
            if (go == null || def == null) return;
            var stats = go.GetComponent<SevenBattles.Battle.Units.UnitStats>();
            if (stats == null) stats = go.AddComponent<SevenBattles.Battle.Units.UnitStats>();
            stats.ApplyBase(def.BaseStats);
        }

        private void ApplySpellsIfAny(GameObject go, UnitSpellLoadout loadout)
        {
            if (go == null || loadout == null) return;
            var stats = go.GetComponent<SevenBattles.Battle.Units.UnitStats>();
            int deckCapacity = stats != null ? stats.DeckCapacity : 0;
            int drawCapacity = stats != null ? stats.DrawCapacity : 0;
            UnitSpellDeck.Ensure(go).Configure(loadout.Spells, deckCapacity, drawCapacity);
        }

        private enum DirectionFacing { Front, Back, Left, Right, Up = Back, Down = Front }
    }
}
