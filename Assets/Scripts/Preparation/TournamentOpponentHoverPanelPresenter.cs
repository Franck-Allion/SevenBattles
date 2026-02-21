using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Localization;
using SevenBattles.Core.Battle;
using SevenBattles.Core.Diagnostics;
using SevenBattles.Core.Players;

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
        [SerializeField, Tooltip("Optional text field used to display the hovered enemy squad name.")]
        private TMP_Text _squadNameText;

        [Header("Behavior")]
        [SerializeField, Tooltip("Hide the panel when the hovered battle has no enemy squad configured.")]
        private bool _hideWhenNoEnemy = true;
        [Header("Diagnostics")]
        [SerializeField, Tooltip("Logs hover-panel subscription and show/hide reasons in the editor.")]
        private bool _enableDiagnostics = false;

        private LocalizedString _activeSquadNameLocalized;
        private string _activeSquadNameFallback = string.Empty;

        private void Awake()
        {
            EnsureCanvasGroup();
            HideImmediate();
        }

        private void OnEnable()
        {
            ResolveMapPresenter();
            if (_enableDiagnostics)
            {
                SBLog.Info(
                    $"TournamentOpponentHoverPanelPresenter: Enabled. mapPresenter='{(_mapPresenter != null ? _mapPresenter.name : "<null>")}', rows={(_rows != null ? _rows.Length : 0)}, squadNameText={(_squadNameText != null ? "set" : "null")}.",
                    this);
            }

            if (_mapPresenter != null)
            {
                _mapPresenter.BattleHoverChanged += HandleBattleHoverChanged;
                if (_enableDiagnostics)
                {
                    SBLog.Info("TournamentOpponentHoverPanelPresenter: Subscribed to BattleHoverChanged.", this);
                }
            }
            else if (_enableDiagnostics)
            {
                SBLog.Warn("TournamentOpponentHoverPanelPresenter: Map presenter not found; panel cannot react to hover events.", this);
            }
        }

        private void OnDisable()
        {
            if (_mapPresenter != null)
            {
                _mapPresenter.BattleHoverChanged -= HandleBattleHoverChanged;
            }

            ClearSquadNameBinding();
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
            if (_enableDiagnostics)
            {
                string battleId = battle == null
                    ? "<null>"
                    : (battle.EnemySquad != null ? battle.EnemySquad.name : "<enemy-squad-null>");
                SBLog.Info(
                    $"TournamentOpponentHoverPanelPresenter: Received BattleHoverChanged(index={index}, battle='{battleId}').",
                    this);
            }

            if (battle == null)
            {
                ClearSquadNameBinding();
                SetSquadNameText(string.Empty);
                HideImmediate();
                return;
            }

            var enemySquad = battle.EnemySquad;
            if (enemySquad == null)
            {
                ClearSquadNameBinding();
                SetSquadNameText(string.Empty);
                if (_enableDiagnostics)
                {
                    SBLog.Warn(
                        $"TournamentOpponentHoverPanelPresenter: Hovered battle index={index} has no EnemySquad assigned.",
                        this);
                }
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

            BindSquadName(enemySquad);

            var loadouts = enemySquad.GetLoadouts();
            if (loadouts == null || loadouts.Length == 0)
            {
                if (_enableDiagnostics)
                {
                    SBLog.Warn(
                        $"TournamentOpponentHoverPanelPresenter: EnemySquad '{enemySquad.name}' has no loadouts.",
                        this);
                }
                if (_hideWhenNoEnemy)
                {
                    ClearSquadNameBinding();
                    SetSquadNameText(string.Empty);
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
            if (_enableDiagnostics)
            {
                SBLog.Info(
                    $"TournamentOpponentHoverPanelPresenter: Showing panel for squad '{enemySquad.name}' with {loadouts.Length} loadouts.",
                    this);
            }
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

        private void SetSquadNameText(string value)
        {
            if (_squadNameText == null)
            {
                return;
            }

            _squadNameText.text = value ?? string.Empty;
        }

        private void BindSquadName(PlayerSquad squad)
        {
            ClearSquadNameBinding();

            if (squad == null)
            {
                SetSquadNameText(string.Empty);
                return;
            }

            _activeSquadNameFallback = squad.name;
            var localizedSquadName = squad.LocalizedSquadName;

            if (!HasLocalizedValue(localizedSquadName))
            {
                SetSquadNameText(_activeSquadNameFallback);
                return;
            }

            _activeSquadNameLocalized = localizedSquadName;
            _activeSquadNameLocalized.StringChanged += HandleSquadNameChanged;
            RefreshSquadNameText();
        }

        private void ClearSquadNameBinding()
        {
            if (_activeSquadNameLocalized != null)
            {
                _activeSquadNameLocalized.StringChanged -= HandleSquadNameChanged;
            }

            _activeSquadNameLocalized = null;
            _activeSquadNameFallback = string.Empty;
        }

        private void RefreshSquadNameText()
        {
            if (_activeSquadNameLocalized == null)
            {
                SetSquadNameText(_activeSquadNameFallback);
                return;
            }

            try
            {
                HandleSquadNameChanged(_activeSquadNameLocalized.GetLocalizedString());
            }
            catch
            {
                SetSquadNameText(_activeSquadNameFallback);
            }
        }

        private void HandleSquadNameChanged(string localizedValue)
        {
            if (string.IsNullOrWhiteSpace(localizedValue))
            {
                SetSquadNameText(_activeSquadNameFallback);
                return;
            }

            SetSquadNameText(localizedValue);
        }

        private static bool HasLocalizedValue(LocalizedString localized)
        {
            return localized != null && !localized.IsEmpty;
        }

        private void ShowImmediate()
        {
            EnsureCanvasGroup();
            _rootCanvasGroup.alpha = 1f;
            // Keep interactable true so Selectable children do not switch to DisabledColor visuals.
            _rootCanvasGroup.interactable = true;
            _rootCanvasGroup.blocksRaycasts = false;
            if (_enableDiagnostics)
            {
                SBLog.Info("TournamentOpponentHoverPanelPresenter: Panel visible (alpha=1).", this);
            }
        }

        private void HideImmediate()
        {
            EnsureCanvasGroup();
            _rootCanvasGroup.alpha = 0f;
            _rootCanvasGroup.interactable = true;
            _rootCanvasGroup.blocksRaycasts = false;
            if (_enableDiagnostics)
            {
                SBLog.Info("TournamentOpponentHoverPanelPresenter: Panel hidden (alpha=0).", this);
            }
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
