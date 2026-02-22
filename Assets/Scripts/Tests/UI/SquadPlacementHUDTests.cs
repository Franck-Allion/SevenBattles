using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SevenBattles.Core;
using SevenBattles.UI;
using UnityEngine.TestTools;
using System.Collections;
using UnityEngine.EventSystems;

namespace SevenBattles.Tests.UI
{
    public class SquadPlacementHUDTests
    {
#pragma warning disable CS0067
        private class FakePlacementController : MonoBehaviour, ISquadPlacementController
        {
            public int SquadSize { get; set; } = 3;
            public bool IsReady { get; set; }
            public bool IsLocked { get; private set; }
            public int[] Levels = new[] { 1, 2, 3 };
            public string[] DisplayNames = new[] { "Unit-1", "Unit-2", "Unit-3" };

            public bool IsPlaced(int index) => false;
            public Sprite GetPortrait(int index) => null;
            public int GetLevel(int index)
            {
                if (Levels != null && index >= 0 && index < Levels.Length)
                {
                    return Levels[index];
                }
                return 1;
            }

            public string GetDisplayName(int index)
            {
                if (DisplayNames != null && index >= 0 && index < DisplayNames.Length)
                {
                    return DisplayNames[index];
                }

                return string.Empty;
            }

            public void SelectWizard(int index) { WizardSelected?.Invoke(index); }
            public void ConfirmAndLock()
            {
                IsLocked = true;
                PlacementLocked?.Invoke();
            }

            public event System.Action<int> WizardSelected;
            public event System.Action<int> WizardPlaced;
            public event System.Action<int> WizardRemoved;
            public event System.Action<bool> ReadyChanged;
            public event System.Action PlacementLocked;

            public void FireReady(bool ready)
            {
                IsReady = ready;
                ReadyChanged?.Invoke(ready);
            }
        }
#pragma warning restore CS0067

        [Test]
        public void Instructions_AreVisible_DuringPlacement_AndHidden_WhenLocked()
        {
            // HUD root with a child TMP_Text used as the instructions label
            var hudGo = new GameObject("HUD");
            var hud = hudGo.AddComponent<SquadPlacementHUD>();

            var instructionsGo = new GameObject("Instructions");
            instructionsGo.transform.SetParent(hudGo.transform);
            var tmp = instructionsGo.AddComponent<TextMeshProUGUI>();

            // Fake controller
            var ctrlGo = new GameObject("FakeCtrl");
            var fake = ctrlGo.AddComponent<FakePlacementController>();

            // Inject private fields via reflection helpers
            SetPrivate(hud, "_controllerBehaviour", fake);
            SetPrivate(hud, "_instructionsTMP", tmp);

            // Simulate lifecycle
            CallPrivate(hud, "Awake");
            CallPrivate(hud, "OnEnable");

            // During placement (not locked), instructions should be visible
            Assert.IsTrue(instructionsGo.activeSelf, "Instructions should be visible during placement");

            // Lock placement
            fake.ConfirmAndLock();

            // After locking, the instructions label should be hidden immediately (HUD may remain active for fade)
            Assert.IsFalse(instructionsGo.activeSelf,
                "Instructions should be hidden after placement is locked");

            Object.DestroyImmediate(hudGo);
            Object.DestroyImmediate(ctrlGo);
        }

        [Test]
        public void LevelLabels_ShowDuringPlacement()
        {
            var hudGo = new GameObject("HUD");
            var hud = hudGo.AddComponent<SquadPlacementHUD>();

            var buttonGo = new GameObject("PortraitButton");
            buttonGo.transform.SetParent(hudGo.transform);
            var button = buttonGo.AddComponent<Button>();
            var portrait = buttonGo.AddComponent<Image>();
            var tex = new Texture2D(2, 2);
            portrait.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));

            var levelGo = new GameObject("LevelText");
            levelGo.transform.SetParent(hudGo.transform);
            var levelText = levelGo.AddComponent<TextMeshProUGUI>();

            var ctrlGo = new GameObject("FakeCtrl");
            var fake = ctrlGo.AddComponent<FakePlacementController>();
            fake.SquadSize = 1;
            fake.Levels = new[] { 2 };

            SetPrivate(hud, "_controllerBehaviour", fake);
            SetPrivate(hud, "_portraitButtons", new[] { button });
            SetPrivate(hud, "_levelTexts", new[] { levelText });

            CallPrivate(hud, "Awake");
            CallPrivate(hud, "OnEnable");

            Assert.AreEqual("2", levelText.text);
            Assert.IsTrue(levelGo.activeSelf, "Level label should be visible during placement.");

