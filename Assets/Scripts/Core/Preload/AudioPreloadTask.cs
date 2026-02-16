using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using SevenBattles.Core.Diagnostics;
using UnityEngine;

namespace SevenBattles.Core.Preload
{
    public sealed class AudioPreloadTask : IPreloadTask
    {
        private const int YieldInterval = 4;

        private readonly AudioClip[] _clips;

        public AudioPreloadTask(AudioClip[] clips)
        {
            _clips = clips ?? Array.Empty<AudioClip>();
        }

        public string Name => "Audio";

        public bool IsCompleted { get; private set; }

        public async Task<PreloadResult> ExecuteAsync(CancellationToken ct)
        {
            IsCompleted = false;
            var stopwatch = Stopwatch.StartNew();

            try
            {
                if (ct.IsCancellationRequested)
                {
                    stopwatch.Stop();
                    return PreloadResult.Fail((float)stopwatch.Elapsed.TotalMilliseconds, "Operation canceled.");
                }

                int processedCount = 0;
                for (int i = 0; i < _clips.Length; i++)
                {
                    if (ct.IsCancellationRequested)
                    {
                        stopwatch.Stop();
                        return PreloadResult.Fail((float)stopwatch.Elapsed.TotalMilliseconds, "Operation canceled.");
                    }

                    AudioClip clip = _clips[i];
                    if (clip == null)
                    {
                        continue;
                    }

                    bool shouldMark = clip.loadState == AudioDataLoadState.Loaded;
                    if (clip.loadState != AudioDataLoadState.Loaded)
                    {
                        bool loadRequested = clip.LoadAudioData();
                        shouldMark = loadRequested || clip.loadState == AudioDataLoadState.Loaded;
                    }

                    if (shouldMark)
                    {
                        AssetCacheDiagnostics.MarkAssetPreloaded(clip);
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
                IsCompleted = true;
            }
        }
    }
}
