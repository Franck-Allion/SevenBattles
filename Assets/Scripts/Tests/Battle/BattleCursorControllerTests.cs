using NUnit.Framework;
using UnityEngine;
using SevenBattles.Battle.Cursors;
using SevenBattles.Core.Battle;

namespace SevenBattles.Tests.Battle
{
    public class BattleCursorControllerTests
    {
        private sealed class FakeCursorBackend : BattleCursorController.ICursorBackend
        {
            public Texture2D LastTexture { get; private set; }
            public Vector2 LastHotspot { get; private set; }
            public CursorMode LastMode { get; private set; }

            public void SetCursor(Texture2D texture, Vector2 hotspot, CursorMode mode)
            {
                LastTexture = texture;
                LastHotspot = hotspot;
                LastMode = mode;
            }
        }

        private static void SetPrivate(object obj, string field, object value)
        {
            var fi = obj.GetType().GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(fi, $"Field '{field}' was not found on type '{obj.GetType().FullName}'.");
            fi.SetValue(obj, value);
        }

        private static void CallPrivate(object obj, string method)
        {
            var mi = obj.GetType().GetMethod(method, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(mi, $"Method '{method}' was not found on type '{obj.GetType().FullName}'.");
            mi.Invoke(obj, null);
        }

        [Test]
        public void ApplyDefaultCursor_SetsCursorToConfiguredDefault()
        {
            var go = new GameObject("Cursor");
            var controller = go.AddComponent<BattleCursorController>();
            var backend = new FakeCursorBackend();

            var defaultTexture = new Texture2D(8, 8);
            var defaultHotspot = new Vector2(3f, 5f);

            SetPrivate(controller, "_defaultCursorTexture", defaultTexture);
            SetPrivate(controller, "_defaultCursorHotspot", defaultHotspot);
            controller.SetCursorBackendForTests(backend);

            controller.ApplyDefaultCursor();

            Assert.AreSame(defaultTexture, backend.LastTexture);
            Assert.AreEqual(defaultHotspot, backend.LastHotspot);
            Assert.AreEqual(CursorMode.Auto, backend.LastMode);

            Object.DestroyImmediate(defaultTexture);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void OnEnable_AppliesDefaultCursor_WhenNoActiveCursor()
        {
            var go = new GameObject("Cursor");
            var controller = go.AddComponent<BattleCursorController>();
            var backend = new FakeCursorBackend();

            var defaultTexture = new Texture2D(8, 8);
            var defaultHotspot = new Vector2(2f, 7f);

            SetPrivate(controller, "_defaultCursorTexture", defaultTexture);
            SetPrivate(controller, "_defaultCursorHotspot", defaultHotspot);
            controller.SetCursorBackendForTests(backend);

            CallPrivate(controller, "OnEnable");

            Assert.AreSame(defaultTexture, backend.LastTexture);
            Assert.AreEqual(defaultHotspot, backend.LastHotspot);

            Object.DestroyImmediate(defaultTexture);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void SetSpellCursor_WithNullSpellTexture_UsesDefaultCursor()
        {
            var go = new GameObject("Cursor");
            var controller = go.AddComponent<BattleCursorController>();
            var backend = new FakeCursorBackend();

            var defaultTexture = new Texture2D(8, 8);
            var defaultHotspot = new Vector2(4f, 2f);

            SetPrivate(controller, "_defaultCursorTexture", defaultTexture);
            SetPrivate(controller, "_defaultCursorHotspot", defaultHotspot);
            controller.SetCursorBackendForTests(backend);

            var spell = ScriptableObject.CreateInstance<SpellDefinition>();
            controller.SetSpellCursor(true, spell);

            Assert.AreSame(defaultTexture, backend.LastTexture);
            Assert.AreEqual(defaultHotspot, backend.LastHotspot);

            Object.DestroyImmediate(spell);
            Object.DestroyImmediate(defaultTexture);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void ClearAll_RevertsToDefaultCursor_WhenConfigured()
        {
            var go = new GameObject("Cursor");
            var controller = go.AddComponent<BattleCursorController>();
            var backend = new FakeCursorBackend();

            var defaultTexture = new Texture2D(8, 8);
            var defaultHotspot = new Vector2(1f, 1f);
            var moveTexture = new Texture2D(8, 8);

            SetPrivate(controller, "_defaultCursorTexture", defaultTexture);
            SetPrivate(controller, "_defaultCursorHotspot", defaultHotspot);
            controller.SetCursorBackendForTests(backend);

            controller.SetMoveCursor(true, moveTexture, new Vector2(0f, 0f));
            controller.ClearAll();

            Assert.AreSame(defaultTexture, backend.LastTexture);
            Assert.AreEqual(defaultHotspot, backend.LastHotspot);

            Object.DestroyImmediate(moveTexture);
            Object.DestroyImmediate(defaultTexture);
            Object.DestroyImmediate(go);
        }
    }
}
