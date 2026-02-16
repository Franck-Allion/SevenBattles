using System.Diagnostics;
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

        public static void Reset()
        {
            lock (_sync)
            {
                _preloadedAssetIds.Clear();
            }
        }

        public static void MarkAssetPreloaded(Object asset)
        {
            if (asset == null)
            {
                return;
            }

            int id = asset.GetInstanceID();
            lock (_sync)
            {
                _preloadedAssetIds.Add(id);
            }

            SBLog.Info($"[AssetCache] Preloaded '{asset.name}' ({asset.GetType().Name}).", asset);
        }

        [Conditional("UNITY_EDITOR")]
        public static void LogAccess(Object asset, string usagePoint, Object context = null)
        {
            if (asset == null)
            {
                SBLog.Warn($"[AssetCache] Access '{usagePoint}': asset is null.", context);
                return;
            }

            int id = asset.GetInstanceID();
            bool isPreloaded;
            lock (_sync)
            {
                isPreloaded = _preloadedAssetIds.Contains(id);
            }

            if (isPreloaded)
            {
                SBLog.Info($"[AssetCache] Access '{usagePoint}': asset='{asset.name}', source='cache(preloaded)'.", context);
                return;
            }

            SBLog.Warn($"[AssetCache] Access '{usagePoint}': asset='{asset.name}', source='NOT preloaded' (add to manifest!).", context);
        }
    }
}
