using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SevenBattles.Core.Diagnostics
{
    /// <summary>
    /// Tracks assets preloaded through scene preload tasks.
    /// </summary>
    public static class AssetCacheDiagnostics
    {
        private static readonly HashSet<int> _preloadedAssetIds = new HashSet<int>();
        private static readonly object _sync = new object();

        public static void MarkAssetPreloaded(Object asset)
        {
            if (asset == null)
            {
                return;
            }

            bool added;
            int id = asset.GetInstanceID();
            lock (_sync)
            {
                added = _preloadedAssetIds.Add(id);
            }

            if (added)
            {
                SBLog.Info($"[AssetCache] Preloaded '{asset.name}' ({asset.GetType().Name}).", asset);
            }
        }
    }
}
