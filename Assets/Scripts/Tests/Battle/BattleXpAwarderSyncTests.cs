using System.Reflection;
using NUnit.Framework;
using SevenBattles.Battle.Progression;
using SevenBattles.Core.Battle;
using SevenBattles.Core.Players;
using SevenBattles.Core.Units;
using UnityEngine;

namespace SevenBattles.Tests.Battle
{
    public class BattleXpAwarderSyncTests
    {
        private const BindingFlags PRIVATE_FLAGS =
            BindingFlags.Instance | BindingFlags.NonPublic;

        [TearDown]
        public void TearDown()
        {
            PlayerContext.SetRuntimeInstance(null);
        }

        [Test]
        public void ResolvePlayerContext_PrefersRuntimeInstance()
        {
            var assetContext = ScriptableObject.CreateInstance<PlayerContext>();
            assetContext.name = "AssetCtx";

            var runtimeContext = ScriptableObject.CreateInstance<PlayerContext>();
            runtimeContext.name = "RuntimeCtx";
            PlayerContext.SetRuntimeInstance(runtimeContext);

            var go = new GameObject("BattleXpAwarderTest");
            var awarder = go.AddComponent<BattleXpAwarder>();

            try
            {
                var field = typeof(BattleXpAwarder).GetField("_playerContext", PRIVATE_FLAGS);
                Assert.IsNotNull(field, "_playerContext field not found via reflection.");
                field.SetValue(awarder, assetContext);

                var method = typeof(BattleXpAwarder).GetMethod("ResolvePlayerContext", PRIVATE_FLAGS);
                Assert.IsNotNull(method, "ResolvePlayerContext method not found via reflection.");
                method.Invoke(awarder, null);

                var resolved = field.GetValue(awarder) as PlayerContext;
                Assert.IsNotNull(resolved, "ResolvePlayerContext did not set _playerContext.");
                Assert.AreEqual(runtimeContext, resolved, "ResolvePlayerContext should prefer RuntimeInstance.");
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(runtimeContext);
                Object.DestroyImmediate(assetContext);
            }
        }

        [Test]
        public void TrySyncPlayerContextFromSession_UsesRuntimeContext_AndLeavesAssetUnchanged()
        {
            var unitDefA = ScriptableObject.CreateInstance<UnitDefinition>();
            unitDefA.Id = "unit.a";
            var unitDefB = ScriptableObject.CreateInstance<UnitDefinition>();
            unitDefB.Id = "unit.b";

            var assetSquad = ScriptableObject.CreateInstance<PlayerSquad>();
            assetSquad.UnitLoadouts = new[]
            {
                new UnitSpellLoadout { Definition = unitDefA, Level = 1, Xp = 0 },
                new UnitSpellLoadout { Definition = unitDefB, Level = 1, Xp = 0 }
            };

            var runtimeSquad = ScriptableObject.CreateInstance<PlayerSquad>();
            runtimeSquad.UnitLoadouts = new[]
            {
                new UnitSpellLoadout { Definition = unitDefA, Level = 1, Xp = 0 },
                new UnitSpellLoadout { Definition = unitDefB, Level = 1, Xp = 0 }
            };

            var assetContext = ScriptableObject.CreateInstance<PlayerContext>();
            assetContext.PlayerSquad = assetSquad;

            var runtimeContext = ScriptableObject.CreateInstance<PlayerContext>();
            runtimeContext.PlayerSquad = runtimeSquad;
            PlayerContext.SetRuntimeInstance(runtimeContext);

            var go = new GameObject("BattleXpAwarderTest");
            var awarder = go.AddComponent<BattleXpAwarder>();

            try
            {
                var ctxField = typeof(BattleXpAwarder).GetField("_playerContext", PRIVATE_FLAGS);
                Assert.IsNotNull(ctxField, "_playerContext field not found.");
                ctxField.SetValue(awarder, assetContext);

                var syncToAssetsField = typeof(BattleXpAwarder).GetField("_syncToPlayerContextAssets", PRIVATE_FLAGS);
                Assert.IsNotNull(syncToAssetsField, "_syncToPlayerContextAssets field not found.");
                syncToAssetsField.SetValue(awarder, false);

                var sessionSquad = new[]
                {
                    new UnitSpellLoadout { Definition = unitDefA, Level = 3, Xp = 15 },
                    new UnitSpellLoadout { Definition = unitDefB, Level = 2, Xp = 8 }
                };

                var method = typeof(BattleXpAwarder).GetMethod("TrySyncPlayerContextFromSession", PRIVATE_FLAGS);
                Assert.IsNotNull(method, "TrySyncPlayerContextFromSession method not found.");
                method.Invoke(awarder, new object[] { sessionSquad });

                var runtimeLoadouts = runtimeSquad.GetLoadouts();
                Assert.AreEqual(3, runtimeLoadouts[0].EffectiveLevel);
                Assert.AreEqual(15, runtimeLoadouts[0].EffectiveXp);
                Assert.AreEqual(2, runtimeLoadouts[1].EffectiveLevel);
                Assert.AreEqual(8, runtimeLoadouts[1].EffectiveXp);

                var assetLoadouts = assetSquad.GetLoadouts();
                Assert.AreEqual(1, assetLoadouts[0].EffectiveLevel, "Authored asset loadouts must remain unchanged.");
                Assert.AreEqual(0, assetLoadouts[0].EffectiveXp, "Authored asset loadouts must remain unchanged.");
                Assert.AreEqual(1, assetLoadouts[1].EffectiveLevel, "Authored asset loadouts must remain unchanged.");
                Assert.AreEqual(0, assetLoadouts[1].EffectiveXp, "Authored asset loadouts must remain unchanged.");
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(runtimeContext);
                Object.DestroyImmediate(assetContext);
                Object.DestroyImmediate(runtimeSquad);
                Object.DestroyImmediate(assetSquad);
                Object.DestroyImmediate(unitDefB);
                Object.DestroyImmediate(unitDefA);
            }
        }

