using System;
using System.Collections.Generic;
using UnityEngine;
using SevenBattles.Battle.Board;
using SevenBattles.Battle.Wizards;
using SevenBattles.Core.Players;
using SevenBattles.Core.Wizards;

namespace SevenBattles.Battle.Start
{
    // Spawns an enemy squad on the back (top) two rows, in random distinct tiles.
    // Enemies face toward the player side (Vector2.down for Character4D).
    [DefaultExecutionOrder(-50)]
    public class WorldEnemySquadStartController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private WorldPerspectiveBoard _board;

        [Header("Squad Data")]
        [Tooltip("Enemy squad (reuses PlayerSquad as a list of WizardDefinitions).")]
        [SerializeField] private PlayerSquad _enemySquad;

        [Header("Placement Rules")]
        [SerializeField, Tooltip("How many rows from the back (top) are valid for enemy spawn.")]
        private int _enemyRowsFromBack = 2;

        [Header("Rendering")]
        [SerializeField] private string _sortingLayer = "Characters";
        [SerializeField, Tooltip("Baseline sorting order added before per-row ordering. Increase if background overlaps.")]
        private int _baseSortingOrder = 100;
        [SerializeField, Tooltip("Ensures enemies are at least this sorting order to stay above background.")]
        private int _sortingOrderFloor = 50;
        [SerializeField, Tooltip("Uniform scale multiplier applied to each spawned enemy instance.")]
        private float _scaleMultiplier = 1.75f;

        [Header("Behavior")]
        [SerializeField] private bool _autoStartOnPlay = true;
        [SerializeField, Tooltip("When enabled, assigns enemies to the 'Ignore Raycast' layer to prevent interaction.")]
        private bool _ignoreRaycast = true;

        private void Start()
        {
            if (_autoStartOnPlay)
            {
                StartEnemySquad();
            }
        }

        // Public entry to spawn all configured enemy wizards at random valid tiles.
        public void StartEnemySquad()
        {
            if (_board == null)
            {
                Debug.LogWarning("WorldEnemySquadStartController: Missing board reference.");
                return;
            }

            var defs = _enemySquad != null ? _enemySquad.Wizards : null;
            if (defs == null || defs.Length == 0)
            {
                Debug.LogWarning("WorldEnemySquadStartController: No enemy wizard definitions configured.");
                return;
            }

            int cols = _board.Columns;
            int rows = _board.Rows;
            if (cols <= 0 || rows <= 0)
            {
                Debug.LogWarning("WorldEnemySquadStartController: Board not initialized.");
                return;
            }

            int backRows = Mathf.Clamp(_enemyRowsFromBack, 1, rows);
            var validTiles = BuildBackRowTiles(cols, rows, backRows);
            Shuffle(validTiles);

            int toSpawn = Mathf.Min(defs.Length, validTiles.Count);
            for (int i = 0; i < toSpawn; i++)
            {
                var def = defs[i];
                if (def == null || def.Prefab == null) continue;
                var tile = validTiles[i];

                var go = Instantiate(def.Prefab);
                WizardVisualUtil.ApplyScale(go, _scaleMultiplier);
                int sortingOrder = _board.ComputeSortingOrder(tile.x, tile.y, _baseSortingOrder, rowStride: 10, intraRowOffset: i % 10);
                if (sortingOrder < _sortingOrderFloor) sortingOrder = _sortingOrderFloor + (i % 3);
                WizardVisualUtil.InitializeHero(go, _sortingLayer, sortingOrder, Vector2.down);
                _board.PlaceHero(go.transform, tile.x, tile.y, _sortingLayer, sortingOrder);
                ApplyStatsIfAny(go, def);
                WizardBattleMetadata.Ensure(go, false, def, tile);
                if (_ignoreRaycast) TrySetIgnoreRaycast(go);
            }
        }

        private static List<Vector2Int> BuildBackRowTiles(int cols, int rows, int backRows)
        {
            var list = new List<Vector2Int>(cols * backRows);
            // Back/top rows are the highest y indices: rows-1, rows-2, ...
            for (int r = 0; r < backRows; r++)
            {
                int y = rows - 1 - r;
                for (int x = 0; x < cols; x++)
                {
                    list.Add(new Vector2Int(x, y));
                }
            }
            return list;
        }

        // Fisher-Yates shuffle using UnityEngine.Random for per-combat variability
        private static void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private void ApplyStatsIfAny(GameObject go, WizardDefinition def)
        {
            if (go == null || def == null) return;
            var stats = go.GetComponent<WizardStats>();
            if (stats == null) stats = go.AddComponent<WizardStats>();
            stats.ApplyBase(def.BaseStats);
        }

        private void TrySetIgnoreRaycast(GameObject go)
        {
            int layer = LayerMask.NameToLayer("Ignore Raycast");
            if (layer < 0) return;
            SetLayerRecursive(go.transform, layer);
        }

        private void SetLayerRecursive(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            for (int i = 0; i < t.childCount; i++)
            {
                SetLayerRecursive(t.GetChild(i), layer);
            }
        }

        private void OnValidate()
        {
            if (_enemyRowsFromBack < 1) _enemyRowsFromBack = 1;
            if (_scaleMultiplier <= 0f) _scaleMultiplier = 1f;
            if (_sortingOrderFloor < 0) _sortingOrderFloor = 0;
            if (_enemySquad == null || _enemySquad.Wizards == null || _enemySquad.Wizards.Length == 0)
            {
                Debug.LogWarning("WorldEnemySquadStartController: Assign an enemy squad with 1..8 WizardDefinitions.", this);
            }
        }
    }
}
