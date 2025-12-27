using System;
using System.Collections.Generic;
using UnityEngine;
using SevenBattles.Battle.Board;
using SevenBattles.Battle.Spells;
using SevenBattles.Battle.Units;
using SevenBattles.Battle.Turn;
using SevenBattles.Battle.Start;
using SevenBattles.Core.Battle;
using SevenBattles.Core.Players;
using SevenBattles.Core.Save;
using SevenBattles.Core.Units;

namespace SevenBattles.Battle.Save
{
    /// <summary>
    /// Applies a SaveGameData snapshot to the active battle scene by
    /// reconstructing units on the board and restoring the turn controller
    /// state (active unit, turn index, action points).
    /// </summary>
    public class BattleGameStateLoadHandler : MonoBehaviour, IGameStateLoadHandler
    {
        [Header("Board")]
        [SerializeField, Tooltip("World-space board used for placing heroes.")]
        private WorldPerspectiveBoard _board;

        [Header("Squads")]
        [SerializeField, Tooltip("Player squad used to resolve unit definitions by id.")]
        private PlayerSquad _playerSquad;
        [SerializeField, Tooltip("Enemy squad used to resolve unit definitions by id.")]
        private PlayerSquad _enemySquad;

        [Header("Spells")]
        [SerializeField, Tooltip("Registry to resolve spell IDs to SpellDefinition ScriptableObjects.")]
        private SpellDefinitionRegistry _spellRegistry;

        [Header("Turn Controller")]
        [SerializeField, Tooltip("Turn order controller whose state will be restored from BattleTurn save data.")]
        private SimpleTurnOrderController _turnController;

        [Header("Rendering")]
        [SerializeField, Tooltip("Sorting layer used for spawned unit visuals.")]
        private string _sortingLayer = "Characters";
        [SerializeField, Tooltip("Base sorting order used when computing per-tile rendering order.")]
        private int _baseSortingOrder = 100;
        [SerializeField, Tooltip("Uniform scale multiplier applied to each spawned unit instance.")]
        private float _scaleMultiplier = 1f;
        [SerializeField, Tooltip("If true, logs detailed information about loaded unit placements.")]
        private bool _logLoadedUnits;

        private Dictionary<string, UnitDefinition> _definitions;
        private readonly Dictionary<string, SpellDefinition> _spellLookup = new Dictionary<string, SpellDefinition>(System.StringComparer.Ordinal);

        private void Awake()
        {
            BuildDefinitionLookup();
            _spellLookup.Clear();
        }

        private void BuildDefinitionLookup()
        {
            if (_definitions == null)
            {
                _definitions = new Dictionary<string, UnitDefinition>(StringComparer.Ordinal);
            }
            else
            {
                _definitions.Clear();
            }
            AddDefinitionsFromSquad(_playerSquad);
            AddDefinitionsFromSquad(_enemySquad);
        }

        private void AddDefinitionsFromSquad(PlayerSquad squad)
        {
            if (squad == null)
            {
                return;
            }

            var loadouts = squad.GetLoadouts();
            if (loadouts == null || loadouts.Length == 0)
            {
                return;
            }

            for (int i = 0; i < loadouts.Length; i++)
            {
                var def = loadouts[i] != null ? loadouts[i].Definition : null;
                if (def == null || string.IsNullOrEmpty(def.Id))
                {
                    continue;
                }

                if (!_definitions.ContainsKey(def.Id))
                {
                    _definitions.Add(def.Id, def);
                }
            }
        }

