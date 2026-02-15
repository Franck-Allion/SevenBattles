using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SevenBattles.Core.Battle;

namespace SevenBattles.Preparation
{
    public sealed class TournamentOpponentHoverPanelPresenter : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TournamentBattleMapPresenter _mapPresenter;
        [SerializeField, Tooltip("CanvasGroup that controls the hover panel visibility.")]
        private CanvasGroup _rootCanvasGroup;

        [Header("Rows (0..7)")]
        [SerializeField] private OpponentRow[] _rows = new OpponentRow[8];

        [Header("Behavior")]
        [SerializeField, Tooltip("Hide the panel when the hovered battle has no enemy squad configured.")]
        private bool _hideWhenNoEnemy = true;

        private void Awake()
        {
            EnsureCanvasGroup();
            HideImmediate();
        }

        private void OnEnable()
        {
            ResolveMapPresenter();
            if (_mapPresenter != null)
            {
                _mapPresenter.BattleHoverChanged += HandleBattleHoverChanged;
            }
        }

        private void OnDisable()
        {
            if (_mapPresenter != null)
            {
                _mapPresenter.BattleHoverChanged -= HandleBattleHoverChanged;
            }

            HideImmediate();
        }

        private void ResolveMapPresenter()
        {
            if (_mapPresenter == null)
            {
                _mapPresenter = UnityEngine.Object.FindFirstObjectByType<TournamentBattleMapPresenter>();
            }
        }

        private void EnsureCanvasGroup()
        {
            if (_rootCanvasGroup == null)
            {
                _rootCanvasGroup = GetComponent<CanvasGroup>();
                if (_rootCanvasGroup == null)
                {
                    _rootCanvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }
        }

        private void HandleBattleHoverChanged(TournamentBattleDefinition battle, int index)
        {
            if (battle == null)
            {
                HideImmediate();
                return;
            }

            var enemySquad = battle.EnemySquad;
            if (enemySquad == null)
            {
                if (_hideWhenNoEnemy)
                {
                    HideImmediate();
                }
                else
                {
                    ClearRows();
                    ShowImmediate();
                }
                return;
            }

            var loadouts = enemySquad.GetLoadouts();
            if (loadouts == null || loadouts.Length == 0)
            {
                if (_hideWhenNoEnemy)
                {
                    HideImmediate();
                }
                else
                {
                    ClearRows();
                    ShowImmediate();
                }
                return;
            }

            PopulateRows(loadouts);
            ShowImmediate();
        }

        private void PopulateRows(UnitSpellLoadout[] loadouts)
        {
            int rowCount = _rows != null ? _rows.Length : 0;
            for (int i = 0; i < rowCount; i++)
            {
                var row = _rows[i];
                var loadout = (loadouts != null && i < loadouts.Length) ? loadouts[i] : null;
                ApplyRow(row, loadout);
            }
        }

        private void ClearRows()
        {
            int rowCount = _rows != null ? _rows.Length : 0;
            for (int i = 0; i < rowCount; i++)
            {
                ApplyRow(_rows[i], null);
            }
        }

        private static void ApplyRow(OpponentRow row, UnitSpellLoadout loadout)
        {
            bool hasData = loadout != null && loadout.Definition != null;

            if (row.Root != null)
            {
                row.Root.SetActive(hasData);
            }

            if (!hasData)
            {
                if (row.Portrait != null)
                {
                    row.Portrait.sprite = null;
                }

                SetLevelText(row, string.Empty);
                return;
            }

            if (row.Portrait != null)
            {
                row.Portrait.sprite = loadout.Definition.Portrait;
                row.Portrait.enabled = loadout.Definition.Portrait != null;
            }

            SetLevelText(row, loadout.EffectiveLevel.ToString());
        }

        private static void SetLevelText(OpponentRow row, string value)
        {
            if (row.LevelTMP != null)
            {
                row.LevelTMP.text = value;
            }
            else if (row.LevelText != null)
            {
                row.LevelText.text = value;
            }
        }

        private void ShowImmediate()
        {
            EnsureCanvasGroup();
            _rootCanvasGroup.alpha = 1f;
            // Keep interactable true so Selectable children do not switch to DisabledColor visuals.
            _rootCanvasGroup.interactable = true;
            _rootCanvasGroup.blocksRaycasts = false;
        }

        private void HideImmediate()
        {
            EnsureCanvasGroup();
            _rootCanvasGroup.alpha = 0f;
            _rootCanvasGroup.interactable = true;
            _rootCanvasGroup.blocksRaycasts = false;
        }

        [Serializable]
        private struct OpponentRow
        {
            public GameObject Root;
            public Image Portrait;
            public TMP_Text LevelTMP;
            public Text LevelText;
        }
    }
}
