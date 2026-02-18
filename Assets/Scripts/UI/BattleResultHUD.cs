using System;
using System.Text;
using UnityEngine;
using UnityEngine.Localization;
using SevenBattles.Core;
using SevenBattles.Core.Battle;
using SevenBattles.Core.Contracts;
using SevenBattles.Core.Save;
using SevenBattles.Core.Players;
using SevenBattles.Core.Preload;

using SevenBattles.Core.Diagnostics;
namespace SevenBattles.UI
{
    /// <summary>
    /// Simple HUD that listens for IBattleTurnController battle outcome and
    /// shows a victory/defeat popup using reusable ConfirmationMessageBoxHUD.
    /// While the popup is visible, interaction with the battlefield is locked.
    /// </summary>
    public class BattleResultHUD : MonoBehaviour
    {
        [Header("Controller")]
        [SerializeField, Tooltip("Reference to a MonoBehaviour that implements IBattleTurnController. If not assigned, one will be auto-found at runtime.")]
        private MonoBehaviour _controllerBehaviour;

        [Header("Popups")]
        [SerializeField, Tooltip("Popup shown when the player wins the battle.")]
        private ConfirmationMessageBoxHUD _victoryPopup;
        [SerializeField, Tooltip("Popup shown when the player loses the battle.")]
        private ConfirmationMessageBoxHUD _defeatPopup;

        [Header("Localization (optional overrides)")]
        [SerializeField, Tooltip("Optional localized title used for the victory popup.")]
        private LocalizedString _victoryTitle;
        [SerializeField, Tooltip("Optional localized message used for the victory popup.")]
        private LocalizedString _victoryMessage;
        [SerializeField, Tooltip("Optional localized label for the victory confirm button.")]
        private LocalizedString _victoryConfirmLabel;
        [SerializeField, Tooltip("Optional localized title used for the defeat popup.")]
        private LocalizedString _defeatTitle;
        [SerializeField, Tooltip("Optional localized message used for the defeat popup.")]
        private LocalizedString _defeatMessage;
        [SerializeField, Tooltip("Optional localized label for the defeat confirm button.")]
        private LocalizedString _defeatConfirmLabel;

        [Header("XP Summary (optional)")]
        [SerializeField, Tooltip("If enabled, the popup message is replaced by the localized XP summary (UI.Common/BattleResult.XpSummary) after XP is awarded.")]
        private bool _showXpSummaryMessage = true;

        [Header("Rewards (optional)")]
        [SerializeField] private BattleRewardPresenter _rewardPresenter;
        [SerializeField] private MonoBehaviour _battlefieldServiceBehaviour;
        [SerializeField] private MonoBehaviour _battleSessionServiceBehaviour;

        [Header("Post-Victory Transition")]
        [SerializeField] private string _preparationSceneName = "PreparationScene";
        [SerializeField] private ScenePreloadManifest _preparationScenePreload;
        [SerializeField] private float _returnFadeOutDuration = 0.5f;
        [SerializeField] private float _returnFadeInDuration = 0.5f;
        [SerializeField] private Color _returnFadeColor = Color.black;

        [Header("Autosave")]
        [SerializeField, Tooltip("Player context used for autosave progression. If not assigned, auto-resolved at runtime.")]
        private PlayerContext _playerContextAsset;

        private IBattleTurnController _controller;
        private IBattleXpResultProvider _xpResultProvider;
        private IBattlefieldService _battlefieldService;
        private IBattleSessionService _battleSessionService;
        private PlayerContext _playerContext;
        private bool _interactionLocked;
        [SerializeField, Tooltip("Delay in seconds before showing the victory/defeat popup after the battle ends. Uses unscaled time.")]
        private float _popupDelaySeconds = 0.5f;

        [Header("XP Progress UI (optional)")]
        [SerializeField, Tooltip("If enabled, animates per-unit XP progress bars inside the victory/defeat popups (requires a BattleResultXpProgressPresenter wired on the popup or referenced below).")]
        private bool _animateXpProgressBars = true;
        [SerializeField, Tooltip("Optional presenter used to animate XP bars for the victory popup. If not set, will try to find one on the victory popup GameObject.")]
        private BattleResultXpProgressPresenter _victoryXpPresenter;
        [SerializeField, Tooltip("Optional presenter used to animate XP bars for the defeat popup. If not set, will try to find one on the defeat popup GameObject.")]
        private BattleResultXpProgressPresenter _defeatXpPresenter;

