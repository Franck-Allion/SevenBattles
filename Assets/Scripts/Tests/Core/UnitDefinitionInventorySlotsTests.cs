using NUnit.Framework;
using SevenBattles.Core.Items;
using SevenBattles.Core.Units;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SevenBattles.Tests.Core
{
    public sealed class UnitDefinitionInventorySlotsTests
    {
        [Test]
        public void TryGetConsumableSlotUnlock_DefaultConfig_AllFourSlotsExistAtLevelOne()
        {
            var definition = ScriptableObject.CreateInstance<UnitDefinition>();

            Assert.IsTrue(definition.TryGetConsumableSlotUnlock(ConsumableSlotType.Object1, out bool exists1, out int level1));
            Assert.IsTrue(exists1);
            Assert.AreEqual(1, level1);

            Assert.IsTrue(definition.TryGetConsumableSlotUnlock(ConsumableSlotType.Object4, out bool exists4, out int level4));
            Assert.IsTrue(exists4);
            Assert.AreEqual(1, level4);

            Object.DestroyImmediate(definition);
        }

        [Test]
        public void TryGetConsumableSlotUnlock_RespectsSlotCount_AndUnlockThresholds()
        {
            var definition = ScriptableObject.CreateInstance<UnitDefinition>();
            definition.InventoryConsumableSlotCount = 2;
            definition.InventoryConsumableSlotUnlockLevels = new[] { 1, 5, 10, 20 };

            Assert.IsTrue(definition.TryGetConsumableSlotUnlock(ConsumableSlotType.Object1, out bool exists1, out int level1));
            Assert.IsTrue(exists1);
            Assert.AreEqual(1, level1);

            Assert.IsTrue(definition.TryGetConsumableSlotUnlock(ConsumableSlotType.Object2, out bool exists2, out int level2));
            Assert.IsTrue(exists2);
            Assert.AreEqual(5, level2);

            Assert.IsTrue(definition.TryGetConsumableSlotUnlock(ConsumableSlotType.Object3, out bool exists3, out int level3));
            Assert.IsFalse(exists3);
            Assert.AreEqual(int.MaxValue, level3);

            Object.DestroyImmediate(definition);
        }

        [Test]
        public void TryGetConsumableSlotUnlock_InvalidOrMissingThresholds_FallbackToLevelOne()
        {
            var definition = ScriptableObject.CreateInstance<UnitDefinition>();
            definition.InventoryConsumableSlotCount = 4;
            definition.InventoryConsumableSlotUnlockLevels = new[] { 0, -10 };

            Assert.IsTrue(definition.TryGetConsumableSlotUnlock(ConsumableSlotType.Object1, out bool exists1, out int level1));
            Assert.IsTrue(exists1);
            Assert.AreEqual(1, level1);

            Assert.IsTrue(definition.TryGetConsumableSlotUnlock(ConsumableSlotType.Object2, out bool exists2, out int level2));
            Assert.IsTrue(exists2);
            Assert.AreEqual(1, level2);

            Assert.IsTrue(definition.TryGetConsumableSlotUnlock(ConsumableSlotType.Object4, out bool exists4, out int level4));
            Assert.IsTrue(exists4);
            Assert.AreEqual(1, level4);

            Object.DestroyImmediate(definition);
        }
    }
}
