using System.IO;
using UnityEngine;
using SevenBattles.Core.Players;
using SevenBattles.Core.Save;
using SevenBattles.Core.Diagnostics;

namespace SevenBattles.Preparation
{
    /// <summary>
    /// Loads PlayerContext autosave progression when the preparation scene starts.
    /// </summary>
    public sealed class PreparationAutoSaveLoader : MonoBehaviour
    {
        [SerializeField]
        private PlayerContext _playerContext;

        [SerializeField, Tooltip("If disabled, autosave loading is skipped on startup.")]
        private bool _enableAutoLoad = true;

        private void Awake()
        {
            if (!_enableAutoLoad)
            {
                SBLog.Info("PreparationAutoSaveLoader: Auto-load disabled. Skipping autosave load.", this);
                return;
            }

            if (_playerContext == null)
            {
                SBLog.Warn("PreparationAutoSaveLoader: PlayerContext is not assigned. Autosave load skipped.", this);
                return;
            }

            if (Application.isEditor)
            {
                SBLog.Warn("PreparationAutoSaveLoader: Running in Editor. ScriptableObject changes during Play Mode may persist to the asset. Consider using a runtime clone.", this);
            }

            bool loaded = global::SevenBattles.Core.Save.PlayerContextAutoSaveUtility.TryLoadIntoPlayerContext(_playerContext, out string path);
            if (loaded)
            {
                SBLog.Info($"PreparationAutoSaveLoader: Autosave loaded from '{path}'.", this);
                return;
            }

            if (!string.IsNullOrWhiteSpace(path) && !File.Exists(path))
            {
                SBLog.Info($"PreparationAutoSaveLoader: No autosave file found at '{path}'.", this);
                return;
            }

            SBLog.Warn($"PreparationAutoSaveLoader: Autosave load failed for '{path}'.", this);
        }
    }
}
