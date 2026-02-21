using System;
using System.Collections.Generic;
using UnityEngine;
using UnityObject = UnityEngine.Object;
using SevenBattles.Battle.Units;
using SevenBattles.Core;
using SevenBattles.Core.Battle;
using SevenBattles.Core.Contracts;
using SevenBattles.Core.Players;
using SevenBattles.Core.Save;

using SevenBattles.Core.Diagnostics;
namespace SevenBattles.Battle.Progression
{
    public class BattleXpAwarder : MonoBehaviour, IBattleXpResultProvider
    {
        [Header("Tuning")]
        [SerializeField] private BattleXpTuning _tuning;

        [Header("References (optional)")]
        [SerializeField, Tooltip("Battle session service (IBattleSessionService). If not assigned, one will be auto-found.")]
        private MonoBehaviour _sessionServiceBehaviour;
        [SerializeField, Tooltip("Turn controller (IBattleTurnController). If not assigned, one will be auto-found.")]
        private MonoBehaviour _turnControllerBehaviour;
        [SerializeField, Tooltip("Optional PlayerContext whose owned active squad can be updated with awarded XP/levels. Disabled by default to avoid mutating ScriptableObject assets during play mode.")]
        private PlayerContext _playerContext;
        [SerializeField, Tooltip("If enabled, writes awarded XP/levels back into PlayerContext owned active squad (this mutates ScriptableObject assets in-editor).")]
        private bool _syncToPlayerContextAssets;

        [Header("Debug")]
        [SerializeField] private bool _logAward;

        private IBattleSessionService _sessionService;
        private IBattleTurnController _turnController;
        private bool _awarded;

        public bool HasAwardedXp => _awarded && LastResult != null;
        public BattleXpAwardResult LastResult { get; private set; }
        public event Action<BattleXpAwardResult> XpAwarded;

        private void Awake()
        {
            ResolveServices();
        }

        private void OnEnable()
        {
            ResolveServices();

            if (_turnController != null)
            {
                _turnController.BattleEnded += HandleBattleEnded;

                if (_turnController.HasBattleEnded)
                {
                    HandleBattleEnded(_turnController.Outcome);
                }
            }
        }

        private void OnDisable()
        {
            if (_turnController != null)
            {
                _turnController.BattleEnded -= HandleBattleEnded;
            }
        }

        private void ResolveServices()
        {
            if (_sessionService == null)
            {
                if (_sessionServiceBehaviour != null)
                {
                    _sessionService = _sessionServiceBehaviour as IBattleSessionService;
                }

                if (_sessionService == null)
                {
                    var behaviours = UnityObject.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
                    for (int i = 0; i < behaviours.Length; i++)
                    {
                        if (behaviours[i] is IBattleSessionService s)
                        {
                            _sessionService = s;
                            _sessionServiceBehaviour = behaviours[i];
                            break;
                        }
                    }
                }
            }

            if (_turnController == null)
            {
                if (_turnControllerBehaviour != null)
                {
                    _turnController = _turnControllerBehaviour as IBattleTurnController;
                }

                if (_turnController == null)
                {
                    var behaviours = UnityObject.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
                    for (int i = 0; i < behaviours.Length; i++)
                    {
                        if (behaviours[i] is IBattleTurnController c)
                        {
                            _turnController = c;
                            _turnControllerBehaviour = behaviours[i];
                            break;
                        }
                    }
                }
            }
        }

