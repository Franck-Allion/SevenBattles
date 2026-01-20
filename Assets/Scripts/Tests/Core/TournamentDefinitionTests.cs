using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using SevenBattles.Core.Battle;

namespace SevenBattles.Tests.Core
{
    public class TournamentDefinitionTests
    {
        [Test]
        public void OnValidate_ResizesBattleArray()
        {
            var def = ScriptableObject.CreateInstance<TournamentDefinition>();
            SetPrivateField(def, "_battles", new TournamentBattleDefinition[2]);

            InvokePrivate(def, "OnValidate");

            var battles = (TournamentBattleDefinition[])GetPrivateField(def, "_battles");
            Assert.AreEqual(TournamentDefinition.BattleCount, battles.Length);
            Assert.IsTrue(battles[0] != null, "First battle entry should be initialized.");
        }

        [Test]
        public void EllipseDefinition_ContainsPoint_ReturnsExpectedValues()
        {
            var ellipse = new EllipseDefinition
            {
                Center = Vector2.zero,
                Radii = new Vector2(2f, 1f),
                RotationDegrees = 0f
            };

            Assert.IsTrue(ellipse.ContainsPoint(new Vector2(1f, 0.5f)));
            Assert.IsFalse(ellipse.ContainsPoint(new Vector2(3f, 0f)));
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' was not found.");
            field.SetValue(target, value);
        }

        private static object GetPrivateField(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' was not found.");
            return field.GetValue(target);
        }

        private static void InvokePrivate(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Method '{methodName}' was not found.");
            method.Invoke(target, null);
        }
    }
}
