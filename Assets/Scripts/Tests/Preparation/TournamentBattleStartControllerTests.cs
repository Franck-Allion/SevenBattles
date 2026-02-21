using System;
using System.Collections.Generic;
using NUnit.Framework;
using SevenBattles.Core.Players;
using SevenBattles.Core.Units;
using SevenBattles.Preparation;
using UnityEngine;

namespace SevenBattles.Tests.Preparation
{
    public class TournamentBattleStartControllerTests
    {
        private static bool InvokeHasAtLeastOneActiveSquadUnit(PlayerContext context)
        {
            var method = typeof(TournamentBattleStartController).GetMethod(
                "HasAtLeastOneActiveSquadUnit",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.IsNotNull(method, "Expected private helper method to exist.");
            return (bool)method.Invoke(null, new object[] { context });
        }

        [Test]
        public void HasAtLeastOneActiveSquadUnit_ReturnsFalse_WhenContextIsNull()
        {
            Assert.IsFalse(InvokeHasAtLeastOneActiveSquadUnit(null));
        }

        [Test]
        public void HasAtLeastOneActiveSquadUnit_ReturnsFalse_WhenActiveSquadIsEmpty()
        {
            var context = ScriptableObject.CreateInstance<PlayerContext>();
            context.SetOwnedUnits(Array.Empty<OwnedUnitData>());
            context.SetActiveSquadOwnedUnitIds(Array.Empty<string>());

            Assert.IsFalse(InvokeHasAtLeastOneActiveSquadUnit(context));

            UnityEngine.Object.DestroyImmediate(context);
        }

        [Test]
        public void HasAtLeastOneActiveSquadUnit_ReturnsTrue_WhenActiveSquadContainsOwnedUnit()
        {
            var context = ScriptableObject.CreateInstance<PlayerContext>();
            var unitDefinition = ScriptableObject.CreateInstance<UnitDefinition>();
            var ownedUnit = new OwnedUnitData
            {
                OwnedUnitId = "owned_unit_01",
                Definition = unitDefinition
            };

            context.SetOwnedUnits(new List<OwnedUnitData> { ownedUnit });
            context.SetActiveSquadOwnedUnitIds(new[] { "owned_unit_01" });

            Assert.IsTrue(InvokeHasAtLeastOneActiveSquadUnit(context));

            UnityEngine.Object.DestroyImmediate(unitDefinition);
            UnityEngine.Object.DestroyImmediate(context);
        }
    }
}
