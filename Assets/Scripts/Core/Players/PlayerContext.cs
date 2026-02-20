using System;
using System.Collections.Generic;
using SevenBattles.Core.Battle;
using SevenBattles.Core.Items;
using UnityEngine;

namespace SevenBattles.Core.Players
{
    /// <summary>
    /// Holds runtime context for the player, such as their current squad.
    /// This allows sharing the player's state across different systems (Save, Battle, etc.)
    /// without duplicating the reference.
    /// </summary>
    [CreateAssetMenu(menuName = "SevenBattles/Player Context", fileName = "PlayerContext")]
    public class PlayerContext : ScriptableObject
    {
        [Obsolete("PlayerContext.PlayerSquad is deprecated. Use OwnedUnits + ActiveSquadOwnedUnitIds and GetActiveSquadLoadoutsNonAlloc().", false)]
        [Tooltip("DEPRECATED: Legacy PlayerSquad mirror. Use OwnedUnits + ActiveSquadOwnedUnitIds as the source of truth.")]
        public PlayerSquad PlayerSquad;

        [Header("Inventory")]
        [Tooltip("Runtime player inventory for equipment, spells, and consumable items.")]
        public PlayerInventory Inventory;

        [Header("Tournament")]
        [SerializeField, Tooltip("Persistent tournament progression (completed battles and next unlocked round).")]
        private TournamentProgressState _tournamentProgress = new TournamentProgressState();

        [Header("Resources")]
        [SerializeField, Min(0), Tooltip("Current amount of gold owned by the player.")]
        private int _gold = 1000;
        [SerializeField, Min(0), Tooltip("Current amount of gems owned by the player.")]
        private int _gems = 10;

        [Header("Squad Rules")]
        [SerializeField, Min(1), Tooltip("Maximum number of units allowed in the player's squad.")]
        private int _maxSquadSize = 5;
        [SerializeField, Tooltip("All units currently owned by the player (persistent).")]
        private List<OwnedUnitData> _ownedUnits = new List<OwnedUnitData>();
        [SerializeField, Tooltip("Ordered active squad references by OwnedUnitId.")]
        private List<string> _activeSquadOwnedUnitIds = new List<string>();
        // Reused buffers to expose active squad loadouts without per-call allocations.
        private readonly List<UnitSpellLoadout> _activeLoadoutsCache = new List<UnitSpellLoadout>();
        private readonly Dictionary<string, OwnedUnitData> _ownedLookupCache = new Dictionary<string, OwnedUnitData>(StringComparer.Ordinal);
        private readonly HashSet<string> _activeIdSetCache = new HashSet<string>(StringComparer.Ordinal);

        public int Gold => Mathf.Max(0, _gold);
        public int Gems => Mathf.Max(0, _gems);
        public int MaxSquadSize => Mathf.Max(1, _maxSquadSize);
        public IReadOnlyList<OwnedUnitData> OwnedUnits => EnsureOwnedUnitsList();
        public IReadOnlyList<string> ActiveSquadOwnedUnitIds => EnsureActiveSquadIdsList();
        public TournamentProgressState TournamentProgress => _tournamentProgress ?? (_tournamentProgress = new TournamentProgressState());
        public int CurrentTournamentRoundIndex => TournamentProgress.CurrentRoundIndex;

        public event Action ResourcesChanged;
        public event Action TournamentProgressChanged;
        public event Action OwnedUnitsChanged;

        /// <summary>
        /// Runtime-only clone used during play. All gameplay mutations go through this instance.
        /// Null until initialized by PreparationAutoSaveLoader.
        /// </summary>
        private static PlayerContext _runtimeInstance;

        /// <summary>
        /// Returns the active runtime clone if initialized, or null.
        /// </summary>
        public static PlayerContext RuntimeInstance => _runtimeInstance;

        /// <summary>
        /// Returns true if a runtime clone has been initialized.
        /// </summary>
        public static bool HasRuntimeInstance => _runtimeInstance != null;

        /// <summary>
        /// Sets the runtime instance. Called by PreparationAutoSaveLoader.
        /// </summary>
        public static void SetRuntimeInstance(PlayerContext instance)
        {
            _runtimeInstance = instance;
        }

        /// <summary>
        /// Clears the runtime instance. Used in tests and domain reloads.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _runtimeInstance = null;
        }

        public void SetResources(int gold, int gems)
        {
            int clampedGold = Mathf.Max(0, gold);
            int clampedGems = Mathf.Max(0, gems);
            if (_gold == clampedGold && _gems == clampedGems)
            {
                return;
            }

            _gold = clampedGold;
            _gems = clampedGems;
            ResourcesChanged?.Invoke();
        }

        public void SetGold(int gold)
        {
            SetResources(gold, _gems);
        }

