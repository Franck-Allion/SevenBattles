using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using SevenBattles.Battle;
using SevenBattles.Battle.Save;
using SevenBattles.Core.Battle;
using SevenBattles.Core.Save;
using SevenBattles.Core.Units;

namespace SevenBattles.Tests.Battle.Save
{
    public class BattleSessionLoadHandlerTests
    {
        [Test]
        public void ApplyLoadedGame_ClampsNegativeXp_ToZero()
        {
            var unitDef = ScriptableObject.CreateInstance<UnitDefinition>();
            unitDef.Id = "UnitA";

            var registry = ScriptableObject.CreateInstance<UnitDefinitionRegistry>();
            typeof(UnitDefinitionRegistry)
                .GetField("_definitions", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(registry, new[] { unitDef });

            var go = new GameObject("BattleSessionLoadHandlerTests");
            try
            {
                var service = go.AddComponent<BattleSessionService>();
                var handler = go.AddComponent<BattleSessionLoadHandler>();

                typeof(BattleSessionLoadHandler)
                    .GetField("_sessionServiceBehaviour", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.SetValue(handler, service);

                typeof(BattleSessionLoadHandler)
                    .GetField("_unitRegistry", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.SetValue(handler, registry);

                var data = new SaveGameData
                {
                    BattleSession = new BattleSessionSaveData
                    {
                        PlayerSquadUnits = new[]
                        {
                            new UnitSpellLoadoutSaveData
                            {
                                UnitId = "UnitA",
                                SpellIds = Array.Empty<string>(),
                                Level = 2,
                                Xp = -10
                            }
                        },
                        EnemySquadUnits = Array.Empty<UnitSpellLoadoutSaveData>(),
                        PlayerSquadIds = new[] { "UnitA" },
                        EnemySquadIds = Array.Empty<string>(),
                        BattleType = "test",
                        Difficulty = 0
                    }
                };

                handler.ApplyLoadedGame(data);

                Assert.IsNotNull(service.CurrentSession);
                Assert.AreEqual(1, service.CurrentSession.PlayerSquad.Length);
                Assert.AreEqual(2, service.CurrentSession.PlayerSquad[0].Level);
                Assert.AreEqual(0, service.CurrentSession.PlayerSquad[0].Xp);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}

