using System;
using System.Globalization;
using System.IO;
using TMPro;
using UnityEngine;
using SevenBattles.Core.Players;

using SevenBattles.Core.Diagnostics;

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
        [SerializeField, Tooltip("If enabled, logs the save slots directory path once when this panel is enabled.")]
        private bool _logSaveDirectoryOnEnable = true;

        private bool _isSubscribed;
        private bool _saveDirectoryLogged;

        private void Awake()
        {
            AutoResolveTextReferences();
        }

        private void OnEnable()
        {
            AutoResolveTextReferences();
            TryLogSaveDirectoryHint();
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

        private void TryLogSaveDirectoryHint()
        {
            if (!_logSaveDirectoryOnEnable || _saveDirectoryLogged)
            {
                return;
            }

            string saveDirectory = Path.Combine(Application.persistentDataPath, "Saves");
            string contextName = _playerContext != null ? _playerContext.name : "<none>";
            SBLog.Info($"PreparationResourcesPanelPresenter: PlayerContext='{contextName}'. Save slots path hint: '{saveDirectory}'.");
            _saveDirectoryLogged = true;
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
