using System;
using System.Reflection;
using SevenBattles.Core.Battle;
using SevenBattles.Core.Diagnostics;
using SevenBattles.Core.Items;
using SevenBattles.Core.Players;
using UnityEngine;

namespace SevenBattles.Core.Save
{
    /// <summary>
    /// Creates a runtime-only PlayerContext clone and loads autosave data into it once at startup.
    /// </summary>
    public static class PlayerContextAutoSaveBootstrap
    {
        private const BindingFlags FIELD_FLAGS = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static bool _initialized;
        private static PlayerContext _runtimeContext;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            _initialized = false;
            _runtimeContext = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitializeOnGameStart()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            var sourceContext = FindSourceContext();
            if (sourceContext == null)
            {
                SBLog.Warn("PlayerContextAutoSaveBootstrap: No PlayerContext found at startup. Autosave load skipped.");
                return;
            }

            _runtimeContext = CreateRuntimeContextClone(sourceContext);
            if (_runtimeContext == null)
            {
                SBLog.Error("PlayerContextAutoSaveBootstrap: Failed to create runtime PlayerContext clone.");
                return;
            }

            int reboundCount = RebindScenePlayerContextReferences(sourceContext, _runtimeContext);
            SBLog.Info($"PlayerContextAutoSaveBootstrap: Runtime PlayerContext initialized from '{sourceContext.name}'. Rebound references: {reboundCount}.");

            bool loaded = PlayerContextAutoSaveUtility.TryLoadIntoPlayerContext(_runtimeContext, out string path);
            if (!loaded)
            {
                SBLog.Info($"PlayerContextAutoSaveBootstrap: No autosave applied from '{path}'.");
            }
        }

        private static PlayerContext FindSourceContext()
        {
            var behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            for (int i = 0; i < behaviours.Length; i++)
            {
                var behaviour = behaviours[i];
                if (behaviour == null)
                {
                    continue;
                }

                var fields = behaviour.GetType().GetFields(FIELD_FLAGS);
                for (int j = 0; j < fields.Length; j++)
                {
                    var field = fields[j];
                    if (field.FieldType != typeof(PlayerContext))
                    {
                        continue;
                    }

                    var value = field.GetValue(behaviour) as PlayerContext;
                    if (value != null)
                    {
                        return value;
                    }
                }
            }

            var contexts = Resources.FindObjectsOfTypeAll<PlayerContext>();
            if (contexts == null || contexts.Length == 0)
            {
                return null;
            }

            for (int i = 0; i < contexts.Length; i++)
            {
                if (contexts[i] != null)
                {
                    return contexts[i];
                }
            }

            return null;
        }

        private static PlayerContext CreateRuntimeContextClone(PlayerContext sourceContext)
        {
            if (sourceContext == null)
            {
                return null;
            }

            var context = UnityEngine.Object.Instantiate(sourceContext);
            context.name = $"{sourceContext.name} (Runtime)";
            context.hideFlags = HideFlags.DontSave;

            context.PlayerSquad = ClonePlayerSquad(sourceContext.PlayerSquad);
            context.Inventory = CloneInventory(sourceContext.Inventory);
            return context;
        }

        private static PlayerSquad ClonePlayerSquad(PlayerSquad source)
        {
            if (source == null)
            {
                return null;
            }

            var clone = UnityEngine.Object.Instantiate(source);
            clone.name = $"{source.name} (Runtime)";
            clone.hideFlags = HideFlags.DontSave;
            clone.UnitLoadouts = UnitSpellLoadout.CloneArray(source.GetLoadouts());
            return clone;
        }

        private static PlayerInventory CloneInventory(PlayerInventory source)
        {
            if (source == null)
            {
                return null;
            }

            var clone = UnityEngine.Object.Instantiate(source);
            clone.name = $"{source.name} (Runtime)";
            clone.hideFlags = HideFlags.DontSave;

            var sourceEntries = source.Entries;
            var cloneEntries = clone.Entries;
            cloneEntries.Clear();

            if (sourceEntries == null || sourceEntries.Count == 0)
            {
                return clone;
            }

            for (int i = 0; i < sourceEntries.Count; i++)
            {
                var entry = sourceEntries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.DefinitionId))
                {
                    continue;
                }

                int quantity = entry.Kind == InventoryEntry.EntryKind.Item
                    ? Mathf.Max(1, entry.Quantity)
                    : 1;
                cloneEntries.Add(new InventoryEntry
                {
                    Kind = entry.Kind,
                    DefinitionId = entry.DefinitionId,
                    Quantity = quantity
                });
            }

            return clone;
        }

        private static int RebindScenePlayerContextReferences(PlayerContext source, PlayerContext runtime)
        {
            if (source == null || runtime == null)
            {
                return 0;
            }

            int reboundCount = 0;
            var behaviours = Resources.FindObjectsOfTypeAll<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                var behaviour = behaviours[i];
                if (behaviour == null)
                {
                    continue;
                }

                var scene = behaviour.gameObject.scene;
                if (!scene.IsValid())
                {
                    continue;
                }

                var fields = behaviour.GetType().GetFields(FIELD_FLAGS);
                for (int j = 0; j < fields.Length; j++)
                {
                    var field = fields[j];
                    if (field.FieldType != typeof(PlayerContext) || field.IsInitOnly)
                    {
                        continue;
                    }

                    try
                    {
                        var current = field.GetValue(behaviour) as PlayerContext;
                        if (current == null || ReferenceEquals(current, source))
                        {
                            field.SetValue(behaviour, runtime);
                            reboundCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        SBLog.Warn($"PlayerContextAutoSaveBootstrap: Failed to rebind '{behaviour.GetType().Name}.{field.Name}'. {ex}");
                    }
                }
            }

            return reboundCount;
        }
    }
}
