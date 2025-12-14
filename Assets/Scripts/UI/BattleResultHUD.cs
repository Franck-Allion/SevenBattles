using System;
using UnityEngine;
using UnityEngine.Localization;
using SevenBattles.Core;

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

        private IBattleTurnController _controller;
        private bool _interactionLocked;
        [SerializeField, Tooltip("Delay in seconds before showing the victory/defeat popup after the battle ends. Uses unscaled time.")]
        private float _popupDelaySeconds = 0.5f;
        private bool _battleOutcomeHandled;
        private Coroutine _popupRoutine;

        private void Awake()
        {
            ResolveController();
        }

        private void OnEnable()
        {
            ResolveController();
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

            if (_controller != null)
            {
                _controller.BattleEnded -= HandleBattleEnded;
            }

            if (_interactionLocked && _controller != null)
            {
                _controller.SetInteractionLocked(false);
                _interactionLocked = false;
            }
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
                Debug.LogWarning("BattleResultHUD: Victory popup is not assigned.", this);
                ReleaseInteractionLock();
                return;
            }

            if (_victoryTitle != null || _victoryMessage != null || _victoryConfirmLabel != null)
            {
                var dummyCancel = new LocalizedString();
                _victoryPopup.Show(_victoryTitle, _victoryMessage, _victoryConfirmLabel, dummyCancel, OnPopupConfirmed, OnPopupCancelled);
            }
            else
            {
                _victoryPopup.Show(OnPopupConfirmed, OnPopupCancelled);
            }
        }

        private void ShowDefeatPopup()
        {
            if (_defeatPopup == null)
            {
                Debug.LogWarning("BattleResultHUD: Defeat popup is not assigned.", this);
                ReleaseInteractionLock();
                return;
            }

            if (_defeatTitle != null || _defeatMessage != null || _defeatConfirmLabel != null)
            {
                var dummyCancel = new LocalizedString();
                _defeatPopup.Show(_defeatTitle, _defeatMessage, _defeatConfirmLabel, dummyCancel, OnPopupConfirmed, OnPopupCancelled);
            }
            else
            {
                _defeatPopup.Show(OnPopupConfirmed, OnPopupCancelled);
            }
        }

        private void OnPopupConfirmed()
        {
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
    }
}
