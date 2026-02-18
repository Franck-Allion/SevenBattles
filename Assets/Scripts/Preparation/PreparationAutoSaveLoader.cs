using System;
using System.Reflection;
using UnityEngine;
using SevenBattles.Core.Players;
using SevenBattles.Core.Items;
using SevenBattles.Core.Save;
using SevenBattles.Core.Diagnostics;
namespace SevenBattles.Preparation
{
    /// <summary>
    /// Creates a runtime clone of PlayerContext and loads autosave data into it.
    /// Rebinds all scene MonoBehaviour references from the asset to the clone.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class PreparationAutoSaveLoader : MonoBehaviour
    {
        [SerializeField, Tooltip("The authored PlayerContext asset (never mutated at runtime).")]
        private PlayerContext _playerContext;
        [SerializeField, Tooltip("If disabled, autosave loading is skipped.")]
        private bool _enableAutoLoad = true;

        private const BindingFlags FIELD_FLAGS =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private void Awake()
        {
            if (_playerContext == null)
            {
                SBLog.Warn("PreparationAutoSaveLoader: PlayerContext is not assigned.", this);
                return;
            }

            // If a runtime instance already exists (e.g. returning from BattleScene),
            // reuse it instead of creating a new clone.
            if (PlayerContext.HasRuntimeInstance)
            {
                int reboundCount = RebindSceneReferences(_playerContext, PlayerContext.RuntimeInstance);
                SBLog.Info($"PreparationAutoSaveLoader: Reusing existing runtime PlayerContext. Rebound {reboundCount} references.", this);
                // Still try to load autosave in case progression was updated.
                if (_enableAutoLoad)
                {
                    PlayerContextAutoSaveUtility.TryLoadIntoPlayerContext(PlayerContext.RuntimeInstance, out _);
                }

                return;
            }

            // Create a fresh runtime clone from the authored asset.
            var runtimeContext = CreateRuntimeClone(_playerContext);
            if (runtimeContext == null)
            {
                SBLog.Error("PreparationAutoSaveLoader: Failed to create runtime PlayerContext clone.", this);
                return;
            }

            PlayerContext.SetRuntimeInstance(runtimeContext);
            int rebound = RebindSceneReferences(_playerContext, runtimeContext);
            SBLog.Info($"PreparationAutoSaveLoader: Runtime clone created from '{_playerContext.name}'. Rebound {rebound} references.", this);

            if (!_enableAutoLoad)
            {
                SBLog.Info("PreparationAutoSaveLoader: Auto-load disabled. Skipping autosave.", this);
                return;
            }

            bool loaded = PlayerContextAutoSaveUtility.TryLoadIntoPlayerContext(runtimeContext, out string path);
            if (loaded)
            {
                SBLog.Info($"PreparationAutoSaveLoader: Autosave loaded from '{path}'.", this);
            }
            else
            {
                SBLog.Info($"PreparationAutoSaveLoader: No autosave applied (path='{path}').", this);
            }
        }

        private static PlayerContext CreateRuntimeClone(PlayerContext source)
        {
            if (source == null)
            {
                return null;
            }

            var clone = Instantiate(source);
            clone.name = $"{source.name} (Runtime)";
            clone.hideFlags = HideFlags.DontSave;

            // Deep-clone PlayerSquad so the asset's loadouts are untouched.
            if (source.PlayerSquad != null)
            {
                var squadClone = Instantiate(source.PlayerSquad);
                squadClone.name = $"{source.PlayerSquad.name} (Runtime)";
                squadClone.hideFlags = HideFlags.DontSave;

                // Clone the loadout array so mutations don't affect the asset.
                var sourceLoadouts = source.PlayerSquad.GetLoadouts();
                if (sourceLoadouts != null && sourceLoadouts.Length > 0)
                {
                    squadClone.UnitLoadouts = global::SevenBattles.Core.Battle.UnitSpellLoadout.CloneArray(sourceLoadouts);
                }

                clone.PlayerSquad = squadClone;
            }

            // Deep-clone PlayerInventory so the asset's entries are untouched.
            if (source.Inventory != null)
            {
                var invClone = Instantiate(source.Inventory);
                invClone.name = $"{source.Inventory.name} (Runtime)";
                invClone.hideFlags = HideFlags.DontSave;

                // Copy entries to a fresh list.
                var srcEntries = source.Inventory.Entries;
                var cloneEntries = invClone.Entries;
                cloneEntries.Clear();
                if (srcEntries != null)
                {
                    for (int i = 0; i < srcEntries.Count; i++)
                    {
                        var e = srcEntries[i];
                        if (e == null)
                        {
                            continue;
                        }

                        cloneEntries.Add(new InventoryEntry
                        {
                            Kind = e.Kind,
                            DefinitionId = e.DefinitionId,
                            Quantity = e.Quantity
                        });
                    }
                }

                clone.Inventory = invClone;
            }

            return clone;
        }

        /// <summary>
        /// Scans all MonoBehaviours in the scene and replaces PlayerContext fields
        /// pointing to <paramref name="source"/> with <paramref name="runtime"/>.
        /// Also handles PlayerSquad and PlayerInventory fields.
        /// </summary>
        private static int RebindSceneReferences(PlayerContext source, PlayerContext runtime)
        {
            if (source == null || runtime == null)
            {
                return 0;
            }

            int count = 0;
            var behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            for (int i = 0; i < behaviours.Length; i++)
            {
                var mb = behaviours[i];
                if (mb == null)
                {
                    continue;
                }

                var fields = mb.GetType().GetFields(FIELD_FLAGS);
                for (int j = 0; j < fields.Length; j++)
                {
                    var field = fields[j];
                    if (field.IsInitOnly)
                    {
                        continue;
                    }

                    try
                    {
                        if (field.FieldType == typeof(PlayerContext))
                        {
                            var current = field.GetValue(mb) as PlayerContext;
                            if (ReferenceEquals(current, source))
                            {
                                field.SetValue(mb, runtime);
                                count++;
                            }
                        }
                        else if (field.FieldType == typeof(PlayerSquad) && source.PlayerSquad != null)
                        {
                            var current = field.GetValue(mb) as PlayerSquad;
                            if (ReferenceEquals(current, source.PlayerSquad))
                            {
                                field.SetValue(mb, runtime.PlayerSquad);
                                count++;
                            }
                        }
                        else if (field.FieldType == typeof(PlayerInventory) && source.Inventory != null)
                        {
                            var current = field.GetValue(mb) as PlayerInventory;
                            if (ReferenceEquals(current, source.Inventory))
                            {
                                field.SetValue(mb, runtime.Inventory);
                                count++;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        SBLog.Warn($"PreparationAutoSaveLoader: Rebind failed for '{mb.GetType().Name}.{field.Name}'. {ex}", mb);
                    }
                }
            }

            return count;
        }
    }
}

