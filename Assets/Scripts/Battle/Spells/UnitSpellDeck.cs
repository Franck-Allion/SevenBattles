using System.Collections.Generic;
using UnityEngine;
using SevenBattles.Core.Battle;

namespace SevenBattles.Battle.Spells
{
    /// <summary>
    /// Runtime spell deck for a single unit. Holds the assigned spells and draws a random hand each turn.
    /// </summary>
    public sealed class UnitSpellDeck : MonoBehaviour
    {
        [SerializeField] private SpellDefinition[] _assignedSpells = System.Array.Empty<SpellDefinition>();
        [SerializeField] private int _deckCapacity;
        [SerializeField] private int _drawCapacity;

        private readonly List<SpellDefinition> _deck = new List<SpellDefinition>(8);
        private SpellDefinition[] _drawn = System.Array.Empty<SpellDefinition>();

        public SpellDefinition[] AssignedSpells => _assignedSpells;
        public int DeckCapacity => _deckCapacity;
        public int DrawCapacity => _drawCapacity;
        public SpellDefinition[] CurrentDrawnSpells => _drawn;

        public static UnitSpellDeck Ensure(GameObject go)
        {
            if (go == null) return null;
            var deck = go.GetComponent<UnitSpellDeck>();
            if (deck == null)
            {
                deck = go.AddComponent<UnitSpellDeck>();
            }
            return deck;
        }

        public void Configure(SpellDefinition[] assignedSpells, int deckCapacity, int drawCapacity)
        {
            _assignedSpells = SanitizeAssignedSpells(assignedSpells);
            _deckCapacity = Mathf.Max(0, deckCapacity);
            _drawCapacity = Mathf.Max(0, drawCapacity);
            TrimAssignedSpellsToCapacity();
            ResetDeckForBattle();
        }

        public void ResetDeckForBattle()
        {
            BuildDeck();
            ShuffleDeck();
            _drawn = System.Array.Empty<SpellDefinition>();
        }

        public SpellDefinition[] DrawForTurn()
        {
            if (_deck.Count == 0)
            {
                _drawn = System.Array.Empty<SpellDefinition>();
                return _drawn;
            }

            int drawCount = GetEffectiveDrawCapacity();
            if (drawCount <= 0)
            {
                _drawn = System.Array.Empty<SpellDefinition>();
                return _drawn;
            }

            // Partial shuffle to randomize the drawn subset without allocating.
            for (int i = 0; i < drawCount; i++)
            {
                int j = Random.Range(i, _deck.Count);
                ( _deck[i], _deck[j] ) = ( _deck[j], _deck[i] );
            }

            if (_drawn.Length != drawCount)
            {
                _drawn = new SpellDefinition[drawCount];
            }

            for (int i = 0; i < drawCount; i++)
            {
                _drawn[i] = _deck[i];
            }

            return _drawn;
        }

        public bool RemoveSpellForBattle(SpellDefinition spell)
        {
            if (spell == null)
            {
                return false;
            }

            bool removedFromDeck = _deck.Remove(spell);
            bool removedFromDrawn = RemoveFromDrawn(spell);
            return removedFromDeck || removedFromDrawn;
        }

        private void BuildDeck()
        {
            _deck.Clear();

            if (_assignedSpells == null || _assignedSpells.Length == 0)
            {
                return;
            }

            int capacity = GetEffectiveDeckCapacity();
            if (capacity <= 0)
            {
                return;
            }

            var unique = new HashSet<SpellDefinition>();
            for (int i = 0; i < _assignedSpells.Length; i++)
            {
                var spell = _assignedSpells[i];
                if (spell == null || !unique.Add(spell))
                {
                    continue;
                }

                _deck.Add(spell);
                if (_deck.Count >= capacity)
                {
                    break;
                }
            }

        }

        private void ShuffleDeck()
        {
            for (int i = _deck.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (_deck[i], _deck[j]) = (_deck[j], _deck[i]);
            }
        }

        private int GetEffectiveDeckCapacity()
        {
            if (_deckCapacity > 0)
            {
                return _deckCapacity;
            }

            return _assignedSpells != null ? _assignedSpells.Length : 0;
        }

        private int GetEffectiveDrawCapacity()
        {
            int draw = _drawCapacity > 0 ? _drawCapacity : _deck.Count;
            return Mathf.Min(draw, _deck.Count);
        }

        private static SpellDefinition[] SanitizeAssignedSpells(SpellDefinition[] assigned)
        {
            if (assigned == null || assigned.Length == 0)
            {
                return System.Array.Empty<SpellDefinition>();
            }

            var unique = new HashSet<SpellDefinition>();
            var list = new List<SpellDefinition>(assigned.Length);
            for (int i = 0; i < assigned.Length; i++)
            {
                var spell = assigned[i];
                if (spell == null || !unique.Add(spell))
                {
                    continue;
                }

                list.Add(spell);
            }

            return list.Count == 0 ? System.Array.Empty<SpellDefinition>() : list.ToArray();
        }

        private void TrimAssignedSpellsToCapacity()
        {
            if (_deckCapacity <= 0 || _assignedSpells.Length <= _deckCapacity)
            {
                return;
            }

            int originalCount = _assignedSpells.Length;
            var trimmed = new SpellDefinition[_deckCapacity];
            System.Array.Copy(_assignedSpells, trimmed, _deckCapacity);
            _assignedSpells = trimmed;
            Debug.LogWarning($"UnitSpellDeck: Assigned {originalCount} spells but deck capacity is {_deckCapacity}. Extra spells are ignored.", this);
        }

        private bool RemoveFromDrawn(SpellDefinition spell)
        {
            if (_drawn == null || _drawn.Length == 0 || spell == null)
            {
                return false;
            }

            int count = 0;
            for (int i = 0; i < _drawn.Length; i++)
            {
                if (!ReferenceEquals(_drawn[i], spell))
                {
                    count++;
                }
            }

            if (count == _drawn.Length)
            {
                return false;
            }

            if (count == 0)
            {
                _drawn = System.Array.Empty<SpellDefinition>();
                return true;
            }

            var next = new SpellDefinition[count];
            int index = 0;
            for (int i = 0; i < _drawn.Length; i++)
            {
                var item = _drawn[i];
                if (!ReferenceEquals(item, spell))
                {
                    next[index++] = item;
                }
            }

            _drawn = next;
            return true;
        }
    }
}
