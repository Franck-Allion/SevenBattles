using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using SevenBattles.Core.Diagnostics;
using UnityEngine;

namespace SevenBattles.Core.Preload
{
    public sealed class TexturePreloadTask : IPreloadTask
    {
        private const int YieldInterval = 4;

        private readonly Sprite[] _sprites;
        private readonly Texture2D[] _textures;

        public TexturePreloadTask(Sprite[] sprites, Texture2D[] textures)
        {
            _sprites = sprites ?? Array.Empty<Sprite>();
            _textures = textures ?? Array.Empty<Texture2D>();
        }

        public string Name => "Textures";

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

                for (int i = 0; i < _sprites.Length; i++)
                {
                    if (ct.IsCancellationRequested)
                    {
                        stopwatch.Stop();
                        return PreloadResult.Fail((float)stopwatch.Elapsed.TotalMilliseconds, "Operation canceled.");
                    }

                    Sprite sprite = _sprites[i];
                    if (sprite == null)
                    {
                        continue;
                    }

                    _ = sprite.texture;
                    AssetCacheDiagnostics.MarkAssetPreloaded(sprite);
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

                for (int i = 0; i < _textures.Length; i++)
                {
                    if (ct.IsCancellationRequested)
                    {
                        stopwatch.Stop();
                        return PreloadResult.Fail((float)stopwatch.Elapsed.TotalMilliseconds, "Operation canceled.");
                    }

                    Texture2D texture = _textures[i];
                    if (texture == null)
                    {
                        continue;
                    }

                    _ = texture.width;
                    _ = texture.height;
                    AssetCacheDiagnostics.MarkAssetPreloaded(texture);
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
