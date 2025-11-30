using NUnit.Framework;
using UnityEngine;
using SevenBattles.Battle.Turn;
using SevenBattles.Battle.Units;
using SevenBattles.Core.Units;

namespace SevenBattles.Tests.Battle
{
    public class TurnOrderControllerTests
    {
        [Test]
        public void SkipsDestroyedUnits_WithoutInfiniteLoop()
        {
            var aGo = new GameObject("WizardA");
            var bGo = new GameObject("WizardB");

            var aStats = aGo.AddComponent<UnitStats>();
            var bStats = bGo.AddComponent<UnitStats>();

            aStats.ApplyBase(new UnitStatsData { Initiative = 5 });
            bStats.ApplyBase(new UnitStatsData { Initiative = 10 });

            var def = ScriptableObject.CreateInstance<UnitDefinition>();

            UnitBattleMetadata.Ensure(aGo, true, def, new Vector2Int(0, 0));
            UnitBattleMetadata.Ensure(bGo, false, def, new Vector2Int(1, 0));

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

        [Test]
        public void ActiveUnitActionPoints_AreInitializedFromStats()
        {
            var go = new GameObject("Wizard");
            var stats = go.AddComponent<UnitStats>();
            var data = new UnitStatsData { Life = 30, Attack = 4, ActionPoints = 4, Initiative = 5 };
            stats.ApplyBase(data);

            var def = ScriptableObject.CreateInstance<UnitDefinition>();
            def.Portrait = null;

            UnitBattleMetadata.Ensure(go, true, def, new Vector2Int(0, 0));

            var ctrlGo = new GameObject("TurnController");
            var ctrl = ctrlGo.AddComponent<SimpleTurnOrderController>();

            CallPrivate(ctrl, "BeginBattle");

            Assert.IsTrue(ctrl.HasActiveUnit, "Controller should have an active unit after BeginBattle.");
            Assert.AreEqual(4, ctrl.ActiveUnitMaxActionPoints, "Max AP should be initialized from unit stats ActionPoints.");
            Assert.AreEqual(4, ctrl.ActiveUnitCurrentActionPoints, "Current AP should start equal to max AP at turn start.");

            Object.DestroyImmediate(ctrlGo);
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(def);
        }

        private static void CallPrivate(object obj, string method)
        {
            var mi = obj.GetType().GetMethod(
                method,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Public);
            mi.Invoke(obj, null);
        }

        private static void SetPrivate(object obj, string field, object value)
        {
            var fi = obj.GetType().GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            fi.SetValue(obj, value);
        }
    }
}
