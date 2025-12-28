using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using SevenBattles.Battle.Save;
using SevenBattles.Battle.Spells;
using SevenBattles.Core;
using SevenBattles.Core.Battle;
using SevenBattles.Core.Save;

namespace SevenBattles.Tests.Battle
{
    public class BattleEnchantmentGameStateSaveProviderTests
    {
        private sealed class FakeBattlefieldService : MonoBehaviour, IBattlefieldService
        {
            public BattlefieldDefinition Current { get; set; }
            public event Action<BattlefieldDefinition> BattlefieldChanged;

            public bool TryGetTileColor(Vector2Int tile, out BattlefieldTileColor color)
            {
                color = BattlefieldTileColor.None;
                return false;
            }
        }

        [Test]
        public void PopulateGameState_CapturesActiveEnchantments()
        {
            var battlefield = ScriptableObject.CreateInstance<BattlefieldDefinition>();
            var quad = new EnchantmentQuadDefinition
            {
                TopLeft = new Vector2(-1f, 1f),
                TopRight = new Vector2(1f, 1f),
                BottomRight = new Vector2(1f, -1f),
                BottomLeft = new Vector2(-1f, -1f),
                Scale = 1f
            };
            SetPrivateField(battlefield, "_enchantmentQuads", new[] { quad });

            var serviceGo = new GameObject("BattlefieldService");
            var service = serviceGo.AddComponent<FakeBattlefieldService>();
            service.Current = battlefield;

            var controllerGo = new GameObject("EnchantmentController");
            var controller = controllerGo.AddComponent<BattleEnchantmentController>();
            SetPrivateField(controller, "_battlefieldServiceBehaviour", service);
            CallPrivate(controller, "Awake");

            var spell = ScriptableObject.CreateInstance<SpellDefinition>();
            spell.Id = "spell.enchant.attack";
            spell.IsEnchantment = true;

            Assert.IsTrue(controller.TryRestoreEnchantment(
                spell,
                quadIndex: 0,
                isPlayerControlledCaster: true,
                casterInstanceId: "caster-1",
                casterUnitId: "unit-1",
                skipVisual: true));

            var providerGo = new GameObject("SaveProvider");
            var provider = providerGo.AddComponent<BattleEnchantmentGameStateSaveProvider>();

            var data = new SaveGameData();
            provider.PopulateGameState(data);

            Assert.IsNotNull(data.BattleEnchantments);
            Assert.AreEqual(1, data.BattleEnchantments.Length);
            Assert.AreEqual("spell.enchant.attack", data.BattleEnchantments[0].SpellId);
            Assert.AreEqual(0, data.BattleEnchantments[0].QuadIndex);
            Assert.AreEqual("caster-1", data.BattleEnchantments[0].CasterInstanceId);
            Assert.AreEqual("unit-1", data.BattleEnchantments[0].CasterUnitId);
            Assert.AreEqual("player", data.BattleEnchantments[0].CasterTeam);

            UnityEngine.Object.DestroyImmediate(spell);
            UnityEngine.Object.DestroyImmediate(battlefield);
            UnityEngine.Object.DestroyImmediate(providerGo);
            UnityEngine.Object.DestroyImmediate(controllerGo);
            UnityEngine.Object.DestroyImmediate(serviceGo);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' was not found on type '{target.GetType().FullName}'.");
            field.SetValue(target, value);
        }

        private static void CallPrivate(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Method '{methodName}' was not found on type '{target.GetType().FullName}'.");
            method.Invoke(target, null);
        }
    }
}
