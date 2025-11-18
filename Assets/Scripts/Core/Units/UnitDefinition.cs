using UnityEngine;

namespace SevenBattles.Core.Units
{
    [CreateAssetMenu(menuName = "SevenBattles/Wizard Definition", fileName = "WizardDefinition")]
    public class UnitDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string Id;

        [Header("Presentation")]
        public Sprite Portrait;

        [Header("Prefab")]
        public GameObject Prefab;

        [Header("Base Stats")]
        public UnitStatsData BaseStats;
    }
}