        [Test]
        public void TrySyncPlayerContextFromSession_DoesNotMutateAsset_WhenRuntimeContextMissing()
        {
            var unitDefA = ScriptableObject.CreateInstance<UnitDefinition>();
            unitDefA.Id = "unit.a";
            var unitDefB = ScriptableObject.CreateInstance<UnitDefinition>();
            unitDefB.Id = "unit.b";

            var squad = ScriptableObject.CreateInstance<PlayerSquad>();
            squad.UnitLoadouts = new[]
            {
                new UnitSpellLoadout { Definition = unitDefA, Level = 1, Xp = 0 },
                new UnitSpellLoadout { Definition = unitDefB, Level = 1, Xp = 0 }
            };

            var context = ScriptableObject.CreateInstance<PlayerContext>();
            context.PlayerSquad = squad;
            PlayerContext.SetRuntimeInstance(null);

            var go = new GameObject("BattleXpAwarderTest");
            var awarder = go.AddComponent<BattleXpAwarder>();

            try
            {
                var ctxField = typeof(BattleXpAwarder).GetField("_playerContext", PRIVATE_FLAGS);
                Assert.IsNotNull(ctxField, "_playerContext field not found.");
                ctxField.SetValue(awarder, context);

                var syncToAssetsField = typeof(BattleXpAwarder).GetField("_syncToPlayerContextAssets", PRIVATE_FLAGS);
                Assert.IsNotNull(syncToAssetsField, "_syncToPlayerContextAssets field not found.");
                syncToAssetsField.SetValue(awarder, false);

                var sessionSquad = new[]
                {
                    new UnitSpellLoadout { Definition = unitDefA, Level = 4, Xp = 20 },
                    new UnitSpellLoadout { Definition = unitDefB, Level = 3, Xp = 9 }
                };

                var method = typeof(BattleXpAwarder).GetMethod("TrySyncPlayerContextFromSession", PRIVATE_FLAGS);
                Assert.IsNotNull(method, "TrySyncPlayerContextFromSession method not found.");
                method.Invoke(awarder, new object[] { sessionSquad });

                var loadouts = squad.GetLoadouts();
                Assert.AreEqual(1, loadouts[0].EffectiveLevel, "Asset sync must stay disabled when runtime context is missing.");
                Assert.AreEqual(0, loadouts[0].EffectiveXp, "Asset sync must stay disabled when runtime context is missing.");
                Assert.AreEqual(1, loadouts[1].EffectiveLevel, "Asset sync must stay disabled when runtime context is missing.");
                Assert.AreEqual(0, loadouts[1].EffectiveXp, "Asset sync must stay disabled when runtime context is missing.");
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(context);
                Object.DestroyImmediate(squad);
                Object.DestroyImmediate(unitDefB);
                Object.DestroyImmediate(unitDefA);
            }
        }

        [Test]
        public void SyncPlayerContextFromSession_CopiesLevelAndXp()
        {
            var unitDefA = ScriptableObject.CreateInstance<UnitDefinition>();
            unitDefA.Id = "unit.a";
            var unitDefB = ScriptableObject.CreateInstance<UnitDefinition>();
            unitDefB.Id = "unit.b";

            var squad = ScriptableObject.CreateInstance<PlayerSquad>();
            squad.UnitLoadouts = new[]
            {
                new UnitSpellLoadout { Definition = unitDefA, Level = 1, Xp = 0 },
                new UnitSpellLoadout { Definition = unitDefB, Level = 1, Xp = 0 }
            };

            var context = ScriptableObject.CreateInstance<PlayerContext>();
            context.PlayerSquad = squad;

            var go = new GameObject("BattleXpAwarderTest");
            var awarder = go.AddComponent<BattleXpAwarder>();

            try
            {
                // Inject the player context via reflection.
                var ctxField = typeof(BattleXpAwarder).GetField("_playerContext", PRIVATE_FLAGS);
                Assert.IsNotNull(ctxField, "_playerContext field not found.");
                ctxField.SetValue(awarder, context);

                // Build a session array with higher Level/Xp.
                var sessionSquad = new[]
                {
                    new UnitSpellLoadout { Definition = unitDefA, Level = 3, Xp = 15 },
                    new UnitSpellLoadout { Definition = unitDefB, Level = 2, Xp = 8 }
                };

                var syncMethod = typeof(BattleXpAwarder).GetMethod("SyncPlayerContextFromSession", PRIVATE_FLAGS);
                Assert.IsNotNull(syncMethod, "SyncPlayerContextFromSession method not found.");
                syncMethod.Invoke(awarder, new object[] { sessionSquad });

                var loadouts = squad.GetLoadouts();
                Assert.AreEqual(3, loadouts[0].EffectiveLevel, "Unit A level should be 3 after sync.");
                Assert.AreEqual(15, loadouts[0].EffectiveXp, "Unit A XP should be 15 after sync.");
                Assert.AreEqual(2, loadouts[1].EffectiveLevel, "Unit B level should be 2 after sync.");
                Assert.AreEqual(8, loadouts[1].EffectiveXp, "Unit B XP should be 8 after sync.");
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(context);
                Object.DestroyImmediate(squad);
                Object.DestroyImmediate(unitDefB);
                Object.DestroyImmediate(unitDefA);
            }
        }
    }
}
