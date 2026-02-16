using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using SevenBattles.Core.Diagnostics;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SevenBattles.Core.Preload
{
    public sealed class PrefabWarmupPreloadTask : IPreloadTask
    {
        private const int YieldInterval = 1;

        private readonly Object[] _prefabs;

        public PrefabWarmupPreloadTask(Object[] prefabs)
        {
            _prefabs = prefabs ?? Array.Empty<Object>();
        }

        public string Name => "PrefabWarmup";

        public bool IsCompleted { get; private set; }

        public async Task<PreloadResult> ExecuteAsync(CancellationToken ct)
        {
            IsCompleted = false;
            var stopwatch = Stopwatch.StartNew();
            GameObject warmupRoot = null;

            try
            {
                if (ct.IsCancellationRequested)
                {
                    stopwatch.Stop();
                    return PreloadResult.Fail((float)stopwatch.Elapsed.TotalMilliseconds, "Operation canceled.");
                }

                warmupRoot = new GameObject("PreloadPrefabWarmupRoot");
                warmupRoot.hideFlags = HideFlags.HideAndDontSave;
                warmupRoot.SetActive(false);

                int processedCount = 0;
                for (int i = 0; i < _prefabs.Length; i++)
                {
                    if (ct.IsCancellationRequested)
                    {
                        stopwatch.Stop();
                        return PreloadResult.Fail((float)stopwatch.Elapsed.TotalMilliseconds, "Operation canceled.");
                    }

                    GameObject prefab = _prefabs[i] as GameObject;
                    if (prefab == null)
                    {
                        continue;
                    }

                    GameObject instance = null;
                    try
                    {
                        instance = Object.Instantiate(prefab, warmupRoot.transform, false);
                        AssetCacheDiagnostics.MarkAssetPreloaded(prefab);
                    }
                    finally
                    {
                        if (instance != null)
                        {
                            Object.Destroy(instance);
                        }
                    }

                    processedCount++;
                    if ((processedCount % YieldInterval) == 0)
                    {
                        await Task.Yield();
                        if (ct.IsCancellationRequested)
                        {
                            stopwatch.Stop();
                            return PreloadResult.Fail((float)stopwatch.Elapsed.TotalMilliseconds, "Operation canceled.");
                        }
                    }
                }

                stopwatch.Stop();
                return PreloadResult.Ok((float)stopwatch.Elapsed.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return PreloadResult.Fail((float)stopwatch.Elapsed.TotalMilliseconds, ex.Message);
            }
            finally
            {
                if (warmupRoot != null)
                {
                    Object.Destroy(warmupRoot);
                }

                IsCompleted = true;
            }
        }
    }
}
