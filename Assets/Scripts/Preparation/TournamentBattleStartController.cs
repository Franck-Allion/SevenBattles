using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Localization;
using System;
using System.Collections.Generic;
using SevenBattles.Core.Battle;
using SevenBattles.Core.Contracts;
using SevenBattles.Core.Players;
using SevenBattles.Core.Preload;

using SevenBattles.Core.Diagnostics;
using SevenBattles.Core;
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

        [Header("Transition")]
        [SerializeField, Tooltip("Optional preload manifest executed during the scene transition to BattleScene.")]
        private ScenePreloadManifest _battleScenePreload;
        [SerializeField, Tooltip("Fade-out duration in seconds for the scene transition.")]
        private float _sceneFadeOutDuration = 0.5f;
        [SerializeField, Tooltip("Fade-in duration in seconds for the scene transition.")]
        private float _sceneFadeInDuration = 0.5f;
        [SerializeField, Tooltip("Fade color used during the scene transition.")]
        private Color _sceneFadeColor = Color.black;

        [Header("Localization")]
        [SerializeField] private LocalizedString _confirmTitle;
        [SerializeField] private LocalizedString _confirmMessage;
        [SerializeField] private LocalizedString _confirmLabel;
        [SerializeField] private LocalizedString _cancelLabel;
        [SerializeField] private LocalizedString _emptySquadTitle;
        [SerializeField] private LocalizedString _emptySquadMessage;
        [SerializeField] private LocalizedString _emptySquadOkLabel;

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
            if (!HasLocalizedValue(_confirmTitle))
            {
                _confirmTitle = new LocalizedString("UI.Common", "Confirm.StartBattleTitle");
            }

            if (!HasLocalizedValue(_confirmMessage))
            {
                _confirmMessage = new LocalizedString("UI.Common", "Confirm.StartBattleMessage");
            }

            if (!HasLocalizedValue(_confirmLabel))
            {
                _confirmLabel = new LocalizedString("UI.Common", "Common.Yes");
            }

            if (!HasLocalizedValue(_cancelLabel))
            {
                _cancelLabel = new LocalizedString("UI.Common", "Common.No");
            }

            if (!HasLocalizedValue(_emptySquadTitle))
            {
                _emptySquadTitle = new LocalizedString("UI.Common", "Confirm.StartBattleRequiresUnitTitle");
            }

            if (!HasLocalizedValue(_emptySquadMessage))
            {
                _emptySquadMessage = new LocalizedString("UI.Common", "Confirm.StartBattleRequiresUnitMessage");
            }

            if (!HasLocalizedValue(_emptySquadOkLabel))
            {
                _emptySquadOkLabel = new LocalizedString("UI.Common", "Common.OK");
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

            var playerContext = ResolvePlayerContext();
            if (playerContext == null)
            {
                SBLog.Error("TournamentBattleStartController: PlayerContext is missing.", this);
                return;
            }

            if (playerContext.IsTournamentBattleCompleted(index))
            {
                return;
            }

            if (_confirmation == null)
            {
                SBLog.Error("TournamentBattleStartController: No confirmation message box assigned or found in the scene.", this);
                return;
            }

            if (!HasAtLeastOneActiveSquadUnit(playerContext))
            {
                ShowEmptySquadPopup();
                return;
            }

            _inputLocked = true;
            _mapPresenter.SetInteractionsEnabled(false);

            _confirmation.Show(
                _confirmTitle,
                _confirmMessage,
                _confirmLabel,
                _cancelLabel,
                () => ConfirmStartBattle(battle, index),
                CancelStartBattle);
        }

        private void ShowEmptySquadPopup()
        {
            _inputLocked = true;

            if (_mapPresenter != null)
            {
                _mapPresenter.SetInteractionsEnabled(false);
            }

            _confirmation.Show(
                _emptySquadTitle,
                _emptySquadMessage,
                _emptySquadOkLabel,
                null,
                DismissEmptySquadPopup,
                DismissEmptySquadPopup);
        }

        private void DismissEmptySquadPopup()
        {
            _inputLocked = false;

            if (_mapPresenter != null)
            {
                _mapPresenter.SetInteractionsEnabled(true);
            }
        }

        private void CancelStartBattle()
        {
            _inputLocked = false;

            if (_mapPresenter != null)
            {
                _mapPresenter.SetInteractionsEnabled(true);
            }
        }

        private void ConfirmStartBattle(TournamentBattleDefinition battle, int roundIndex)
        {
            if (_transitioning)
            {
                return;
            }

            if (!TryBuildBattleSessionConfig(battle, roundIndex, out var config))
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

            bool transitionStarted = SceneTransitionFader.TryStartTransition(
                _battleSceneName,
                _battleScenePreload,
                _sceneFadeOutDuration,
                _sceneFadeInDuration,
                _sceneFadeColor,
                HandleSceneTransitionFailed);

            if (!transitionStarted)
            {
                SBLog.Error($"TournamentBattleStartController: Failed to start scene transition to '{_battleSceneName}'.", this);
                HandleSceneTransitionFailed();
                return;
            }
        }

        private void HandleSceneTransitionFailed()
        {
            _inputLocked = false;
            _transitioning = false;
            BattleSessionConfigTransfer.Clear();
            if (_mapPresenter != null)
            {
                _mapPresenter.SetInteractionsEnabled(true);
            }
        }

        private bool TryBuildBattleSessionConfig(TournamentBattleDefinition battle, int roundIndex, out BattleSessionConfig config)
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

            var playerContext = ResolvePlayerContext();
            if (playerContext == null)
            {
                SBLog.Error("TournamentBattleStartController: PlayerContext is missing.", this);
                return false;
            }

            var playerLoadouts = CloneValidLoadouts(playerContext.GetActiveSquadLoadoutsNonAlloc());
            if (playerLoadouts == null || playerLoadouts.Length == 0)
            {
                SBLog.Error("TournamentBattleStartController: Active squad has no owned units.", this);
                return false;
            }

            config = new BattleSessionConfig(playerLoadouts, enemyLoadouts, "tournament", 0)
            {
                CampaignMissionId = TournamentMissionIdUtil.BuildMissionId(roundIndex),
                Battlefield = battlefield,
                BattlefieldId = battlefield != null ? battlefield.Id : null
            };

            return true;
        }

        private PlayerContext ResolvePlayerContext()
        {
            if (PlayerContext.HasRuntimeInstance)
            {
                _playerContext = PlayerContext.RuntimeInstance;
            }

            return _playerContext;
        }

        private static UnitSpellLoadout[] CloneValidLoadouts(IReadOnlyList<UnitSpellLoadout> source)
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<UnitSpellLoadout>();
            }

            var result = new List<UnitSpellLoadout>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                UnitSpellLoadout loadout = source[i];
                if (loadout == null || loadout.Definition == null)
                {
                    continue;
                }

                result.Add(new UnitSpellLoadout
                {
                    Definition = loadout.Definition,
                    Level = loadout.EffectiveLevel,
                    Xp = loadout.EffectiveXp,
                    Spells = loadout.Spells != null ? (SpellDefinition[])loadout.Spells.Clone() : Array.Empty<SpellDefinition>()
                });
            }

            return result.Count > 0 ? result.ToArray() : Array.Empty<UnitSpellLoadout>();
        }

        private static bool HasAtLeastOneActiveSquadUnit(PlayerContext playerContext)
        {
            if (playerContext == null)
            {
                return false;
            }

            var loadouts = playerContext.GetActiveSquadLoadoutsNonAlloc();
            if (loadouts == null || loadouts.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < loadouts.Count; i++)
            {
                var loadout = loadouts[i];
                if (loadout != null && loadout.Definition != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasLocalizedValue(LocalizedString localized)
        {
            if (localized == null)
            {
                return false;
            }

            var tableRef = localized.TableReference;
            var entryRef = localized.TableEntryReference;

            bool hasTable = !string.IsNullOrEmpty(tableRef.TableCollectionName);
            bool hasEntry = entryRef.KeyId != 0 || !string.IsNullOrEmpty(entryRef.Key);
            return hasTable && hasEntry;
        }
    }
}