        private void HandleBattleEnded(BattleOutcome outcome)
        {
            if (_awarded)
            {
                return;
            }

            if (_tuning == null)
            {
                SBLog.Warn("BattleXpAwarder: Missing BattleXpTuning reference. XP will not be awarded.", this);
                _awarded = true;
                LastResult = new BattleXpAwardResult { Outcome = outcome, TotalXp = 0 };
                XpAwarded?.Invoke(LastResult);
                return;
            }

            var session = _sessionService != null ? _sessionService.CurrentSession : null;
            if (session == null)
            {
                SBLog.Warn("BattleXpAwarder: No BattleSessionConfig available. XP will not be awarded.", this);
                _awarded = true;
                LastResult = new BattleXpAwardResult { Outcome = outcome, TotalXp = 0 };
                XpAwarded?.Invoke(LastResult);
                return;
            }

            var playerSquad = session.PlayerSquad ?? Array.Empty<UnitSpellLoadout>();
            int totalPlayerUnits = playerSquad.Length;

            var playerMetas = FindPlayerUnitMetas();
            var survivors = FindSurvivors(playerMetas);
            var survivorIndices = MapSurvivorsToSquadIndices(playerSquad, survivors);

            int alivePlayerUnits = Mathf.Min(survivorIndices.Count, totalPlayerUnits);
            int actualTurns = _turnController != null ? Mathf.Max(1, _turnController.TurnIndex) : 1;

            int totalXp = BattleXpCalculator.CalculateTotalXp(
                _tuning,
                session,
                outcome,
                alivePlayerUnits,
                totalPlayerUnits,
                actualTurns);

            var awards = AwardToSurvivors(playerSquad, survivorIndices, totalXp);
            var unitResults = BuildUnitResults(playerSquad, survivorIndices, awards);

            // Keep player progression across scene changes by syncing awarded XP/levels
            // from the runtime battle session back into PlayerContext.
            TrySyncPlayerContextFromSession(playerSquad);
            TryAutoSavePlayerProgress();

            _awarded = true;

            LastResult = new BattleXpAwardResult
            {
                Outcome = outcome,
                TotalXp = totalXp,
                AlivePlayerUnits = alivePlayerUnits,
                TotalPlayerUnits = totalPlayerUnits,
                ActualTurns = actualTurns,
                Units = unitResults.ToArray()
            };

            if (_logAward)
            {
                SBLog.Info($"BattleXpAwarder: outcome={outcome} totalXp={totalXp} alive={alivePlayerUnits}/{totalPlayerUnits} turns={actualTurns}", this);
            }

            XpAwarded?.Invoke(LastResult);
        }

        private static List<UnitBattleMetadata> FindPlayerUnitMetas()
        {
            var metas = UnityObject.FindObjectsByType<UnitBattleMetadata>(FindObjectsSortMode.None);
            var list = new List<UnitBattleMetadata>(metas.Length);
            for (int i = 0; i < metas.Length; i++)
            {
                var m = metas[i];
                if (m != null && m.IsPlayerControlled)
                {
                    list.Add(m);
                }
            }
            return list;
        }

        private static List<UnitBattleMetadata> FindSurvivors(List<UnitBattleMetadata> playerMetas)
        {
            var survivors = new List<UnitBattleMetadata>(playerMetas != null ? playerMetas.Count : 0);
            if (playerMetas == null)
            {
                return survivors;
            }

            for (int i = 0; i < playerMetas.Count; i++)
            {
                var meta = playerMetas[i];
                if (meta == null)
                {
                    continue;
                }

                var stats = meta.GetComponent<SevenBattles.Battle.Units.UnitStats>();
                if (stats != null && stats.Life > 0)
                {
                    survivors.Add(meta);
                }
            }

            return survivors;
        }

        private static int[] DistributeEvenly(int totalXp, int count, System.Random rng)
        {
            if (count <= 0 || totalXp <= 0)
            {
                return Array.Empty<int>();
            }

            int baseShare = totalXp / count;
            int remainder = totalXp - (baseShare * count);

            var result = new int[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = baseShare;
            }

            if (remainder <= 0)
            {
                return result;
            }

            int[] indices = new int[count];
            for (int i = 0; i < count; i++)
            {
                indices[i] = i;
            }

            // Fisher-Yates shuffle for a random, non-repeating remainder distribution.
            for (int i = count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (indices[i], indices[j]) = (indices[j], indices[i]);
            }

            int take = Mathf.Min(remainder, count);
            for (int i = 0; i < take; i++)
            {
                result[indices[i]] += 1;
            }

            return result;
        }

