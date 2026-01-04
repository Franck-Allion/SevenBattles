using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;
using SevenBattles.Core;
using SevenBattles.Core.Battle;
using SevenBattles.Core.Units;

namespace SevenBattles.UI
{
    /// <summary>
    /// Optional presenter that animates per-unit XP progress bars inside the Victory/Defeat ConfirmationMessageBoxHUD.
    /// This is intended to be wired in the Unity Inspector: each row references the unit root, slider, and TMP labels.
    /// Animations use unscaled time so it works while the game is paused by a modal overlay.
    /// </summary>
    public sealed class BattleResultXpProgressPresenter : MonoBehaviour
    {
        [Serializable]
        public sealed class UnitRow
        {
            [Tooltip("Player squad index (0-based) this row represents.")]
            public int SquadIndex;
            [Tooltip("Root GameObject for this row (will be enabled/disabled based on whether this unit received XP).")]
            public GameObject Root;
            [Tooltip("Optional Image to display the unit portrait (Sprite comes from the unit definition).")]
            public Image PortraitImage;
            [Tooltip("Optional root GameObject for the XP widgets (slider/labels).")]
            public GameObject XpWidgetsRoot;
            [Tooltip("Progress slider (0..1).")]
            public Slider ProgressSlider;
            [Tooltip("Level label (TMP).")]
            public TMP_Text LevelText;
            [Tooltip("XP progress text (TMP), e.g. '12 / 20' or 'MAX'.")]
            public TMP_Text XpText;
        }

        [Header("Rows (Inspector Wiring)")]
        [SerializeField, Tooltip("Rows to animate. Add one entry per player unit slot (Unit0, Unit1, ...).")]
        private UnitRow[] _rows = Array.Empty<UnitRow>();
        [SerializeField, Tooltip("If enabled, rows without awarded XP are hidden.")]
        private bool _hideRowsWithoutAward = true;
        [SerializeField, Tooltip("If enabled, a row is hidden when its XP widgets (slider/texts) are not wired. Disable this to still show the portrait/root even if XP UI is incomplete.")]
        private bool _hideRowsWithMissingXpWidgets = false;

        [Header("Portrait Source (optional)")]
        [SerializeField, Tooltip("Optional battle session service (IBattleSessionService). Used to resolve portraits from the current player squad by index.")]
        private MonoBehaviour _sessionServiceBehaviour;
        [SerializeField, Tooltip("Optional UnitDefinitionRegistry used to resolve portraits by UnitId when no session service is provided.")]
        private UnitDefinitionRegistry _unitDefinitionRegistry;

        [Header("Audio (optional)")]
        [SerializeField, Tooltip("Optional: custom UI SFX player (MonoBehaviour implementing IUiSfxPlayer). Takes precedence over _audio.")]
        private MonoBehaviour _sfxPlayerBehaviour;
        [SerializeField, Tooltip("AudioSource used to play level-up SFX (optional). If not set, PlayClipAtPoint will be used.")]
        private AudioSource _audio;
        [SerializeField, Tooltip("Sound played when a unit levels up during the XP animation.")]
        private AudioClip _levelUpClip;
        [Range(0f, 1.5f)]
        [SerializeField, Tooltip("Volume multiplier for the level-up SFX.")]
        private float _levelUpVolume = 1f;

        [Header("Localization (optional overrides)")]
        [SerializeField, Tooltip("Localized format for the XP progress text. Defaults to UI.Common/BattleResult.XpProgressFormat.")]
        private LocalizedString _xpProgressFormatLocalized;
        [SerializeField, Tooltip("Localized label for max level. Defaults to UI.Common/BattleResult.MaxLevelShort.")]
        private LocalizedString _maxLevelLocalized;

        [Header("Dead Unit Styling")]
        [SerializeField, Tooltip("Tint applied to dead unit portraits (used when no grayscale material is assigned).")]
        private Color _deadPortraitTint = new Color(0.55f, 0.55f, 0.55f, 0.7f);
        [SerializeField, Tooltip("Optional alpha multiplier applied to row CanvasGroup for dead units.")]
        private float _deadRowAlpha = 0.6f;