        private bool _battleOutcomeHandled;
        private bool _battleAutoSaved;
        private bool _returnTransitionInProgress;
        private bool _victoryRewardsApplied;
        private bool _tournamentProgressApplied;
        private BattleRewardResult _cachedVictoryRewards;
        private BattleOutcome _lastOutcome = BattleOutcome.None;
        private Coroutine _popupRoutine;
        private Coroutine _xpProgressRoutine;

        private void Awake()
        {
            ResolveController();
            ResolveXpProvider();
        }

        private void OnEnable()
        {
            ResolveController();
            ResolveXpProvider();
            if (_controller != null)
            {
                _controller.BattleEnded += HandleBattleEnded;

                if (_controller.HasBattleEnded)
                {
                    HandleBattleEnded(_controller.Outcome);
                }
            }
        }

        private void OnDisable()
        {
            if (_popupRoutine != null)
            {
                try
                {
                    StopCoroutine(_popupRoutine);
                }
                catch
                {
                    // Ignore if coroutine is not running.
                }

                _popupRoutine = null;
            }

            if (_xpProgressRoutine != null)
            {
                try
                {
                    StopCoroutine(_xpProgressRoutine);
                }
                catch
                {
                    // Ignore if coroutine is not running.
                }

                _xpProgressRoutine = null;
            }

            if (_controller != null)
            {
                _controller.BattleEnded -= HandleBattleEnded;
            }

            if (_interactionLocked && _controller != null)
            {
                _controller.SetInteractionLocked(false);
                _interactionLocked = false;
            }

            _returnTransitionInProgress = false;
        }

        private void ResolveController()
        {
            if (_controller != null)
            {
                return;
            }

            if (_controllerBehaviour != null)
            {
                _controller = _controllerBehaviour as IBattleTurnController;
            }

            if (_controller == null)
            {
                var behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
                for (int i = 0; i < behaviours.Length; i++)
                {
                    var candidate = behaviours[i] as IBattleTurnController;
                    if (candidate != null)
                    {
                        _controller = candidate;
                        _controllerBehaviour = behaviours[i];
                        break;
                    }
                }
            }
        }

        private void ResolveXpProvider()
        {
            if (_xpResultProvider != null)
            {
                return;
            }

            if (_controllerBehaviour != null)
            {
                // Prefer resolving alongside the controller if authored on the same root.
                var components = _controllerBehaviour.GetComponents<MonoBehaviour>();
                for (int i = 0; i < components.Length; i++)
                {
                    if (components[i] is IBattleXpResultProvider providerFromSameGo)
                    {
                        _xpResultProvider = providerFromSameGo;
                        return;
                    }
                }
            }

            var behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            for (int i = 0; i < behaviours.Length; i++)
            {
                var candidate = behaviours[i] as IBattleXpResultProvider;
                if (candidate != null)
                {
                    _xpResultProvider = candidate;
                    break;
                }
            }
        }

        private void HandleBattleEnded(BattleOutcome outcome)
        {
            if (_controller == null)
            {
                return;
            }

            if (_battleOutcomeHandled)
            {
                return;
            }

            _battleOutcomeHandled = true;
            _battleAutoSaved = false;
            _lastOutcome = outcome;
            if (outcome == BattleOutcome.PlayerVictory)
            {
                _victoryRewardsApplied = false;
                _tournamentProgressApplied = false;
                _cachedVictoryRewards = null;
            }

            if (_popupRoutine != null)
            {
                try
                {
                    StopCoroutine(_popupRoutine);
                }
                catch
                {
                    // Ignore if coroutine is not running.
                }
            }

            _popupRoutine = StartCoroutine(ShowPopupWithDelay(outcome));
        }

