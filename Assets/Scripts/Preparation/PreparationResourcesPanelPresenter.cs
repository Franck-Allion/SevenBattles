using System;
using System.Globalization;
using System.IO;
using TMPro;
using UnityEngine;
using SevenBattles.Core.Battle;
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
        [Header("Victory Reward Animation")]
        [SerializeField, Tooltip("If enabled, plays the battle victory resource gain animation on scene entry when pending data exists.")]
        private bool _animateBattleVictoryRewards = true;
        [SerializeField, Tooltip("Damage-number-style prefab to spawn for gold gains.")]
        private GameObject _goldNumberPrefab;
        [SerializeField, Tooltip("Damage-number-style prefab to spawn for gems gains.")]
        private GameObject _gemNumberPrefab;
        [SerializeField, Tooltip("Optional camera used to project UI positions for floating numbers.")]
        private Camera _currencyNumberCamera;
        [SerializeField, Tooltip("Offset from the amount label top-right corner for floating number spawn.")]
        private Vector2 _currencyNumberOffset = new Vector2(24f, 20f);
        [SerializeField, Tooltip("Additional offset applied to gold number spawn position.")]
        private Vector2 _goldNumberOffset = Vector2.zero;
        [SerializeField, Tooltip("Additional offset applied to gem number spawn position.")]
        private Vector2 _gemNumberOffset = Vector2.zero;
        [SerializeField, Tooltip("Optional override anchor for gold number spawn. Falls back to the gold amount TMP when not assigned.")]
        private RectTransform _goldNumberSpawnAnchor;
        [SerializeField, Tooltip("Optional override anchor for gem number spawn. Falls back to the gem amount TMP when not assigned.")]
        private RectTransform _gemNumberSpawnAnchor;
        [SerializeField, Tooltip("Projection depth from the spawn camera when converting UI screen position to world space.")]
        private float _currencyNumberSpawnDepth = 10f;
        [SerializeField, Tooltip("Shortest counter animation duration for large rewards.")]
        private float _rewardAnimationMinSeconds = 1.5f;
        [SerializeField, Tooltip("Longest counter animation duration for small rewards.")]
        private float _rewardAnimationMaxSeconds = 3f;
        [SerializeField, Tooltip("Reward amount where the fastest animation duration is reached.")]
        private int _rewardAmountForFastDuration = 180;
        [SerializeField, Tooltip("Scale multiplier used for counter punch feedback.")]
        private float _counterPunchScale = 1.1f;
        [SerializeField, Tooltip("Counter punch duration in seconds.")]
        private float _counterPunchDuration = 0.08f;
        [SerializeField, Tooltip("Extra lifetime added to floating number prefabs.")]
        private float _floatingNumberLifetimePadding = 0.1f;
        [Header("Debug Spawn (Play Mode)")]
        [SerializeField, Tooltip("Enable keyboard shortcuts to test gold/gem floating number spawn in play mode.")]
        private bool _enableDebugSpawnHotkeys;
        [SerializeField, Tooltip("Keyboard key used to spawn a gold floating number while in play mode.")]
        private KeyCode _debugSpawnGoldKey = KeyCode.F7;
        [SerializeField, Tooltip("Keyboard key used to spawn a gem floating number while in play mode.")]
        private KeyCode _debugSpawnGemsKey = KeyCode.F8;
        [SerializeField, Tooltip("Amount used by debug spawn hotkeys/context menu.")]
        private int _debugSpawnAmount = 25;

        private bool _isSubscribed;
        private bool _saveDirectoryLogged;
        private Coroutine _rewardAnimationRoutine;
        private CounterAnimationState _goldAnimation;
        private CounterAnimationState _gemsAnimation;
        private readonly Vector3[] _tmpCorners = new Vector3[4];

        private void Awake()
        {
            AutoResolveTextReferences();
            if (_currencyNumberCamera == null)
            {
                _currencyNumberCamera = Camera.main;
            }
        }

        private void OnEnable()
        {
            AutoResolveTextReferences();
            TryLogSaveDirectoryHint();
            Subscribe();
            if (!TryStartPendingVictoryRewardAnimation())
            {
                Refresh();
            }
        }

        private void OnDisable()
        {
            StopRewardAnimation(applyFinalValue: true);
            Unsubscribe();
        }

        private void Update()
        {
            if (!Application.isPlaying || !_enableDebugSpawnHotkeys)
            {
                return;
            }

            if (Input.GetKeyDown(_debugSpawnGoldKey))
            {
                DebugSpawnGoldNumber();
            }

            if (Input.GetKeyDown(_debugSpawnGemsKey))
            {
                DebugSpawnGemNumber();
            }
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
            StopRewardAnimation(applyFinalValue: true);
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

            SetTextValue(_goldValueTMP, gold);
            SetTextValue(_gemsValueTMP, gems);
        }

        private bool TryStartPendingVictoryRewardAnimation()
        {
            if (!_animateBattleVictoryRewards)
            {
                return false;
            }

            if (!BattleVictoryRewardTransfer.TryConsume(out var pending))
            {
                return false;
            }

            bool canAnimateGold = _goldValueTMP != null && pending.GoldGained > 0;
            bool canAnimateGems = _gemsValueTMP != null && pending.GemsGained > 0;
            if (!canAnimateGold && !canAnimateGems)
            {
                return false;
            }

            StopRewardAnimation(applyFinalValue: false);

            if (_goldValueTMP != null)
            {
                if (canAnimateGold)
                {
                    _goldAnimation = CreateCounterAnimationState(_goldValueTMP, pending.FromGold, pending.ToGold, pending.GoldGained);
                    TrySpawnFloatingNumber(
                        ResolveSpawnAnchor(ResourceType.Gold),
                        ResolveSpawnOffset(ResourceType.Gold),
                        _goldNumberPrefab,
                        pending.GoldGained,
                        _goldAnimation.StepIntervalSeconds * pending.GoldGained);
                }
                else
                {
                    SetTextValue(_goldValueTMP, pending.ToGold);
                }
            }

            if (_gemsValueTMP != null)
            {
                if (canAnimateGems)
                {
                    _gemsAnimation = CreateCounterAnimationState(_gemsValueTMP, pending.FromGems, pending.ToGems, pending.GemsGained);
                    TrySpawnFloatingNumber(
                        ResolveSpawnAnchor(ResourceType.Gems),
                        ResolveSpawnOffset(ResourceType.Gems),
                        _gemNumberPrefab,
                        pending.GemsGained,
                        _gemsAnimation.StepIntervalSeconds * pending.GemsGained);
                }
                else
                {
                    SetTextValue(_gemsValueTMP, pending.ToGems);
                }
            }

            _rewardAnimationRoutine = StartCoroutine(AnimateCountersRoutine());
            return true;
        }

        private CounterAnimationState CreateCounterAnimationState(TMP_Text label, int fromValue, int toValue, int gained)
        {
            int safeFrom = Mathf.Max(0, fromValue);
            int safeTo = Mathf.Max(safeFrom, toValue);
            int safeGained = Mathf.Max(1, gained);
            float duration = ComputeAnimationDuration(safeGained);

            SetTextValue(label, safeFrom);
            return new CounterAnimationState
            {
                Label = label,
                CurrentValue = safeFrom,
                TargetValue = safeTo,
                StepIntervalSeconds = duration / safeGained,
                StepTimerSeconds = 0f,
                PunchTimerSeconds = 0f,
                BaseScale = label != null ? label.rectTransform.localScale : Vector3.one,
                IsActive = true
            };
        }

        private System.Collections.IEnumerator AnimateCountersRoutine()
        {
            while (true)
            {
                float dt = Time.unscaledDeltaTime;
                bool goldActive = UpdateCounterAnimation(_goldAnimation, dt);
                bool gemsActive = UpdateCounterAnimation(_gemsAnimation, dt);
                if (!goldActive && !gemsActive)
                {
                    break;
                }

                yield return null;
            }

            FinalizeCounterAnimation(_goldAnimation);
            FinalizeCounterAnimation(_gemsAnimation);
            _goldAnimation = null;
            _gemsAnimation = null;
            _rewardAnimationRoutine = null;
        }

        private bool UpdateCounterAnimation(CounterAnimationState state, float deltaTime)
        {
            if (state == null || !state.IsActive || state.Label == null)
            {
                return false;
            }

            const int MAX_STEPS_PER_FRAME = 1000;
            float stepInterval = Mathf.Max(0.0001f, state.StepIntervalSeconds);
            state.StepTimerSeconds += Mathf.Max(0f, deltaTime);

            int steps = 0;
            while (state.StepTimerSeconds >= stepInterval && state.CurrentValue < state.TargetValue && steps < MAX_STEPS_PER_FRAME)
            {
                state.StepTimerSeconds -= stepInterval;
                state.CurrentValue++;
                steps++;
            }

            if (steps > 0)
            {
                SetTextValue(state.Label, state.CurrentValue);
                state.PunchTimerSeconds = Mathf.Max(0f, _counterPunchDuration);
            }

            if (state.CurrentValue >= state.TargetValue)
            {
                state.CurrentValue = state.TargetValue;
                SetTextValue(state.Label, state.TargetValue);
            }

            ApplyCounterPunch(state, deltaTime);

            bool counting = state.CurrentValue < state.TargetValue;
            bool punching = state.PunchTimerSeconds > 0f;
            if (!counting && !punching)
            {
                ResetCounterScale(state);
                state.IsActive = false;
            }

            return state.IsActive;
        }

        private void ApplyCounterPunch(CounterAnimationState state, float deltaTime)
        {
            if (state == null || state.Label == null || state.Label.rectTransform == null)
            {
                return;
            }

            if (_counterPunchScale <= 1f || _counterPunchDuration <= 0f)
            {
                state.PunchTimerSeconds = 0f;
                state.Label.rectTransform.localScale = state.BaseScale;
                return;
            }

            if (state.PunchTimerSeconds > 0f)
            {
                state.PunchTimerSeconds = Mathf.Max(0f, state.PunchTimerSeconds - Mathf.Max(0f, deltaTime));
                float t = state.PunchTimerSeconds / Mathf.Max(0.0001f, _counterPunchDuration);
                float scale = Mathf.Lerp(1f, _counterPunchScale, t);
                state.Label.rectTransform.localScale = state.BaseScale * scale;
            }
            else
            {
                state.Label.rectTransform.localScale = state.BaseScale;
            }
        }

        private void StopRewardAnimation(bool applyFinalValue)
        {
            if (_rewardAnimationRoutine != null)
            {
                try
                {
                    StopCoroutine(_rewardAnimationRoutine);
                }
                catch
                {
                    // Ignore if coroutine is not running.
                }

                _rewardAnimationRoutine = null;
            }

            if (applyFinalValue)
            {
                FinalizeCounterAnimation(_goldAnimation);
                FinalizeCounterAnimation(_gemsAnimation);
            }

            _goldAnimation = null;
            _gemsAnimation = null;
        }

        private static void FinalizeCounterAnimation(CounterAnimationState state)
        {
            if (state == null)
            {
                return;
            }

            SetTextValue(state.Label, state.TargetValue);
            ResetCounterScale(state);
            state.IsActive = false;
        }

        private static void ResetCounterScale(CounterAnimationState state)
        {
            if (state == null || state.Label == null || state.Label.rectTransform == null)
            {
                return;
            }

            state.Label.rectTransform.localScale = state.BaseScale;
        }

        private float ComputeAnimationDuration(int gained)
        {
            float minDuration = Mathf.Max(0.05f, _rewardAnimationMinSeconds);
            float maxDuration = Mathf.Max(minDuration, _rewardAnimationMaxSeconds);
            float t = Mathf.Clamp01((float)Mathf.Max(0, gained) / Mathf.Max(1, _rewardAmountForFastDuration));
            return Mathf.Lerp(maxDuration, minDuration, t);
        }

        private void TrySpawnFloatingNumber(RectTransform amountRect, Vector2 offset, GameObject prefab, int amount, float durationSeconds)
        {
            if (amountRect == null || prefab == null || amount <= 0)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();
            amountRect.GetWorldCorners(_tmpCorners);
            Vector3 anchorWorld = _tmpCorners[2];
            anchorWorld += new Vector3(offset.x, offset.y, 0f);
            Vector3 spawnWorld = ConvertUiPointToWorld(amountRect, anchorWorld);

            try
            {
                var instance = Instantiate(prefab, spawnWorld, Quaternion.identity);
                TrySetNumberValue(instance, amount);
                TrySetNumberLifetime(instance, durationSeconds + Mathf.Max(0f, _floatingNumberLifetimePadding));
                TrySetNumberPlusPrefix(instance);
            }
            catch (Exception ex)
            {
                SBLog.Warn($"PreparationResourcesPanelPresenter: Failed to spawn floating reward number '{prefab.name}': {ex.Message}", this);
            }
        }

        private Vector3 ConvertUiPointToWorld(RectTransform sourceRect, Vector3 uiWorld)
        {
            Camera worldCamera = _currencyNumberCamera != null ? _currencyNumberCamera : Camera.main;
            if (worldCamera == null)
            {
                return uiWorld;
            }

            Camera uiCamera = null;
            var parentCanvas = sourceRect != null ? sourceRect.GetComponentInParent<Canvas>() : null;
            if (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                uiCamera = parentCanvas.worldCamera;
            }

            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, uiWorld);
            float depth = Mathf.Max(0.01f, _currencyNumberSpawnDepth);
            Vector3 worldPoint = worldCamera.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, depth));
            return worldPoint;
        }

        [ContextMenu("Debug Spawn/Gold")]
        private void DebugSpawnGoldNumber()
        {
            DebugSpawnNumber(ResourceType.Gold);
        }

        [ContextMenu("Debug Spawn/Gems")]
        private void DebugSpawnGemNumber()
        {
            DebugSpawnNumber(ResourceType.Gems);
        }

        private void DebugSpawnNumber(ResourceType type)
        {
            if (!Application.isPlaying)
            {
                SBLog.Warn("PreparationResourcesPanelPresenter: Debug spawn is only available in play mode.", this);
                return;
            }

            AutoResolveTextReferences();
            int amount = Mathf.Max(1, _debugSpawnAmount);
            float duration = ComputeAnimationDuration(amount);

            switch (type)
            {
                case ResourceType.Gold:
                    TrySpawnFloatingNumber(
                        ResolveSpawnAnchor(ResourceType.Gold),
                        ResolveSpawnOffset(ResourceType.Gold),
                        _goldNumberPrefab,
                        amount,
                        duration);
                    break;
                case ResourceType.Gems:
                    TrySpawnFloatingNumber(
                        ResolveSpawnAnchor(ResourceType.Gems),
                        ResolveSpawnOffset(ResourceType.Gems),
                        _gemNumberPrefab,
                        amount,
                        duration);
                    break;
            }
        }

        private RectTransform ResolveSpawnAnchor(ResourceType type)
        {
            switch (type)
            {
                case ResourceType.Gold:
                    if (_goldNumberSpawnAnchor != null)
                    {
                        return _goldNumberSpawnAnchor;
                    }

                    return _goldValueTMP != null ? _goldValueTMP.rectTransform : null;
                case ResourceType.Gems:
                    if (_gemNumberSpawnAnchor != null)
                    {
                        return _gemNumberSpawnAnchor;
                    }

                    return _gemsValueTMP != null ? _gemsValueTMP.rectTransform : null;
                default:
                    return null;
            }
        }

        private Vector2 ResolveSpawnOffset(ResourceType type)
        {
            switch (type)
            {
                case ResourceType.Gold:
                    return _currencyNumberOffset + _goldNumberOffset;
                case ResourceType.Gems:
                    return _currencyNumberOffset + _gemNumberOffset;
                default:
                    return _currencyNumberOffset;
            }
        }

        private static bool TrySetNumberValue(GameObject instance, int value)
        {
            if (instance == null)
            {
                return false;
            }

            var components = instance.GetComponents<MonoBehaviour>();
            for (int i = 0; i < components.Length; i++)
            {
                var component = components[i];
                if (component == null)
                {
                    continue;
                }

                var type = component.GetType();
                var setNumberMethod = type.GetMethod(
                    "SetNumber",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (setNumberMethod != null && setNumberMethod.GetParameters().Length == 1)
                {
                    try
                    {
                        var parameterType = setNumberMethod.GetParameters()[0].ParameterType;
                        object arg = parameterType == typeof(int) ? value : (object)(float)value;
                        setNumberMethod.Invoke(component, new[] { arg });
                        return true;
                    }
                    catch
                    {
                    }
                }

                var numberField = type.GetField("number", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?? type.GetField("value", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (numberField != null && (numberField.FieldType == typeof(float) || numberField.FieldType == typeof(int)))
                {
                    try
                    {
                        numberField.SetValue(component, numberField.FieldType == typeof(float) ? (float)value : value);
                        return true;
                    }
                    catch
                    {
                    }
                }
            }

            return false;
        }

        private static void TrySetNumberLifetime(GameObject instance, float lifetimeSeconds)
        {
            if (instance == null)
            {
                return;
            }

            var components = instance.GetComponents<MonoBehaviour>();
            for (int i = 0; i < components.Length; i++)
            {
                var component = components[i];
                if (component == null)
                {
                    continue;
                }

                var type = component.GetType();
                var lifetimeField = type.GetField("lifetime", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (lifetimeField != null && lifetimeField.FieldType == typeof(float))
                {
                    try
                    {
                        lifetimeField.SetValue(component, Mathf.Max(0.1f, lifetimeSeconds));
                    }
                    catch
                    {
                    }
                }
            }
        }

        private static void TrySetNumberPlusPrefix(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            var components = instance.GetComponents<MonoBehaviour>();
            for (int i = 0; i < components.Length; i++)
            {
                var component = components[i];
                if (component == null)
                {
                    continue;
                }

                var type = component.GetType();
                var enableLeftField = type.GetField("enableLeftText", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var leftTextField = type.GetField("leftText", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (enableLeftField != null && enableLeftField.FieldType == typeof(bool) && leftTextField != null && leftTextField.FieldType == typeof(string))
                {
                    try
                    {
                        enableLeftField.SetValue(component, true);
                        leftTextField.SetValue(component, "+");
                    }
                    catch
                    {
                    }
                }
            }
        }

        private static void SetTextValue(TMP_Text label, int value)
        {
            if (label == null)
            {
                return;
            }

            label.text = Mathf.Max(0, value).ToString(CultureInfo.InvariantCulture);
        }

        private sealed class CounterAnimationState
        {
            public TMP_Text Label;
            public int CurrentValue;
            public int TargetValue;
            public float StepIntervalSeconds;
            public float StepTimerSeconds;
            public float PunchTimerSeconds;
            public Vector3 BaseScale;
            public bool IsActive;
        }

        private enum ResourceType
        {
            Gold = 0,
            Gems = 1
        }
    }
}
