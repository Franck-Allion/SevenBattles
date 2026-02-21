using NUnit.Framework;
using UnityEngine;
using SevenBattles.Core.Players;
using SevenBattles.Core.Save;
using SevenBattles.Core.Units;

namespace SevenBattles.Tests.Core
{
    public class PlayerResourcesLoadHandlerTests
    {
        private static void SetPrivate(object target, string fieldName, object value)
        {
            var type = target.GetType();
            var field = type.GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' was not found on type '{type.FullName}'.");
            field.SetValue(target, value);
        }

        [Test]
        public void ApplyLoadedGame_RestoresPlayerResources()
        {
            var context = ScriptableObject.CreateInstance<PlayerContext>();
            context.SetResources(10, 2);

            var go = new GameObject("PlayerResourcesLoadHandler");
            var handler = go.AddComponent<PlayerResourcesLoadHandler>();
            SetPrivate(handler, "_playerContext", context);

            var data = new SaveGameData
            {
                PlayerResources = new PlayerResourcesSaveData
                {
                    Gold = 123,
                    Gems = 7
                }
            };

            handler.ApplyLoadedGame(data);

            Assert.AreEqual(123, context.Gold);
            Assert.AreEqual(7, context.Gems);

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(context);
        }

        [Test]
        public void ApplyLoadedGame_ClampsNegativeValues_ToZero()
        {
            var context = ScriptableObject.CreateInstance<PlayerContext>();
            context.SetResources(10, 2);

            var go = new GameObject("PlayerResourcesLoadHandler");
            var handler = go.AddComponent<PlayerResourcesLoadHandler>();
            SetPrivate(handler, "_playerContext", context);

            var data = new SaveGameData
            {
                PlayerResources = new PlayerResourcesSaveData
                {
                    Gold = -99,
                    Gems = -3
                }
            };

            handler.ApplyLoadedGame(data);

            Assert.AreEqual(0, context.Gold);
            Assert.AreEqual(0, context.Gems);

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(context);
        }

        [Test]
        public void ApplyLoadedGame_RestoresTournamentProgress()
        {
            var context = ScriptableObject.CreateInstance<PlayerContext>();
            context.SetTournamentProgress(1, new[] { false, false, false, false, false, false, false });

            var go = new GameObject("PlayerResourcesLoadHandler");
            var handler = go.AddComponent<PlayerResourcesLoadHandler>();
            SetPrivate(handler, "_playerContext", context);

            var data = new SaveGameData
            {
                TournamentProgress = new TournamentProgressSaveData
                {
                    CurrentRoundIndex = 4,
                    CompletedBattles = new[] { true, true, true, false, false, false, false }
                }
            };

            handler.ApplyLoadedGame(data);

            Assert.AreEqual(4, context.CurrentTournamentRoundIndex);
            Assert.IsTrue(context.IsTournamentBattleCompleted(1));
            Assert.IsTrue(context.IsTournamentBattleCompleted(2));
            Assert.IsTrue(context.IsTournamentBattleCompleted(3));
            Assert.IsFalse(context.IsTournamentBattleCompleted(4));

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(context);
        }

        [Test]
        public void ApplyLoadedGame_MissingOwnedUnitCustomName_GeneratesSafeDefault()
        {
            var context = ScriptableObject.CreateInstance<PlayerContext>();
            var definition = ScriptableObject.CreateInstance<UnitDefinition>();
            definition.Id = "wizard_a";
            definition.name = "Wizard";

            var registry = ScriptableObject.CreateInstance<UnitDefinitionRegistry>();
            SetPrivate(registry, "_definitions", new[] { definition });

            var go = new GameObject("PlayerResourcesLoadHandler");
            var handler = go.AddComponent<PlayerResourcesLoadHandler>();
            SetPrivate(handler, "_playerContext", context);
            SetPrivate(handler, "_unitRegistry", registry);

            var data = new SaveGameData
            {
                PlayerOwnedUnits = new PlayerOwnedUnitsSaveData
                {
                    Units = new[]
                    {
                        new OwnedUnitSaveData
                        {
                            OwnedUnitId = "owned_1",
                            CustomName = "",
                            UnitId = "wizard_a",
                            Level = 1,
                            Xp = 0,
                            SpellIds = new string[0]
                        }
                    },
                    ActiveSquadOwnedUnitIds = new[] { "owned_1" }
                }
            };

            handler.ApplyLoadedGame(data);

            Assert.AreEqual(1, context.OwnedUnits.Count);
            Assert.AreEqual("Wizard-1", context.OwnedUnits[0].CustomName);
            Assert.AreEqual(1, context.ActiveSquadOwnedUnitIds.Count);
            Assert.AreEqual("owned_1", context.ActiveSquadOwnedUnitIds[0]);

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(registry);
            Object.DestroyImmediate(definition);
            Object.DestroyImmediate(context);
        }

