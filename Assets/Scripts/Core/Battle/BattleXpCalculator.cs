using System;
using UnityEngine;

namespace SevenBattles.Core.Battle
{
    public static class BattleXpCalculator
    {
        private const double LevelDeltaSlope = 0.12;
        private const double MinLevelFactor = 0.6;
        private const double MaxLevelFactor = 1.8;

        private const double VictoryWinFactor = 1.0;
        private const double VictoryLossFactor = 0.35;

        private const double TurnFactorMin = 0.8;
        private const double TurnFactorMax = 1.2;

        public static int CalculateTotalXp(
            BattleXpTuning tuning,
            BattleSessionConfig session,
            BattleOutcome outcome,
            int alivePlayerUnits,
            int totalPlayerUnits,
            int actualTurns)
        {
            if (tuning == null || session == null)
            {
                return 0;
            }

            int difficulty = session.Difficulty;
            double baseXpPerEnemy = tuning.GetBaseXpPerEnemy(difficulty);
            if (baseXpPerEnemy <= 0)
            {
                return 0;
            }

            var playerSquad = session.PlayerSquad ?? Array.Empty<UnitSpellLoadout>();
            var enemySquad = session.EnemySquad ?? Array.Empty<UnitSpellLoadout>();

            double playerAvgLevel = ComputeAverageLevel(playerSquad);
            double encounterXp = ComputeEncounterXp(baseXpPerEnemy, playerAvgLevel, enemySquad);

            double victoryFactor = outcome == BattleOutcome.PlayerVictory
                ? VictoryWinFactor
                : (outcome == BattleOutcome.PlayerDefeat ? VictoryLossFactor : 0.0);

            double survivalFactor = ComputeSurvivalFactor(alivePlayerUnits, totalPlayerUnits);
            double turnFactor = ComputeTurnFactor(tuning, difficulty, actualTurns);

            double total = encounterXp * victoryFactor * survivalFactor * turnFactor;
            if (double.IsNaN(total) || double.IsInfinity(total) || total <= 0)
            {
                return 0;
            }

            if (total > int.MaxValue)
            {
                return int.MaxValue;
            }

            return (int)System.Math.Round(total, MidpointRounding.AwayFromZero);
        }

        public static double ComputeAverageLevel(UnitSpellLoadout[] playerSquad)
        {
            if (playerSquad == null || playerSquad.Length == 0)
            {
                return UnitSpellLoadout.DefaultLevel;
            }

            double sum = 0;
            int count = 0;
            for (int i = 0; i < playerSquad.Length; i++)
            {
                var loadout = playerSquad[i];
                if (loadout == null)
                {
                    continue;
                }

                sum += Mathf.Max(UnitSpellLoadout.DefaultLevel, loadout.EffectiveLevel);
                count++;
            }

            if (count <= 0)
            {
                return UnitSpellLoadout.DefaultLevel;
            }

            return sum / count;
        }

        public static double ComputeEncounterXp(double baseXpPerEnemy, double playerAvgLevel, UnitSpellLoadout[] enemySquad)
        {
            if (enemySquad == null || enemySquad.Length == 0 || baseXpPerEnemy <= 0)
            {
                return 0;
            }

            double sum = 0;
            for (int i = 0; i < enemySquad.Length; i++)
            {
                var enemy = enemySquad[i];
                if (enemy == null || enemy.Definition == null)
                {
                    continue;
                }

                int enemyLevel = Mathf.Max(UnitSpellLoadout.DefaultLevel, enemy.EffectiveLevel);
                double levelDelta = enemyLevel - playerAvgLevel;
                double levelFactor = Clamp(1.0 + (LevelDeltaSlope * levelDelta), MinLevelFactor, MaxLevelFactor);

                double threat = UnitXpProgressionUtil.GetThreatFactor(enemy.Definition);
                double enemyXp = baseXpPerEnemy * threat * levelFactor;

                if (!double.IsNaN(enemyXp) && !double.IsInfinity(enemyXp) && enemyXp > 0)
                {
                    sum += enemyXp;
                }
            }

            return sum;
        }

        private static double ComputeSurvivalFactor(int alivePlayerUnits, int totalPlayerUnits)
        {
            if (totalPlayerUnits <= 0)
            {
                return 1.0;
            }

            int alive = Mathf.Clamp(alivePlayerUnits, 0, totalPlayerUnits);
            double ratio = (double)alive / totalPlayerUnits;
            return 0.85 + (0.15 * ratio);
        }

        private static double ComputeTurnFactor(BattleXpTuning tuning, int difficulty, int actualTurns)
        {
            if (tuning == null || !tuning.EnableTurnFactor)
            {
                return 1.0;
            }

            int targetTurns = tuning.GetTargetTurns(difficulty);
            int turns = Mathf.Max(1, actualTurns);
            if (targetTurns <= 0)
            {
                return 1.0;
            }

            return Clamp((double)targetTurns / turns, TurnFactorMin, TurnFactorMax);
        }

        private static double Clamp(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