        public void SetGems(int gems)
        {
            SetResources(_gold, gems);
        }

        public bool IsTournamentBattleCompleted(int roundIndex)
        {
            return TournamentProgress.IsBattleCompleted(roundIndex);
        }

        public void MarkTournamentBattleCompleted(int roundIndex, int totalBattles = TournamentDefinition.BattleCount)
        {
            if (TournamentProgress.MarkBattleCompleted(roundIndex, totalBattles))
            {
                TournamentProgressChanged?.Invoke();
            }
        }

        public void SetTournamentProgress(int currentRoundIndex, bool[] completedBattleFlags, int totalBattles = TournamentDefinition.BattleCount)
        {
            if (TournamentProgress.SetState(currentRoundIndex, completedBattleFlags, totalBattles))
            {
                TournamentProgressChanged?.Invoke();
            }
        }

        public void SetOwnedUnits(IReadOnlyList<OwnedUnitData> ownedUnits)
        {
            var target = EnsureOwnedUnitsList();
            target.Clear();

            if (ownedUnits != null)
            {
                for (int i = 0; i < ownedUnits.Count; i++)
                {
                    OwnedUnitData clone = OwnedUnitData.Clone(ownedUnits[i]);
                    if (clone == null || clone.Definition == null)
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(clone.OwnedUnitId))
                    {
                        clone.OwnedUnitId = Guid.NewGuid().ToString("N");
                    }

                    target.Add(clone);
                }
            }

            OwnedUnitsChanged?.Invoke();
        }

        public void SetActiveSquadOwnedUnitIds(IReadOnlyList<string> activeOwnedUnitIds)
        {
            var target = EnsureActiveSquadIdsList();
            target.Clear();

            if (activeOwnedUnitIds != null)
            {
                for (int i = 0; i < activeOwnedUnitIds.Count; i++)
                {
                    string id = activeOwnedUnitIds[i];
                    if (string.IsNullOrWhiteSpace(id))
                    {
                        continue;
                    }

                    target.Add(id);
                }
            }

            OwnedUnitsChanged?.Invoke();
        }

        /// <summary>
        /// Returns active squad loadouts resolved from owned units and active owned-unit IDs.
        /// This method reuses internal buffers to avoid per-call list allocations.
        /// </summary>
        public IReadOnlyList<UnitSpellLoadout> GetActiveSquadLoadoutsNonAlloc()
        {
            var owned = EnsureOwnedUnitsList();
            var activeIds = EnsureActiveSquadIdsList();

            _ownedLookupCache.Clear();
            for (int i = 0; i < owned.Count; i++)
            {
                OwnedUnitData unit = owned[i];
                if (unit == null || unit.Definition == null || string.IsNullOrWhiteSpace(unit.OwnedUnitId))
                {
                    continue;
                }

                if (!_ownedLookupCache.ContainsKey(unit.OwnedUnitId))
                {
                    _ownedLookupCache.Add(unit.OwnedUnitId, unit);
                }
            }

            _activeIdSetCache.Clear();
            int writeIndex = 0;
            for (int i = 0; i < activeIds.Count && writeIndex < MaxSquadSize; i++)
            {
                string id = activeIds[i];
                if (string.IsNullOrWhiteSpace(id) || !_activeIdSetCache.Add(id))
                {
                    continue;
                }

                if (!_ownedLookupCache.TryGetValue(id, out OwnedUnitData ownedUnit))
                {
                    continue;
                }

                if (writeIndex >= _activeLoadoutsCache.Count)
                {
                    _activeLoadoutsCache.Add(new UnitSpellLoadout());
                }

                UnitSpellLoadout loadout = _activeLoadoutsCache[writeIndex];
                loadout.Definition = ownedUnit.Definition;
                loadout.Level = ownedUnit.EffectiveLevel;
                loadout.Xp = ownedUnit.EffectiveXp;
                loadout.Spells = ownedUnit.Spells ?? Array.Empty<SpellDefinition>();
                writeIndex++;
            }

            if (_activeLoadoutsCache.Count > writeIndex)
            {
                _activeLoadoutsCache.RemoveRange(writeIndex, _activeLoadoutsCache.Count - writeIndex);
            }

            return _activeLoadoutsCache;
        }

        private List<OwnedUnitData> EnsureOwnedUnitsList()
        {
            if (_ownedUnits == null)
            {
                _ownedUnits = new List<OwnedUnitData>();
            }

            return _ownedUnits;
        }

        private List<string> EnsureActiveSquadIdsList()
        {
            if (_activeSquadOwnedUnitIds == null)
            {
                _activeSquadOwnedUnitIds = new List<string>();
            }

            return _activeSquadOwnedUnitIds;
        }
    }
}
