using NUnit.Framework;
using UnityEngine;
using SevenBattles.Battle.Turn;
using SevenBattles.Battle.Wizards;
using SevenBattles.Core.Wizards;

namespace SevenBattles.Tests.Battle
{
    public class TurnOrderControllerTests
    {
        [Test]
        public void OrdersUnitsByInitiative_AndLoopsTurns()
        {
            // Arrange: two wizards with different initiative.
            var aGo = new GameObject("WizardA");
            var bGo = new GameObject("WizardB");

            var aStats = aGo.AddComponent<WizardStats>();
            var bStats = bGo.AddComponent<WizardStats>();

            var aData = new WizardStatsData { Initiative = 5 };
            var bData = new WizardStatsData { Initiative = 10 };
            aStats.ApplyBase(aData);
            bStats.ApplyBase(bData);

            var def = ScriptableObject.CreateInstance<WizardDefinition>();
            def.Portrait = null;

            var aMeta = WizardBattleMetadata.Ensure(aGo, true, def, new Vector2Int(0, 0));
            var bMeta = WizardBattleMetadata.Ensure(bGo, false, def, new Vector2Int(1, 0));
            Assert.IsNotNull(aMeta);
            Assert.IsNotNull(bMeta);

            var ctrlGo = new GameObject("TurnController");
            var ctrl = ctrlGo.AddComponent<SimpleTurnOrderController>();

            // Act: build initiative list and start.
            CallPrivate(ctrl, "BeginBattle");

            // Assert: highest initiative (B) should act first and be AI.
            Assert.IsTrue(ctrl.HasActiveUnit);
            Assert.IsFalse(ctrl.IsActiveUnitPlayerControlled, "First active unit should be AI (higher initiative).");

            // Simulate AI timeout by directly advancing to next unit.
            CallPrivate(ctrl, "AdvanceToNextUnit");

            // Now A should be active (player-controlled).
            Assert.IsTrue(ctrl.HasActiveUnit);
            Assert.IsTrue(ctrl.IsActiveUnitPlayerControlled, "Second active unit should be player-controlled.");

            // Next advance loops back to B.
            CallPrivate(ctrl, "AdvanceToNextUnit");
            Assert.IsTrue(ctrl.HasActiveUnit);
            Assert.IsFalse(ctrl.IsActiveUnitPlayerControlled, "Turn order should loop back to AI unit.");

            Object.DestroyImmediate(ctrlGo);
            Object.DestroyImmediate(aGo);
            Object.DestroyImmediate(bGo);
            Object.DestroyImmediate(def);
        }

        [Test]
        public void SkipsDestroyedUnits_WithoutInfiniteLoop()
        {
            var aGo = new GameObject("WizardA");
            var bGo = new GameObject("WizardB");

            var aStats = aGo.AddComponent<WizardStats>();
            var bStats = bGo.AddComponent<WizardStats>();

            aStats.ApplyBase(new WizardStatsData { Initiative = 5 });
            bStats.ApplyBase(new WizardStatsData { Initiative = 10 });

            var def = ScriptableObject.CreateInstance<WizardDefinition>();

            WizardBattleMetadata.Ensure(aGo, true, def, new Vector2Int(0, 0));
            WizardBattleMetadata.Ensure(bGo, false, def, new Vector2Int(1, 0));

            var ctrlGo = new GameObject("TurnController");
            var ctrl = ctrlGo.AddComponent<SimpleTurnOrderController>();

            CallPrivate(ctrl, "BeginBattle");
            Assert.IsTrue(ctrl.HasActiveUnit);

            // Destroy both units to simulate end of combat.
            Object.DestroyImmediate(aGo);
            Object.DestroyImmediate(bGo);

            // Multiple advances should not loop infinitely or throw even if all units are gone.
            Assert.DoesNotThrow(() =>
            {
                for (int i = 0; i < 10; i++)
                {
                    CallPrivate(ctrl, "AdvanceToNextUnit");
                }
            }, "Advancing turns with all units destroyed must not cause infinite loops or exceptions.");

            Object.DestroyImmediate(ctrlGo);
            Object.DestroyImmediate(def);
        }

        private static void CallPrivate(object obj, string method)
        {
            var mi = obj.GetType().GetMethod(method, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            mi.Invoke(obj, null);
        }
    }
}
