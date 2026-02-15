namespace SevenBattles.Core.Preload
{
    /// <summary>
    /// Uniform contract for an asynchronous preload task.
    /// Implementations must return a failed <see cref="PreloadResult"/> instead of throwing.
    /// </summary>
    public interface IPreloadTask
    {
        /// <summary>
        /// Human-readable task identifier (for example: "ShaderWarmup").
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets whether the task has completed.
        /// </summary>
        bool IsCompleted { get; }

        /// <summary>
        /// Executes the preload task asynchronously.
        /// Must not propagate uncaught exceptions.
        /// </summary>
        System.Threading.Tasks.Task<PreloadResult> ExecuteAsync(System.Threading.CancellationToken ct);
    }
}
