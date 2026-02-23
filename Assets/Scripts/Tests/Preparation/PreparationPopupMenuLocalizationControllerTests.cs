using System;
using System.Collections.Generic;
using NUnit.Framework;
using SevenBattles.Core;
using SevenBattles.Core.Battle;
using SevenBattles.Core.Units;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SevenBattles.Preparation;
using Object = UnityEngine.Object;

namespace SevenBattles.Tests.Preparation
{
    public class PreparationPopupMenuLocalizationControllerTests
    {
        private sealed class FakeSquadSetupController : ISquadSetupController
        {
            public int MaxSquadSize => 1;
            public int ActiveSquadCount => 1;
            public bool IsSquadFull => true;
            public UnitSpellLoadout SelectedUnit { get; set; }
            public IReadOnlyList<UnitSpellLoadout> AllAvailableUnits => Array.Empty<UnitSpellLoadout>();
            public IReadOnlyList<UnitSpellLoadout> ActiveSquad => Array.Empty<UnitSpellLoadout>();
            public string DisplayNameToReturn { get; set; } = string.Empty;

            public bool TryAddToSquad(UnitSpellLoadout loadout) => false;
            public bool TryRemoveFromSquad(UnitSpellLoadout loadout) => false;
            public void SelectUnit(UnitSpellLoadout loadout) => SelectedUnit = loadout;
            public string ResolveDisplayName(UnitSpellLoadout loadout) => DisplayNameToReturn;

            public event Action<UnitSpellLoadout> UnitAddedToSquad { add { } remove { } }
            public event Action<UnitSpellLoadout> UnitRemovedFromSquad { add { } remove { } }
            public event Action SquadChanged { add { } remove { } }
            public event Action<UnitSpellLoadout> UnitSelected { add { } remove { } }
        }

