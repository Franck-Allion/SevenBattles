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
            SetOverlayVisible(true, 0f);

            yield return FadeTo(1f, fadeOutDuration);

            if (preloadManifest != null)
            {
                var preloader = new ScenePreloader();
                Task<PreloadResult[]> preloadTask = preloader.RunAllAsync(preloadManifest, destroyCancellationToken);
                while (!preloadTask.IsCompleted)
                {
                    yield return null;
                }

                int completedTaskCount = 0;
                if (preloadTask.Status == TaskStatus.RanToCompletion && preloadTask.Result != null)
                {
                    completedTaskCount = preloadTask.Result.Length;
                }

                if (completedTaskCount > 0)
                {
                    SBLog.Info($"[Preload] Completed {completedTaskCount} task(s).", this);
                }
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
    }
}
