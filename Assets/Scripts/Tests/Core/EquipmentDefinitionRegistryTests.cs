using System;
using NUnit.Framework;
using SevenBattles.Core.Items;
using UnityEngine;

namespace SevenBattles.Tests.Core
{
    public class EquipmentDefinitionRegistryTests
    {
        [Test]
        public void GetById_DefinitionMissingFromSerializedArray_ResolvesFromFallbackLoadedAssets()
        {
            var registry = ScriptableObject.CreateInstance<EquipmentDefinitionRegistry>();
            SetPrivateField(registry, "_definitions", Array.Empty<EquipmentDefinition>());

            var fallbackDefinition = ScriptableObject.CreateInstance<EquipmentDefinition>();
            fallbackDefinition.Id = $"eq.fallback.{Guid.NewGuid():N}";
            fallbackDefinition.SlotType = EquipmentSlotType.Weapon;

            EquipmentDefinition resolved = registry.GetById(fallbackDefinition.Id);

            Assert.AreSame(fallbackDefinition, resolved);

            UnityEngine.Object.DestroyImmediate(fallbackDefinition);
            UnityEngine.Object.DestroyImmediate(registry);
        }

        [Test]
        public void GetById_SerializedDefinitionTakesPriorityOverFallback()
        {
            var registry = ScriptableObject.CreateInstance<EquipmentDefinitionRegistry>();

            string id = $"eq.priority.{Guid.NewGuid():N}";
            var serializedDefinition = ScriptableObject.CreateInstance<EquipmentDefinition>();
            serializedDefinition.Id = id;
            serializedDefinition.SlotType = EquipmentSlotType.Weapon;

            var fallbackDefinition = ScriptableObject.CreateInstance<EquipmentDefinition>();
            fallbackDefinition.Id = id;
            fallbackDefinition.SlotType = EquipmentSlotType.Armor;

            SetPrivateField(registry, "_definitions", new[] { serializedDefinition });

            EquipmentDefinition resolved = registry.GetById(id);

            Assert.AreSame(serializedDefinition, resolved);

            UnityEngine.Object.DestroyImmediate(fallbackDefinition);
            UnityEngine.Object.DestroyImmediate(serializedDefinition);
            UnityEngine.Object.DestroyImmediate(registry);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' was not found on type '{target.GetType().FullName}'.");
            field.SetValue(target, value);
        }
    }
}