        [Test]
        public void OwnedUnitNamingPolicy_NormalizeAllInPlace_AssignsDefaultsAndSanitizes()
        {
            var definition = ScriptableObject.CreateInstance<UnitDefinition>();
            definition.Id = "wizard_policy";
            definition.name = "Wizard";

            var units = new[]
            {
                new OwnedUnitData { OwnedUnitId = "u1", Definition = definition, CustomName = "  Keeper  " },
                new OwnedUnitData { OwnedUnitId = "u2", Definition = definition, CustomName = "" },
                new OwnedUnitData { OwnedUnitId = "u3", Definition = definition, CustomName = null }
            };

            OwnedUnitNamingPolicy.NormalizeAllInPlace(units);

            Assert.AreEqual("Keeper", units[0].CustomName);
            Assert.AreEqual("Wizard-1", units[1].CustomName);
            Assert.AreEqual("Wizard-2", units[2].CustomName);

            Object.DestroyImmediate(definition);
        }

        [Test]
        public void PlayerInventoryService_TryRenameOwnedUnit_UpdatesNameAndRaisesEvent()
        {
            var context = ScriptableObject.CreateInstance<PlayerContext>();
            var definition = ScriptableObject.CreateInstance<UnitDefinition>();
            definition.Id = "wizard_inventory";
            definition.name = "Wizard";

            context.SetOwnedUnits(new[]
            {
                new OwnedUnitData
                {
                    OwnedUnitId = "owned_1",
                    Definition = definition,
                    CustomName = "Wizard-1"
                }
            });

            var service = new PlayerInventoryService(context, null);
            int changedCount = 0;
            string changedName = null;
            service.OwnedUnitChanged += owned =>
            {
                changedCount++;
                changedName = owned != null ? owned.CustomName : null;
            };

            bool renamed = service.TryRenameOwnedUnit("owned_1", "  Merlin  ", out string appliedName);

            Assert.IsTrue(renamed);
            Assert.AreEqual("Merlin", appliedName);
            Assert.AreEqual("Merlin", context.OwnedUnits[0].CustomName);
            Assert.AreEqual(1, changedCount);
            Assert.AreEqual("Merlin", changedName);

            Object.DestroyImmediate(definition);
            Object.DestroyImmediate(context);
        }

        [Test]
        public void PlayerInventoryService_TryRenameOwnedUnit_EmptyInput_UsesDefault()
        {
            var context = ScriptableObject.CreateInstance<PlayerContext>();
            var definition = ScriptableObject.CreateInstance<UnitDefinition>();
            definition.Id = "wizard_inventory_default";
            definition.name = "Wizard";

            context.SetOwnedUnits(new[]
            {
                new OwnedUnitData
                {
                    OwnedUnitId = "owned_1",
                    Definition = definition,
                    CustomName = "Wizard-1"
                },
                new OwnedUnitData
                {
                    OwnedUnitId = "owned_2",
                    Definition = definition,
                    CustomName = "AnyName"
                }
            });

            var service = new PlayerInventoryService(context, null);
            bool renamed = service.TryRenameOwnedUnit("owned_2", "   ", out string appliedName);

            Assert.IsTrue(renamed);
            Assert.AreEqual("Wizard-2", appliedName);
            Assert.AreEqual("Wizard-2", context.OwnedUnits[1].CustomName);

            Object.DestroyImmediate(definition);
            Object.DestroyImmediate(context);
        }
    }
}
