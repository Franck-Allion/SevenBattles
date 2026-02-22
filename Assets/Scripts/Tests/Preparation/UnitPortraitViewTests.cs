using NUnit.Framework;
using SevenBattles.Core.Battle;
using SevenBattles.Core.Units;
using SevenBattles.Preparation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenBattles.Tests.Preparation
{
    public class UnitPortraitViewTests
    {
        private const float Tolerance = 0.0001f;

        private static void SetPrivate(object target, string fieldName, object value)
        {
            var type = target.GetType();
            var field = type.GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' was not found on type '{type.FullName}'.");
            field.SetValue(target, value);
        }

        [Test]
        public void ApplyGridCellLayout_ScalesRootOnly_WithoutChangingLevelLayout()
        {
            UnitPortraitView view = BuildView(out RectTransform levelRoot, out RectTransform levelTextRect, out _);
            RectTransform rootRect = (RectTransform)view.transform;
            levelRoot.anchoredPosition = new Vector2(3f, -7f);
            levelRoot.sizeDelta = new Vector2(174f, 39f);
            levelTextRect.sizeDelta = new Vector2(160f, 17f);

            view.ApplyGridCellLayout(1f);

            Assert.AreEqual(1f, view.transform.localScale.x, Tolerance);
            Assert.AreEqual(0f, rootRect.anchoredPosition.x, Tolerance);
            Assert.AreEqual(0f, rootRect.anchoredPosition.y, Tolerance);
            Assert.AreEqual(0.5f, rootRect.anchorMin.x, Tolerance);
            Assert.AreEqual(0.5f, rootRect.anchorMin.y, Tolerance);
            Assert.AreEqual(0.5f, rootRect.anchorMax.x, Tolerance);
            Assert.AreEqual(0.5f, rootRect.anchorMax.y, Tolerance);
            Assert.AreEqual(0.5f, rootRect.pivot.x, Tolerance);
            Assert.AreEqual(0.5f, rootRect.pivot.y, Tolerance);
            Assert.AreEqual(3f, levelRoot.anchoredPosition.x, Tolerance);
            Assert.AreEqual(-7f, levelRoot.anchoredPosition.y, Tolerance);
            Assert.AreEqual(174f, levelRoot.sizeDelta.x, Tolerance);
            Assert.AreEqual(39f, levelRoot.sizeDelta.y, Tolerance);
            Assert.AreEqual(160f, levelTextRect.sizeDelta.x, Tolerance);
            Assert.AreEqual(17f, levelTextRect.sizeDelta.y, Tolerance);

            Object.DestroyImmediate(view.gameObject);
        }

        [Test]
        public void ApplyGridCellLayout_ScalesRootUniformly()
        {
            UnitPortraitView view = BuildView(out _, out _, out _);

            view.ApplyGridCellLayout(1.2f);

            Assert.AreEqual(1.2f, view.transform.localScale.x, Tolerance);
            Assert.AreEqual(1.2f, view.transform.localScale.y, Tolerance);

            Object.DestroyImmediate(view.gameObject);
        }

        [Test]
        public void ApplyGridCellLayout_AllowsSubUnitScaleForFittedMiniPortraits()
        {
            UnitPortraitView view = BuildView(out _, out _, out _);

            view.ApplyGridCellLayout(0.45f);

            Assert.AreEqual(0.45f, view.transform.localScale.x, Tolerance);
            Assert.AreEqual(0.45f, view.transform.localScale.y, Tolerance);

            Object.DestroyImmediate(view.gameObject);
        }

        [Test]
        public void Bind_ExplicitDisplayName_UpdatesDisplayNameProperty()
        {
            UnitPortraitView view = BuildView(out _, out _, out _);
            UnitDefinition definition = ScriptableObject.CreateInstance<UnitDefinition>();
            definition.Id = "unit_01";
            definition.name = "Arcanist";
            UnitSpellLoadout loadout = new UnitSpellLoadout { Definition = definition, Level = 2 };

            view.Bind(loadout, "Raven");

            Assert.AreEqual("Raven", view.DisplayName);

            Object.DestroyImmediate(definition);
            Object.DestroyImmediate(view.gameObject);
        }

        [Test]
        public void Bind_WithoutExplicitName_UsesDefinitionName()
        {
            UnitPortraitView view = BuildView(out _, out _, out _);
            UnitDefinition definition = ScriptableObject.CreateInstance<UnitDefinition>();
            definition.Id = "unit_02";
            definition.name = "Guardian";
            UnitSpellLoadout loadout = new UnitSpellLoadout { Definition = definition, Level = 1 };

            view.Bind(loadout);

            Assert.AreEqual("Guardian", view.DisplayName);

            Object.DestroyImmediate(definition);
            Object.DestroyImmediate(view.gameObject);
        }

        [Test]
        public void Clear_ResetsDisplayNameProperty()
        {
            UnitPortraitView view = BuildView(out _, out _, out _);
            UnitDefinition definition = ScriptableObject.CreateInstance<UnitDefinition>();
            definition.Id = "unit_03";
            definition.name = "Invoker";
            UnitSpellLoadout loadout = new UnitSpellLoadout { Definition = definition, Level = 4 };

            view.Bind(loadout, "Echo");
            view.Clear();

            Assert.AreEqual(string.Empty, view.DisplayName);

            Object.DestroyImmediate(definition);
            Object.DestroyImmediate(view.gameObject);
        }

        private static UnitPortraitView BuildView(out RectTransform levelRoot, out RectTransform levelTextRect, out TMP_Text nameLabel)
        {
            var root = new GameObject("UnitPortrait", typeof(RectTransform), typeof(UnitPortraitView));
            var view = root.GetComponent<UnitPortraitView>();

            var portrait = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
            portrait.transform.SetParent(root.transform, false);

            var level = new GameObject("Level", typeof(RectTransform), typeof(Image));
            level.transform.SetParent(root.transform, false);
            levelRoot = level.GetComponent<RectTransform>();

            var levelText = new GameObject("LevelText", typeof(RectTransform), typeof(TextMeshProUGUI));
            levelText.transform.SetParent(level.transform, false);
            var label = levelText.GetComponent<TextMeshProUGUI>();
            levelTextRect = levelText.GetComponent<RectTransform>();

            var nameText = new GameObject("NameText", typeof(RectTransform), typeof(TextMeshProUGUI));
            nameText.transform.SetParent(root.transform, false);
            nameLabel = nameText.GetComponent<TextMeshProUGUI>();

            SetPrivate(view, "_portraitImage", portrait.GetComponent<Image>());
            SetPrivate(view, "_levelLabel", label);
            SetPrivate(view, "_nameLabel", nameLabel);

            return view;
        }
    }
}
