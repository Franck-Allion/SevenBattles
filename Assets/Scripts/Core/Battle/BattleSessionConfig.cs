using System;
using System.Collections.Generic;

namespace SevenBattles.Core.Battle
{
    /// <summary>
    /// Holds all configuration needed to start a battle.
    /// Injected into BattleScene by the system that initiates the battle
    /// (e.g., campaign, menu, or load system).
    /// This is the single source of truth for battle initialization data.
    /// </summary>
    [Serializable]
    public sealed class BattleSessionConfig
    {
        /// <summary>
        /// Player squad composition (1-8 unit loadouts).
        /// </summary>
        public UnitSpellLoadout[] PlayerSquad;

        /// <summary>
        /// Enemy squad composition (1-8 unit loadouts).
        /// </summary>
        public UnitSpellLoadout[] EnemySquad;

        /// <summary>
        /// Battle type identifier (e.g., "campaign", "arena", "tutorial").
        /// Used for context-specific logic or analytics.
        /// </summary>
        public string BattleType;

        /// <summary>
        /// Difficulty level (0 = easy, 1 = normal, 2 = hard, etc.).
        /// Can be used to scale enemy stats or AI behavior.
        /// </summary>
        public int Difficulty;

        /// <summary>
        /// Optional campaign mission identifier.
        /// Null if not a campaign battle.
        /// </summary>
        public string CampaignMissionId;

        /// <summary>
        /// Optional custom metadata for extensibility.
        /// Can store additional context (e.g., weather, terrain modifiers).
        /// </summary>
        public Dictionary<string, object> CustomData;

        /// <summary>
        /// Creates a default battle session config with empty squads.
        /// </summary>
        public BattleSessionConfig()
        {
            PlayerSquad = Array.Empty<UnitSpellLoadout>();
            EnemySquad = Array.Empty<UnitSpellLoadout>();
            BattleType = "unknown";
            Difficulty = 0;
            CampaignMissionId = null;
            CustomData = new Dictionary<string, object>();
        }

        /// <summary>
        /// Creates a battle session config with specified squads.
        /// </summary>
        public BattleSessionConfig(UnitSpellLoadout[] playerSquad, UnitSpellLoadout[] enemySquad, string battleType = "unknown", int difficulty = 0)
        {
            PlayerSquad = playerSquad ?? Array.Empty<UnitSpellLoadout>();
            EnemySquad = enemySquad ?? Array.Empty<UnitSpellLoadout>();
            BattleType = battleType;
            Difficulty = difficulty;
            CampaignMissionId = null;
            CustomData = new Dictionary<string, object>();
        }
    }
}