        [Header("Animation")]
        [SerializeField, Tooltip("Delay between starting each row animation (seconds). Uses unscaled time.")]
        private float _rowStaggerSeconds = 0.08f;
        [SerializeField, Tooltip("Minimum duration of a single XP segment animation (seconds).")]
        private float _minSegmentSeconds = 0.12f;
        [SerializeField, Tooltip("Maximum duration of a single XP segment animation (seconds).")]
        private float _maxSegmentSeconds = 0.6f;
        [SerializeField, Tooltip("Small XP speed in XP/second (used when XP gained is low).")]
        private float _xpPerSecondSmall = 60f;
        [SerializeField, Tooltip("Large XP speed in XP/second (used when XP gained is high).")]
        private float _xpPerSecondLarge = 260f;
        [SerializeField, Tooltip("XP amount that maps to 'large' speed.")]
        private float _xpForMaxSpeed = 600f;
        [SerializeField, Tooltip("Pause after reaching a level threshold, before resetting the bar to 0 (seconds). Uses unscaled time.")]
        private float _levelUpPauseSeconds = 0.12f;
        [SerializeField, Tooltip("Optional hold at the end of the animation (seconds). Uses unscaled time.")]
        private float _finalHoldSeconds = 0.15f;

        private readonly List<Coroutine> _running = new List<Coroutine>();
        private readonly Dictionary<Image, Color> _portraitColorCache = new Dictionary<Image, Color>();
        private readonly Dictionary<Image, Material> _portraitMaterialCache = new Dictionary<Image, Material>();
        private readonly Dictionary<CanvasGroup, float> _rowAlphaCache = new Dictionary<CanvasGroup, float>();

        private string _xpProgressFormat;
        private string _maxLevelLabel;

        private IBattleSessionService _sessionService;
        private IUiSfxPlayer _sfxPlayer;

        public void Play(BattleXpAwardResult result)
        {
            StopAll();

            if (result == null)
            {
                return;
            }

            EnsureDeadStylingDefaults();
            CacheLocalization();
            ResolveSessionService();
            ResolveSfxPlayer();

            if (_rows == null || _rows.Length == 0)
            {
                return;
            }

            var awardsByIndex = new Dictionary<int, BattleXpAwardResult.UnitAward>();
            if (result.Units != null)
            {
                for (int i = 0; i < result.Units.Length; i++)
                {
                    awardsByIndex[result.Units[i].SquadIndex] = result.Units[i];
                }
            }

            for (int i = 0; i < _rows.Length; i++)
            {
                var row = _rows[i];
                if (row == null)
                {
                    continue;
                }

                if (row.Root == null)
                {
                    continue;
                }

                if (!awardsByIndex.TryGetValue(row.SquadIndex, out var award))
                {
                    if (_hideRowsWithoutAward)
                    {
                        row.Root.SetActive(false);
                    }
                    else
                    {
                        row.Root.SetActive(true);
                        if (row.ProgressSlider != null && row.LevelText != null && row.XpText != null)
                        {
                            ApplyInstantState(new RowView(row.ProgressSlider, row.LevelText, row.XpText), level: 1, xp: 0, xpToNext: 0, maxLevel: 0, isMax: false);
                        }
                    }
                    continue;
                }

                row.Root.SetActive(true);
                ApplyPortrait(row, award);
                ApplyDeadStyling(row, award.IsAlive);

                bool hasXpWidgets = row.ProgressSlider != null && row.LevelText != null && row.XpText != null;

                if (!award.IsAlive)
                {
                    SetXpWidgetsVisible(row, false);
                    continue;
                }

                if (!hasXpWidgets)
                {
                    if (_hideRowsWithMissingXpWidgets)
                    {
                        row.Root.SetActive(false);
                    }
                    continue;
                }

                SetXpWidgetsVisible(row, true);

                var view = new RowView(row.ProgressSlider, row.LevelText, row.XpText);
                ApplyInstantState(view, award.LevelBefore, award.XpBefore, award.XpToNextBefore, award.MaxLevel, isMax: award.LevelBefore >= award.MaxLevel && award.MaxLevel > 0);

                float delay = Mathf.Max(0f, _rowStaggerSeconds) * i;
                if (award.XpApplied > 0 && award.XpSteps != null && award.XpSteps.Length > 0)
                {
                    _running.Add(StartCoroutine(AnimateRow(view, award, delay)));
                }
            }
        }

        private void OnDisable()
        {
            StopAll();
        }

        private void StopAll()
        {
            for (int i = 0; i < _running.Count; i++)
            {
                if (_running[i] == null) continue;
                try
                {
                    StopCoroutine(_running[i]);
                }
                catch
                {
                    // Ignore if coroutine is not running.
                }
            }
            _running.Clear();
        }

