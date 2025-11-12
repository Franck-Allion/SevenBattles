using UnityEngine;
using SevenBattles.Core.Wizards;

namespace SevenBattles.Battle.Wizards
{
    // Runtime stats holder for a wizard instance.
    public class WizardStats : MonoBehaviour
    {
        [SerializeField] private int _maxHP;
        [SerializeField] private int _actionPoints;
        [SerializeField] private int _speed;

        public int MaxHP => _maxHP;
        public int ActionPoints => _actionPoints;
        public int Speed => _speed;

        public void ApplyBase(WizardStatsData data)
        {
            _maxHP = data.MaxHP;
            _actionPoints = data.ActionPoints;
            _speed = data.Speed;
        }
    }
}
