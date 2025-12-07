using NUnit.Framework;
using UnityEngine;
using SevenBattles.Battle;

namespace SevenBattles.Tests.Battle
{
    public class BattleVisualFeedbackServiceTests
    {
        [Test]
        public void ShowDamageNumber_WithNullPrefab_DoesNotCrash()
        {
            // Arrange
            var go = new GameObject("TestService");
            var service = go.AddComponent<BattleVisualFeedbackService>();
            // _damageNumberPrefab is null by default

            // Act & Assert - should not throw
            Assert.DoesNotThrow(() => service.ShowDamageNumber(Vector3.zero, 10));

            Object.DestroyImmediate(go);
        }

        [Test]
        public void ShowDamageNumber_WithNegativeDamage_LogsWarning()
        {
            // Arrange
            var go = new GameObject("TestService");
            var service = go.AddComponent<BattleVisualFeedbackService>();
            var prefab = new GameObject("DamageNumberPrefab");

            // Use reflection to set the private field for testing
            var field = typeof(BattleVisualFeedbackService).GetField("_damageNumberPrefab", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.SetValue(service, prefab);

            // Act & Assert - should not crash with negative damage
            Assert.DoesNotThrow(() => service.ShowDamageNumber(Vector3.zero, -5));

            Object.DestroyImmediate(prefab);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void ShowDamageNumber_WithValidPrefab_InstantiatesPrefab()
        {
            // Arrange
            var go = new GameObject("TestService");
            var service = go.AddComponent<BattleVisualFeedbackService>();
            var prefab = new GameObject("DamageNumberPrefab");

            // Use reflection to set the private field
            var field = typeof(BattleVisualFeedbackService).GetField("_damageNumberPrefab", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.SetValue(service, prefab);

            int initialCount = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None).Length;

            // Act
            service.ShowDamageNumber(new Vector3(5, 10, 0), 42);

            // Assert - a new instance should be created
            int finalCount = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None).Length;
            Assert.Greater(finalCount, initialCount, "Prefab should have been instantiated");

            // Cleanup
            var instances = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            foreach (var instance in instances)
            {
                if (instance.name.Contains("DamageNumberPrefab"))
                {
                    Object.DestroyImmediate(instance);
                }
            }
            Object.DestroyImmediate(prefab);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void ShowDamageNumber_AppliesYOffset()
        {
            // Arrange
            var go = new GameObject("TestService");
            var service = go.AddComponent<BattleVisualFeedbackService>();
            var prefab = new GameObject("DamageNumberPrefab");

            // Set prefab and Y offset
            var prefabField = typeof(BattleVisualFeedbackService).GetField("_damageNumberPrefab", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            prefabField.SetValue(service, prefab);

            var offsetField = typeof(BattleVisualFeedbackService).GetField("_damageNumberYOffset", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            offsetField.SetValue(service, 3f);

            Vector3 basePosition = new Vector3(10, 5, 0);

            // Act
            service.ShowDamageNumber(basePosition, 100);

            // Assert - find the instantiated object and check its position
            var instances = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            GameObject spawnedInstance = null;
            foreach (var instance in instances)
            {
                if (instance.name.Contains("DamageNumberPrefab") && instance != prefab)
                {
                    spawnedInstance = instance;
                    break;
                }
            }

            Assert.IsNotNull(spawnedInstance, "Damage number instance should be spawned");
            Assert.AreEqual(basePosition.x, spawnedInstance.transform.position.x, 0.01f, "X position should match");
            Assert.AreEqual(basePosition.y + 3f, spawnedInstance.transform.position.y, 0.01f, "Y position should include offset");
            Assert.AreEqual(basePosition.z, spawnedInstance.transform.position.z, 0.01f, "Z position should match");

            // Cleanup
            Object.DestroyImmediate(spawnedInstance);
            Object.DestroyImmediate(prefab);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void ShowHealNumber_LogsNotImplemented()
        {
            // Arrange
            var go = new GameObject("TestService");
            var service = go.AddComponent<BattleVisualFeedbackService>();

            // Act & Assert - should not crash, just log
            Assert.DoesNotThrow(() => service.ShowHealNumber(Vector3.zero, 25));

            Object.DestroyImmediate(go);
        }

        [Test]
        public void ShowBuffText_LogsNotImplemented()
        {
            // Arrange
            var go = new GameObject("TestService");
            var service = go.AddComponent<BattleVisualFeedbackService>();

            // Act & Assert - should not crash, just log
            Assert.DoesNotThrow(() => service.ShowBuffText(Vector3.zero, "STUNNED"));

            Object.DestroyImmediate(go);
        }
    }
}
