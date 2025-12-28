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
            var colors = new BattlefieldTileColor[def.TileCount];
            colors[def.ToIndex(0, 0)] = BattlefieldTileColor.Yellow;
            colors[def.ToIndex(def.Columns - 1, def.Rows - 1)] = BattlefieldTileColor.Red;

            SetPrivateField(def, "_tileColors", colors);

            Assert.AreEqual(BattlefieldTileColor.Yellow, def.GetTileColor(0, 0));
            Assert.AreEqual(BattlefieldTileColor.Red, def.GetTileColor(def.Columns - 1, def.Rows - 1));
        }

        [Test]
        public void TryGetTileColor_OutOfBounds_ReturnsNone()
        {
            var def = ScriptableObject.CreateInstance<BattlefieldDefinition>();

            var ok = def.TryGetTileColor(-1, 0, out var color);
            Assert.IsFalse(ok);
            Assert.AreEqual(BattlefieldTileColor.None, color);

            ok = def.TryGetTileColor(def.Columns, def.Rows - 1, out color);
            Assert.IsFalse(ok);
            Assert.AreEqual(BattlefieldTileColor.None, color);
        }

        [Test]
        public void OnValidate_ResizesTileArrayToGrid()
        {
            var def = ScriptableObject.CreateInstance<BattlefieldDefinition>();
            SetPrivateField(def, "_columns", 8);
            SetPrivateField(def, "_rows", 5);
            SetPrivateField(def, "_tileHighlightInset01", 1.5f);
            SetPrivateField(def, "_tileColors", new BattlefieldTileColor[1]);

            InvokePrivate(def, "OnValidate");

            var colors = (BattlefieldTileColor[])GetPrivateField(def, "_tileColors");
            Assert.AreEqual(40, colors.Length);

            var inset = (float)GetPrivateField(def, "_tileHighlightInset01");
            Assert.GreaterOrEqual(inset, 0f);
            Assert.LessOrEqual(inset, 0.45f);
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
