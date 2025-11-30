using System;
using System.Collections.Generic;
using UnityEngine;
using SevenBattles.Battle.Units;
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

        public void PopulateGameState(SaveGameData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

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
                    dead = stats.Life <= 0;
                    statsSave = new UnitStatsSaveData
                    {
                        Life = stats.Life,
                        MaxLife = stats.MaxLife,
                        Attack = stats.Attack,
                        Shoot = stats.Shoot,
                        Spell = stats.Spell,
                        Speed = stats.Speed,
                        Luck = stats.Luck,
                        Defense = stats.Defense,
                        Protection = stats.Protection,
                        Initiative = stats.Initiative,
                        Morale = stats.Morale
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
    }
}
