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
                    return Array.Empty<PreloadResult>();
                }

                _tasks.Clear();

                ShaderVariantCollection[] shaderCollections = manifest.ShaderCollections;
                if (shaderCollections != null && shaderCollections.Length > 0)
                {
                    _tasks.Add(new ShaderWarmupPreloadTask(shaderCollections));
                }

                string[] localizationTableNames = manifest.LocalizationTableNames;
                if (localizationTableNames != null && localizationTableNames.Length > 0)
                {
                    _tasks.Add(new LocalizationPreloadTask(localizationTableNames));
                }

                var audioClips = manifest.AudioClips;
                if (audioClips != null && audioClips.Length > 0)
                {
                    _tasks.Add(new AudioPreloadTask(audioClips));
                }

                var sprites = manifest.Sprites;
                var textures = manifest.Textures;
                bool hasSprites = sprites != null && sprites.Length > 0;
                bool hasTextures = textures != null && textures.Length > 0;
                if (hasSprites || hasTextures)
                {
                    _tasks.Add(new TexturePreloadTask(sprites, textures));
                }

                var prefabsToWarm = manifest.PrefabsToWarm;
                if (prefabsToWarm != null && prefabsToWarm.Length > 0)
                {
                    _tasks.Add(new PrefabWarmupPreloadTask(prefabsToWarm));
                }

                if (_tasks.Count == 0)
                {
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
                    SBLog.Info($"[Preload] {task.Name}: {result.DurationMs:F1}ms");
                }

                if (shouldLogProfilerSummary)
                {
                    var report = new PreloadTimingReport(_tasks.Count, totalMs, results, taskNames);
                    SBLog.Info(report.ToSummary());
                }

                return results;
            }
        }
    }
}
