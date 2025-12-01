using SevenBattles.Core.Battle;

namespace SevenBattles.Core
{
    /// <summary>
    /// Service interface for accessing and managing the current battle session configuration.
    /// Controllers in the Battle domain should depend on this interface rather than
    /// holding direct references to ScriptableObject squad data.
    /// </summary>
    public interface IBattleSessionService
    {
        /// <summary>
        /// Gets the current battle session configuration.
        /// Returns null if no session has been initialized.
        /// </summary>
        BattleSessionConfig CurrentSession { get; }

        /// <summary>
        /// Initializes a new battle session with the given configuration.
        /// This should be called before any battle controllers attempt to spawn units.
        /// </summary>
        /// <param name="config">The battle configuration to use.</param>
        void InitializeSession(BattleSessionConfig config);

        /// <summary>
        /// Clears the current battle session.
        /// Typically called when exiting the battle scene.
        /// </summary>
        void ClearSession();
    }
}
