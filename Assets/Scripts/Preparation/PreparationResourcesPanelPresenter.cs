using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using SevenBattles.Core.Players;

namespace SevenBattles.Preparation
{
    /// <summary>
    /// Displays player resources on the preparation scene resources panel.
    /// </summary>
    public sealed class PreparationResourcesPanelPresenter : MonoBehaviour
    {
        [SerializeField, Tooltip("Player context used as the source of truth for displayed resources.")]
        private PlayerContext _playerContext;
        [SerializeField, Tooltip("TMP label showing the current gold amount.")]
        private TMP_Text _goldValueTMP;
        [SerializeField, Tooltip("TMP label showing the current gems amount.")]
        private TMP_Text _gemsValueTMP;

        private bool _isSubscribed;

        private void Awake()
        {
            AutoResolveTextReferences();
        }

        private void OnEnable()
        {
            AutoResolveTextReferences();
            Subscribe();
            Refresh();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (_isSubscribed || _playerContext == null)
            {
                return;
            }

            _playerContext.ResourcesChanged += HandleResourcesChanged;
            _isSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_isSubscribed || _playerContext == null)
            {
                return;
            }

            _playerContext.ResourcesChanged -= HandleResourcesChanged;
            _isSubscribed = false;
        }

        private void HandleResourcesChanged()
        {
            Refresh();
        }

        private void AutoResolveTextReferences()
        {
            if (_goldValueTMP != null && _gemsValueTMP != null)
            {
                return;
            }

            var texts = GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                var tmp = texts[i];
                if (tmp == null)
                {
                    continue;
                }

                var objectName = tmp.gameObject.name;
                if (_goldValueTMP == null && objectName.IndexOf("CoinValue", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _goldValueTMP = tmp;
                }
                else if (_goldValueTMP == null && objectName.IndexOf("GoldValue", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _goldValueTMP = tmp;
                }

                if (_gemsValueTMP == null && objectName.IndexOf("GemValue", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _gemsValueTMP = tmp;
                }
            }
        }

        public void Refresh()
        {
            int gold = _playerContext != null ? _playerContext.Gold : 0;
            int gems = _playerContext != null ? _playerContext.Gems : 0;

            if (_goldValueTMP != null)
            {
                _goldValueTMP.text = gold.ToString(CultureInfo.InvariantCulture);
            }

            if (_gemsValueTMP != null)
            {
                _gemsValueTMP.text = gems.ToString(CultureInfo.InvariantCulture);
            }
        }
    }
}
