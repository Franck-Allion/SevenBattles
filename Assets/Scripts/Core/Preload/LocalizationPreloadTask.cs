using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Localization.Settings;

namespace SevenBattles.Core.Preload
{
    public sealed class LocalizationPreloadTask : IPreloadTask
    {
        private const int InitializationTimeoutMs = 5000;

        private readonly string[] _tableNames;

        public LocalizationPreloadTask(string[] tableNames)
        {
            _tableNames = tableNames ?? Array.Empty<string>();
        }

        public string Name => "LocalizationTables";

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

                Task initializationTask = LocalizationSettings.InitializationOperation.Task;
                Task timeoutTask = ct.CanBeCanceled
                    ? Task.Delay(InitializationTimeoutMs, ct)
                    : Task.Delay(InitializationTimeoutMs);

                Task completedTask = await Task.WhenAny(initializationTask, timeoutTask);
                if (completedTask != initializationTask)
                {
                    stopwatch.Stop();
                    string timeoutMessage = ct.IsCancellationRequested
                        ? "Operation canceled."
                        : $"Localization initialization timed out after {InitializationTimeoutMs} ms.";
                    return PreloadResult.Fail((float)stopwatch.Elapsed.TotalMilliseconds, timeoutMessage);
                }

                await initializationTask;

                for (int i = 0; i < _tableNames.Length; i++)
                {
                    if (ct.IsCancellationRequested)
                    {
                        stopwatch.Stop();
                        return PreloadResult.Fail((float)stopwatch.Elapsed.TotalMilliseconds, "Operation canceled.");
                    }

                    string tableName = _tableNames[i];
                    if (string.IsNullOrWhiteSpace(tableName))
                    {
                        continue;
                    }

                    try
                    {
                        var table = await LocalizationSettings.StringDatabase.GetTableAsync(tableName).Task;
                        if (table == null)
                        {
                            UnityEngine.Debug.LogWarning($"LocalizationPreloadTask: String Table '{tableName}' was not found.");
                            continue;
                        }
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogWarning($"LocalizationPreloadTask: Failed to preload table '{tableName}'. {ex.Message}");
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
