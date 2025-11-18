using UnityEngine;
using SevenBattles.Core.Units;

namespace SevenBattles.Battle.Units
{
    // Runtime stats holder for a wizard instance.
    public class UnitStats : MonoBehaviour
    {
        [Header("Core Stats")]
        [SerializeField] private int _life;
        [SerializeField] private int _attack;
        [SerializeField] private int _shoot;
        [SerializeField] private int _spell;
        [SerializeField] private int _speed;
        [SerializeField] private int _luck;
        [SerializeField] private int _defense;
        [SerializeField] private int _protection;
        [SerializeField] private int _initiative;
        [SerializeField] private int _morale;

        public int Life => _life;
        public int MaxLife => _life;
        public int Attack => _attack;
        public int Shoot => _shoot;
        public int Spell => _spell;
        public int Speed => _speed;
        public int Luck => _luck;
        public int Defense => _defense;
        public int Protection => _protection;
        public int Initiative => _initiative;
        public int Morale => _morale;

        // Backwards-compatible aliases for older tests/usages.
        public int MaxHP => _life;
        public int ActionPoints => _attack;

        public void ApplyBase(UnitStatsData data)
        {
            _life = data.Life;
            // Prefer the new Attack field when present, but fall back to
            // ActionPoints for backwards compatibility with existing content.
            _attack = data.Attack != 0 ? data.Attack : data.ActionPoints;
            _shoot = data.Shoot;
            _spell = data.Spell;
            _speed = data.Speed;
            _luck = data.Luck;
            _defense = data.Defense;
            _protection = data.Protection;
            _initiative = data.Initiative;
            _morale = data.Morale;
        }
    }
}
