using NUnit.Framework;
using UnityEngine;
using SevenBattles.Core.Players;
using SevenBattles.Core.Save;

namespace SevenBattles.Tests.Core
{
    public class PlayerResourcesLoadHandlerTests
    {
        private static void SetPrivate(object target, string fieldName, object value)
        {
            var type = target.GetType();
            var field = type.GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' was not found on type '{type.FullName}'.");
            field.SetValue(target, value);
        }

        [Test]
        public void ApplyLoadedGame_RestoresPlayerResources()
        {
            var context = ScriptableObject.CreateInstance<PlayerContext>();
            context.SetResources(10, 2);

            var go = new GameObject("PlayerResourcesLoadHandler");
            var handler = go.AddComponent<PlayerResourcesLoadHandler>();
            SetPrivate(handler, "_playerContext", context);

            var data = new SaveGameData
            {
                PlayerResources = new PlayerResourcesSaveData
                {
                    Gold = 123,
                    Gems = 7
                }
            };

            handler.ApplyLoadedGame(data);

            Assert.AreEqual(123, context.Gold);
            Assert.AreEqual(7, context.Gems);

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(context);
        }

        [Test]
        public void ApplyLoadedGame_ClampsNegativeValues_ToZero()
        {
            var context = ScriptableObject.CreateInstance<PlayerContext>();
            context.SetResources(10, 2);

            var go = new GameObject("PlayerResourcesLoadHandler");
            var handler = go.AddComponent<PlayerResourcesLoadHandler>();
            SetPrivate(handler, "_playerContext", context);

            var data = new SaveGameData
            {
                PlayerResources = new PlayerResourcesSaveData
                {
                    Gold = -99,
                    Gems = -3
                }
            };

            handler.ApplyLoadedGame(data);

            Assert.AreEqual(0, context.Gold);
            Assert.AreEqual(0, context.Gems);

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(context);
        }

        [Test]
        public void ApplyLoadedGame_RestoresTournamentProgress()
        {
            var context = ScriptableObject.CreateInstance<PlayerContext>();
            context.SetTournamentProgress(1, new[] { false, false, false, false, false, false, false });

            var go = new GameObject("PlayerResourcesLoadHandler");
            var handler = go.AddComponent<PlayerResourcesLoadHandler>();
            SetPrivate(handler, "_playerContext", context);

            var data = new SaveGameData
            {
                TournamentProgress = new TournamentProgressSaveData
                {
                    CurrentRoundIndex = 4,
                    CompletedBattles = new[] { true, true, true, false, false, false, false }
                }
            };

            handler.ApplyLoadedGame(data);

            Assert.AreEqual(4, context.CurrentTournamentRoundIndex);
            Assert.IsTrue(context.IsTournamentBattleCompleted(1));
            Assert.IsTrue(context.IsTournamentBattleCompleted(2));
            Assert.IsTrue(context.IsTournamentBattleCompleted(3));
            Assert.IsFalse(context.IsTournamentBattleCompleted(4));

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(context);
        }
    }
}
