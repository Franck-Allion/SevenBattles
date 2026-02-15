using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ShaderVariantCollection = UnityEngine.ShaderVariantCollection;

namespace SevenBattles.Core.Preload
{
    public sealed class ShaderWarmupPreloadTask : IPreloadTask
    {
        private readonly ShaderVariantCollection[] _collections;

        public ShaderWarmupPreloadTask(ShaderVariantCollection[] collections)
        {
            _collections = collections ?? Array.Empty<ShaderVariantCollection>();
        }

        public string Name => "ShaderWarmup";

        public bool IsCompleted { get; private set; }

        public async Task<PreloadResult> ExecuteAsync(CancellationToken ct)
        {
            IsCompleted = false;
            var stopwatch = Stopwatch.StartNew();

            try
            {
                bool hasWarmedAtLeastOne = false;
                for (int i = 0; i < _collections.Length; i++)
                {
                    if (ct.IsCancellationRequested)
                    {
                        stopwatch.Stop();
                        return PreloadResult.Fail((float)stopwatch.Elapsed.TotalMilliseconds, "Operation canceled.");
                    }

                    ShaderVariantCollection collection = _collections[i];
                    if (collection == null)
                    {
                        continue;
                    }

                    if (hasWarmedAtLeastOne)
                    {
                        await Task.Yield();
                    }

                    collection.WarmUp();
                    hasWarmedAtLeastOne = true;
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
