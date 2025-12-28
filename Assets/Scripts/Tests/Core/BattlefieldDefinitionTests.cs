using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using SevenBattles.Core.Battle;

namespace SevenBattles.Tests.Core
{
    public class BattlefieldDefinitionTests
    {
        [Test]
        public void GetTileColor_ReturnsExpectedValues()
        {
            var def = ScriptableObject.CreateInstance<BattlefieldDefinition>();
            var colors = new BattlefieldTileColor[BattlefieldDefinition.TileCount];
            colors[BattlefieldDefinition.ToIndex(0, 0)] = BattlefieldTileColor.Yellow;
            colors[BattlefieldDefinition.ToIndex(6, 5)] = BattlefieldTileColor.Red;

            SetPrivateField(def, "_tileColors", colors);

            Assert.AreEqual(BattlefieldTileColor.Yellow, def.GetTileColor(0, 0));
            Assert.AreEqual(BattlefieldTileColor.Red, def.GetTileColor(6, 5));
        }

        [Test]
        public void TryGetTileColor_OutOfBounds_ReturnsNone()
        {
            var def = ScriptableObject.CreateInstance<BattlefieldDefinition>();

            var ok = def.TryGetTileColor(-1, 0, out var color);
            Assert.IsFalse(ok);
            Assert.AreEqual(BattlefieldTileColor.None, color);

            ok = def.TryGetTileColor(7, 5, out color);
            Assert.IsFalse(ok);
            Assert.AreEqual(BattlefieldTileColor.None, color);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' was not found.");
            field.SetValue(target, value);
        }
    }
}
