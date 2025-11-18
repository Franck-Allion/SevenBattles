using NUnit.Framework;
using UnityEngine;
using SevenBattles.Battle.Board;

namespace SevenBattles.Tests.Battle
{
    public class WorldPerspectiveBoardHighlightTests
    {
        [Test]
        public void SetHighlightMaterial_OverridesRendererMaterial()
        {
            var boardGo = new GameObject("WorldBoard");
            var board = boardGo.AddComponent<WorldPerspectiveBoard>();

            // Use a simple built-in sprite shader for test materials.
            var shader = Shader.Find("Sprites/Default");
            Assert.NotNull(shader, "Sprites/Default shader should be available in tests.");

            var baseMat = new Material(shader) { name = "BaseHighlightMat" };
            var overrideMat = new Material(shader) { name = "OverrideHighlightMat" };

            SetPrivate(board, "_highlightMaterial", baseMat);

            // Attack creation of the internal highlight renderer using the default material first.
            board.SetHighlightVisible(true);

            var renderer = GetPrivate<MeshRenderer>(board, "_highlightMr");
            Assert.NotNull(renderer, "Highlight MeshRenderer should be created.");
            Assert.AreSame(baseMat, renderer.sharedMaterial, "Initial highlight material should match the serialized one.");

            // Act: override material via the new API.
            board.SetHighlightMaterial(overrideMat);

            Assert.AreSame(overrideMat, renderer.sharedMaterial, "Highlight material should be overridden by SetHighlightMaterial.");

            Object.DestroyImmediate(boardGo);
        }

        private static void SetPrivate(object obj, string field, object value)
        {
            var f = obj.GetType().GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            f.SetValue(obj, value);
        }

        private static T GetPrivate<T>(object obj, string field) where T : class
        {
            var f = obj.GetType().GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return f.GetValue(obj) as T;
        }
    }
}