        private void ResolveSessionService()
        {
            if (_sessionService != null)
            {
                return;
            }

            if (_sessionServiceBehaviour != null)
            {
                _sessionService = _sessionServiceBehaviour as IBattleSessionService;
                if (_sessionService != null)
                {
                    return;
                }
            }
        }

        private void ResolveSfxPlayer()
        {
            if (_sfxPlayer != null)
            {
                return;
            }

            if (_sfxPlayerBehaviour != null)
            {
                _sfxPlayer = _sfxPlayerBehaviour as IUiSfxPlayer;
            }
        }

        private void PlayLevelUpSfx()
        {
            if (_levelUpClip == null)
            {
                return;
            }

            float volume = Mathf.Clamp(_levelUpVolume, 0f, 1.5f);

            if (_sfxPlayer != null)
            {
                _sfxPlayer.PlayOneShot(_levelUpClip, volume);
                return;
            }

            if (_audio != null)
            {
                _audio.PlayOneShot(_levelUpClip, volume);
                return;
            }

            AudioSource.PlayClipAtPoint(_levelUpClip, Vector3.zero, volume);
        }

        private void ApplyPortrait(UnitRow row, BattleXpAwardResult.UnitAward award)
        {
            if (row == null || row.PortraitImage == null)
            {
                return;
            }

            var sprite = award.Portrait != null ? award.Portrait : ResolvePortraitSprite(award.UnitId, row.SquadIndex);
            if (sprite != null)
            {
                row.PortraitImage.sprite = sprite;
                row.PortraitImage.enabled = true;
            }
            else
            {
                // Preserve any authoring-time sprite if present; otherwise hide the Image to avoid showing a blank.
                row.PortraitImage.enabled = row.PortraitImage.sprite != null;
            }
        }

        private void ApplyDeadStyling(UnitRow row, bool isAlive)
        {
            if (row == null)
            {
                return;
            }

            var image = row.PortraitImage;
            if (image != null)
            {
                CachePortraitDefaults(image);
                if (isAlive)
                {
                    image.color = _portraitColorCache[image];
                    image.material = _portraitMaterialCache[image];
                }
                else
                {
                    image.color = _deadPortraitTint;
                }
            }

            if (row.Root != null)
            {
                var canvasGroup = row.Root.GetComponent<CanvasGroup>();
                if (canvasGroup != null)
                {
                    CacheRowAlpha(canvasGroup);
                    canvasGroup.alpha = isAlive ? _rowAlphaCache[canvasGroup] : Mathf.Clamp01(_deadRowAlpha);
                }
            }
        }

        private void SetXpWidgetsVisible(UnitRow row, bool visible)
        {
            if (row == null)
            {
                return;
            }

            var root = ResolveXpWidgetsRoot(row);
            if (root != null)
            {
                root.SetActive(visible);
                return;
            }

            if (row.ProgressSlider != null)
            {
                row.ProgressSlider.gameObject.SetActive(visible);
            }

            if (row.LevelText != null)
            {
                row.LevelText.gameObject.SetActive(visible);
            }

            if (row.XpText != null)
            {
                row.XpText.gameObject.SetActive(visible);
            }
        }

        private static GameObject ResolveXpWidgetsRoot(UnitRow row)
        {
            if (row == null)
            {
                return null;
            }

            if (row.XpWidgetsRoot != null)
            {
                return row.XpWidgetsRoot;
            }

            if (row.ProgressSlider != null && row.ProgressSlider.transform.parent != null)
            {
                return row.ProgressSlider.transform.parent.gameObject;
            }

            if (row.LevelText != null && row.LevelText.transform.parent != null)
            {
                return row.LevelText.transform.parent.gameObject;
            }

            if (row.XpText != null && row.XpText.transform.parent != null)
            {
                return row.XpText.transform.parent.gameObject;
            }

            return null;
        }

        private void EnsureDeadStylingDefaults()
        {
            if (_deadPortraitTint.a <= 0f)
            {
                _deadPortraitTint = new Color(0.55f, 0.55f, 0.55f, 0.7f);
            }

            if (_deadRowAlpha <= 0f)
            {
                _deadRowAlpha = 0.6f;
            }
        }

        private void CachePortraitDefaults(Image image)
        {
            if (image == null)
            {
                return;
            }

            if (!_portraitColorCache.ContainsKey(image))
            {
                _portraitColorCache[image] = image.color;
                _portraitMaterialCache[image] = image.material;
            }
        }

        private void CacheRowAlpha(CanvasGroup canvasGroup)
        {
            if (canvasGroup == null)
            {
                return;
            }

            if (!_rowAlphaCache.ContainsKey(canvasGroup))
            {
                _rowAlphaCache[canvasGroup] = canvasGroup.alpha;
            }
        }

