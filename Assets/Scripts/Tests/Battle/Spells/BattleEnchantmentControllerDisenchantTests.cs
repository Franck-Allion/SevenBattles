using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using SevenBattles.Battle.Spells;
using SevenBattles.Battle.Units;
using SevenBattles.Core.Battle;
using SevenBattles.Core.Units;

namespace SevenBattles.Tests.Battle
{
    public class BattleEnchantmentControllerDisenchantTests
    {
        [Test]
        public void TryRemoveEnchantment_RemovesBonusesAndEntry()
        {
            var controllerGo = new GameObject("EnchantmentController");
            var controller = controllerGo.AddComponent<BattleEnchantmentController>();

            var quad = new EnchantmentQuadDefinition
            {
                TopLeft = new Vector2(0f, 1f),
                TopRight = new Vector2(1f, 1f),
                BottomRight = new Vector2(1f, 0f),
                BottomLeft = new Vector2(0f, 0f),
                Offset = Vector2.zero,
                Scale = 1f
            };
            SetPrivate(controller, "_quads", new[] { quad });

            var unitDef = ScriptableObject.CreateInstance<UnitDefinition>();
            var unitGo = new GameObject("Unit");
            var stats = unitGo.AddComponent<UnitStats>();
            stats.ApplyBase(new UnitStatsData { Life = 10, ActionPoints = 1, Speed = 1, Initiative = 1 });
            UnitBattleMetadata.Ensure(unitGo, true, unitDef, new Vector2Int(0, 0));

            var enchantSpell = ScriptableObject.CreateInstance<SpellDefinition>();
            enchantSpell.IsEnchantment = true;
            enchantSpell.EnchantmentTargetScope = EnchantmentTargetScope.AllUnits;
            enchantSpell.EnchantmentStatBonus = new EnchantmentStatBonus { Attack = 2 };

            bool placed = controller.TryRestoreEnchantment(
                enchantSpell,
                quadIndex: 0,
                isPlayerControlledCaster: true,
                casterInstanceId: null,
                casterUnitId: null,
                skipVisual: true);

            Assert.IsTrue(placed);
            Assert.AreEqual(2, stats.Attack);
            Assert.IsTrue(controller.HasActiveEnchantments);

            var disenchantSpell = ScriptableObject.CreateInstance<SpellDefinition>();
            bool removed = controller.TryRemoveEnchantment(disenchantSpell, quadIndex: 0, casterMeta: null);

            Assert.IsTrue(removed);
            Assert.AreEqual(0, stats.Attack);
            Assert.IsFalse(controller.HasActiveEnchantments);

            Object.DestroyImmediate(disenchantSpell);
            Object.DestroyImmediate(enchantSpell);
            Object.DestroyImmediate(unitDef);
            Object.DestroyImmediate(unitGo);
            Object.DestroyImmediate(controllerGo);
        }

        private static void SetPrivate(object obj, string field, object value)
        {
            var f = obj.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(f, $"Field '{field}' not found on {obj.GetType().Name}");
            f.SetValue(obj, value);
        }
    }
}