        private List<BattleXpAwardResult.UnitAward> AwardToSurvivors(UnitSpellLoadout[] playerSquad, List<int> survivorIndices, int totalXp)
        {
            var awards = new List<BattleXpAwardResult.UnitAward>();

            if (playerSquad == null || playerSquad.Length == 0 || survivorIndices == null || survivorIndices.Count == 0 || totalXp <= 0)
            {
                return awards;
            }

            var rng = new System.Random(Environment.TickCount);
            int[] split = DistributeEvenly(totalXp, survivorIndices.Count, rng);

            for (int i = 0; i < survivorIndices.Count; i++)
            {
                int squadIndex = survivorIndices[i];
                if (squadIndex < 0 || squadIndex >= playerSquad.Length)
                {
                    continue;
                }

                var loadout = playerSquad[squadIndex];
                if (loadout == null || loadout.Definition == null)
                {
                    continue;
                }

                int levelBefore = loadout.EffectiveLevel;
                int xpBefore = loadout.EffectiveXp;

                var unitDef = loadout.Definition;
                int maxLevel = unitDef != null ? Mathf.Max(1, unitDef.MaxLevel) : levelBefore;
                int[] thresholds = unitDef != null ? (unitDef.XpToNextLevel ?? Array.Empty<int>()) : Array.Empty<int>();
                int xpToNextBefore = UnitXpProgressionUtil.GetXpToNextLevel(levelBefore, maxLevel, thresholds);

                int xpAwarded = split[i];
                var applyResult = UnitXpProgressionUtil.ApplyXp(loadout, xpAwarded);

                int levelAfter = loadout.EffectiveLevel;
                int xpAfter = loadout.EffectiveXp;
                int xpToNextAfter = UnitXpProgressionUtil.GetXpToNextLevel(levelAfter, maxLevel, thresholds);
                var xpSteps = UnitXpProgressionUtil.BuildXpSteps(levelBefore, xpBefore, applyResult.XpApplied, maxLevel, thresholds);

                awards.Add(new BattleXpAwardResult.UnitAward
                {
                    SquadIndex = squadIndex,
                    UnitId = loadout.Definition.Id,
                    Portrait = loadout.Definition.Portrait,
                    IsAlive = true,
                    XpAwarded = xpAwarded,
                    XpApplied = applyResult.XpApplied,
                    XpBefore = xpBefore,
                    XpAfter = xpAfter,
                    XpToNextBefore = xpToNextBefore,
                    XpToNextAfter = xpToNextAfter,
                    LevelBefore = applyResult.LevelBefore,
                    LevelAfter = applyResult.LevelAfter,
                    MaxLevel = maxLevel,
                    ReachedMaxLevel = applyResult.ReachedMaxLevel,
                    XpSteps = xpSteps
                });
            }

            awards.Sort((a, b) => a.SquadIndex.CompareTo(b.SquadIndex));
            return awards;
        }

        private static List<BattleXpAwardResult.UnitAward> BuildUnitResults(UnitSpellLoadout[] playerSquad, List<int> survivorIndices, List<BattleXpAwardResult.UnitAward> awards)
        {
            var results = new List<BattleXpAwardResult.UnitAward>();

            if (playerSquad == null || playerSquad.Length == 0)
            {
                return results;
            }

            var aliveSet = new HashSet<int>();
            if (survivorIndices != null)
            {
                for (int i = 0; i < survivorIndices.Count; i++)
                {
                    aliveSet.Add(survivorIndices[i]);
                }
            }

            var awardByIndex = new Dictionary<int, BattleXpAwardResult.UnitAward>();
            if (awards != null)
            {
                for (int i = 0; i < awards.Count; i++)
                {
                    awardByIndex[awards[i].SquadIndex] = awards[i];
                }
            }

            for (int i = 0; i < playerSquad.Length; i++)
            {
                var loadout = playerSquad[i];
                if (loadout == null || loadout.Definition == null)
                {
                    continue;
                }

                bool isAlive = aliveSet.Contains(i);
                if (awardByIndex.TryGetValue(i, out var award))
                {
                    award.IsAlive = isAlive;
                    results.Add(award);
                    continue;
                }

                results.Add(BuildNoAwardUnitResult(loadout, i, isAlive));
            }

            return results;
        }

