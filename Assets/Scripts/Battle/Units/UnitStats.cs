using UnityEngine;
using SevenBattles.Core.Units;

namespace SevenBattles.Battle.Units
{
    // Runtime stats holder for a wizard instance.
    public class UnitStats : MonoBehaviour
    {
        [Header("Core Stats")]
        [SerializeField] private int _life;
        [SerializeField] private int _maxLife;
        [SerializeField] private int _attack;
        [SerializeField] private int _actionPoints;
        [SerializeField] private int _shoot;
        [SerializeField] private int _spell;
        [SerializeField] private int _speed;
        [SerializeField] private int _luck;
        [SerializeField] private int _defense;
        [SerializeField] private int _protection;
        [SerializeField] private int _initiative;
        [SerializeField] private int _morale;

        public int Life => _life;
        public int MaxLife => _maxLife;
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
        public int MaxHP => _maxLife;
        public int ActionPoints => _actionPoints;

        public void ApplyBase(UnitStatsData data)
        {
            _maxLife = Mathf.Max(0, data.Life);
            _life = _maxLife;
            _attack = data.Attack;
            // Action points are fully independent from Attack.
            _actionPoints = Mathf.Max(0, data.ActionPoints);
            _shoot = data.Shoot;
            _spell = data.Spell;
            _speed = data.Speed;
            _luck = data.Luck;
            _defense = data.Defense;
            _protection = data.Protection;
            _initiative = data.Initiative;
            _morale = data.Morale;
        }

        public void ApplySaved(SevenBattles.Core.Save.UnitStatsSaveData data)
        {
            if (data == null)
            {
                return;
            }

            _maxLife = data.MaxLife > 0 ? data.MaxLife : Mathf.Max(0, data.Life);
            _life = Mathf.Clamp(data.Life, 0, _maxLife);
            _attack = data.Attack;
            _shoot = data.Shoot;
            _spell = data.Spell;
            _speed = data.Speed;
            _luck = data.Luck;
            _defense = data.Defense;
            _protection = data.Protection;
            _initiative = data.Initiative;
            _morale = data.Morale;
        }

        /// <summary>
        /// Reduces the unit's current life by the specified damage amount.
        /// Life is clamped to a minimum of 0.
        /// </summary>
        /// <param name="damage">Amount of damage to apply (must be >= 0).</param>
        public void TakeDamage(int damage)
        {
            if (damage < 0)
            {
                Debug.LogWarning($"[UnitStats] TakeDamage called with negative damage: {damage}. Ignoring.");
                return;
            }

            _life = Mathf.Max(0, _life - damage);
        }

        public void Heal(int amount)
        {
            if (amount < 0)
            {
                Debug.LogWarning($"[UnitStats] Heal called with negative amount: {amount}. Ignoring.");
                return;
            }

            _life = Mathf.Clamp(_life + amount, 0, Mathf.Max(0, _maxLife));
        }
    }
}
