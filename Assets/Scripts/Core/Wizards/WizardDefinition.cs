using UnityEngine;

namespace SevenBattles.Core.Wizards
{
    [CreateAssetMenu(menuName = "SevenBattles/Wizard Definition", fileName = "WizardDefinition")]
    public class WizardDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string Id;

        [Header("Presentation")]
        public Sprite Portrait;

        [Header("Prefab")]
        public GameObject Prefab;

        [Header("Base Stats")]
        public WizardStatsData BaseStats;
    }
}