        public void ApplyLoadedGame(SaveGameData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (_board == null)
            {
                Debug.LogWarning("BattleGameStateLoadHandler: Board reference is not assigned.", this);
                return;
            }

            BuildDefinitionLookup();

            var placements = data.UnitPlacements;
            int placementCount = placements != null ? placements.Length : -1;
            string phase = data.BattleTurn != null ? data.BattleTurn.Phase : "null";
            int defCount = _definitions != null ? _definitions.Count : -1;
            Debug.Log($"BattleGameStateLoadHandler: ApplyLoadedGame started. placements={placementCount}, phase='{phase}', definitions={defCount}", this);
            if (_definitions != null && _definitions.Count > 0)
            {
                foreach (var kv in _definitions)
                {
                    var def = kv.Value;
                    string prefabName = def != null && def.Prefab != null ? def.Prefab.name : "<null prefab>";
                    Debug.Log($"BattleGameStateLoadHandler: definition id='{kv.Key}' prefab='{prefabName}'.", this);
                }
            }

            // Clear existing units so we can rebuild from the snapshot.
            var existingMetas = UnityEngine.Object.FindObjectsByType<UnitBattleMetadata>(FindObjectsSortMode.None);
            for (int i = 0; i < existingMetas.Length; i++)
            {
                var meta = existingMetas[i];
                if (meta != null && meta.gameObject != null)
                {
                    Destroy(meta.gameObject);
                }
            }

            bool isPlacementPhase = string.Equals(phase, "placement", StringComparison.OrdinalIgnoreCase);
            bool isBattlePhase = string.Equals(phase, "battle", StringComparison.OrdinalIgnoreCase);
            WorldSquadPlacementController placementController = null;
            bool[] playerIndexUsed = null;

            if (isPlacementPhase)
            {
                placementController = UnityEngine.Object.FindFirstObjectByType<WorldSquadPlacementController>();
                if (placementController != null)
                {
                    placementController.ResetPlacementState();
                }

                if (_playerSquad != null)
                {
                    var loadouts = _playerSquad.GetLoadouts();
                    if (loadouts != null)
                    {
                        playerIndexUsed = new bool[loadouts.Length];
                    }
                }
            }

            if (placements != null)
            {
                for (int i = 0; i < placements.Length; i++)
                {
                    var placement = placements[i];
                    Debug.Log(
                        $"BattleGameStateLoadHandler: Processing placement index={i} id='{placement?.UnitId}' team='{placement?.Team}' tile=({placement?.X},{placement?.Y}) dead={placement?.Dead}.",
                        this);

                    if (placement == null)
                    {
                        if (_logLoadedUnits)
                        {
                            Debug.Log("BattleGameStateLoadHandler: Skipping null placement entry.", this);
                        }
                        continue;
                    }

                    if (placement.Dead)
                    {
                        // Dead units remain dead and are not spawned.
                        if (_logLoadedUnits)
                        {
                            Debug.Log($"BattleGameStateLoadHandler: Skipping dead unit id='{placement.UnitId}' team='{placement.Team}'.", this);
                        }
                        continue;
                    }

                    if (placement.X < 0 || placement.Y < 0)
                    {
                        // Invalid tile, skip.
                        if (_logLoadedUnits)
                        {
                            Debug.Log($"BattleGameStateLoadHandler: Skipping unit id='{placement.UnitId}' team='{placement.Team}' with invalid tile=({placement.X},{placement.Y}).", this);
                        }
                        continue;
                    }

                    var tile = new Vector2Int(placement.X, placement.Y);

                    if (isPlacementPhase &&
                        placementController != null &&
                        string.Equals(placement.Team, "player", StringComparison.OrdinalIgnoreCase))
                    {
                        int playerIndex = FindPlayerWizardIndexForUnitId(placement.UnitId, playerIndexUsed);
                        if (playerIndex >= 0)
                        {
                            bool ok = placementController.TryPlaceAt(playerIndex, tile);
                            if (!ok)
                            {
                                Debug.LogWarning($"BattleGameStateLoadHandler: Failed to TryPlaceAt index={playerIndex} tile=({tile.x},{tile.y}) for unitId='{placement.UnitId}'.", this);
                            }
                            else if (_logLoadedUnits)
                            {
                                Debug.Log($"BattleGameStateLoadHandler: Placed player unitId='{placement.UnitId}' at tile=({tile.x},{tile.y}) via WorldSquadPlacementController index={playerIndex}.", this);
                            }
                            continue;
                        }
                        else
                        {
                            Debug.LogWarning($"BattleGameStateLoadHandler: Could not map player unitId='{placement.UnitId}' to a PlayerSquad index; falling back to direct spawn.", this);
                        }
                    }

                    bool isPlayerControlled = string.Equals(placement.Team, "player", StringComparison.OrdinalIgnoreCase);
                    var def = ResolveDefinition(placement.UnitId);
                    if (def == null || def.Prefab == null)
                    {
                        Debug.LogWarning(
                            $"BattleGameStateLoadHandler: Unable to resolve UnitDefinition for id '{placement.UnitId}'. Unit will not be spawned.",
                            this);
                        continue;
                    }

                    var go = Instantiate(def.Prefab);
                    UnitVisualUtil.ApplyScale(go, _scaleMultiplier);

                    var meta = UnitBattleMetadata.Ensure(go, isPlayerControlled, def, tile);
                    if (!string.IsNullOrEmpty(placement.InstanceId))
                    {
                        meta.SaveInstanceId = placement.InstanceId;
                    }

                    meta.Facing = DecodeFacing(placement.Facing);
                    meta.SortingLayer = _sortingLayer;
                    meta.BaseSortingOrder = _baseSortingOrder;

                    var stats = go.GetComponent<UnitStats>();
                    if (stats == null)
                    {
                        stats = go.AddComponent<UnitStats>();
                    }

                    // Always initialize from definition so ActionPoints and other base fields are set,
                    // then override with any saved runtime stat values (Life, Attack, etc.).
                    stats.ApplyBase(def.BaseStats);
                    if (placement.Stats != null)
                    {
                        stats.ApplySaved(placement.Stats);
                    }

                    var assignedSpells = ResolvePlacementSpells(placement, def);
                    UnitSpellDeck.Ensure(go).Configure(assignedSpells, stats.DeckCapacity, stats.DrawCapacity);

                    int sortingOrder = ComputeSortingOrder(tile.x, tile.y, i);
                    UnitVisualUtil.InitializeHero(go, _sortingLayer, sortingOrder, meta.Facing);
                    _board.PlaceHero(go.transform, tile.x, tile.y, _sortingLayer, sortingOrder);

                    if (_logLoadedUnits)
                    {
                        Debug.Log($"BattleGameStateLoadHandler: Spawned unit id='{placement.UnitId}' team='{placement.Team}' at tile=({tile.x},{tile.y}), sortingOrder={sortingOrder}.", this);
                    }
                }
            }

            var metasAfter = UnityEngine.Object.FindObjectsByType<UnitBattleMetadata>(FindObjectsSortMode.None);
            Debug.Log($"BattleGameStateLoadHandler: After ApplyLoadedGame, UnitBattleMetadata count={metasAfter.Length}.", this);

            if (data.BattleTurn != null && _turnController != null && isBattlePhase)
            {
                var placementCtrl = placementController ?? UnityEngine.Object.FindFirstObjectByType<WorldSquadPlacementController>();
                if (placementCtrl != null)
                {
                    placementCtrl.LockFromLoad();
                }

                _turnController.RestoreFromSave(data.BattleTurn);
            }
        }

