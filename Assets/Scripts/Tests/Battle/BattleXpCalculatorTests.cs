using System;
using System.Reflection;
using NUnit.Framework;
using SevenBattles.Battle.Progression;
using UnityEngine;
using SevenBattles.Core;
using SevenBattles.Core.Battle;
using SevenBattles.Core.Players;
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

    public class BattleXpAwarderSyncTests
    {
        [Test]
        public void TrySyncPlayerContextFromSession_UpdatesPlayerSquadProgression()
        {
            var go = new GameObject("BattleXpAwarder");
            var awarder = go.AddComponent<BattleXpAwarder>();

            var unitDef = ScriptableObject.CreateInstance<UnitDefinition>();
            unitDef.Id = "unit.alpha";

            var playerSquad = ScriptableObject.CreateInstance<PlayerSquad>();
            playerSquad.UnitLoadouts = new[]
            {
                new UnitSpellLoadout
                {
                    Definition = unitDef,
                    Level = 1,
                    Xp = 0,
                    Spells = Array.Empty<SpellDefinition>()
                }
            };

            var playerContext = ScriptableObject.CreateInstance<PlayerContext>();
            playerContext.PlayerSquad = playerSquad;

            var sessionSquad = new[]
            {
                new UnitSpellLoadout
                {
                    Definition = unitDef,
                    Level = 4,
                    Xp = 23,
                    Spells = Array.Empty<SpellDefinition>()
                }
            };

            SetPrivateField(awarder, "_playerContext", playerContext);
            SetPrivateField(awarder, "_syncToPlayerContextAssets", true);
            InvokePrivate(awarder, "TrySyncPlayerContextFromSession", sessionSquad);

            var updated = playerContext.PlayerSquad.GetLoadouts();
            Assert.IsNotNull(updated);
            Assert.AreEqual(1, updated.Length);
            Assert.AreEqual(4, updated[0].EffectiveLevel);
            Assert.AreEqual(23, updated[0].EffectiveXp);

            UnityEngine.Object.DestroyImmediate(playerContext);
            UnityEngine.Object.DestroyImmediate(playerSquad);
            UnityEngine.Object.DestroyImmediate(unitDef);
            UnityEngine.Object.DestroyImmediate(go);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found.");
            field.SetValue(target, value);
        }

        private static void InvokePrivate(object target, string methodName, params object[] args)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Method '{methodName}' not found.");
            method.Invoke(target, args);
        }
    }
}
