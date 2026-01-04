using System;
using System.Collections.Generic;
using UnityEngine;
using SevenBattles.Battle.Units;
using SevenBattles.Core.Units;

namespace SevenBattles.Battle.Turn
{
    /// <summary>
    /// Service managing unit lifecycle: discovery, initiative sorting, compaction, and healing event subscriptions.
    /// Extracted from SimpleTurnOrderController to follow SRP.
    /// </summary>
    public class BattleUnitLifecycleService : MonoBehaviour
    {
        /// <summary>
        /// Represents a unit participating in battle with its metadata and stats.
        /// </summary>
        public struct UnitEntry
        {
            public UnitBattleMetadata Metadata;
            public UnitStats Stats;
        }

        private readonly HashSet<UnitStats> _healingSubscriptions = new HashSet<UnitStats>();
        private BattleVisualFeedbackService _visualFeedback;

        /// <summary>
        /// Discovers all units in the scene, sorts them by initiative (descending), and returns the list.
        /// </summary>
        public List<UnitEntry> DiscoverAndSortUnits()
        {
            var units = new List<UnitEntry>();

            var metas = UnityEngine.Object.FindObjectsByType<UnitBattleMetadata>(FindObjectsSortMode.None);
            for (int i = 0; i < metas.Length; i++)
            {
                var meta = metas[i];
                if (meta == null || !meta.isActiveAndEnabled) continue;
                var stats = meta.GetComponent<UnitStats>();
                if (stats == null) continue;
                units.Add(new UnitEntry { Metadata = meta, Stats = stats });
            }

            // Sort by initiative (descending), with SaveInstanceId as stable tie-breaker
            units.Sort((a, b) =>
            {
                int ia = a.Stats != null ? a.Stats.Initiative : 0;
                int ib = b.Stats != null ? b.Stats.Initiative : 0;
                // Higher initiative acts first
                int cmp = ib.CompareTo(ia);
                if (cmp != 0) return cmp;

                // Stable tie-breaker based on SaveInstanceId
                string ida = a.Metadata != null ? a.Metadata.SaveInstanceId : null;
                string idb = b.Metadata != null ? b.Metadata.SaveInstanceId : null;
                if (ida == null && idb == null) return 0;
                if (ida == null) return -1;
                if (idb == null) return 1;
                return string.CompareOrdinal(ida, idb);
            });

            return units;
        }

        /// <summary>
        /// Removes dead units from the list and returns the new active index.
        /// If the current active unit was removed, returns the next valid index or -1 if none remain.
        /// </summary>
        public int CompactDeadUnits(List<UnitEntry> units, int currentActiveIndex)
        {
            int newActiveIndex = currentActiveIndex;

            for (int i = units.Count - 1; i >= 0; i--)
            {
                if (!IsAlive(units[i]))
                {
                    UnsubscribeFromHealing(units[i].Stats);
                    units.RemoveAt(i);
                    if (i <= newActiveIndex)
                    {
                        newActiveIndex--;
                    }
                }
            }

            // If active index is now out of bounds or invalid, find first valid unit
            if (units.Count > 0 && (newActiveIndex < 0 || newActiveIndex >= units.Count || !IsAlive(units[newActiveIndex])))
            {
                newActiveIndex = FindFirstValidUnitIndex(units);
            }

            return newActiveIndex;
        }

        /// <summary>
        /// Checks if a unit is alive and valid for battle.
        /// </summary>
        public bool IsAlive(UnitEntry unit)
        {
            if (unit.Metadata == null || !unit.Metadata.isActiveAndEnabled) return false;
            if (unit.Stats == null) return false;
            if (unit.Stats.Life <= 0) return false;
            if (!unit.Metadata.HasTile) return false;
            return true;
        }

        /// <summary>
        /// Finds the first valid (alive) unit index in the list, or -1 if none exist.
        /// </summary>
        public int FindFirstValidUnitIndex(List<UnitEntry> units)
        {
            if (units == null || units.Count == 0) return -1;

            for (int i = 0; i < units.Count; i++)
            {
                if (IsAlive(units[i]))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Subscribes to healing events for all units in the list.
        /// </summary>
        public void SubscribeToHealingEvents(List<UnitEntry> units, BattleVisualFeedbackService visualFeedback)
        {
            _visualFeedback = visualFeedback;

            for (int i = 0; i < units.Count; i++)
            {
                var stats = units[i].Stats;
                if (stats != null && _healingSubscriptions.Add(stats))
                {
                    stats.Healed += HandleUnitHealed;
                }
            }
        }

        /// <summary>
        /// Unsubscribes from all healing events.
        /// </summary>
        public void UnsubscribeFromHealingEvents()
        {
            foreach (var stats in _healingSubscriptions)
            {
                if (stats != null)
                {
                    stats.Healed -= HandleUnitHealed;
                }
            }
            _healingSubscriptions.Clear();
        }

        private void UnsubscribeFromHealing(UnitStats stats)
        {
            if (stats == null) return;

            if (_healingSubscriptions.Remove(stats))
            {
                stats.Healed -= HandleUnitHealed;
            }
        }

        private void HandleUnitHealed(UnitStats stats, int effectiveHealAmount)
        {
            if (_visualFeedback == null || stats == null) return;
            _visualFeedback.ShowHealNumber(stats.transform.position, effectiveHealAmount);
        }

        private void OnDisable()
        {
            UnsubscribeFromHealingEvents();
        }
    }
}
