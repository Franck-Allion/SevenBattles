using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SevenBattles.Core;
using SevenBattles.Core.Battle;
using SevenBattles.UI;

namespace SevenBattles.Tests.UI
{
    public class BattleSpellsHUDTests
    {
        private sealed class TestSfxPlayer : MonoBehaviour, IUiSfxPlayer
        {
            public int Calls { get; private set; }
            public AudioClip LastClip { get; private set; }
            public float LastVolume { get; private set; }

            public void PlayOneShot(AudioClip clip, float volume)
            {
                Calls++;
                LastClip = clip;
                LastVolume = volume;
            }
        }

        private sealed class FakeTurnController : MonoBehaviour, ITurnOrderController
        {
            public bool HasActiveUnit { get; set; }
            public bool IsActiveUnitPlayerControlled { get; set; }
            public Sprite ActiveUnitPortrait { get; set; }
            public bool HasSpellPreview { get; set; }
            public SpellAmountPreview SpellPreview { get; set; }

            public event System.Action ActiveUnitChanged;
            public event System.Action ActiveUnitActionPointsChanged;
            public event System.Action ActiveUnitStatsChanged;

            public int ActiveUnitCurrentActionPoints { get; set; }
            public int ActiveUnitMaxActionPoints { get; set; }

            public SpellDefinition[] ActiveUnitSpells { get; set; } = System.Array.Empty<SpellDefinition>();

            public bool TryGetActiveUnitStats(out UnitStatsViewData stats)
            {
                stats = default;
                return false;
            }

            public bool TryGetActiveUnitSpellAmountPreview(SpellDefinition spell, out SpellAmountPreview preview)
            {
                if (HasSpellPreview)
                {
                    preview = SpellPreview;
                    return true;
                }

                preview = default;
                return false;
            }

            public void RequestEndTurn()
            {
            }

            public void FireActiveUnitChanged()
            {
                ActiveUnitChanged?.Invoke();
            }

            public void FireActiveUnitStatsChanged()
            {
                ActiveUnitStatsChanged?.Invoke();
            }
        }