        private static BattleXpAwardResult.UnitAward BuildNoAwardUnitResult(UnitSpellLoadout loadout, int squadIndex, bool isAlive)
        {
            var def = loadout.Definition;
            int level = loadout.EffectiveLevel;
            int xp = loadout.EffectiveXp;

            int maxLevel = def != null ? Mathf.Max(1, def.MaxLevel) : level;
            int[] thresholds = def != null ? (def.XpToNextLevel ?? Array.Empty<int>()) : Array.Empty<int>();
            int xpToNext = UnitXpProgressionUtil.GetXpToNextLevel(level, maxLevel, thresholds);
            bool reachedMax = maxLevel > 0 && level >= maxLevel;

            return new BattleXpAwardResult.UnitAward
            {
                SquadIndex = squadIndex,
                UnitId = def != null ? def.Id : null,
                Portrait = def != null ? def.Portrait : null,
                IsAlive = isAlive,
                XpAwarded = 0,
                XpApplied = 0,
                XpBefore = xp,
                XpAfter = xp,
                XpToNextBefore = xpToNext,
                XpToNextAfter = xpToNext,
                LevelBefore = level,
                LevelAfter = level,
                MaxLevel = maxLevel,
                ReachedMaxLevel = reachedMax,
                XpSteps = Array.Empty<UnitXpProgressionStep>()
            };
        }

        private static List<int> MapSurvivorsToSquadIndices(UnitSpellLoadout[] squad, List<UnitBattleMetadata> survivors)
        {
            var indices = new List<int>(survivors.Count);
            var used = new HashSet<int>();

            for (int i = 0; i < survivors.Count; i++)
            {
                var meta = survivors[i];
                var def = meta != null ? meta.Definition : null;
                string unitId = def != null ? def.Id : null;

                int mapped = -1;
                if (!string.IsNullOrEmpty(unitId))
                {
                    for (int j = 0; j < squad.Length; j++)
                    {
                        if (used.Contains(j))
                        {
                            continue;
                        }

                        var loadout = squad[j];
                        if (loadout == null || loadout.Definition == null)
                        {
                            continue;
                        }

                        if (string.Equals(loadout.Definition.Id, unitId, StringComparison.Ordinal))
                        {
                            mapped = j;
                            break;
                        }
                    }
                }

                if (mapped < 0)
                {
                    for (int j = 0; j < squad.Length; j++)
                    {
                        if (!used.Contains(j) && squad[j] != null)
                        {
                            mapped = j;
                            break;
                        }
                    }
                }

                if (mapped >= 0)
                {
                    used.Add(mapped);
                    indices.Add(mapped);
                }
            }

            return indices;
        }

        private void SyncPlayerContextFromSession(UnitSpellLoadout[] sessionPlayerSquad)
        {
            if (_playerContext == null || sessionPlayerSquad == null || sessionPlayerSquad.Length == 0)
            {
                return;
            }

            IReadOnlyList<OwnedUnitData> currentOwned = _playerContext.OwnedUnits;
            IReadOnlyList<string> currentActiveIds = _playerContext.ActiveSquadOwnedUnitIds;

            var nextOwned = new List<OwnedUnitData>(Mathf.Max(currentOwned.Count, sessionPlayerSquad.Length));
            var ownedIndexById = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < currentOwned.Count; i++)
            {
                OwnedUnitData existing = OwnedUnitData.Clone(currentOwned[i]);
                if (existing == null || existing.Definition == null || string.IsNullOrWhiteSpace(existing.OwnedUnitId))
                {
                    continue;
                }

                if (ownedIndexById.ContainsKey(existing.OwnedUnitId))
                {
                    continue;
                }

                ownedIndexById.Add(existing.OwnedUnitId, nextOwned.Count);
                nextOwned.Add(existing);
            }

