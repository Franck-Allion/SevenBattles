using System;
using UnityEngine;
using SevenBattles.Core;
using SevenBattles.Core.Battle;

namespace SevenBattles.Battle
{
    /// <summary>
    /// MonoBehaviour implementation of IBattleSessionService.
    /// Holds the current battle session configuration and provides access to it
    /// for all battle controllers and systems.
    /// Should be attached to the _System GameObject in BattleScene.
    /// </summary>
    public class BattleSessionService : MonoBehaviour, IBattleSessionService
    {
        private BattleSessionConfig _currentSession;

        /// <summary>
        /// Gets the current battle session configuration.
        /// Returns null if no session has been initialized.
        /// </summary>
        public BattleSessionConfig CurrentSession => _currentSession;

        /// <summary>
        /// Initializes a new battle session with the given configuration.
        /// This should be called before any battle controllers attempt to spawn units.
        /// </summary>
        /// <param name="config">The battle configuration to use.</param>
        /// <exception cref="ArgumentNullException">Thrown if config is null.</exception>
        public void InitializeSession(BattleSessionConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config), "BattleSessionService: Cannot initialize with null config.");
            }

            _currentSession = config;
            Debug.Log($"BattleSessionService: Session initialized. BattleType={config.BattleType}, " +
                      $"PlayerSquad={config.PlayerSquad?.Length ?? 0}, EnemySquad={config.EnemySquad?.Length ?? 0}");
        }

        /// <summary>
        /// Clears the current battle session.
        /// Typically called when exiting the battle scene.
        /// </summary>
        public void ClearSession()
        {
            _currentSession = null;
            Debug.Log("BattleSessionService: Session cleared.");
        }

        private void OnDestroy()
        {
            // Auto-clear session when service is destroyed
            ClearSession();
        }
    }
}