        private UnitDefinition ResolveDefinition(string unitId)
        {
            if (string.IsNullOrEmpty(unitId))
            {
                return null;
            }

            if (_definitions == null || _definitions.Count == 0)
            {
                BuildDefinitionLookup();
            }

            if (_definitions != null && _definitions.TryGetValue(unitId, out var defFromSquads))
            {
                return defFromSquads;
            }

            // Fallback: scan all loaded UnitDefinition assets and cache matches by Id.
            var allDefs = Resources.FindObjectsOfTypeAll<UnitDefinition>();
            for (int i = 0; i < allDefs.Length; i++)
            {
                var def = allDefs[i];
                if (def == null || string.IsNullOrEmpty(def.Id))
                {
                    continue;
                }

                if (!_definitions.ContainsKey(def.Id))
                {
                    _definitions.Add(def.Id, def);
                }
            }

            return _definitions.TryGetValue(unitId, out var resolved) ? resolved : null;
        }

        private SpellDefinition[] ResolvePlacementSpells(UnitPlacementSaveData placement, UnitDefinition def)
        {
            if (placement != null)
            {
                var resolved = ResolveSpells(placement.SpellIds);
                if (resolved.Length > 0)
                {
                    return resolved;
                }
            }

            return def != null ? (def.Spells ?? System.Array.Empty<SpellDefinition>()) : System.Array.Empty<SpellDefinition>();
        }

        private SpellDefinition[] ResolveSpells(string[] ids)
        {
            if (ids == null || ids.Length == 0)
            {
                return System.Array.Empty<SpellDefinition>();
            }

            var list = new List<SpellDefinition>(ids.Length);
            for (int i = 0; i < ids.Length; i++)
            {
                var spell = ResolveSpellDefinition(ids[i]);
                if (spell != null)
                {
                    list.Add(spell);
                }
            }

            return list.ToArray();
        }

        private SpellDefinition ResolveSpellDefinition(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            if (_spellRegistry != null)
            {
                return _spellRegistry.GetById(id);
            }

            if (_spellLookup.TryGetValue(id, out var cached))
            {
                return cached;
            }

            var allSpells = Resources.FindObjectsOfTypeAll<SpellDefinition>();
            for (int i = 0; i < allSpells.Length; i++)
            {
                var spell = allSpells[i];
                if (spell == null || string.IsNullOrEmpty(spell.Id))
                {
                    continue;
                }

                if (!_spellLookup.ContainsKey(spell.Id))
                {
                    _spellLookup.Add(spell.Id, spell);
                }
            }

            return _spellLookup.TryGetValue(id, out var resolved) ? resolved : null;
        }

        private int FindPlayerWizardIndexForUnitId(string unitId, bool[] usedIndices)
        {
            if (string.IsNullOrEmpty(unitId) || _playerSquad == null)
            {
                return -1;
            }

            var loadouts = _playerSquad.GetLoadouts();
            if (loadouts == null)
            {
                return -1;
            }

            int length = loadouts.Length;

            for (int i = 0; i < length; i++)
            {
                if (usedIndices != null && i < usedIndices.Length && usedIndices[i])
                {
                    continue;
                }

                var def = loadouts[i] != null ? loadouts[i].Definition : null;
                if (def != null && string.Equals(def.Id, unitId, StringComparison.Ordinal))
                {
                    if (usedIndices != null && i < usedIndices.Length)
                    {
                        usedIndices[i] = true;
                    }

                    return i;
                }
            }

            return -1;
        }

        private static Vector2 DecodeFacing(string facing)
        {
            switch (facing)
            {
                case "down":
                    return Vector2.down;
                case "left":
                    return Vector2.left;
                case "right":
                    return Vector2.right;
                default:
                    return Vector2.up;
            }
        }

        private int ComputeSortingOrder(int x, int y, int index)
        {
            if (_board == null)
            {
                return _baseSortingOrder + index;
            }

            return _board.ComputeSortingOrder(x, y, _baseSortingOrder, rowStride: 10, intraRowOffset: index % 10);
        }
    }
}