        private Sprite ResolvePortraitSprite(string unitId, int squadIndex)
        {
            var session = _sessionService != null ? _sessionService.CurrentSession : null;
            var playerSquad = session != null ? session.PlayerSquad : null;
            if (playerSquad != null && squadIndex >= 0 && squadIndex < playerSquad.Length)
            {
                var loadout = playerSquad[squadIndex];
                var def = loadout != null ? loadout.Definition : null;
                if (def != null && def.Portrait != null)
                {
                    return def.Portrait;
                }
            }

            if (_unitDefinitionRegistry != null)
            {
                var def = _unitDefinitionRegistry.GetById(unitId);
                if (def != null && def.Portrait != null)
                {
                    return def.Portrait;
                }
            }

            return null;
        }

        private void CacheLocalization()
        {
            _xpProgressFormat = GetLocalizedOrUiCommonDefault(_xpProgressFormatLocalized, "BattleResult.XpProgressFormat", "{0} / {1}");
            _maxLevelLabel = GetLocalizedOrUiCommonDefault(_maxLevelLocalized, "BattleResult.MaxLevelShort", "MAX");
        }

        private static string GetLocalizedOrUiCommonDefault(LocalizedString localizedOverride, string uiCommonKey, string fallback)
        {
            string fromOverride = TryGetLocalizedString(localizedOverride);
            if (!string.IsNullOrEmpty(fromOverride))
            {
                return fromOverride;
            }

            var ls = new LocalizedString
            {
                TableReference = "UI.Common",
                TableEntryReference = uiCommonKey
            };

            string fromDefault = TryGetLocalizedString(ls);
            if (!string.IsNullOrEmpty(fromDefault))
            {
                return fromDefault;
            }

            return fallback;
        }

        private readonly struct RowView
        {
            public readonly Slider Slider;
            public readonly TMP_Text LevelText;
            public readonly TMP_Text XpText;

            public RowView(Slider slider, TMP_Text levelText, TMP_Text xpText)
            {
                Slider = slider;
                LevelText = levelText;
                XpText = xpText;
            }
        }

        private static string TryGetLocalizedString(LocalizedString localized)
        {
            if (localized == null)
            {
                return null;
            }

            try
            {
                return localized.GetLocalizedString();
            }
            catch
            {
                return null;
            }
        }

        private void ApplyInstantState(RowView view, int level, int xp, int xpToNext, int maxLevel, bool isMax)
        {
            if (view.LevelText != null)
            {
                view.LevelText.text = Mathf.Max(1, level).ToString();
            }

            if (view.Slider != null)
            {
                view.Slider.minValue = 0f;
                view.Slider.maxValue = 1f;
                float v = 0f;
                if (!isMax && xpToNext > 0)
                {
                    v = Mathf.Clamp01((float)Mathf.Max(0, xp) / xpToNext);
                }
                else if (isMax && maxLevel > 0)
                {
                    v = 1f;
                }
                view.Slider.SetValueWithoutNotify(v);
            }

            if (view.XpText != null)
            {
                if (isMax && maxLevel > 0)
                {
                    view.XpText.text = _maxLevelLabel ?? string.Empty;
                }
                else if (xpToNext > 0)
                {
                    view.XpText.text = string.Format(_xpProgressFormat ?? "{0} / {1}", Mathf.Max(0, xp), xpToNext);
                }
                else
                {
                    view.XpText.text = string.Empty;
                }
            }
        }

