using System;
using System.Collections.Generic;
using NUnit.Framework;
using SevenBattles.Core;
using SevenBattles.Core.Battle;
using SevenBattles.Core.Players;
using SevenBattles.Preparation;
using TMPro;
using UnityEngine;

namespace SevenBattles.Tests.Preparation
{
    public class SquadSetupControllerTests
    {
        private sealed class StubSquadService : ISquadService
        {
            public StubSquadService(int maxSquadSize)
            {
                MaxSquadSize = Mathf.Max(1, maxSquadSize);
            }

            public int MaxSquadSize { get; }
            public int ActiveSquadCount => 0;
            public bool IsSquadFull => false;
            public IReadOnlyList<OwnedUnitData> ActiveSquad => Array.Empty<OwnedUnitData>();
            public IReadOnlyList<OwnedUnitData> AvailableUnits => Array.Empty<OwnedUnitData>();

            public event Action SquadChanged
            {
                add { }
                remove { }
            }

            public event Action<OwnedUnitData> UnitAddedToSquad
            {
                add { }
                remove { }
            }

            public event Action<OwnedUnitData> UnitRemovedFromSquad
            {
                add { }
                remove { }
            }

            public event Action<OwnedUnitData> UnitSelected
            {
                add { }
                remove { }
            }

            public void InitializeFromContext()
            {
            }

            public bool TryAddToSquad(string ownedUnitId)
            {
                return false;
            }

            public bool TryRemoveFromSquad(string ownedUnitId)
            {
                return false;
            }

            public void SelectUnit(string ownedUnitId)
            {
            }
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

        private static void AssertColorEqual(Color expected, Color actual)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.0001f));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.0001f));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.0001f));
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(0.0001f));
        }

        [Test]
        public void RefreshSquadValueDisplay_EmptySquad_UsesEmptyColor()
        {
            var root = new GameObject("SquadSetupRoot");
            var controller = root.AddComponent<SquadSetupController>();
            var labelObject = new GameObject("SquadValue", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(root.transform, false);
            var label = labelObject.GetComponent<TextMeshProUGUI>();

            var emptyColor = new Color(0.8f, 0.25f, 0.25f, 1f);
            var nonEmptyColor = Color.white;

            SetPrivate(controller, "_squadService", new StubSquadService(3));
            SetPrivate(controller, "_squadValueLabel", null);
            SetPrivate(controller, "_squadValueObjectName", "SquadValue");
            SetPrivate(controller, "_emptySquadValueColor", emptyColor);
            SetPrivate(controller, "_nonEmptySquadValueColor", nonEmptyColor);

            var activeLoadouts = GetPrivate<List<UnitSpellLoadout>>(controller, "_activeSquadLoadouts");
            activeLoadouts.Clear();

            CallPrivate(controller, "RefreshSquadValueDisplay");

            Assert.AreEqual("0/3", label.text);
            AssertColorEqual(emptyColor, label.color);

            UnityEngine.Object.DestroyImmediate(root);
        }

        [Test]
        public void RefreshSquadValueDisplay_NonEmptySquad_UsesNonEmptyColor()
        {
            var root = new GameObject("SquadSetupRoot");
            var controller = root.AddComponent<SquadSetupController>();
            var labelObject = new GameObject("SquadValue", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(root.transform, false);
            var label = labelObject.GetComponent<TextMeshProUGUI>();

            var emptyColor = new Color(0.8f, 0.25f, 0.25f, 1f);
            var nonEmptyColor = Color.white;

            SetPrivate(controller, "_squadService", new StubSquadService(3));
            SetPrivate(controller, "_squadValueLabel", null);
            SetPrivate(controller, "_squadValueObjectName", "SquadValue");
            SetPrivate(controller, "_emptySquadValueColor", emptyColor);
            SetPrivate(controller, "_nonEmptySquadValueColor", nonEmptyColor);

            var activeLoadouts = GetPrivate<List<UnitSpellLoadout>>(controller, "_activeSquadLoadouts");
            activeLoadouts.Clear();
            activeLoadouts.Add(new UnitSpellLoadout());

            CallPrivate(controller, "RefreshSquadValueDisplay");

            Assert.AreEqual("1/3", label.text);
            AssertColorEqual(nonEmptyColor, label.color);

            UnityEngine.Object.DestroyImmediate(root);
        }
    }
}
