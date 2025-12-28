using System;
using UnityEngine;
using SevenBattles.Core.Units;
using SevenBattles.Core.Battle;
using SevenBattles.Battle.Tiles;

namespace SevenBattles.Battle.Units
{
    // Runtime stats holder for a wizard instance.
    public class UnitStats : MonoBehaviour
    {
        private const int DefaultLevel = 1;

        [Header("Core Stats")]
        [SerializeField] private int _level = DefaultLevel;
        [SerializeField] private UnitLevelBonusData _levelBonus;
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
        [SerializeField] private int _deckCapacity;
        [SerializeField] private int _drawCapacity;

        public int Level => _level;
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
        public int DeckCapacity => _deckCapacity;
        public int DrawCapacity => _drawCapacity;

        // Backwards-compatible aliases for older tests/usages.
        public int MaxHP => _maxLife;
        public int ActionPoints => _actionPoints;

        public event Action Changed;
        public event Action<UnitStats, int> Healed;

        public void ApplyBase(UnitStatsData data)
        {
            ApplyBase(data, default, DefaultLevel);
        }

        public void ApplyBase(UnitStatsData baseData, UnitLevelBonusData levelBonus, int level)
        {
            _levelBonus = levelBonus;
            _level = NormalizeLevel(level);
            var scaled = levelBonus.ApplyTo(baseData, _level);
            ApplyBaseInternal(scaled);
        }

        public void ApplySaved(SevenBattles.Core.Save.UnitStatsSaveData data)
        {
            if (data == null)
            {
                return;
            }

            _level = ResolveSavedLevel(data.Level);
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
            if (data.DeckCapacity > 0)
            {
                _deckCapacity = data.DeckCapacity;
            }
            if (data.DrawCapacity > 0)
            {
                _drawCapacity = data.DrawCapacity;
            }
            NotifyChanged();
        }

        public void SetLevel(int level)
        {
            int normalized = NormalizeLevel(level);
            if (normalized == _level)
            {
                return;
            }

            int deltaLevel = normalized - _level;
            if (deltaLevel != 0 && !_levelBonus.IsZero)
            {
                ApplyLevelDelta(deltaLevel);
            }

            _level = normalized;
            NotifyChanged();
        }

        public void ApplyStatDelta(TileStatBonus delta)
        {
            if (delta.IsZero)
            {
                return;
            }

            _maxLife = Mathf.Max(0, _maxLife + delta.Life);
            _life = Mathf.Clamp(_life + delta.Life, 0, _maxLife);
            _attack = Mathf.Max(0, _attack + delta.Attack);
            _shoot = Mathf.Max(0, _shoot + delta.Shoot);
            _spell = Mathf.Max(0, _spell + delta.Spell);
            _speed = Mathf.Max(0, _speed + delta.Speed);
            _luck = Mathf.Max(0, _luck + delta.Luck);
            _defense = Mathf.Max(0, _defense + delta.Defense);
            _protection = Mathf.Max(0, _protection + delta.Protection);
            _initiative = Mathf.Max(0, _initiative + delta.Initiative);
            _morale = Mathf.Max(0, _morale + delta.Morale);

            NotifyChanged();
        }

        public void ApplyStatDelta(EnchantmentStatBonus delta)
        {
            if (delta.IsZero)
            {
                return;
            }

            var tileDelta = new TileStatBonus
            {
                Life = delta.Life,
                Attack = delta.Attack,
                Shoot = delta.Shoot,
                Spell = delta.Spell,
                Speed = delta.Speed,
                Luck = delta.Luck,
                Defense = delta.Defense,
                Protection = delta.Protection,
                Initiative = delta.Initiative,
                Morale = delta.Morale
            };

            ApplyStatDelta(tileDelta);
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

            int previous = _life;
            _life = Mathf.Max(0, _life - damage);
            if (_life != previous)
            {
                NotifyChanged();
            }
        }

        public int Heal(int amount)
        {
            if (amount < 0)
            {
                Debug.LogWarning($"[UnitStats] Heal called with negative amount: {amount}. Ignoring.");
                return 0;
            }

            int previousLife = _life;
            int maxLife = Mathf.Max(0, _maxLife);
            _life = Mathf.Clamp(_life + amount, 0, maxLife);

            int effectiveHeal = _life - previousLife;
            Healed?.Invoke(this, effectiveHeal);
            if (effectiveHeal != 0)
            {
                NotifyChanged();
            }
            return effectiveHeal;
        }

        private void NotifyChanged()
        {
            Changed?.Invoke();
        }

        private void ApplyBaseInternal(UnitStatsData data)
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
            _deckCapacity = Mathf.Max(0, data.DeckCapacity);
            _drawCapacity = Mathf.Max(0, data.DrawCapacity);
            NotifyChanged();
        }

        private void ApplyLevelDelta(int deltaLevel)
        {
            int lifeDelta = _levelBonus.Life * deltaLevel;
            _maxLife = Mathf.Max(0, _maxLife + lifeDelta);
            _life = Mathf.Clamp(_life + lifeDelta, 0, _maxLife);
            _attack = Mathf.Max(0, _attack + (_levelBonus.Attack * deltaLevel));
            _shoot = Mathf.Max(0, _shoot + (_levelBonus.Shoot * deltaLevel));
            _spell = Mathf.Max(0, _spell + (_levelBonus.Spell * deltaLevel));
            _speed = Mathf.Max(0, _speed + (_levelBonus.Speed * deltaLevel));
            _luck = Mathf.Max(0, _luck + (_levelBonus.Luck * deltaLevel));
            _defense = Mathf.Max(0, _defense + (_levelBonus.Defense * deltaLevel));
            _protection = Mathf.Max(0, _protection + (_levelBonus.Protection * deltaLevel));
            _initiative = Mathf.Max(0, _initiative + (_levelBonus.Initiative * deltaLevel));
            _morale = Mathf.Max(0, _morale + (_levelBonus.Morale * deltaLevel));
        }

        private static int NormalizeLevel(int level)
        {
            return level > 0 ? level : DefaultLevel;
        }

        private int ResolveSavedLevel(int savedLevel)
        {
            if (savedLevel > 0)
            {
                return savedLevel;
            }

            return _level > 0 ? _level : DefaultLevel;
        }
    }
}