        private IEnumerator AnimateRow(RowView view, BattleXpAwardResult.UnitAward award, float delaySeconds)
        {
            if (delaySeconds > 0f)
            {
                float t = 0f;
                while (t < delaySeconds)
                {
                    t += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            if (award.MaxLevel > 0 && award.LevelBefore >= award.MaxLevel)
            {
                ApplyInstantState(view, award.LevelBefore, award.XpBefore, award.XpToNextBefore, award.MaxLevel, isMax: true);
                yield break;
            }

            float xpPerSecond = GetXpPerSecond(award.XpApplied);
            float pause = GetLevelUpPauseSeconds(award);

            var steps = award.XpSteps ?? Array.Empty<UnitXpProgressionStep>();
            for (int i = 0; i < steps.Length; i++)
            {
                var step = steps[i];
                if (step.XpToNext <= 0)
                {
                    break;
                }

                int deltaXp = Mathf.Max(0, step.XpTo - step.XpFrom);
                float duration = deltaXp > 0 ? (deltaXp / Mathf.Max(1f, xpPerSecond)) : 0f;
                duration = Mathf.Clamp(duration, _minSegmentSeconds, _maxSegmentSeconds);

                int lastShownXp = int.MinValue;

                float fromNorm = Mathf.Clamp01((float)Mathf.Max(0, step.XpFrom) / step.XpToNext);
                float toNorm = Mathf.Clamp01((float)Mathf.Max(0, step.XpTo) / step.XpToNext);

                float t = 0f;
                while (t < duration)
                {
                    t += Time.unscaledDeltaTime;
                    float p = Mathf.Clamp01(t / duration);
                    float eased = p * p * (3f - 2f * p);

                    float sliderValue = Mathf.LerpUnclamped(fromNorm, toNorm, eased);
                    view.Slider.SetValueWithoutNotify(sliderValue);

                    int xpNow = Mathf.RoundToInt(Mathf.LerpUnclamped(step.XpFrom, step.XpTo, eased));
                    xpNow = Mathf.Clamp(xpNow, Mathf.Min(step.XpFrom, step.XpTo), Mathf.Max(step.XpFrom, step.XpTo));
                    if (xpNow != lastShownXp)
                    {
                        lastShownXp = xpNow;
                        view.XpText.text = string.Format(_xpProgressFormat ?? "{0} / {1}", xpNow, step.XpToNext);
                    }

                    yield return null;
                }

                view.Slider.SetValueWithoutNotify(toNorm);
                view.XpText.text = string.Format(_xpProgressFormat ?? "{0} / {1}", step.XpTo, step.XpToNext);

                if (!step.LevelUpAtEnd)
                {
                    continue;
                }

                PlayLevelUpSfx();

                if (pause > 0f)
                {
                    float tp = 0f;
                    while (tp < pause)
                    {
                        tp += Time.unscaledDeltaTime;
                        yield return null;
                    }
                }

                int nextLevel = Mathf.Max(1, step.Level + 1);
                if (award.MaxLevel > 0)
                {
                    nextLevel = Mathf.Min(nextLevel, award.MaxLevel);
                }

                view.LevelText.text = nextLevel.ToString();

                bool reachedMaxNow = step.ReachedMaxLevelAtEnd || (award.MaxLevel > 0 && nextLevel >= award.MaxLevel && award.ReachedMaxLevel);
                if (reachedMaxNow)
                {
                    ApplyInstantState(view, nextLevel, 0, 0, award.MaxLevel, isMax: true);
                    yield break;
                }

                int nextToNext = 0;
                if (i + 1 < steps.Length)
                {
                    nextToNext = steps[i + 1].XpToNext;
                }
                if (nextToNext <= 0)
                {
                    nextToNext = award.XpToNextAfter;
                }

                view.Slider.SetValueWithoutNotify(0f);
                if (nextToNext > 0)
                {
                    view.XpText.text = string.Format(_xpProgressFormat ?? "{0} / {1}", 0, nextToNext);
                }
                else
                {
                    view.XpText.text = string.Empty;
                }
            }

            ApplyInstantState(view, award.LevelAfter, award.XpAfter, award.XpToNextAfter, award.MaxLevel, isMax: award.ReachedMaxLevel);

            if (_finalHoldSeconds > 0f)
            {
                float t = 0f;
                while (t < _finalHoldSeconds)
                {
                    t += Time.unscaledDeltaTime;
                    yield return null;
                }
            }
        }

        private float GetXpPerSecond(int xpApplied)
        {
            float x = Mathf.Max(0, xpApplied);
            float t = _xpForMaxSpeed > 0f ? Mathf.Clamp01(x / _xpForMaxSpeed) : 1f;
            // Sublinear ramp: large awards animate faster overall while small awards stay readable.
            t = Mathf.Sqrt(t);
            return Mathf.Lerp(_xpPerSecondSmall, _xpPerSecondLarge, t);
        }

        private float GetLevelUpPauseSeconds(BattleXpAwardResult.UnitAward award)
        {
            int levels = Mathf.Max(0, award.LevelAfter - award.LevelBefore);
            if (levels <= 1)
            {
                return Mathf.Max(0f, _levelUpPauseSeconds);
            }

            // Many level-ups: keep the "ding" moment but shorten pauses.
            float divisor = 1f + (levels - 1) * 0.35f;
            return Mathf.Max(0f, _levelUpPauseSeconds) / divisor;
        }
    }
}