        private static void CallPrivate(object target, string methodName)
        {
            var type = target.GetType();
            var method = type.GetMethod(methodName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Method '{methodName}' was not found on type '{type.FullName}'.");
            method.Invoke(target, null);
        }

        private static T GetPrivate<T>(object target, string fieldName)
        {
            var type = target.GetType();
            var field = type.GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' was not found on type '{type.FullName}'.");
            return (T)field.GetValue(target);
        }

        private static void SetPrivate(object target, string fieldName, object value)
        {
            var type = target.GetType();
            var field = type.GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' was not found on type '{type.FullName}'.");
            field.SetValue(target, value);
        }

        private static GameObject CreateButton(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static TMP_Text CreateStatValueRow(Transform parent, string rowName)
        {
            var row = new GameObject(rowName, typeof(RectTransform));
            row.transform.SetParent(parent, false);

            var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(row.transform, false);

            var value = new GameObject("Value", typeof(RectTransform), typeof(TextMeshProUGUI));
            value.transform.SetParent(row.transform, false);
            return value.GetComponent<TextMeshProUGUI>();
        }

        private static bool HasHoverForwarder(GameObject go)
        {
            var components = go.GetComponents<MonoBehaviour>();
            for (int i = 0; i < components.Length; i++)
            {
                var component = components[i];
                if (component != null && component.GetType().Name == "MenuButtonHoverForwarder")
                {
                    return true;
                }
            }

            return false;
        }

        [Test]
        public void WireHoverFeedback_AutoFindsShopAndSquadButtons_AndAddsForwarders()
        {
            var root = new GameObject("PopupMenu");
            var controller = root.AddComponent<PreparationPopupMenuLocalizationController>();
            var shopButton = CreateButton(root.transform, "ShopButtonMenu");
            var squadButton = CreateButton(root.transform, "SquadButtonMenu");

            CallPrivate(controller, "ResolveButtonTargets");
            CallPrivate(controller, "WireHoverFeedback");

            Assert.IsTrue(HasHoverForwarder(shopButton));
            Assert.IsTrue(HasHoverForwarder(squadButton));

            Object.DestroyImmediate(root);
        }

        [Test]
        public void HandleMenuButtonPointerExit_NeverDropsHoveredCountBelowZero()
        {
            var root = new GameObject("PopupMenu");
            var controller = root.AddComponent<PreparationPopupMenuLocalizationController>();

            CallPrivate(controller, "HandleMenuButtonPointerEnter");
            CallPrivate(controller, "HandleMenuButtonPointerExit");
            CallPrivate(controller, "HandleMenuButtonPointerExit");

            int hoveredCount = GetPrivate<int>(controller, "_hoveredButtonCount");
            Assert.AreEqual(0, hoveredCount);

            Object.DestroyImmediate(root);
        }

        [Test]
        public void ButtonClick_PlaysConfiguredSfxPath()
        {
            var root = new GameObject("PopupMenu");
            var controller = root.AddComponent<PreparationPopupMenuLocalizationController>();
            var shopButtonGo = CreateButton(root.transform, "ShopButtonMenu");
            CreateButton(root.transform, "SquadButtonMenu");

            var clickClip = AudioClip.Create("TestClick", 64, 1, 44100, false);
            SetPrivate(controller, "_clickSfxClip", clickClip);

            CallPrivate(controller, "ResolveButtonTargets");
            CallPrivate(controller, "WireHoverFeedback");

            float before = GetPrivate<float>(controller, "_lastClickSfxTime");
            shopButtonGo.GetComponent<Button>().onClick.Invoke();
            float after = GetPrivate<float>(controller, "_lastClickSfxTime");

            Assert.Greater(after, before);

            Object.DestroyImmediate(clickClip);
            Object.DestroyImmediate(root);
        }

        [Test]
        public void SquadButtonClick_ShowsSquadPanel_AndEnablesPanelInteraction()
        {
            var root = new GameObject("PopupMenu");
            var controller = root.AddComponent<PreparationPopupMenuLocalizationController>();
            var squadButtonGo = CreateButton(root.transform, "SquadButtonMenu");
            var squadPanel = new GameObject("SquadPanel", typeof(RectTransform));
            squadPanel.transform.SetParent(root.transform, false);
            squadPanel.SetActive(false);

            SetPrivate(controller, "_squadPanelFadeDuration", 0f);

            CallPrivate(controller, "ResolveButtonTargets");
            CallPrivate(controller, "ResolveSquadPanel");
            CallPrivate(controller, "WireSquadPanelButton");

            squadButtonGo.GetComponent<Button>().onClick.Invoke();

            Assert.IsTrue(squadPanel.activeSelf);

            var canvasGroup = squadPanel.GetComponent<CanvasGroup>();
            Assert.IsNotNull(canvasGroup);
            Assert.AreEqual(1f, canvasGroup.alpha, 0.001f);
            Assert.IsTrue(canvasGroup.interactable);
            Assert.IsTrue(canvasGroup.blocksRaycasts);

            Object.DestroyImmediate(root);
        }

        [Test]
        public void ResolveSquadPanel_ForceHidesInitiallyActivePanel_UntilButtonClick()
        {
            var root = new GameObject("PopupMenu");
            var controller = root.AddComponent<PreparationPopupMenuLocalizationController>();
            var squadButtonGo = CreateButton(root.transform, "SquadButtonMenu");
            var squadPanel = new GameObject("SquadPanel", typeof(RectTransform));
            squadPanel.transform.SetParent(root.transform, false);
            squadPanel.SetActive(true);

            CallPrivate(controller, "ResolveButtonTargets");
            CallPrivate(controller, "ResolveSquadPanel");
            CallPrivate(controller, "WireSquadPanelButton");

            var canvasGroup = squadPanel.GetComponent<CanvasGroup>();
            Assert.IsNotNull(canvasGroup);
            Assert.IsFalse(squadPanel.activeSelf);
            Assert.AreEqual(0f, canvasGroup.alpha, 0.001f);
            Assert.IsFalse(canvasGroup.interactable);
            Assert.IsFalse(canvasGroup.blocksRaycasts);

            SetPrivate(controller, "_squadPanelFadeDuration", 0f);
            squadButtonGo.GetComponent<Button>().onClick.Invoke();

            Assert.IsTrue(squadPanel.activeSelf);
            Assert.AreEqual(1f, canvasGroup.alpha, 0.001f);
            Assert.IsTrue(canvasGroup.interactable);
            Assert.IsTrue(canvasGroup.blocksRaycasts);

            Object.DestroyImmediate(root);
        }

        [Test]
        public void BackButtonClick_HidesSquadPanel_AndDisablesPanelInteraction()
        {
            var root = new GameObject("PopupMenu");
            var controller = root.AddComponent<PreparationPopupMenuLocalizationController>();
            var squadButtonGo = CreateButton(root.transform, "SquadButtonMenu");
            var squadPanel = new GameObject("SquadPanel", typeof(RectTransform));
            squadPanel.transform.SetParent(root.transform, false);
            squadPanel.SetActive(true);
            var backButtonGo = CreateButton(squadPanel.transform, "Button_Back");

            SetPrivate(controller, "_squadPanelFadeDuration", 0f);

            CallPrivate(controller, "ResolveButtonTargets");
            CallPrivate(controller, "ResolveSquadPanel");
            CallPrivate(controller, "WireSquadPanelButton");

            squadButtonGo.GetComponent<Button>().onClick.Invoke();
            Assert.IsTrue(squadPanel.activeSelf);

            backButtonGo.GetComponent<Button>().onClick.Invoke();

            var canvasGroup = squadPanel.GetComponent<CanvasGroup>();
            Assert.IsNotNull(canvasGroup);
            Assert.IsFalse(squadPanel.activeSelf);
            Assert.AreEqual(0f, canvasGroup.alpha, 0.001f);
            Assert.IsFalse(canvasGroup.interactable);
            Assert.IsFalse(canvasGroup.blocksRaycasts);

            Object.DestroyImmediate(root);
        }

        [Test]
        public void RefreshInventorySelectedUnitPreview_UpdatesSelectedUnitNameLabel()
        {
            var root = new GameObject("PopupMenu");
            var controller = root.AddComponent<PreparationPopupMenuLocalizationController>();

            var inventoryPanel = new GameObject("InventoryPanel", typeof(RectTransform));
            inventoryPanel.transform.SetParent(root.transform, false);

            var inventoryView = new GameObject("InventoryView", typeof(RectTransform));
            inventoryView.transform.SetParent(inventoryPanel.transform, false);

            var character = new GameObject("Character", typeof(RectTransform));
            character.transform.SetParent(inventoryView.transform, false);

            var unitNameObject = new GameObject("UnitName", typeof(RectTransform), typeof(TextMeshProUGUI));
            unitNameObject.transform.SetParent(character.transform, false);
            var unitNameLabel = unitNameObject.GetComponent<TextMeshProUGUI>();

            var unitDefinition = ScriptableObject.CreateInstance<UnitDefinition>();
            unitDefinition.name = "Arcane Knight";
            unitDefinition.Id = "unit_arcane_knight";

            var selectedLoadout = new UnitSpellLoadout
            {
                Definition = unitDefinition,
                Level = 1
            };

            SetPrivate(controller, "_inventorySelectedLoadout", selectedLoadout);
            CallPrivate(controller, "RefreshInventorySelectedUnitPreview");

            Assert.AreEqual("Arcane Knight", unitNameLabel.text);

            Object.DestroyImmediate(unitDefinition);
            Object.DestroyImmediate(root);
        }

        [Test]
        public void RefreshInventorySelectedUnitPreview_UsesSquadSetupDisplayName_WhenAvailable()
        {
            var root = new GameObject("PopupMenu");
            var controller = root.AddComponent<PreparationPopupMenuLocalizationController>();

            var inventoryPanel = new GameObject("InventoryPanel", typeof(RectTransform));
            inventoryPanel.transform.SetParent(root.transform, false);

            var unitNameObject = new GameObject("UnitName", typeof(RectTransform), typeof(TextMeshProUGUI));
            unitNameObject.transform.SetParent(inventoryPanel.transform, false);
            var unitNameLabel = unitNameObject.GetComponent<TextMeshProUGUI>();

            var unitDefinition = ScriptableObject.CreateInstance<UnitDefinition>();
            unitDefinition.name = "Arcane Knight";
            unitDefinition.Id = "unit_arcane_knight";

            var selectedLoadout = new UnitSpellLoadout
            {
                Definition = unitDefinition,
                Level = 1
            };

            var fakeSquadSetup = new FakeSquadSetupController
            {
                SelectedUnit = selectedLoadout,
                DisplayNameToReturn = "Sir Nova"
            };

            SetPrivate(controller, "_resolvedSquadSetupController", fakeSquadSetup);
            CallPrivate(controller, "RefreshInventorySelectedUnitPreview");

            Assert.AreEqual("Sir Nova", unitNameLabel.text);

            Object.DestroyImmediate(unitDefinition);
            Object.DestroyImmediate(root);
        }

        [Test]
        public void RefreshInventorySelectedUnitPreview_UpdatesInventoryStatsWithSquadFormula()
        {
            var root = new GameObject("PopupMenu");
            var controller = root.AddComponent<PreparationPopupMenuLocalizationController>();

            var inventoryPanel = new GameObject("InventoryPanel", typeof(RectTransform));
            inventoryPanel.transform.SetParent(root.transform, false);

            var statsRoot = new GameObject("Stats", typeof(RectTransform));
            statsRoot.transform.SetParent(inventoryPanel.transform, false);

            TMP_Text lifeValue = CreateStatValueRow(statsRoot.transform, "Life");
            TMP_Text attackValue = CreateStatValueRow(statsRoot.transform, "Attack");
            TMP_Text shootValue = CreateStatValueRow(statsRoot.transform, "Shoot");
            TMP_Text spellValue = CreateStatValueRow(statsRoot.transform, "Spell");
            TMP_Text speedValue = CreateStatValueRow(statsRoot.transform, "Speed");
            TMP_Text luckValue = CreateStatValueRow(statsRoot.transform, "Luck");
            TMP_Text defenseValue = CreateStatValueRow(statsRoot.transform, "Defense");
            TMP_Text protectionValue = CreateStatValueRow(statsRoot.transform, "Protection");
            TMP_Text initiativeValue = CreateStatValueRow(statsRoot.transform, "Initiative");
            TMP_Text moraleValue = CreateStatValueRow(statsRoot.transform, "Morale");

            var unitDefinition = ScriptableObject.CreateInstance<UnitDefinition>();
            unitDefinition.BaseStats = new UnitStatsData
            {
                Life = 10,
                Attack = 20,
                Shoot = 30,
                Spell = 40,
                Speed = 50,
                Luck = 60,
                Defense = 70,
                Protection = 80,
                Initiative = 90,
                Morale = 100
            };
            unitDefinition.LevelBonus = new UnitLevelBonusData
            {
                Life = 1,
                Attack = 2,
                Shoot = 3,
                Spell = 4,
                Speed = 5,
                Luck = 6,
                Defense = 7,
                Protection = 8,
                Initiative = 9,
                Morale = 10
            };

            var selectedLoadout = new UnitSpellLoadout
            {
                Definition = unitDefinition,
                Level = 2
            };

            SetPrivate(controller, "_inventorySelectedLoadout", selectedLoadout);
            CallPrivate(controller, "RefreshInventorySelectedUnitPreview");

            Assert.AreEqual("12", lifeValue.text);
            Assert.AreEqual("24", attackValue.text);
            Assert.AreEqual("36", shootValue.text);
            Assert.AreEqual("48", spellValue.text);
            Assert.AreEqual("60", speedValue.text);
            Assert.AreEqual("72", luckValue.text);
            Assert.AreEqual("84", defenseValue.text);
            Assert.AreEqual("96", protectionValue.text);
            Assert.AreEqual("108", initiativeValue.text);
            Assert.AreEqual("120", moraleValue.text);

            Object.DestroyImmediate(unitDefinition);
            Object.DestroyImmediate(root);
        }

        [Test]
        public void RefreshInventorySelectedUnitPreview_UsesNestedValueText_AndDoesNotOverwriteLabel()
        {
            var root = new GameObject("PopupMenu");
            var controller = root.AddComponent<PreparationPopupMenuLocalizationController>();

            var inventoryPanel = new GameObject("InventoryPanel", typeof(RectTransform));
            inventoryPanel.transform.SetParent(root.transform, false);

            var statsRoot = new GameObject("Stats", typeof(RectTransform));
            statsRoot.transform.SetParent(inventoryPanel.transform, false);

            var lifeRow = new GameObject("Life", typeof(RectTransform));
            lifeRow.transform.SetParent(statsRoot.transform, false);

            var lifeLabelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            lifeLabelObject.transform.SetParent(lifeRow.transform, false);
            var lifeLabel = lifeLabelObject.GetComponent<TextMeshProUGUI>();
            lifeLabel.text = "Life";

            var valueContainer = new GameObject("Content", typeof(RectTransform));
            valueContainer.transform.SetParent(lifeRow.transform, false);
            var lifeValueObject = new GameObject("Value", typeof(RectTransform), typeof(TextMeshProUGUI));
            lifeValueObject.transform.SetParent(valueContainer.transform, false);
            var lifeValue = lifeValueObject.GetComponent<TextMeshProUGUI>();
            lifeValue.text = string.Empty;

            var unitDefinition = ScriptableObject.CreateInstance<UnitDefinition>();
            unitDefinition.BaseStats = new UnitStatsData { Life = 10 };
            unitDefinition.LevelBonus = new UnitLevelBonusData { Life = 1 };

            var selectedLoadout = new UnitSpellLoadout
            {
                Definition = unitDefinition,
                Level = 2
            };

            SetPrivate(controller, "_inventorySelectedLoadout", selectedLoadout);
            CallPrivate(controller, "RefreshInventorySelectedUnitPreview");

            Assert.AreEqual("Life", lifeLabel.text);
            Assert.AreEqual("12", lifeValue.text);

            Object.DestroyImmediate(unitDefinition);
            Object.DestroyImmediate(root);
        }

        [Test]
        public void RefreshLabels_LocalizesInventoryStatLabelObjects()
        {
            var root = new GameObject("PopupMenu");
            var controller = root.AddComponent<PreparationPopupMenuLocalizationController>();

            var inventoryPanel = new GameObject("InventoryPanel", typeof(RectTransform));
            inventoryPanel.transform.SetParent(root.transform, false);

            var statsRoot = new GameObject("Stats", typeof(RectTransform));
            statsRoot.transform.SetParent(inventoryPanel.transform, false);

            var lifeRow = new GameObject("Life", typeof(RectTransform));
            lifeRow.transform.SetParent(statsRoot.transform, false);
            var lifeLabelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            lifeLabelObject.transform.SetParent(lifeRow.transform, false);
            var lifeLabel = lifeLabelObject.GetComponent<TextMeshProUGUI>();
            lifeLabel.text = "OLD";
            var lifeValueObject = new GameObject("Value", typeof(RectTransform), typeof(TextMeshProUGUI));
            lifeValueObject.transform.SetParent(lifeRow.transform, false);

            CallPrivate(controller, "RefreshLabels");

            Assert.AreEqual("Life", lifeLabel.text);

            Object.DestroyImmediate(root);
        }

        [Test]
        public void RefreshInventorySelectedUnitPreview_UpdatesInventoryLevelAndXpProgression()
        {
            var root = new GameObject("PopupMenu");
            var controller = root.AddComponent<PreparationPopupMenuLocalizationController>();

            var inventoryPanel = new GameObject("InventoryPanel", typeof(RectTransform));
            inventoryPanel.transform.SetParent(root.transform, false);

            var levelTextObject = new GameObject("TextLevelNum", typeof(RectTransform), typeof(TextMeshProUGUI));
            levelTextObject.transform.SetParent(inventoryPanel.transform, false);
            var levelText = levelTextObject.GetComponent<TextMeshProUGUI>();

            var xpTextObject = new GameObject("TextExp", typeof(RectTransform), typeof(TextMeshProUGUI));
            xpTextObject.transform.SetParent(inventoryPanel.transform, false);
            var xpText = xpTextObject.GetComponent<TextMeshProUGUI>();

            var sliderObject = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            sliderObject.transform.SetParent(inventoryPanel.transform, false);
            var xpSlider = sliderObject.GetComponent<Slider>();

            var unitDefinition = ScriptableObject.CreateInstance<UnitDefinition>();
            unitDefinition.MaxLevel = 5;
            unitDefinition.XpToNextLevel = new[] { 100, 200, 300, 400 };

            var selectedLoadout = new UnitSpellLoadout
            {
                Definition = unitDefinition,
                Level = 2,
                Xp = 75
            };

            SetPrivate(controller, "_inventorySelectedLoadout", selectedLoadout);
            CallPrivate(controller, "RefreshInventorySelectedUnitPreview");

            Assert.AreEqual("2", levelText.text);
            Assert.AreEqual("75/200", xpText.text);
            Assert.AreEqual(0f, xpSlider.minValue, 0.001f);
            Assert.AreEqual(1f, xpSlider.maxValue, 0.001f);
            Assert.AreEqual(0.375f, xpSlider.value, 0.001f);

            Object.DestroyImmediate(unitDefinition);
            Object.DestroyImmediate(root);
        }

        [Test]
        public void RefreshInventorySelectedUnitPreview_ClearsInventoryLevelAndXpProgression_WhenSelectionIsMissing()
        {
            var root = new GameObject("PopupMenu");
            var controller = root.AddComponent<PreparationPopupMenuLocalizationController>();

            var inventoryPanel = new GameObject("InventoryPanel", typeof(RectTransform));
            inventoryPanel.transform.SetParent(root.transform, false);

            var levelTextObject = new GameObject("TextLevelNum", typeof(RectTransform), typeof(TextMeshProUGUI));
            levelTextObject.transform.SetParent(inventoryPanel.transform, false);
            var levelText = levelTextObject.GetComponent<TextMeshProUGUI>();
            levelText.text = "9";

            var xpTextObject = new GameObject("TextExp", typeof(RectTransform), typeof(TextMeshProUGUI));
            xpTextObject.transform.SetParent(inventoryPanel.transform, false);
            var xpText = xpTextObject.GetComponent<TextMeshProUGUI>();
            xpText.text = "999/999";

            var sliderObject = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            sliderObject.transform.SetParent(inventoryPanel.transform, false);
            var xpSlider = sliderObject.GetComponent<Slider>();
            xpSlider.minValue = 0f;
            xpSlider.maxValue = 1f;
            xpSlider.SetValueWithoutNotify(0.75f);

            SetPrivate(controller, "_inventorySelectedLoadout", null);
            CallPrivate(controller, "RefreshInventorySelectedUnitPreview");

            Assert.AreEqual(string.Empty, levelText.text);
            Assert.AreEqual(string.Empty, xpText.text);
            Assert.AreEqual(0f, xpSlider.minValue, 0.001f);
            Assert.AreEqual(1f, xpSlider.maxValue, 0.001f);
            Assert.AreEqual(0f, xpSlider.value, 0.001f);

            Object.DestroyImmediate(root);
        }
    }
}