        [Test]
        public void PopulatesSlots_FromActiveUnitSpells_AndClearsOnEmpty()
        {
            var hudGo = new GameObject("SpellHUD");
            var hud = hudGo.AddComponent<BattleSpellsHUD>();

            var containerGo = new GameObject("SpellsContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            containerGo.transform.SetParent(hudGo.transform);
            var containerRt = (RectTransform)containerGo.transform;

            var ctrlGo = new GameObject("Ctrl");
            var ctrl = ctrlGo.AddComponent<FakeTurnController>();

            var s1 = ScriptableObject.CreateInstance<SpellDefinition>();
            s1.Id = "spell.firebolt";
            s1.ActionPointCost = 1;
            s1.Icon = MakeSprite();

            var s2 = ScriptableObject.CreateInstance<SpellDefinition>();
            s2.Id = "spell.arcane_shield";
            s2.ActionPointCost = 2;
            s2.Icon = MakeSprite();

            ctrl.HasActiveUnit = true;
            ctrl.IsActiveUnitPlayerControlled = true;
            ctrl.ActiveUnitSpells = new[] { s1, s2 };

            SetPrivate(hud, "_controllerBehaviour", ctrl);
            SetPrivate(hud, "_spellsContainer", containerRt);
            SetPrivate(hud, "_useFixedSlots", false);

            CallPrivate(hud, "Awake");
            CallPrivate(hud, "OnEnable");

            Assert.AreEqual(2, containerRt.childCount, "Should create one slot per spell when no template exists.");
            AssertSlot(containerRt.GetChild(0), expectedApCost: "1", expectedSprite: s1.Icon);
            AssertSlot(containerRt.GetChild(1), expectedApCost: "2", expectedSprite: s2.Icon);

            // Clear
            ctrl.ActiveUnitSpells = System.Array.Empty<SpellDefinition>();
            ctrl.FireActiveUnitChanged();

            Assert.IsFalse(containerRt.GetChild(0).gameObject.activeSelf, "Slot should be hidden when there are no spells.");
            Assert.IsFalse(containerRt.GetChild(1).gameObject.activeSelf, "Slot should be hidden when there are no spells.");

            Object.DestroyImmediate(hudGo);
            Object.DestroyImmediate(containerGo);
            Object.DestroyImmediate(ctrlGo);
            Object.DestroyImmediate(s1);
            Object.DestroyImmediate(s2);
        }

        [Test]
        public void HidesSlots_WhenActiveUnitNotPlayerControlled()
        {
            var hudGo = new GameObject("SpellHUD");
            var hud = hudGo.AddComponent<BattleSpellsHUD>();

            var containerGo = new GameObject("SpellsContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            containerGo.transform.SetParent(hudGo.transform);
            var containerRt = (RectTransform)containerGo.transform;

            var ctrlGo = new GameObject("Ctrl");
            var ctrl = ctrlGo.AddComponent<FakeTurnController>();

            var s1 = ScriptableObject.CreateInstance<SpellDefinition>();
            s1.Id = "spell.firebolt";
            s1.ActionPointCost = 1;
            s1.Icon = MakeSprite();

            ctrl.HasActiveUnit = true;
            ctrl.IsActiveUnitPlayerControlled = false;
            ctrl.ActiveUnitSpells = new[] { s1 };

            SetPrivate(hud, "_controllerBehaviour", ctrl);
            SetPrivate(hud, "_spellsContainer", containerRt);
            SetPrivate(hud, "_useFixedSlots", false);

            CallPrivate(hud, "Awake");
            CallPrivate(hud, "OnEnable");

            Assert.AreEqual(0, containerRt.childCount, "Should not create slots when hidden by non-player-controlled unit.");

            Object.DestroyImmediate(hudGo);
            Object.DestroyImmediate(containerGo);
            Object.DestroyImmediate(ctrlGo);
            Object.DestroyImmediate(s1);
        }

        [Test]
        public void UsesTemplateSlotView_WhenProvided()
        {
            var hudGo = new GameObject("SpellHUD");
            var hud = hudGo.AddComponent<BattleSpellsHUD>();

            var containerGo = new GameObject("SpellsContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            containerGo.transform.SetParent(hudGo.transform);
            var containerRt = (RectTransform)containerGo.transform;

            // Slot template (inactive), with a typed view so BattleSpellsHUD does not need name-based lookup.
            var templateGo = new GameObject("SpellSlotTemplate", typeof(RectTransform));
            templateGo.transform.SetParent(hudGo.transform);
            var templateRt = (RectTransform)templateGo.transform;

            var iconGo = new GameObject("MyIcon", typeof(RectTransform));
            iconGo.transform.SetParent(templateRt, false);
            var iconImg = iconGo.AddComponent<Image>();

            var apGo = new GameObject("MyApCost", typeof(RectTransform));
            apGo.transform.SetParent(templateRt, false);
            var apTmp = apGo.AddComponent<TextMeshProUGUI>();

            var view = templateGo.AddComponent<BattleSpellSlotView>();
            SetPrivate(view, "_icon", iconImg);
            SetPrivate(view, "_apCost", apTmp);

            templateGo.SetActive(false);

            var ctrlGo = new GameObject("Ctrl");
            var ctrl = ctrlGo.AddComponent<FakeTurnController>();

            var s1 = ScriptableObject.CreateInstance<SpellDefinition>();
            s1.Id = "spell.firebolt";
            s1.ActionPointCost = 1;
            s1.Icon = MakeSprite();

            ctrl.HasActiveUnit = true;
            ctrl.IsActiveUnitPlayerControlled = true;
            ctrl.ActiveUnitSpells = new[] { s1 };

            SetPrivate(hud, "_controllerBehaviour", ctrl);
            SetPrivate(hud, "_spellsContainer", containerRt);
            SetPrivate(hud, "_slotTemplate", templateRt);
            SetPrivate(hud, "_useFixedSlots", false);

            CallPrivate(hud, "Awake");
            CallPrivate(hud, "OnEnable");

            Assert.AreEqual(1, containerRt.childCount);
            var slot = containerRt.GetChild(0);
            var slotView = slot.GetComponentInChildren<BattleSpellSlotView>(true);
            Assert.IsNotNull(slotView, "Instantiated slot should retain the typed view component.");
            Assert.AreEqual(s1.Icon, slotView.Icon.sprite);
            Assert.AreEqual("1", slotView.ApCost.text);

            Object.DestroyImmediate(hudGo);
            Object.DestroyImmediate(containerGo);
            Object.DestroyImmediate(templateGo);
            Object.DestroyImmediate(ctrlGo);
            Object.DestroyImmediate(s1);
        }

        [Test]
        public void FixedSlots_BindsSpell0ToSpellN_AndClickSelectsFrame0()
        {
            var hudGo = new GameObject("SpellHUD");
            var hud = hudGo.AddComponent<BattleSpellsHUD>();

            var containerGo = new GameObject("SpellsContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            containerGo.transform.SetParent(hudGo.transform);
            var containerRt = (RectTransform)containerGo.transform;

            // Create 7 authored slots Spell0..Spell6 (inactive).
            var slots = new RectTransform[7];
            for (int i = 0; i < slots.Length; i++)
            {
                var slotGo = new GameObject($"Spell{i}", typeof(RectTransform));
                slotGo.transform.SetParent(containerRt, false);
                slotGo.SetActive(false);

                var btn = slotGo.AddComponent<Button>();
                var bg = slotGo.AddComponent<Image>();
                bg.raycastTarget = true;

                var visualRoot = new GameObject("VisualRoot", typeof(RectTransform));
                visualRoot.transform.SetParent(slotGo.transform, false);

                var iconGo = new GameObject("Icon", typeof(RectTransform));
                iconGo.transform.SetParent(visualRoot.transform, false);
                var iconImg = iconGo.AddComponent<Image>();

                var apGo = new GameObject("APCost", typeof(RectTransform));
                apGo.transform.SetParent(visualRoot.transform, false);
                var apTmp = apGo.AddComponent<TextMeshProUGUI>();

                var bgRoot = new GameObject("Bg", typeof(RectTransform));
                bgRoot.transform.SetParent(visualRoot.transform, false);
                var frameGo = new GameObject("Frame0");
                frameGo.transform.SetParent(bgRoot.transform, false);
                frameGo.SetActive(false);

                var view = slotGo.AddComponent<BattleSpellSlotView>();
                SetPrivate(view, "_button", btn);
                SetPrivate(view, "_icon", iconImg);
                SetPrivate(view, "_apCost", apTmp);
                SetPrivate(view, "_selectionFrame", frameGo);

                slots[i] = (RectTransform)slotGo.transform;
            }

            var ctrlGo = new GameObject("Ctrl");
            var ctrl = ctrlGo.AddComponent<FakeTurnController>();

            var s1 = ScriptableObject.CreateInstance<SpellDefinition>();
            s1.Id = "spell.firebolt";
            s1.ActionPointCost = 1;
            s1.Icon = MakeSprite();

            var s2 = ScriptableObject.CreateInstance<SpellDefinition>();
            s2.Id = "spell.arcane_shield";
            s2.ActionPointCost = 2;
            s2.Icon = MakeSprite();

            ctrl.HasActiveUnit = true;
            ctrl.IsActiveUnitPlayerControlled = true;
            ctrl.ActiveUnitSpells = new[] { s1, s2 };

            SetPrivate(hud, "_controllerBehaviour", ctrl);
            SetPrivate(hud, "_spellsContainer", containerRt);
            SetPrivate(hud, "_useFixedSlots", true);
            SetPrivate(hud, "_fixedSlotRoots", slots);

            CallPrivate(hud, "Awake");
            CallPrivate(hud, "OnEnable");

            Assert.IsTrue(slots[0].gameObject.activeSelf);
            Assert.IsTrue(slots[1].gameObject.activeSelf);
            Assert.IsFalse(slots[2].gameObject.activeSelf);

            var view0 = slots[0].GetComponent<BattleSpellSlotView>();
            var view1 = slots[1].GetComponent<BattleSpellSlotView>();
            Assert.AreEqual(s1.Icon, view0.Icon.sprite);
            Assert.AreEqual("1", view0.ApCost.text);
            Assert.AreEqual(s2.Icon, view1.Icon.sprite);
            Assert.AreEqual("2", view1.ApCost.text);

            // Click Spell1: Frame0 should be active on Spell1 only.
            view1.Button.onClick.Invoke();
            Assert.IsFalse(view0.SelectionFrame.activeSelf);
            Assert.IsTrue(view1.SelectionFrame.activeSelf);

            // Click Spell1 again: selection toggles off.
            view1.Button.onClick.Invoke();
            Assert.IsFalse(view0.SelectionFrame.activeSelf);
            Assert.IsFalse(view1.SelectionFrame.activeSelf);

            // Click Spell0: Frame0 should move.
            view0.Button.onClick.Invoke();
            Assert.IsTrue(view0.SelectionFrame.activeSelf);
            Assert.IsFalse(view1.SelectionFrame.activeSelf);

            // Click Spell0 again: selection toggles off.
            view0.Button.onClick.Invoke();
            Assert.IsFalse(view0.SelectionFrame.activeSelf);
            Assert.IsFalse(view1.SelectionFrame.activeSelf);

            Object.DestroyImmediate(hudGo);
            Object.DestroyImmediate(containerGo);
            Object.DestroyImmediate(ctrlGo);
            Object.DestroyImmediate(s1);
            Object.DestroyImmediate(s2);
        }

        [Test]
        public void PlaysSfx_WhenSlotClicked()
        {
            var hudGo = new GameObject("SpellHUD");
            var hud = hudGo.AddComponent<BattleSpellsHUD>();

            var containerGo = new GameObject("SpellsContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            containerGo.transform.SetParent(hudGo.transform);
            var containerRt = (RectTransform)containerGo.transform;

            var slotGo = new GameObject("Spell0", typeof(RectTransform));
            slotGo.transform.SetParent(containerRt, false);
            slotGo.SetActive(false);
            var btn = slotGo.AddComponent<Button>();
            var bg = slotGo.AddComponent<Image>();
            btn.targetGraphic = bg;

            var iconGo = new GameObject("Icon", typeof(RectTransform));
            iconGo.transform.SetParent(slotGo.transform, false);
            var iconImg = iconGo.AddComponent<Image>();

            var apGo = new GameObject("APCost", typeof(RectTransform));
            apGo.transform.SetParent(slotGo.transform, false);
            var apTmp = apGo.AddComponent<TextMeshProUGUI>();

            var frameGo = new GameObject("Frame0");
            frameGo.transform.SetParent(slotGo.transform, false);
            frameGo.SetActive(false);

            var view = slotGo.AddComponent<BattleSpellSlotView>();
            SetPrivate(view, "_button", btn);
            SetPrivate(view, "_icon", iconImg);
            SetPrivate(view, "_apCost", apTmp);
            SetPrivate(view, "_selectionFrame", frameGo);

            var ctrlGo = new GameObject("Ctrl");
            var ctrl = ctrlGo.AddComponent<FakeTurnController>();
            ctrl.HasActiveUnit = true;
            ctrl.IsActiveUnitPlayerControlled = true;

            var s1 = ScriptableObject.CreateInstance<SpellDefinition>();
            s1.Id = "spell.firebolt";
            s1.ActionPointCost = 1;
            s1.Icon = MakeSprite();
            ctrl.ActiveUnitSpells = new[] { s1 };

            var sfxGo = new GameObject("Sfx");
            var sfx = sfxGo.AddComponent<TestSfxPlayer>();
            var clip = AudioClip.Create("click", 64, 1, 44100, false);

            SetPrivate(hud, "_controllerBehaviour", ctrl);
            SetPrivate(hud, "_spellsContainer", containerRt);
            SetPrivate(hud, "_useFixedSlots", true);
            SetPrivate(hud, "_fixedSlotRoots", new[] { (RectTransform)slotGo.transform });
            SetPrivate(hud, "_sfxPlayerBehaviour", sfx);
            SetPrivate(hud, "_spellClickClip", clip);
            SetPrivate(hud, "_spellClickCooldown", 0f);
            SetPrivate(hud, "_spellClickVolume", 1f);

            CallPrivate(hud, "Awake");
            CallPrivate(hud, "OnEnable");

            Assert.AreEqual(0, sfx.Calls);
            btn.onClick.Invoke();
            Assert.AreEqual(1, sfx.Calls);
            Assert.AreEqual(clip, sfx.LastClip);
            Assert.AreEqual(1f, sfx.LastVolume, 1e-4f);

            Object.DestroyImmediate(hudGo);
            Object.DestroyImmediate(containerGo);
            Object.DestroyImmediate(slotGo);
            Object.DestroyImmediate(ctrlGo);
            Object.DestroyImmediate(s1);
            Object.DestroyImmediate(sfxGo);
        }

        [Test]
        public void SelectedSpellDescription_UpdatesOnSelect_AndClearsOnDeselect()
        {
            var hudGo = new GameObject("SpellHUD");
            var hud = hudGo.AddComponent<BattleSpellsHUD>();

            var containerGo = new GameObject("SpellsContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            containerGo.transform.SetParent(hudGo.transform);
            var containerRt = (RectTransform)containerGo.transform;

            var slotGo = new GameObject("Spell0", typeof(RectTransform));
            slotGo.transform.SetParent(containerRt, false);
            slotGo.SetActive(false);
            var btn = slotGo.AddComponent<Button>();
            var bg = slotGo.AddComponent<Image>();
            btn.targetGraphic = bg;

            var iconGo = new GameObject("Icon", typeof(RectTransform));
            iconGo.transform.SetParent(slotGo.transform, false);
            var iconImg = iconGo.AddComponent<Image>();

            var apGo = new GameObject("APCost", typeof(RectTransform));
            apGo.transform.SetParent(slotGo.transform, false);
            var apTmp = apGo.AddComponent<TextMeshProUGUI>();

            var frameGo = new GameObject("Frame0");
            frameGo.transform.SetParent(slotGo.transform, false);
            frameGo.SetActive(false);

            var view = slotGo.AddComponent<BattleSpellSlotView>();
            SetPrivate(view, "_button", btn);
            SetPrivate(view, "_icon", iconImg);
            SetPrivate(view, "_apCost", apTmp);
            SetPrivate(view, "_selectionFrame", frameGo);

            var descGo = new GameObject("Desc", typeof(RectTransform));
            descGo.transform.SetParent(hudGo.transform, false);
            var descText = descGo.AddComponent<TextMeshProUGUI>();

            var ctrlGo = new GameObject("Ctrl");
            var ctrl = ctrlGo.AddComponent<FakeTurnController>();
            ctrl.HasActiveUnit = true;
            ctrl.IsActiveUnitPlayerControlled = true;

            var spell = ScriptableObject.CreateInstance<SpellDefinition>();
            spell.Id = "spell.firebolt";
            spell.ActionPointCost = 1;
            spell.Icon = MakeSprite();
            spell.DescriptionLocalizationKey = string.Empty; // fallback path (no localization system in unit tests)
            spell.Description = "Deal X damage to an enemy unit.";
            ctrl.ActiveUnitSpells = new[] { spell };

            SetPrivate(hud, "_controllerBehaviour", ctrl);
            SetPrivate(hud, "_spellsContainer", containerRt);
            SetPrivate(hud, "_useFixedSlots", true);
            SetPrivate(hud, "_fixedSlotRoots", new[] { (RectTransform)slotGo.transform });
            SetPrivate(hud, "_selectedSpellDescriptionText", descText);

            CallPrivate(hud, "Awake");
            CallPrivate(hud, "OnEnable");

            Assert.AreEqual(string.Empty, descText.text);

            btn.onClick.Invoke();
            Assert.AreEqual(spell.Description, descText.text);

            btn.onClick.Invoke();
            Assert.AreEqual(string.Empty, descText.text);

            Object.DestroyImmediate(hudGo);
            Object.DestroyImmediate(containerGo);
            Object.DestroyImmediate(slotGo);
            Object.DestroyImmediate(ctrlGo);
            Object.DestroyImmediate(descGo);
            Object.DestroyImmediate(spell);
        }

        [Test]
        public void SelectedSpellDescription_FormatsPrimaryAmount_WithColorAndBold_WhenPreviewChanges()
        {
            var hudGo = new GameObject("SpellHUD");
            var hud = hudGo.AddComponent<BattleSpellsHUD>();

            var containerGo = new GameObject("SpellsContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            containerGo.transform.SetParent(hudGo.transform);
            var containerRt = (RectTransform)containerGo.transform;

            var slotGo = new GameObject("Spell0", typeof(RectTransform));
            slotGo.transform.SetParent(containerRt, false);
            slotGo.SetActive(false);
            var btn = slotGo.AddComponent<Button>();
            var bg = slotGo.AddComponent<Image>();
            btn.targetGraphic = bg;

            var iconGo = new GameObject("Icon", typeof(RectTransform));
            iconGo.transform.SetParent(slotGo.transform, false);
            var iconImg = iconGo.AddComponent<Image>();

            var apGo = new GameObject("APCost", typeof(RectTransform));
            apGo.transform.SetParent(slotGo.transform, false);
            var apTmp = apGo.AddComponent<TextMeshProUGUI>();

            var frameGo = new GameObject("Frame0");
            frameGo.transform.SetParent(slotGo.transform, false);
            frameGo.SetActive(false);

            var view = slotGo.AddComponent<BattleSpellSlotView>();
            SetPrivate(view, "_button", btn);
            SetPrivate(view, "_icon", iconImg);
            SetPrivate(view, "_apCost", apTmp);
            SetPrivate(view, "_selectionFrame", frameGo);

            var descGo = new GameObject("Desc", typeof(RectTransform));
            descGo.transform.SetParent(hudGo.transform, false);
            var descText = descGo.AddComponent<TextMeshProUGUI>();

            var ctrlGo = new GameObject("Ctrl");
            var ctrl = ctrlGo.AddComponent<FakeTurnController>();
            ctrl.HasActiveUnit = true;
            ctrl.IsActiveUnitPlayerControlled = true;

            var spell = ScriptableObject.CreateInstance<SpellDefinition>();
            spell.Id = "spell.firebolt";
            spell.ActionPointCost = 1;
            spell.Icon = MakeSprite();
            spell.DescriptionLocalizationKey = string.Empty;
            spell.Description = "Deal {0} damage to an enemy unit.";
            ctrl.ActiveUnitSpells = new[] { spell };

            ctrl.HasSpellPreview = true;
            ctrl.SpellPreview = new SpellAmountPreview { BaseAmount = 5, ModifiedAmount = 7 };

            SetPrivate(hud, "_controllerBehaviour", ctrl);
            SetPrivate(hud, "_spellsContainer", containerRt);
            SetPrivate(hud, "_useFixedSlots", true);
            SetPrivate(hud, "_fixedSlotRoots", new[] { (RectTransform)slotGo.transform });
            SetPrivate(hud, "_selectedSpellDescriptionText", descText);

            CallPrivate(hud, "Awake");
            CallPrivate(hud, "OnEnable");

            btn.onClick.Invoke();
            Assert.AreEqual("Deal <color=#00C853><b>7</b></color> damage to an enemy unit.", descText.text);

            ctrl.SpellPreview = new SpellAmountPreview { BaseAmount = 5, ModifiedAmount = 3 };
            ctrl.FireActiveUnitStatsChanged();
            Assert.AreEqual("Deal <color=#D50000><b>3</b></color> damage to an enemy unit.", descText.text);

            ctrl.SpellPreview = new SpellAmountPreview { BaseAmount = 5, ModifiedAmount = 5 };
            ctrl.FireActiveUnitStatsChanged();
            Assert.AreEqual("Deal 5 damage to an enemy unit.", descText.text);

            Object.DestroyImmediate(hudGo);
            Object.DestroyImmediate(containerGo);
            Object.DestroyImmediate(slotGo);
            Object.DestroyImmediate(ctrlGo);
            Object.DestroyImmediate(descGo);
            Object.DestroyImmediate(spell);
        }

        private static void AssertSlot(Transform slotRoot, string expectedApCost, Sprite expectedSprite)
        {
            var iconTf = slotRoot.Find("Icon");
            Assert.IsNotNull(iconTf, "Slot should contain an 'Icon' child.");
            var iconImg = iconTf.GetComponent<Image>();
            Assert.IsNotNull(iconImg, "Icon child should have an Image.");
            Assert.AreEqual(expectedSprite, iconImg.sprite);

            var costTf = slotRoot.Find("APCost");
            Assert.IsNotNull(costTf, "Slot should contain an 'APCost' child.");
            var tmp = costTf.GetComponent<TMP_Text>();
            Assert.IsNotNull(tmp, "APCost child should have a TMP_Text.");
            Assert.AreEqual(expectedApCost, tmp.text);
        }

        private static Sprite MakeSprite()
        {
            var tex = Texture2D.blackTexture;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }

        private static void SetPrivate(object target, string fieldName, object value)
        {
            var type = target.GetType();
            var field = type.GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' was not found on type '{type.FullName}'.");
            field.SetValue(target, value);
        }

        private static void CallPrivate(object target, string methodName)
        {
            var type = target.GetType();
            var method = type.GetMethod(methodName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Method '{methodName}' was not found on type '{type.FullName}'.");
            method.Invoke(target, null);
        }
    }
}
