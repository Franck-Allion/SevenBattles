using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Localization;
using UnityEngine.SceneManagement;
using SevenBattles.Core.Battle;
using SevenBattles.Core.Contracts;
using SevenBattles.Core.Players;

using SevenBattles.Core.Diagnostics;
namespace SevenBattles.Preparation
{
    public sealed class TournamentBattleStartController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TournamentBattleMapPresenter _mapPresenter;
        [SerializeField, Tooltip("Player context used to resolve the current player squad.")]
        private PlayerContext _playerContext;
        [SerializeField, Tooltip("Reference to a MonoBehaviour implementing IConfirmationMessageBox (ConfirmationMessageBoxHUD).")]
        private MonoBehaviour _confirmationBehaviour;

        [Header("Scene")]
        [SerializeField] private string _battleSceneName = "BattleScene";

        [Header("Localization")]
        [SerializeField] private LocalizedString _confirmTitle;
        [SerializeField] private LocalizedString _confirmMessage;
        [SerializeField] private LocalizedString _confirmLabel;
        [SerializeField] private LocalizedString _cancelLabel;

        private IConfirmationMessageBox _confirmation;
        private bool _inputLocked;
        private bool _transitioning;

        private void Awake()
        {
            ResolveReferences();
            SetupLocalizationDefaults();
            EnsureEventSystem();
        }

        private void OnEnable()
        {
            ResolveReferences();

            if (_mapPresenter != null)
            {
                _mapPresenter.BattleClicked += HandleBattleClicked;
            }
        }

        private void OnDisable()
        {
            if (_mapPresenter != null)
            {
                _mapPresenter.BattleClicked -= HandleBattleClicked;
                _mapPresenter.SetInteractionsEnabled(true);
            }

            _inputLocked = false;
            _transitioning = false;
        }

        private void ResolveReferences()
        {
            if (_mapPresenter == null)
            {
                _mapPresenter = UnityEngine.Object.FindFirstObjectByType<TournamentBattleMapPresenter>();
            }

            if (_confirmation == null)
            {
                if (_confirmationBehaviour != null)
                {
                    _confirmation = _confirmationBehaviour as IConfirmationMessageBox;
                }

                if (_confirmation == null)
                {
                    var behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
                    for (int i = 0; i < behaviours.Length; i++)
                    {
                        if (behaviours[i] is IConfirmationMessageBox messageBox)
                        {
                            _confirmation = messageBox;
                            _confirmationBehaviour = behaviours[i];
                            break;
                        }
                    }
                }
            }
        }

        private void SetupLocalizationDefaults()
        {
            if (_confirmTitle == null)
            {
                _confirmTitle = new LocalizedString("UI.Common", "Confirm.StartBattleTitle");
            }

            if (_confirmMessage == null)
            {
                _confirmMessage = new LocalizedString("UI.Common", "Confirm.StartBattleMessage");
            }

            if (_confirmLabel == null)
            {
                _confirmLabel = new LocalizedString("UI.Common", "Common.Yes");
            }

            if (_cancelLabel == null)
            {
                _cancelLabel = new LocalizedString("UI.Common", "Common.No");
            }
        }

        private void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        private void HandleBattleClicked(TournamentBattleDefinition battle, int index)
        {
            if (_inputLocked || _transitioning || _mapPresenter == null)
            {
                return;
            }

            if (battle == null)
            {
                return;
            }

            if (index != _mapPresenter.CurrentRoundIndex)
            {
                return;
            }

            if (_confirmation == null)
            {
                SBLog.Error("TournamentBattleStartController: No confirmation message box assigned or found in the scene.", this);
                return;
            }

            _inputLocked = true;
            _mapPresenter.SetInteractionsEnabled(false);

            _confirmation.Show(
                _confirmTitle,
                _confirmMessage,
                _confirmLabel,
                _cancelLabel,
                () => ConfirmStartBattle(battle),
                CancelStartBattle);
        }

        private void CancelStartBattle()
        {
            _inputLocked = false;

            if (_mapPresenter != null)
            {
                _mapPresenter.SetInteractionsEnabled(true);
            }
        }

        private void ConfirmStartBattle(TournamentBattleDefinition battle)
        {
            if (_transitioning)
            {
                return;
            }

            if (!TryBuildBattleSessionConfig(battle, out var config))
            {
                SBLog.Error("TournamentBattleStartController: Battle start aborted due to missing data.", this);
                _inputLocked = false;
                _transitioning = false;
                if (_mapPresenter != null)
                {
                    _mapPresenter.SetInteractionsEnabled(true);
                }
                return;
            }

            _transitioning = true;
            _inputLocked = true;

            if (_mapPresenter != null)
            {
                _mapPresenter.SetInteractionsEnabled(false);
            }

            if (string.IsNullOrWhiteSpace(_battleSceneName))
            {
                SBLog.Error("TournamentBattleStartController: Battle scene name is empty. Aborting load.", this);
                _inputLocked = false;
                _transitioning = false;
                if (_mapPresenter != null)
                {
                    _mapPresenter.SetInteractionsEnabled(true);
                }
                return;
            }

            BattleSessionConfigTransfer.SetPending(config);

            var loadOp = SceneManager.LoadSceneAsync(_battleSceneName, LoadSceneMode.Single);
            if (loadOp == null)
            {
                SBLog.Error($"TournamentBattleStartController: Failed to load scene '{_battleSceneName}'.", this);
                _inputLocked = false;
                _transitioning = false;
                BattleSessionConfigTransfer.Clear();
                if (_mapPresenter != null)
                {
                    _mapPresenter.SetInteractionsEnabled(true);
                }
            }
        }

        private bool TryBuildBattleSessionConfig(TournamentBattleDefinition battle, out BattleSessionConfig config)
        {
            config = null;

            if (battle == null)
            {
                SBLog.Error("TournamentBattleStartController: Battle definition is missing.", this);
                return false;
            }

            var battlefield = battle.Battlefield;
            if (battlefield == null)
            {
                SBLog.Error("TournamentBattleStartController: Battle definition is missing a Battlefield.", this);
                return false;
            }

            var enemySquad = battle.EnemySquad;
            if (enemySquad == null)
            {
                SBLog.Error("TournamentBattleStartController: Battle definition is missing an EnemySquad.", this);
                return false;
            }

            var enemyLoadouts = enemySquad.GetLoadouts();
            if (enemyLoadouts == null || enemyLoadouts.Length == 0)
            {
                SBLog.Error("TournamentBattleStartController: EnemySquad has no loadouts.", this);
                return false;
            }

            var playerSquad = _playerContext != null ? _playerContext.PlayerSquad : null;
            if (playerSquad == null)
            {
                SBLog.Error("TournamentBattleStartController: PlayerContext has no PlayerSquad assigned.", this);
                return false;
            }

            var playerLoadouts = playerSquad.GetLoadouts();
            if (playerLoadouts == null || playerLoadouts.Length == 0)
            {
                SBLog.Error("TournamentBattleStartController: PlayerSquad has no loadouts.", this);
                return false;
            }

            config = new BattleSessionConfig(playerLoadouts, enemyLoadouts, "tournament", 0)
            {
                Battlefield = battlefield,
                BattlefieldId = battlefield != null ? battlefield.Id : null
            };

            return true;
        }
    }
}