            Object.DestroyImmediate(hudGo);
            Object.DestroyImmediate(ctrlGo);
        }

        [UnityTest]
        public IEnumerator StartButton_FadesAndHides_OnPlacementLocked()
        {
            // HUD root
            var hudGo = new GameObject("HUD");
            var hud = hudGo.AddComponent<SquadPlacementHUD>();

            // Battle HUD root (initially inactive)
            var battleHudGo = new GameObject("BattleHUD");
            battleHudGo.SetActive(false);
            SetPrivate(hud, "_battleHudRoot", battleHudGo);

            // Start button with CanvasGroup
            var startGo = new GameObject("StartButton");
            startGo.transform.SetParent(hudGo.transform);
            var btn = startGo.AddComponent<Button>();
            var cg = startGo.AddComponent<CanvasGroup>();
            cg.alpha = 1f;
            cg.interactable = true;
            cg.blocksRaycasts = true;

            // Minimal Text to satisfy potential lookups
            var label = new GameObject("Label").AddComponent<TextMeshProUGUI>();
            label.transform.SetParent(startGo.transform);

            // Fake controller
            var ctrlGo = new GameObject("FakeCtrl");
            var fake = ctrlGo.AddComponent<FakePlacementController>();
            fake.IsReady = true;

            // Inject fields
            SetPrivate(hud, "_controllerBehaviour", fake);
            SetPrivate(hud, "_startBattleButton", btn);
            SetPrivate(hud, "_startButtonTMP", label);
            SetPrivate(hud, "_startButtonCanvasGroup", cg);
            // Speed up the test fade duration
            SetPrivate(hud, "_startButtonFadeDuration", 0.1f);

            // Lifecycle
            CallPrivate(hud, "Awake");
            CallPrivate(hud, "OnEnable");

            // Ready makes button visible and interactable
            fake.FireReady(true);
            Assert.IsTrue(startGo.activeSelf, "Start button should be active when ready");
            Assert.IsTrue(btn.interactable, "Start button should be interactable when ready");
            Assert.IsFalse(battleHudGo.activeSelf, "Battle HUD should remain inactive during placement");

            // Lock triggers fade, should disable interaction immediately
            fake.ConfirmAndLock();
            Assert.IsFalse(btn.interactable, "Start button must become non-interactable on fade start");
            Assert.IsFalse(cg.blocksRaycasts, "Start button must stop blocking raycasts on fade start");

            // Wait for fade to complete
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.IsFalse(startGo.activeSelf, "Start button should be hidden after fade completes");
            Assert.IsFalse(battleHudGo.activeSelf, "Battle HUD should remain inactive; world bootstrap controls its activation");

            Object.DestroyImmediate(hudGo);
            Object.DestroyImmediate(battleHudGo);
            Object.DestroyImmediate(ctrlGo);
        }

        [Test]
        public void PortraitHover_ShowsAndHides_NameTooltip()
        {
            var eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var hudGo = new GameObject("HUD");
            hudGo.transform.SetParent(canvasGo.transform, false);
            var hud = hudGo.AddComponent<SquadPlacementHUD>();

            var portraitGo = new GameObject("PortraitButton", typeof(RectTransform), typeof(Image), typeof(Button));
            portraitGo.transform.SetParent(hudGo.transform, false);
            var portraitButton = portraitGo.GetComponent<Button>();
            var portraitImage = portraitGo.GetComponent<Image>();
            var texture = new Texture2D(2, 2);
            portraitImage.sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));

            var ctrlGo = new GameObject("FakeCtrl");
            var fake = ctrlGo.AddComponent<FakePlacementController>();
            fake.SquadSize = 1;
            fake.DisplayNames = new[] { "Nova" };

            SetPrivate(hud, "_controllerBehaviour", fake);
            SetPrivate(hud, "_portraitButtons", new[] { portraitButton });
            SetPrivate(hud, "_portraitImages", new[] { portraitImage });
            SetPrivate(hud, "_enableNameTooltip", true);
            SetPrivate(hud, "_nameTooltipShowDelaySeconds", 0f);

            CallPrivate(hud, "Awake");
            CallPrivate(hud, "OnEnable");

            var enterData = new PointerEventData(EventSystem.current);
            ExecuteEvents.Execute<IPointerEnterHandler>(
                portraitGo,
                enterData,
                (handler, data) => handler.OnPointerEnter((PointerEventData)data));

            var tooltipCanvasGroup = GetPrivate<CanvasGroup>(hud, "_nameTooltipCanvasGroup");
            Assert.IsNotNull(tooltipCanvasGroup, "Expected tooltip canvas group to be created at runtime.");
            Assert.Greater(tooltipCanvasGroup.alpha, 0.001f, "Tooltip should be visible on hover enter.");

            var tooltipLabel = GetPrivate<TMP_Text>(hud, "_nameTooltipLabel");
            Assert.IsNotNull(tooltipLabel, "Expected tooltip label to be created at runtime.");
            Assert.AreEqual("Nova", tooltipLabel.text);

            var exitData = new PointerEventData(EventSystem.current);
            ExecuteEvents.Execute<IPointerExitHandler>(
                portraitGo,
                exitData,
                (handler, data) => handler.OnPointerExit((PointerEventData)data));

            Assert.LessOrEqual(tooltipCanvasGroup.alpha, 0.001f, "Tooltip should be hidden on hover exit.");

            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(eventSystemGo);
            Object.DestroyImmediate(canvasGo);
            Object.DestroyImmediate(ctrlGo);
        }

        [Test]
        public void PortraitHover_RespectsConfiguredDelay()
        {
            var eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var hudGo = new GameObject("HUD");
            hudGo.transform.SetParent(canvasGo.transform, false);
            var hud = hudGo.AddComponent<SquadPlacementHUD>();

            var portraitGo = new GameObject("PortraitButton", typeof(RectTransform), typeof(Image), typeof(Button));
            portraitGo.transform.SetParent(hudGo.transform, false);
            var portraitButton = portraitGo.GetComponent<Button>();
            var portraitImage = portraitGo.GetComponent<Image>();
            var texture = new Texture2D(2, 2);
            portraitImage.sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));

            var ctrlGo = new GameObject("FakeCtrl");
            var fake = ctrlGo.AddComponent<FakePlacementController>();
            fake.SquadSize = 1;
            fake.DisplayNames = new[] { "Nova" };

            SetPrivate(hud, "_controllerBehaviour", fake);
            SetPrivate(hud, "_portraitButtons", new[] { portraitButton });
            SetPrivate(hud, "_portraitImages", new[] { portraitImage });
            SetPrivate(hud, "_enableNameTooltip", true);
            SetPrivate(hud, "_nameTooltipShowDelaySeconds", 1f);

            CallPrivate(hud, "Awake");
            CallPrivate(hud, "OnEnable");

            var enterData = new PointerEventData(EventSystem.current);
            ExecuteEvents.Execute<IPointerEnterHandler>(
                portraitGo,
                enterData,
                (handler, data) => handler.OnPointerEnter((PointerEventData)data));

            var tooltipCanvasGroup = GetPrivate<CanvasGroup>(hud, "_nameTooltipCanvasGroup");
            Assert.IsNotNull(tooltipCanvasGroup, "Expected tooltip canvas group to be created at runtime.");
            Assert.LessOrEqual(tooltipCanvasGroup.alpha, 0.001f, "Tooltip should stay hidden until delay elapses.");
            Assert.IsTrue(GetPrivateValue<bool>(hud, "_nameTooltipShowPending"), "Tooltip show should be pending.");

            SetPrivate(hud, "_pendingTooltipShowTime", Time.unscaledTime - 0.01f);
            CallPrivate(hud, "LateUpdate");

            Assert.Greater(tooltipCanvasGroup.alpha, 0.001f, "Tooltip should become visible after pending delay expires.");

            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(eventSystemGo);
            Object.DestroyImmediate(canvasGo);
            Object.DestroyImmediate(ctrlGo);
        }

        [Test]
        public void PortraitHover_RespectsConfiguredMinWidthAndMinHeight()
        {
            var eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var hudGo = new GameObject("HUD");
            hudGo.transform.SetParent(canvasGo.transform, false);
            var hud = hudGo.AddComponent<SquadPlacementHUD>();

            var portraitGo = new GameObject("PortraitButton", typeof(RectTransform), typeof(Image), typeof(Button));
            portraitGo.transform.SetParent(hudGo.transform, false);
            var portraitButton = portraitGo.GetComponent<Button>();
            var portraitImage = portraitGo.GetComponent<Image>();
            var texture = new Texture2D(2, 2);
            portraitImage.sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));

            var ctrlGo = new GameObject("FakeCtrl");
            var fake = ctrlGo.AddComponent<FakePlacementController>();
            fake.SquadSize = 1;
            fake.DisplayNames = new[] { "A" };

            SetPrivate(hud, "_controllerBehaviour", fake);
            SetPrivate(hud, "_portraitButtons", new[] { portraitButton });
            SetPrivate(hud, "_portraitImages", new[] { portraitImage });
            SetPrivate(hud, "_enableNameTooltip", true);
            SetPrivate(hud, "_nameTooltipShowDelaySeconds", 0f);
            SetPrivate(hud, "_nameTooltipMinWidth", 260f);
            SetPrivate(hud, "_nameTooltipMinHeight", 96f);
            SetPrivate(hud, "_nameTooltipMaxWidth", 420f);

            CallPrivate(hud, "Awake");
            CallPrivate(hud, "OnEnable");

            var enterData = new PointerEventData(EventSystem.current);
            ExecuteEvents.Execute<IPointerEnterHandler>(
                portraitGo,
                enterData,
                (handler, data) => handler.OnPointerEnter((PointerEventData)data));

            var tooltipRect = GetPrivate<RectTransform>(hud, "_nameTooltipRect");
            Assert.IsNotNull(tooltipRect, "Expected tooltip rect to be created at runtime.");
            Assert.GreaterOrEqual(tooltipRect.rect.width, 260f, "Tooltip width should honor _nameTooltipMinWidth.");
            Assert.GreaterOrEqual(tooltipRect.rect.height, 96f, "Tooltip height should honor _nameTooltipMinHeight.");

            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(eventSystemGo);
            Object.DestroyImmediate(canvasGo);
            Object.DestroyImmediate(ctrlGo);
        }

        [Test]
        public void PortraitHover_RespectsConfiguredOffsetXAndOffsetY()
        {
            var eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var hudGo = new GameObject("HUD");
            hudGo.transform.SetParent(canvasGo.transform, false);
            var hud = hudGo.AddComponent<SquadPlacementHUD>();

            var portraitGo = new GameObject("PortraitButton", typeof(RectTransform), typeof(Image), typeof(Button));
            portraitGo.transform.SetParent(hudGo.transform, false);
            var portraitButton = portraitGo.GetComponent<Button>();
            var portraitImage = portraitGo.GetComponent<Image>();
            var texture = new Texture2D(2, 2);
            portraitImage.sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));

            var ctrlGo = new GameObject("FakeCtrl");
            var fake = ctrlGo.AddComponent<FakePlacementController>();
            fake.SquadSize = 1;
            fake.DisplayNames = new[] { "Nova" };

            SetPrivate(hud, "_controllerBehaviour", fake);
            SetPrivate(hud, "_portraitButtons", new[] { portraitButton });
            SetPrivate(hud, "_portraitImages", new[] { portraitImage });
            SetPrivate(hud, "_enableNameTooltip", true);
            SetPrivate(hud, "_nameTooltipShowDelaySeconds", 0f);
            SetPrivate(hud, "_nameTooltipEdgePadding", Vector2.zero);
            SetPrivate(hud, "_nameTooltipOffsetX", 0f);
            SetPrivate(hud, "_nameTooltipOffsetY", 0f);

            CallPrivate(hud, "Awake");
            CallPrivate(hud, "OnEnable");

            var enterData = new PointerEventData(EventSystem.current);
            ExecuteEvents.Execute<IPointerEnterHandler>(
                portraitGo,
                enterData,
                (handler, data) => handler.OnPointerEnter((PointerEventData)data));

            var tooltipRect = GetPrivate<RectTransform>(hud, "_nameTooltipRect");
            Assert.IsNotNull(tooltipRect, "Expected tooltip rect to be created at runtime.");

            // Use a very large positioning rect to avoid edge clamping and isolate offset behavior.
            var boundsGo = new GameObject("HugeBounds", typeof(RectTransform));
            var boundsRect = boundsGo.GetComponent<RectTransform>();
            boundsRect.anchorMin = new Vector2(0.5f, 0.5f);
            boundsRect.anchorMax = new Vector2(0.5f, 0.5f);
            boundsRect.pivot = new Vector2(0.5f, 0.5f);
            boundsRect.sizeDelta = new Vector2(200000f, 200000f);
            SetPrivate(hud, "_tooltipCanvasRect", boundsRect);

            CallPrivate(hud, "UpdateNameTooltipPosition");
            Vector3 originPosition = tooltipRect.position;

            SetPrivate(hud, "_nameTooltipOffsetX", 120f);
            SetPrivate(hud, "_nameTooltipOffsetY", -80f);
            CallPrivate(hud, "UpdateNameTooltipPosition");
            Vector3 shiftedPosition = tooltipRect.position;

            Vector3 delta = shiftedPosition - originPosition;
            Assert.AreEqual(120f, delta.x, 0.01f, "Tooltip X position should reflect _nameTooltipOffsetX.");
            Assert.AreEqual(-80f, delta.y, 0.01f, "Tooltip Y position should reflect _nameTooltipOffsetY.");

            Object.DestroyImmediate(boundsGo);
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(eventSystemGo);
            Object.DestroyImmediate(canvasGo);
            Object.DestroyImmediate(ctrlGo);
        }

        private static void SetPrivate(object obj, string field, object value)
        {
            var fi = obj.GetType().GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            fi.SetValue(obj, value);
        }

        private static T GetPrivate<T>(object obj, string field) where T : class
        {
            var fi = obj.GetType().GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return fi.GetValue(obj) as T;
        }

        private static T GetPrivateValue<T>(object obj, string field)
        {
            var fi = obj.GetType().GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return (T)fi.GetValue(obj);
        }

        private static void CallPrivate(object obj, string method)
        {
            var mi = obj.GetType().GetMethod(method, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            mi.Invoke(obj, null);
        }
    }
}
