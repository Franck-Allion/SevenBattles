using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenBattles.UI
{
    public sealed class BattleSpellSlotView : MonoBehaviour
    {
        [SerializeField, Tooltip("Optional Button used for click selection. If null, a Button on this GameObject (or children) may be used by the HUD.")]
        private Button _button;
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _apCost;
        [SerializeField, Tooltip("Optional selection frame root (e.g., child named 'Frame0') toggled when this slot is selected.")]
        private GameObject _selectionFrame;

        public Button Button => _button;
        public Image Icon => _icon;
        public TMP_Text ApCost => _apCost;
        public GameObject SelectionFrame => _selectionFrame;
    }
}