            var nextActiveIds = new List<string>(Mathf.Min(_playerContext.MaxSquadSize, sessionPlayerSquad.Length));
            var nextActiveIdSet = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < sessionPlayerSquad.Length; i++)
            {
                UnitSpellLoadout sessionLoadout = sessionPlayerSquad[i];
                if (sessionLoadout == null || sessionLoadout.Definition == null)
                {
                    continue;
                }

                string ownedId = i < currentActiveIds.Count ? currentActiveIds[i] : null;
                if (string.IsNullOrWhiteSpace(ownedId) || !nextActiveIdSet.Add(ownedId))
                {
                    ownedId = Guid.NewGuid().ToString("N");
                    while (!nextActiveIdSet.Add(ownedId))
                    {
                        ownedId = Guid.NewGuid().ToString("N");
                    }
                }

                var updated = new OwnedUnitData
                {
                    OwnedUnitId = ownedId,
                    CustomName = null,
                    Definition = sessionLoadout.Definition,
                    Level = sessionLoadout.EffectiveLevel,
                    Xp = sessionLoadout.EffectiveXp,
                    Spells = sessionLoadout.Spells != null
                        ? (SpellDefinition[])sessionLoadout.Spells.Clone()
                        : Array.Empty<SpellDefinition>()
                };

                if (ownedIndexById.TryGetValue(ownedId, out int existingIndex))
                {
                    updated.CustomName = nextOwned[existingIndex] != null ? nextOwned[existingIndex].CustomName : null;
                    nextOwned[existingIndex] = updated;
                }
                else
                {
                    ownedIndexById.Add(ownedId, nextOwned.Count);
                    nextOwned.Add(updated);
                }

                if (nextActiveIds.Count < _playerContext.MaxSquadSize)
                {
                    nextActiveIds.Add(ownedId);
                }
            }

            OwnedUnitNamingPolicy.NormalizeAllInPlace(nextOwned);
            _playerContext.SetOwnedUnits(nextOwned);
            _playerContext.SetActiveSquadOwnedUnitIds(nextActiveIds);
            SBLog.Info($"BattleXpAwarder: Synced {nextActiveIds.Count} active owned unit(s) Level/Xp from session to PlayerContext.", this);
        }

        private void TrySyncPlayerContextFromSession(UnitSpellLoadout[] sessionPlayerSquad)
        {
            if (sessionPlayerSquad == null || sessionPlayerSquad.Length == 0)
            {
                return;
            }

            ResolvePlayerContext();
            if (_playerContext == null)
            {
                return;
            }

            bool hasRuntimeContext = PlayerContext.HasRuntimeInstance &&
                                     ReferenceEquals(_playerContext, PlayerContext.RuntimeInstance);
            if (!_syncToPlayerContextAssets && !hasRuntimeContext)
            {
                return;
            }

            SyncPlayerContextFromSession(sessionPlayerSquad);
        }

        private void ResolvePlayerContext()
        {
            if (PlayerContext.HasRuntimeInstance)
            {
                _playerContext = PlayerContext.RuntimeInstance;
                return;
            }

            if (_playerContext != null)
            {
                return;
            }

            var contexts = Resources.FindObjectsOfTypeAll<PlayerContext>();
            for (int i = 0; i < contexts.Length; i++)
            {
                if (contexts[i] != null)
                {
                    _playerContext = contexts[i];
                    return;
                }
            }
        }

        private void TryAutoSavePlayerProgress()
        {
            ResolvePlayerContext();
            if (_playerContext == null)
            {
                return;
            }

            bool hasRuntimeContext = PlayerContext.HasRuntimeInstance &&
                                     ReferenceEquals(_playerContext, PlayerContext.RuntimeInstance);
            if (!_syncToPlayerContextAssets && !hasRuntimeContext)
            {
                return;
            }

            if (!PlayerContextAutoSaveUtility.TrySaveFromPlayerContext(_playerContext, out string path))
            {
                SBLog.Warn($"BattleXpAwarder: Autosave failed at '{path}'.", this);
            }
        }
    }
}
