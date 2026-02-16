using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SevenBattles.Core.Diagnostics;
using Unity.Profiling;
using UnityEngine.Profiling;
using ShaderVariantCollection = UnityEngine.ShaderVariantCollection;

namespace SevenBattles.Core.Preload
{
    public sealed class ScenePreloader
    {
        private static readonly ProfilerMarker _preloadMarker = new ProfilerMarker("SevenBattles.Preload.RunAll");
        private static readonly ProfilerMarker _shaderMarker = new ProfilerMarker("SevenBattles.Preload.ShaderWarmup");
        private static readonly ProfilerMarker _localizationMarker = new ProfilerMarker("SevenBattles.Preload.Localization");
        private static readonly ProfilerMarker _audioMarker = new ProfilerMarker("SevenBattles.Preload.Audio");
        private static readonly ProfilerMarker _textureMarker = new ProfilerMarker("SevenBattles.Preload.Texture");
        private static readonly ProfilerMarker _prefabMarker = new ProfilerMarker("SevenBattles.Preload.Prefab");

        private readonly List<IPreloadTask> _tasks = new List<IPreloadTask>(6);

        public async Task<PreloadResult[]> RunAllAsync(ScenePreloadManifest manifest, CancellationToken ct)
        {
            using (_preloadMarker.Auto())
            {
                AssetCacheDiagnostics.Reset();

                if (manifest == null)
                {
                    SBLog.Warn("[Preload] ScenePreloader received a null manifest. Skipping preload.");
                    return Array.Empty<PreloadResult>();
                }

                AssetCacheDiagnostics.RegisterManifestAssets(manifest.PrefabsToWarm);
                AssetCacheDiagnostics.RegisterManifestAssets(manifest.AudioClips);
                AssetCacheDiagnostics.RegisterManifestAssets(manifest.Sprites);
                AssetCacheDiagnostics.RegisterManifestAssets(manifest.Textures);

                int shaderCount = CountNonNull(manifest.ShaderCollections);
                int localizationTableCount = CountNonEmpty(manifest.LocalizationTableNames);
                int audioCount = CountNonNull(manifest.AudioClips);
                int spriteCount = CountNonNull(manifest.Sprites);
                int textureCount = CountNonNull(manifest.Textures);
                int prefabCount = CountNonNull(manifest.PrefabsToWarm);
                SBLog.Info(
                    $"[Preload] Manifest '{manifest.name}' (scene='{manifest.SceneName}') entries: " +
                    $"shaders={shaderCount}, localizationTables={localizationTableCount}, audio={audioCount}, " +
                    $"sprites={spriteCount}, textures={textureCount}, prefabs={prefabCount}.");

                _tasks.Clear();

                ShaderVariantCollection[] shaderCollections = manifest.ShaderCollections;
                if (shaderCount > 0)
                {
                    _tasks.Add(new ShaderWarmupPreloadTask(shaderCollections));
                }

                string[] localizationTableNames = manifest.LocalizationTableNames;
                if (localizationTableCount > 0)
                {
                    _tasks.Add(new LocalizationPreloadTask(localizationTableNames));
                }

                var audioClips = manifest.AudioClips;
                if (audioCount > 0)
                {
                    _tasks.Add(new AudioPreloadTask(audioClips));
                }

                var sprites = manifest.Sprites;
                var textures = manifest.Textures;
                bool hasSprites = spriteCount > 0;
                bool hasTextures = textureCount > 0;
                if (hasSprites || hasTextures)
                {
                    _tasks.Add(new TexturePreloadTask(sprites, textures));
                }

                var prefabsToWarm = manifest.PrefabsToWarm;
                if (prefabCount > 0)
                {
                    _tasks.Add(new PrefabWarmupPreloadTask(prefabsToWarm));
                }

                if (_tasks.Count == 0)
                {
                    SBLog.Warn($"[Preload] Manifest '{manifest.name}' contains no valid preload entries. Skipping tasks.");
                    return Array.Empty<PreloadResult>();
                }

                bool shouldLogProfilerSummary = Profiler.enabled;
                var results = new PreloadResult[_tasks.Count];
                string[] taskNames = shouldLogProfilerSummary ? new string[_tasks.Count] : null;
                float totalMs = 0f;
                for (int i = 0; i < _tasks.Count; i++)
                {
                    IPreloadTask task = _tasks[i];
                    if (shouldLogProfilerSummary)
                    {
                        taskNames[i] = task.Name;
                    }
                    PreloadResult result;

                    try
                    {
                        if (task is ShaderWarmupPreloadTask)
                        {
                            using (_shaderMarker.Auto())
                            {
                                result = await task.ExecuteAsync(ct);
                            }
                        }
                        else if (task is LocalizationPreloadTask)
                        {
                            using (_localizationMarker.Auto())
                            {
                                result = await task.ExecuteAsync(ct);
                            }
                        }
                        else if (task is AudioPreloadTask)
                        {
                            using (_audioMarker.Auto())
                            {
                                result = await task.ExecuteAsync(ct);
                            }
                        }
                        else if (task is TexturePreloadTask)
                        {
                            using (_textureMarker.Auto())
                            {
                                result = await task.ExecuteAsync(ct);
                            }
                        }
                        else if (task is PrefabWarmupPreloadTask)
                        {
                            using (_prefabMarker.Auto())
                            {
                                result = await task.ExecuteAsync(ct);
                            }
                        }
                        else
                        {
                            using (_preloadMarker.Auto())
                            {
                                result = await task.ExecuteAsync(ct);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        result = PreloadResult.Fail(0f, ex.Message);
                    }

                    results[i] = result;
                    totalMs += result.DurationMs;
                    if (result.Success)
                    {
                        SBLog.Info($"[Preload] {task.Name}: {result.DurationMs:F1}ms");
                    }
                    else
                    {
                        SBLog.Warn($"[Preload] {task.Name} failed after {result.DurationMs:F1}ms: {result.ErrorMessage}");
                    }
                }

                if (shouldLogProfilerSummary)
                {
                    var report = new PreloadTimingReport(_tasks.Count, totalMs, results, taskNames);
                    SBLog.Info(report.ToSummary());
                }

                return results;
            }
        }

        private static int CountNonNull<T>(T[] values) where T : UnityEngine.Object
        {
            if (values == null || values.Length == 0)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] != null)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountNonEmpty(string[] values)
        {
            if (values == null || values.Length == 0)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < values.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(values[i]))
                {
                    count++;
                }
            }

            return count;
        }
    }
}
