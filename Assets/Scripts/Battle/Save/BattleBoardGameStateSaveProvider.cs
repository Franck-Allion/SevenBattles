using System;
using System.Collections.Generic;
using UnityEngine;
using SevenBattles.Battle.Units;
using SevenBattles.Battle.Tiles;
using SevenBattles.Battle.Spells;
using SevenBattles.Core;
using SevenBattles.Core.Battle;
using SevenBattles.Core.Save;

namespace SevenBattles.Battle.Save
{
    /// <summary>
    /// Captures the current placement of all player and enemy units on the battlefield.
    /// Uses UnitBattleMetadata as the single source of truth for team and tile coordinates.
    /// </summary>
    public class BattleBoardGameStateSaveProvider : MonoBehaviour, IGameStateSaveProvider
    {
        [SerializeField, Tooltip("Optional: log captured unit placements for debugging.")]
        private bool _logCapturedUnits;
        [SerializeField, Tooltip("Optional battlefield service used to strip transient tile bonuses from saved stats.")]
        private MonoBehaviour _battlefieldServiceBehaviour;
        [SerializeField, Tooltip("Optional enchantment controller used to strip active enchantment bonuses from saved stats.")]
        private BattleEnchantmentController _enchantmentController;
        private IBattlefieldService _battlefieldService;

        public void PopulateGameState(SaveGameData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            ResolveBattlefieldService();
            ResolveEnchantmentController();
            var metas = UnityEngine.Object.FindObjectsByType<UnitBattleMetadata>(FindObjectsSortMode.None);
            var placements = new List<UnitPlacementSaveData>(metas.Length);

            for (int i = 0; i < metas.Length; i++)
            {
                var meta = metas[i];
                if (meta == null)
                {
                    continue;
                }

                var def = meta.Definition;
                string unitId = def != null ? def.Id : null;
                string instanceId = meta.SaveInstanceId;

                // Derive team from IsPlayerControlled.
                string team = meta.IsPlayerControlled ? "player" : "enemy";

                // Tile coordinates; if no tile is assigned, use -1/-1 but do not fail.
                int x = -1;
                int y = -1;
                if (meta.HasTile)
                {
                    var tile = meta.Tile;
                    x = tile.x;
                    y = tile.y;
                }

                // Alive/dead status and stats derived from UnitStats; missing stats treated as alive with default values.
                bool dead = false;
                UnitStatsSaveData statsSave = null;
                var stats = meta.GetComponent<UnitStats>();
                if (stats != null)
                {
                    var tileBonus = default(TileStatBonus);
                    if (meta.HasTile && _battlefieldService != null && _battlefieldService.TryGetTileColor(meta.Tile, out var color))
                    {
                        tileBonus = BattleTileEffectRules.GetStatBonus(color);
                    }

                    dead = stats.Life <= 0;
                    var enchantmentBonus = _enchantmentController != null
                        ? _enchantmentController.GetTotalBonusFor(meta)
                        : default;
                    statsSave = new UnitStatsSaveData
                    {
                        Life = Mathf.Max(0, stats.Life - tileBonus.Life - enchantmentBonus.Life),
                        MaxLife = Mathf.Max(0, stats.MaxLife - tileBonus.Life - enchantmentBonus.Life),
                        Level = stats.Level,
                        Attack = Mathf.Max(0, stats.Attack - tileBonus.Attack - enchantmentBonus.Attack),
                        Shoot = Mathf.Max(0, stats.Shoot - tileBonus.Shoot - enchantmentBonus.Shoot),
                        ShootRange = Mathf.Max(0, stats.ShootRange),
                        ShootDefense = Mathf.Max(0, stats.ShootDefense),
                        Spell = Mathf.Max(0, stats.Spell - tileBonus.Spell - enchantmentBonus.Spell),
                        Speed = Mathf.Max(0, stats.Speed - tileBonus.Speed - enchantmentBonus.Speed),
                        Luck = Mathf.Max(0, stats.Luck - tileBonus.Luck - enchantmentBonus.Luck),
                        Defense = Mathf.Max(0, stats.Defense - tileBonus.Defense - enchantmentBonus.Defense),
                        Protection = Mathf.Max(0, stats.Protection - tileBonus.Protection - enchantmentBonus.Protection),
                        Initiative = Mathf.Max(0, stats.Initiative - tileBonus.Initiative - enchantmentBonus.Initiative),
                        Morale = Mathf.Max(0, stats.Morale - tileBonus.Morale - enchantmentBonus.Morale),
                        DeckCapacity = stats.DeckCapacity,
                        DrawCapacity = stats.DrawCapacity
                    };
                }

                // Facing comes from UnitBattleMetadata.Facing and is quantized to 4 directions.
                Vector2 facing = meta.Facing;
                if (facing.sqrMagnitude < 1e-4f)
                {
                    facing = Vector2.up;
                }

                string facingLabel = QuantizeFacing(facing);

                var placement = new UnitPlacementSaveData
                {
                    UnitId = unitId,
                    InstanceId = instanceId,
                    SpellIds = ResolveSpellIds(meta),
                    Team = team,
                    X = x,
                    Y = y,
                    Facing = facingLabel,
                    Dead = dead,
                    Stats = statsSave
                };

                placements.Add(placement);

                if (_logCapturedUnits)
                {
                    Debug.Log($"BattleBoardGameStateSaveProvider: Captured unit placement -> id='{unitId}', team='{team}', tile=({x},{y}), dead={dead}.", this);
                }
            }

            data.UnitPlacements = placements.ToArray();
        }

        private void ResolveBattlefieldService()
        {
            if (_battlefieldService != null)
            {
                return;
            }

            if (_battlefieldServiceBehaviour != null)
            {
                _battlefieldService = _battlefieldServiceBehaviour as IBattlefieldService;
            }

            if (_battlefieldService == null)
            {
                var behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
                for (int i = 0; i < behaviours.Length; i++)
                {
                    if (behaviours[i] is IBattlefieldService service)
                    {
                        _battlefieldService = service;
                        _battlefieldServiceBehaviour = behaviours[i];
                        break;
                    }
                }
            }
        }

        private void ResolveEnchantmentController()
        {
            if (_enchantmentController != null)
            {
                return;
            }

            _enchantmentController = UnityEngine.Object.FindFirstObjectByType<BattleEnchantmentController>();
        }

        private static string QuantizeFacing(Vector2 facing)
        {
            if (facing.sqrMagnitude < 1e-4f)
            {
                return "up";
            }

            facing.Normalize();
            float ax = Mathf.Abs(facing.x);
            float ay = Mathf.Abs(facing.y);

            if (ax >= ay)
            {
                // Horizontal dominates.
                return facing.x >= 0f ? "right" : "left";
            }

            // Vertical dominates.
            return facing.y >= 0f ? "up" : "down";
        }

        private static string[] ResolveSpellIds(UnitBattleMetadata meta)
        {
            if (meta == null)
            {
                return Array.Empty<string>();
            }

            var deck = meta.GetComponent<SevenBattles.Battle.Spells.UnitSpellDeck>();
            var spells = deck != null ? deck.AssignedSpells : null;
            if (spells == null || spells.Length == 0)
            {
                spells = meta.Definition != null ? meta.Definition.Spells : null;
            }

            if (spells == null || spells.Length == 0)
            {
                return Array.Empty<string>();
            }

            var list = new List<string>(spells.Length);
            for (int i = 0; i < spells.Length; i++)
            {
                var spell = spells[i];
                if (spell != null && !string.IsNullOrEmpty(spell.Id))
                {
                    list.Add(spell.Id);
                }
            }

            return list.ToArray();
        }
    }
}
