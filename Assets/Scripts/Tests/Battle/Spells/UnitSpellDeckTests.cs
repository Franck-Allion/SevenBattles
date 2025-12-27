using System.Linq;
using NUnit.Framework;
using UnityEngine;
using SevenBattles.Battle.Spells;
using SevenBattles.Core.Battle;

namespace SevenBattles.Tests.Battle
{
    public class UnitSpellDeckTests
    {
        [Test]
        public void DrawForTurn_UsesFallbackCapacity_WhenZero()
        {
            var go = new GameObject("Unit");
            var deck = go.AddComponent<UnitSpellDeck>();

            var s1 = ScriptableObject.CreateInstance<SpellDefinition>();
            var s2 = ScriptableObject.CreateInstance<SpellDefinition>();

            deck.Configure(new[] { s1, s2 }, deckCapacity: 0, drawCapacity: 0);
            var drawn = deck.DrawForTurn();

            Assert.AreEqual(2, drawn.Length);
            Assert.IsTrue(drawn.Contains(s1));
            Assert.IsTrue(drawn.Contains(s2));

            Object.DestroyImmediate(s1);
            Object.DestroyImmediate(s2);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void DrawForTurn_RespectsDeckCapacity_AndFiltersDuplicates()
        {
            Random.InitState(1234);

            var go = new GameObject("Unit");
            var deck = go.AddComponent<UnitSpellDeck>();

            var s1 = ScriptableObject.CreateInstance<SpellDefinition>();
            var s2 = ScriptableObject.CreateInstance<SpellDefinition>();
            var s3 = ScriptableObject.CreateInstance<SpellDefinition>();

            deck.Configure(new[] { s1, null, s2, s1, s3 }, deckCapacity: 2, drawCapacity: 5);
            var drawn = deck.DrawForTurn();

            Assert.AreEqual(2, drawn.Length);
            Assert.IsTrue(drawn.All(spell => spell != null));
            Assert.AreEqual(drawn.Distinct().Count(), drawn.Length);
            Assert.IsTrue(drawn.All(spell => spell == s1 || spell == s2 || spell == s3));

            Object.DestroyImmediate(s1);
            Object.DestroyImmediate(s2);
            Object.DestroyImmediate(s3);
            Object.DestroyImmediate(go);
        }
    }
}
