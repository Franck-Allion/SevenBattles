using NUnit.Framework;
using UnityEngine;
using SevenBattles.Core;
using SevenBattles.Core.Battle;
using SevenBattles.Core.Units;

namespace SevenBattles.Tests.Battle
{
    public class BattleXpCalculatorTests
    {
        [Test]
        public void CalculateTotalXp_UsesThreatAndRelativeLevels()
        {
            var tuning = ScriptableObject.CreateInstance<BattleXpTuning>();
            tuning.BaseXpPerEnemy = 10f;
            tuning.EnableTurnFactor = false;

            var playerDef = ScriptableObject.CreateInstance<UnitDefinition>();
            playerDef.Id = "Player";

            var enemyDef = ScriptableObject.CreateInstance<UnitDefinition>();
            enemyDef.Id = "Enemy";
            enemyDef.ThreatFactor = 2f;

            var session = new BattleSessionConfig
            {
                Difficulty = 0,
                PlayerSquad = new[]
                {
                    new UnitSpellLoadout { Definition = playerDef, Level = 3 },
                    new UnitSpellLoadout { Definition = playerDef, Level = 3 }
                },
                EnemySquad = new[]
                {
                    new UnitSpellLoadout { Definition = enemyDef, Level = 5 }
                }
            };

            int xp = BattleXpCalculator.CalculateTotalXp(
                tuning,
                session,
                BattleOutcome.PlayerVictory,
                alivePlayerUnits: 2,
                totalPlayerUnits: 2,
                actualTurns: 5);

            Assert.AreEqual(25, xp, "Expected round(10*2*(1+0.12*(5-3))) = round(24.8) = 25.");
        }
    }
}
