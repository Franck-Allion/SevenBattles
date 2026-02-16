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
        private static readonly HashSet<int> _manifestListedAssetIds = new HashSet<int>();
        private static readonly object _sync = new object();

        public static void Reset()
        {
            lock (_sync)
            {
                _preloadedAssetIds.Clear();
                _manifestListedAssetIds.Clear();
            }

            SBLog.Info("[AssetCache] Reset preloaded asset registry.");
        }

        public static void RegisterManifestAssets<T>(T[] assets) where T : Object
        {
            if (assets == null || assets.Length == 0)
            {
                return;
            }

            int addedCount = 0;
            lock (_sync)
            {
                for (int i = 0; i < assets.Length; i++)
                {
                    Object asset = assets[i];
                    if (asset == null)
                    {
                        continue;
                    }

                    if (_manifestListedAssetIds.Add(asset.GetInstanceID()))
                    {
                        addedCount++;
                    }
                }
            }

            if (addedCount > 0)
            {
                SBLog.Info($"[AssetCache] Registered {addedCount} manifest-listed asset(s) for diagnostics.");
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
            bool isManifestListed;
            lock (_sync)
            {
                isPreloaded = _preloadedAssetIds.Contains(id);
                isManifestListed = _manifestListedAssetIds.Contains(id);
            }

            if (isPreloaded)
            {
                SBLog.Info(
                    $"[AssetCache] Access '{usagePoint}': asset='{asset.name}' ({asset.GetType().Name}), source='cache(preloaded)'.",
                    context);
                return;
            }

            if (isManifestListed)
            {
                SBLog.Info(
                    $"[AssetCache] Access '{usagePoint}': asset='{asset.name}' ({asset.GetType().Name}), source='manifest(listed, preload phase not executed yet)'.",
                    context);
                return;
            }

            string manifestField = GetSuggestedManifestField(asset);
            SBLog.Warn(
                $"[AssetCache] Access '{usagePoint}': asset='{asset.name}' ({asset.GetType().Name}), source='NOT preloaded' " +
                $"(add to manifest field '{manifestField}').",
                context);
        }

        private static string GetSuggestedManifestField(Object asset)
        {
            if (asset is AudioClip)
            {
                return "AudioClips";
            }

            if (asset is Sprite)
            {
                return "Sprites";
            }

            if (asset is Texture2D)
            {
                return "Textures";
            }

            if (asset is GameObject)
            {
                return "PrefabsToWarm";
            }

            return "PrefabsToWarm";
        }
    }
}
