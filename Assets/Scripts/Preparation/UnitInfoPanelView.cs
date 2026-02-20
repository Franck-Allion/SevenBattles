using SevenBattles.Core.Battle;
using SevenBattles.Core.Units;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenBattles.Preparation
{
    public sealed class UnitInfoPanelView : MonoBehaviour
    {
        [SerializeField] private Image _portrait;
        [SerializeField] private TMP_Text _nameLabel;
        [SerializeField] private TMP_Text _levelLabel;
        [SerializeField] private TMP_Text _lifeValue;
        [SerializeField] private TMP_Text _attackValue;
        [SerializeField] private TMP_Text _shootValue;
        [SerializeField] private TMP_Text _spellValue;
        [SerializeField] private TMP_Text _speedValue;
        [SerializeField] private TMP_Text _luckValue;
        [SerializeField] private TMP_Text _defenseValue;
        [SerializeField] private TMP_Text _protectionValue;
        [SerializeField] private TMP_Text _initiativeValue;
        [SerializeField] private TMP_Text _moraleValue;
        [SerializeField] private GameObject _emptyState;
        [SerializeField] private GameObject _statsContainer;

        public void ShowUnit(UnitSpellLoadout loadout)
        {
            if (loadout == null || loadout.Definition == null)
            {
                Clear();
                return;
            }

            UnitDefinition def = loadout.Definition;
            int level = loadout.EffectiveLevel;
            UnitStatsData baseStats = def.BaseStats;
            UnitStatsData stats = def.LevelBonus.ApplyTo(baseStats, level);

            if (_portrait != null)
            {
                _portrait.sprite = def.Portrait;
                _portrait.enabled = _portrait.sprite != null;
            }

            SetText(_nameLabel, ResolveDisplayName(def));
            SetText(_levelLabel, level.ToString());
            SetText(_lifeValue, stats.Life.ToString());
            SetText(_attackValue, stats.Attack.ToString());
            SetText(_shootValue, stats.Shoot.ToString());
            SetText(_spellValue, stats.Spell.ToString());
            SetText(_speedValue, stats.Speed.ToString());
            SetText(_luckValue, stats.Luck.ToString());
            SetText(_defenseValue, stats.Defense.ToString());
            SetText(_protectionValue, stats.Protection.ToString());
            SetText(_initiativeValue, stats.Initiative.ToString());
            SetText(_moraleValue, stats.Morale.ToString());

            if (_emptyState != null)
            {
                _emptyState.SetActive(false);
            }

            if (_statsContainer != null)
            {
                _statsContainer.SetActive(true);
            }
        }

        public void Clear()
        {
            if (_portrait != null)
            {
                _portrait.sprite = null;
                _portrait.enabled = false;
            }

            SetText(_nameLabel, string.Empty);
            SetText(_levelLabel, string.Empty);
            SetText(_lifeValue, string.Empty);
            SetText(_attackValue, string.Empty);
            SetText(_shootValue, string.Empty);
            SetText(_spellValue, string.Empty);
            SetText(_speedValue, string.Empty);
            SetText(_luckValue, string.Empty);
            SetText(_defenseValue, string.Empty);
            SetText(_protectionValue, string.Empty);
            SetText(_initiativeValue, string.Empty);
            SetText(_moraleValue, string.Empty);

            if (_emptyState != null)
            {
                _emptyState.SetActive(true);
            }

            if (_statsContainer != null)
            {
                _statsContainer.SetActive(false);
            }
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null)
            {
                target.text = value ?? string.Empty;
            }
        }

        private static string ResolveDisplayName(UnitDefinition definition)
        {
            if (definition == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(definition.name))
            {
                return definition.name;
            }

            return definition.Id ?? string.Empty;
        }
    }
}
