using System;
using System.Collections.Generic;
using NUnit.Framework;
using SevenBattles.Core.Battle;
using SevenBattles.Preparation;
using TMPro;
using UnityEngine;

namespace SevenBattles.Tests.Preparation
{
    public class ActiveSquadGridViewTests
    {
        private static void SetPrivate(object target, string fieldName, object value)
        {
            var type = target.GetType();
            var field = type.GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' was not found on type '{type.FullName}'.");
            field.SetValue(target, value);
        }

        private static ActiveSquadGridView BuildGridView(out UnitDropZone dropZone, out TMP_Text emptyLabel, out TMP_Text fullLabel)
        {
            var root = new GameObject("ActiveSquadRoot", typeof(RectTransform));
            dropZone = root.AddComponent<UnitDropZone>();
            var gridView = root.AddComponent<ActiveSquadGridView>();

            var emptyLabelObject = new GameObject("EmptyLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            emptyLabelObject.transform.SetParent(root.transform, false);
            emptyLabel = emptyLabelObject.GetComponent<TextMeshProUGUI>();
            emptyLabel.gameObject.SetActive(false);

            var fullLabelObject = new GameObject("FullLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            fullLabelObject.transform.SetParent(root.transform, false);
            fullLabel = fullLabelObject.GetComponent<TextMeshProUGUI>();
            fullLabel.gameObject.SetActive(true);

            var contentObject = new GameObject("Content", typeof(RectTransform));
            contentObject.transform.SetParent(root.transform, false);
            var content = contentObject.GetComponent<RectTransform>();

            var portraitPrefabObject = new GameObject("PortraitPrefab", typeof(RectTransform), typeof(UnitPortraitView));
            var portraitPrefab = portraitPrefabObject.GetComponent<UnitPortraitView>();
            var pool = new UnitPortraitPool(portraitPrefab, content, 0);

            SetPrivate(gridView, "_emptyLabel", emptyLabel);
            SetPrivate(gridView, "_fullLabel", fullLabel);
            SetPrivate(gridView, "_dropZone", dropZone);
            SetPrivate(gridView, "_pool", pool);

            return gridView;
        }

        [Test]
        public void Refresh_EmptySquad_ShowsOnlyEmptyLabelAndNoCompletionVisual()
        {
            var gridView = BuildGridView(out var dropZone, out var emptyLabel, out var fullLabel);

            gridView.SetIsFull(false);
            gridView.Refresh(Array.Empty<UnitSpellLoadout>());

            Assert.IsTrue(emptyLabel.gameObject.activeSelf);
            Assert.IsFalse(fullLabel.gameObject.activeSelf);
            Assert.IsFalse(dropZone.IsCompletionVisualActive);

            UnityEngine.Object.DestroyImmediate(gridView.gameObject);
        }

        [Test]
        public void Refresh_FullSquad_HidesFullLabelAndEnablesCompletionVisual()
        {
            var gridView = BuildGridView(out var dropZone, out var emptyLabel, out var fullLabel);
            var loadouts = new List<UnitSpellLoadout> { new UnitSpellLoadout() };

            gridView.SetIsFull(true);
            gridView.Refresh(loadouts);

            Assert.IsFalse(emptyLabel.gameObject.activeSelf);
            Assert.IsFalse(fullLabel.gameObject.activeSelf);
            Assert.IsTrue(dropZone.IsCompletionVisualActive);

            UnityEngine.Object.DestroyImmediate(gridView.gameObject);
        }
    }
}
