using System.Collections;
using System.Threading.Tasks;
using SevenBattles.Core.Diagnostics;
using SevenBattles.Core.Preload;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SevenBattles.Core
{
    public sealed class SceneTransitionFader : MonoBehaviour
    {
        private const string FaderObjectName = "SceneTransitionFader";
        private const int SortingOrder = 5000;

        private static SceneTransitionFader _instance;

        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private Image _fadeImage;

        private Coroutine _transitionRoutine;

        public static bool TryStartTransition(
            string sceneName,
            ScenePreloadManifest preloadManifest,
            float fadeOutDuration,
            float fadeInDuration,
            Color fadeColor,
            System.Action onFailed = null)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                SBLog.Error("SceneTransitionFader: Scene name is empty. Aborting transition.");
                onFailed?.Invoke();
                return false;
            }

            SceneTransitionFader instance = EnsureInstance();
            return instance.Begin(sceneName, preloadManifest, fadeOutDuration, fadeInDuration, fadeColor, onFailed);
        }

        private static SceneTransitionFader EnsureInstance()
        {
            if (_instance != null)
            {
                return _instance;
            }

            var faderObject = new GameObject(FaderObjectName);
            DontDestroyOnLoad(faderObject);

            var fader = faderObject.AddComponent<SceneTransitionFader>();
            fader.BuildOverlay();
            _instance = fader;

            return fader;
        }

        private bool Begin(
            string sceneName,
            ScenePreloadManifest preloadManifest,
            float fadeOutDuration,
            float fadeInDuration,
            Color fadeColor,
            System.Action onFailed)
        {
            if (_transitionRoutine != null)
            {
                SBLog.Warn("SceneTransitionFader: Transition already in progress.", this);
                onFailed?.Invoke();
                return false;
            }

            if (_fadeImage != null)
            {
                _fadeImage.color = fadeColor;
            }

            _transitionRoutine = StartCoroutine(TransitionRoutine(
                sceneName,
                preloadManifest,
                fadeOutDuration,
                fadeInDuration,
                onFailed));
            return true;
        }

        private IEnumerator TransitionRoutine(
            string sceneName,
            ScenePreloadManifest preloadManifest,
            float fadeOutDuration,
            float fadeInDuration,
            System.Action onFailed)
        {
            AssetCacheDiagnostics.Reset();
            RegisterManifestAssetsForDiagnostics(preloadManifest);
            SetOverlayVisible(true, 0f);

            yield return FadeTo(1f, fadeOutDuration);

            if (preloadManifest != null)
            {
                SBLog.Info($"[Preload] Starting manifest '{preloadManifest.name}' before loading scene '{sceneName}'.", this);
                var preloader = new ScenePreloader();
                Task<PreloadResult[]> preloadTask = preloader.RunAllAsync(preloadManifest, destroyCancellationToken);
                while (!preloadTask.IsCompleted)
                {
                    yield return null;
                }

                if (preloadTask.Status == TaskStatus.RanToCompletion)
                {
                    PreloadResult[] results = preloadTask.Result ?? System.Array.Empty<PreloadResult>();
                    int completedTaskCount = results.Length;
                    int failedTaskCount = CountFailedTasks(results);

                    if (completedTaskCount <= 0)
                    {
                        SBLog.Warn("[Preload] Manifest executed but produced zero tasks. Check manifest entries.", this);
                    }
                    else if (failedTaskCount > 0)
                    {
                        SBLog.Warn($"[Preload] Completed {completedTaskCount} task(s) with {failedTaskCount} failure(s).", this);
                    }
                    else
                    {
                        SBLog.Info($"[Preload] Completed {completedTaskCount} task(s) successfully.", this);
                    }
                }
                else if (preloadTask.IsCanceled || preloadTask.Status == TaskStatus.Canceled)
                {
                    SBLog.Warn("[Preload] Manifest execution was canceled.", this);
                }
                else if (preloadTask.IsFaulted || preloadTask.Status == TaskStatus.Faulted)
                {
                    string errorMessage = preloadTask.Exception != null && preloadTask.Exception.GetBaseException() != null
                        ? preloadTask.Exception.GetBaseException().Message
                        : "Unknown preload error.";
                    SBLog.Error($"[Preload] Manifest execution faulted: {errorMessage}", this);
                }
            }
            else
            {
                SBLog.Warn($"[Preload] No ScenePreloadManifest provided before loading scene '{sceneName}'.", this);
            }

            var loadOp = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (loadOp == null)
            {
                SBLog.Error($"SceneTransitionFader: Failed to load scene '{sceneName}'.", this);
                yield return FadeTo(0f, fadeInDuration);
                SetOverlayVisible(false, 0f);
                _transitionRoutine = null;
                onFailed?.Invoke();
                CleanupIfIdle();
                yield break;
            }

            while (!loadOp.isDone)
            {
                yield return null;
            }

            yield return FadeTo(0f, fadeInDuration);
            SetOverlayVisible(false, 0f);

            _transitionRoutine = null;
            CleanupIfIdle();
        }

        private IEnumerator FadeTo(float targetAlpha, float duration)
        {
            if (_canvasGroup == null)
            {
                yield break;
            }

            float startAlpha = _canvasGroup.alpha;
            float fadeDuration = Mathf.Max(0.01f, duration);
            float t = 0f;

            while (t < fadeDuration)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / fadeDuration);
                float eased = p * p * (3f - 2f * p);
                _canvasGroup.alpha = Mathf.LerpUnclamped(startAlpha, targetAlpha, eased);
                yield return null;
            }

            _canvasGroup.alpha = targetAlpha;
        }

        private void SetOverlayVisible(bool visible, float alpha)
        {
            if (_canvasGroup == null)
            {
                return;
            }

            _canvasGroup.gameObject.SetActive(true);
            _canvasGroup.alpha = Mathf.Clamp01(alpha);
            _canvasGroup.blocksRaycasts = visible;
            _canvasGroup.interactable = false;
        }

        private void BuildOverlay()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;

            gameObject.AddComponent<GraphicRaycaster>();

            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;

            var imageObject = new GameObject("FadeOverlay");
            imageObject.transform.SetParent(transform, false);

            var rect = imageObject.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            _fadeImage = imageObject.AddComponent<Image>();
            _fadeImage.color = Color.black;
        }

        private void CleanupIfIdle()
        {
            if (_transitionRoutine != null)
            {
                return;
            }

            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private static int CountFailedTasks(PreloadResult[] results)
        {
            if (results == null || results.Length == 0)
            {
                return 0;
            }

            int failed = 0;
            for (int i = 0; i < results.Length; i++)
            {
                if (!results[i].Success)
                {
                    failed++;
                }
            }

            return failed;
        }

        private static void RegisterManifestAssetsForDiagnostics(ScenePreloadManifest manifest)
        {
            if (manifest == null)
            {
                return;
            }

            AssetCacheDiagnostics.RegisterManifestAssets(manifest.PrefabsToWarm);
            AssetCacheDiagnostics.RegisterManifestAssets(manifest.AudioClips);
            AssetCacheDiagnostics.RegisterManifestAssets(manifest.Sprites);
            AssetCacheDiagnostics.RegisterManifestAssets(manifest.Textures);
        }
    }
}