        private System.Collections.IEnumerator ShowPopupWithDelay(BattleOutcome outcome)
        {
            float delay = Mathf.Max(0f, _popupDelaySeconds);
            if (delay > 0f)
            {
                float t = 0f;
                while (t < delay)
                {
                    t += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            if (_controller == null)
            {
                yield break;
            }

            if (!_interactionLocked)
            {
                _controller.SetInteractionLocked(true);
                _interactionLocked = true;
            }

            switch (outcome)
            {
                case BattleOutcome.PlayerVictory:
                    ShowVictoryPopup();
                    break;
                case BattleOutcome.PlayerDefeat:
                    ShowDefeatPopup();
                    break;
            }

            _popupRoutine = null;
        }

        private void ShowVictoryPopup()
        {
            if (_victoryPopup == null)
            {
                SBLog.Warn("BattleResultHUD: Victory popup is not assigned.", this);
                ReleaseInteractionLock();
                return;
            }

            var xpMessage = _showXpSummaryMessage ? BuildXpSummaryMessage() : null;
            if (_victoryTitle != null || _victoryMessage != null || _victoryConfirmLabel != null || xpMessage != null)
            {
                _victoryPopup.ShowWithOverrides(_victoryTitle, xpMessage ?? _victoryMessage, _victoryConfirmLabel, null, OnPopupConfirmed, OnPopupCancelled);
            }
            else
            {
                _victoryPopup.Show(OnPopupConfirmed, OnPopupCancelled);
            }

            var rewards = GetOrComputeVictoryRewards();
            if (_rewardPresenter != null)
            {
                if (rewards != null)
                {
                    _rewardPresenter.Show(rewards);
                }
                else
                {
                    _rewardPresenter.Clear();
                }
            }

            TryAutoSaveBattleProgress();
            TryPlayXpProgress(_victoryPopup, _victoryXpPresenter);
        }

        private BattleRewardResult GetOrComputeVictoryRewards()
        {
            if (_cachedVictoryRewards != null)
            {
                return _cachedVictoryRewards;
            }

            var battlefieldService = ResolveBattlefieldService();
            var rewardTable = battlefieldService?.Current?.RewardTable;
            if (rewardTable == null)
            {
                TryApplyTournamentProgressOnly();
                return null;
            }

            var rewardService = new BattleRewardService();
            _cachedVictoryRewards = rewardService.ComputeRewards(rewardTable);

            if (!_victoryRewardsApplied)
            {
                ApplyRewardsToPlayerContext(_cachedVictoryRewards);
                _victoryRewardsApplied = true;
            }

            return _cachedVictoryRewards;
        }

        private void ShowDefeatPopup()
        {
            var popup = ResolveDefeatPopup(out var presenterOverride);
            if (popup == null)
            {
                SBLog.Warn("BattleResultHUD: Defeat popup is not assigned.", this);
                ReleaseInteractionLock();
                return;
            }

            var xpMessage = _showXpSummaryMessage ? BuildXpSummaryMessage() : null;
            var title = ResolveLocalizedOverride(_defeatTitle, "BattleDefeat.Title");
            var message = ResolveLocalizedOverride(_defeatMessage, "BattleDefeat.Message");
            var confirm = ResolveLocalizedOverride(_defeatConfirmLabel, "Common.Continue");

            if (title != null || message != null || confirm != null || xpMessage != null)
            {
                popup.ShowWithOverrides(title, xpMessage ?? message, confirm, null, OnPopupConfirmed, OnPopupCancelled);
            }
            else
            {
                popup.Show(OnPopupConfirmed, OnPopupCancelled);
            }

            TryAutoSaveBattleProgress();
            TryPlayXpProgress(popup, presenterOverride);
        }

        private void TryAutoSaveBattleProgress()
        {
            if (_battleAutoSaved)
            {
                return;
            }

            var context = FindPlayerContext();
            if (context == null)
            {
                SBLog.Warn("BattleResultHUD: Autosave skipped because PlayerContext could not be resolved.", this);
                return;
            }

            if (TrySavePlayerContextAutosave(context, out string path))
            {
                _battleAutoSaved = true;
            }
            else
            {
                SBLog.Warn($"BattleResultHUD: Autosave failed for path '{path}'.", this);
            }
        }

        private static bool TrySavePlayerContextAutosave(PlayerContext context, out string path)
        {
            return PlayerContextAutoSaveUtility.TrySaveFromPlayerContext(context, out path);
        }

        private void TryPlayXpProgress(ConfirmationMessageBoxHUD popup, BattleResultXpProgressPresenter presenterOverride)
        {
            if (!_animateXpProgressBars || popup == null)
            {
                return;
            }

            ResolveXpProvider();
            if (_xpResultProvider == null || !_xpResultProvider.HasAwardedXp || _xpResultProvider.LastResult == null)
            {
                return;
            }

            var presenter = presenterOverride != null ? presenterOverride : popup.GetComponent<BattleResultXpProgressPresenter>();
            if (presenter == null)
            {
                // Inspector-driven: allow placing the presenter on a child object within the popup.
                presenter = popup.GetComponentInChildren<BattleResultXpProgressPresenter>(true);
                if (presenter == null)
                {
                    // If no presenter is wired, skip quietly.
                    return;
                }
            }

            if (_xpProgressRoutine != null)
            {
                try
                {
                    StopCoroutine(_xpProgressRoutine);
                }
                catch
                {
                    // Ignore if coroutine is not running.
                }
                _xpProgressRoutine = null;
            }

            var canvasGroup = popup.GetComponent<CanvasGroup>();
            _xpProgressRoutine = StartCoroutine(PlayXpProgressWhenPopupVisible(popup, canvasGroup, presenter, _xpResultProvider.LastResult));
        }

        private System.Collections.IEnumerator PlayXpProgressWhenPopupVisible(ConfirmationMessageBoxHUD popup, CanvasGroup canvasGroup, BattleResultXpProgressPresenter presenter, BattleXpAwardResult result)
        {
            // Ensure the popup has had a chance to start its fade-in transition.
            yield return null;

            float timeoutSeconds = 2f;
            float t = 0f;
            while (t < timeoutSeconds)
            {
                if (popup == null || presenter == null || result == null)
                {
                    yield break;
                }

                if (!popup.IsVisible)
                {
                    yield break;
                }

                if (canvasGroup == null || canvasGroup.alpha >= 0.99f)
                {
                    break;
                }

                t += Time.unscaledDeltaTime;
                yield return null;
            }

            if (popup != null && presenter != null && result != null && popup.IsVisible)
            {
                presenter.Play(result);
            }

            _xpProgressRoutine = null;
        }

        private LocalizedString BuildXpSummaryMessage()
        {
            ResolveXpProvider();
            if (_xpResultProvider == null || !_xpResultProvider.HasAwardedXp || _xpResultProvider.LastResult == null)
            {
                return null;
            }

            var result = _xpResultProvider.LastResult;
            string unitLines = BuildXpUnitLines(result);
            if (string.IsNullOrEmpty(unitLines))
            {
                unitLines = GetUiCommonLocalized("BattleResult.NoSurvivors");
            }

            var summary = new LocalizedString
            {
                TableReference = "UI.Common",
                TableEntryReference = "BattleResult.XpSummary"
            };
            summary.Arguments = new object[] { result.TotalXp, "\n", unitLines };
            return summary;
        }

        private string BuildXpUnitLines(BattleXpAwardResult result)
        {
            if (result == null || result.Units == null || result.Units.Length == 0)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            for (int i = 0; i < result.Units.Length; i++)
            {
                var u = result.Units[i];
                if (!u.IsAlive)
                {
                    continue;
                }

                string key = u.ReachedMaxLevel
                    ? "BattleResult.UnitLineMaxLevel"
                    : (u.LevelAfter > u.LevelBefore ? "BattleResult.UnitLineLevelUp" : "BattleResult.UnitLine");

                string line;
                if (key == "BattleResult.UnitLineLevelUp")
                {
                    line = GetUiCommonLocalized(key, u.SquadIndex + 1, u.XpApplied, u.LevelBefore, u.LevelAfter);
                }
                else
                {
                    line = GetUiCommonLocalized(key, u.SquadIndex + 1, u.XpApplied);
                }

                if (!string.IsNullOrEmpty(line))
                {
                    if (sb.Length > 0)
                    {
                        sb.Append('\n');
                    }
                    sb.Append(line);
                }
            }

            return sb.ToString();
        }

        private ConfirmationMessageBoxHUD ResolveDefeatPopup(out BattleResultXpProgressPresenter presenterOverride)
        {
            presenterOverride = _defeatXpPresenter;

            if (_defeatPopup != null)
            {
                var presenter = ResolveXpPresenter(_defeatPopup, presenterOverride);
                if (presenter != null)
                {
                    presenterOverride = presenterOverride != null ? presenterOverride : presenter;
                    return _defeatPopup;
                }
            }

            if (_victoryPopup != null)
            {
                presenterOverride = _victoryXpPresenter;
                return _victoryPopup;
            }

            return _defeatPopup;
        }

        private static BattleResultXpProgressPresenter ResolveXpPresenter(ConfirmationMessageBoxHUD popup, BattleResultXpProgressPresenter presenterOverride)
        {
            if (presenterOverride != null)
            {
                return presenterOverride;
            }

            if (popup == null)
            {
                return null;
            }

            var presenter = popup.GetComponent<BattleResultXpProgressPresenter>();
            if (presenter != null)
            {
                return presenter;
            }

            return popup.GetComponentInChildren<BattleResultXpProgressPresenter>(true);
        }

        private static LocalizedString ResolveLocalizedOverride(LocalizedString candidate, string defaultUiCommonKey)
        {
            if (HasLocalizedValue(candidate))
            {
                return candidate;
            }

            if (string.IsNullOrEmpty(defaultUiCommonKey))
            {
                return null;
            }

            return new LocalizedString
            {
                TableReference = "UI.Common",
                TableEntryReference = defaultUiCommonKey
            };
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

        private static string GetUiCommonLocalized(string entryKey, params object[] args)
        {
            var ls = new LocalizedString
            {
                TableReference = "UI.Common",
                TableEntryReference = entryKey
            };
            if (args != null && args.Length > 0)
            {
                ls.Arguments = args;
            }

            try
            {
                LocalizationCacheDiagnostics.LogDisplay(ls, "BattleResultHUD.UiCommonLookup");
                return ls.GetLocalizedString();
            }
            catch
            {
                return null;
            }
        }

        private IBattlefieldService ResolveBattlefieldService()
        {
            if (_battlefieldService != null)
            {
                return _battlefieldService;
            }

            if (_battlefieldServiceBehaviour != null)
            {
                _battlefieldService = _battlefieldServiceBehaviour as IBattlefieldService;
            }

            if (_battlefieldService == null)
            {
                var behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
                for (int i = 0; i < behaviours.Length; i++)
                {
                    if (behaviours[i] is IBattlefieldService service)
                    {
                        _battlefieldService = service;
                        _battlefieldServiceBehaviour = behaviours[i];
                        break;
                    }
                }
            }

            return _battlefieldService;
        }

        private void ApplyRewardsToPlayerContext(BattleRewardResult rewards)
        {
            if (rewards == null)
            {
                return;
            }

            var context = FindPlayerContext();
            if (context == null)
            {
                SBLog.Warn("BattleResultHUD: PlayerContext not found. Rewards were not applied to player state.", this);
                return;
            }

            context.SetGold(context.Gold + rewards.GoldAmount);

            var bonusRewards = rewards.BonusRewards;
            if (bonusRewards == null)
            {
                return;
            }

            for (int i = 0; i < bonusRewards.Length; i++)
            {
                var bonus = bonusRewards[i];
                if (bonus == null)
                {
                    continue;
                }

                switch (bonus.Type)
                {
                    case BattleRewardType.Gems:
                        context.SetGems(context.Gems + bonus.Amount);
                        break;
                    case BattleRewardType.Equipment:
                        context.Inventory?.AddEquipment(bonus.EquipmentDef);
                        break;
                    case BattleRewardType.Spell:
                        context.Inventory?.AddSpell(bonus.SpellDef);
                        break;
                    case BattleRewardType.Item:
                        context.Inventory?.AddItem(bonus.ItemDef, bonus.Amount);
                        break;
                }
            }

            TryApplyTournamentProgressOnly();
        }

        private void TryApplyTournamentProgressOnly()
        {
            if (_tournamentProgressApplied)
            {
                return;
            }

            var context = FindPlayerContext();
            if (context == null)
            {
                return;
            }

            if (TryApplyTournamentVictoryProgress(context))
            {
                _tournamentProgressApplied = true;
            }
        }

        private bool TryApplyTournamentVictoryProgress(PlayerContext context)
        {
            if (context == null)
            {
                return false;
            }

            var sessionService = ResolveBattleSessionService();
            var session = sessionService != null ? sessionService.CurrentSession : null;
            if (session == null)
            {
                return false;
            }

            if (!string.Equals(session.BattleType, "tournament", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!TournamentMissionIdUtil.TryParseRoundIndex(session.CampaignMissionId, out int roundIndex))
            {
                return false;
            }

            context.MarkTournamentBattleCompleted(roundIndex, TournamentDefinition.BattleCount);
            return true;
        }

        private PlayerContext FindPlayerContext()
        {
            if (_playerContext != null)
            {
                return _playerContext;
            }

            // Prefer the runtime clone to avoid mutating the asset.
            if (PlayerContext.HasRuntimeInstance)
            {
                _playerContext = PlayerContext.RuntimeInstance;
                return _playerContext;
            }

            if (_playerContextAsset != null)
            {
                _playerContext = _playerContextAsset;
                return _playerContext;
            }

            var behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            const System.Reflection.BindingFlags FLAGS = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            for (int i = 0; i < behaviours.Length; i++)
            {
                var behaviour = behaviours[i];
                if (behaviour == null)
                {
                    continue;
                }

                var fields = behaviour.GetType().GetFields(FLAGS);
                for (int j = 0; j < fields.Length; j++)
                {
                    var field = fields[j];
                    if (field.FieldType != typeof(PlayerContext))
                    {
                        continue;
                    }

                    var value = field.GetValue(behaviour) as PlayerContext;
                    if (value != null)
                    {
                        _playerContext = value;
                        return _playerContext;
                    }
                }
            }

            var contexts = Resources.FindObjectsOfTypeAll<PlayerContext>();
            for (int i = 0; i < contexts.Length; i++)
            {
                if (contexts[i] == null)
                {
                    continue;
                }

                _playerContext = contexts[i];
                return _playerContext;
            }

            return null;
        }

        private void OnPopupConfirmed()
        {
            if (_lastOutcome == BattleOutcome.PlayerVictory)
            {
                TryReturnToPreparationScene();
                return;
            }

            ReleaseInteractionLock();
        }

        private void OnPopupCancelled()
        {
            ReleaseInteractionLock();
        }

        private void ReleaseInteractionLock()
        {
            if (_controller != null && _interactionLocked)
            {
                _controller.SetInteractionLocked(false);
                _interactionLocked = false;
            }
        }

        private void TryReturnToPreparationScene()
        {
            if (_returnTransitionInProgress)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_preparationSceneName))
            {
                SBLog.Error("BattleResultHUD: Preparation scene name is empty. Cannot return after victory.", this);
                ReleaseInteractionLock();
                return;
            }

            _returnTransitionInProgress = true;

            bool started = SceneTransitionFader.TryStartTransition(
                _preparationSceneName,
                _preparationScenePreload,
                _returnFadeOutDuration,
                _returnFadeInDuration,
                _returnFadeColor,
                HandleReturnTransitionFailed);

            if (!started)
            {
                HandleReturnTransitionFailed();
            }
        }

        private void HandleReturnTransitionFailed()
        {
            _returnTransitionInProgress = false;
            ReleaseInteractionLock();
        }

        private IBattleSessionService ResolveBattleSessionService()
        {
            if (_battleSessionService != null)
            {
                return _battleSessionService;
            }

            if (_battleSessionServiceBehaviour != null)
            {
                _battleSessionService = _battleSessionServiceBehaviour as IBattleSessionService;
            }

            if (_battleSessionService == null)
            {
                var behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
                for (int i = 0; i < behaviours.Length; i++)
                {
                    if (behaviours[i] is IBattleSessionService service)
                    {
                        _battleSessionService = service;
                        _battleSessionServiceBehaviour = behaviours[i];
                        break;
                    }
                }
            }

            return _battleSessionService;
        }
    }
}
