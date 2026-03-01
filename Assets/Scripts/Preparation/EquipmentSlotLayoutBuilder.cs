using System;
using System.Collections.Generic;
using SevenBattles.Core.Items;
using SevenBattles.Core.Players;
using UnityEngine;
using UnityEngine.UI;

namespace SevenBattles.Preparation
{
    public sealed class EquipmentSlotLayoutBuilder : MonoBehaviour
    {
        private static readonly EquipmentSlotType[] SlotOrder =
        {
            EquipmentSlotType.Weapon,
            EquipmentSlotType.Shield,
            EquipmentSlotType.Helmet,
            EquipmentSlotType.Gloves,
            EquipmentSlotType.Boots,
            EquipmentSlotType.Ring,
            EquipmentSlotType.Amulet
        };

        [SerializeField, Tooltip("Parent transform hosting the generated equipment slot views.")]
        private RectTransform _slotsRoot;
        [SerializeField, Tooltip("Optional slot prefab. When empty, a runtime slot object is created.")]
        private EquipmentDropSlotView _slotPrefab;
        [SerializeField, Tooltip("Optional registry used to resolve equipped item icons for slot visuals and reverse drag ghost icon.")]
        private EquipmentDefinitionRegistry _equipmentDefinitionRegistry;
        [SerializeField, Tooltip("Optional shared drag ghost root used by equipment-slot reverse drag handlers.")]
        private RectTransform _dragGhostRoot;
        [SerializeField, Min(1f), Tooltip("Default slot width used for runtime-created slots when no prefab is provided.")]
        private float _runtimeSlotWidth = 56f;
        [SerializeField, Min(1f), Tooltip("Default slot height used for runtime-created slots when no prefab is provided.")]
        private float _runtimeSlotHeight = 56f;

        private readonly List<EquipmentDropSlotView> _slotViews = new List<EquipmentDropSlotView>(SlotOrder.Length);

        public IReadOnlyList<EquipmentDropSlotView> SlotViews => _slotViews;

        public void SetEquipmentDefinitionRegistry(EquipmentDefinitionRegistry equipmentDefinitionRegistry)
        {
            _equipmentDefinitionRegistry = equipmentDefinitionRegistry;
        }

        public void SetDragGhostRoot(RectTransform dragGhostRoot)
        {
            _dragGhostRoot = dragGhostRoot;
            for (int i = 0; i < _slotViews.Count; i++)
            {
                EquipmentDropSlotView slot = _slotViews[i];
                if (slot == null)
                {
                    continue;
                }

                slot.SetDragGhostRoot(_dragGhostRoot);
            }
        }

        private void Awake()
        {
            EnsureSlots();
            RefreshForUnit(null);
        }

        public void RefreshForUnit(OwnedUnitData unit)
        {
            EnsureSlots();
            if (_slotViews.Count == 0)
            {
                return;
            }

            EquipmentSlotEntry[] equippedItems = unit != null
                ? unit.EquippedItems
                : Array.Empty<EquipmentSlotEntry>();

            for (int i = 0; i < _slotViews.Count; i++)
            {
                EquipmentDropSlotView slotView = _slotViews[i];
                if (slotView == null)
                {
                    continue;
                }

                EquipmentSlotType slotType = slotView.SlotType;
                bool hasEquipped = false;
                string equippedDefinitionId = null;
                EquipmentDefinition equippedDefinition = null;

                for (int j = 0; j < equippedItems.Length; j++)
                {
                    EquipmentSlotEntry entry = equippedItems[j];
                    if (entry.SlotType != slotType)
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(entry.EquipmentDefinitionId))
                    {
                        continue;
                    }

                    hasEquipped = true;
                    equippedDefinitionId = entry.EquipmentDefinitionId;
                    equippedDefinition = ResolveDefinition(entry.EquipmentDefinitionId);
                    break;
                }

                slotView.SetDropPreviewState(false, false);
                slotView.SetEquippedItem(equippedDefinitionId, equippedDefinition);
                slotView.SetCompletionVisual(hasEquipped);
            }
        }

        private void EnsureSlots()
        {
            RectTransform root = ResolveSlotsRoot();
            if (root == null)
            {
                _slotViews.Clear();
                return;
            }

            RebuildSlotCache(root);
            while (_slotViews.Count < SlotOrder.Length)
            {
                EquipmentDropSlotView created = CreateSlot(root, _slotViews.Count);
                if (created == null)
                {
                    break;
                }

                _slotViews.Add(created);
            }

            for (int i = 0; i < _slotViews.Count; i++)
            {
                EquipmentDropSlotView slotView = _slotViews[i];
                if (slotView == null)
                {
                    continue;
                }

                bool shouldBeActive = i < SlotOrder.Length;
                if (slotView.gameObject.activeSelf != shouldBeActive)
                {
                    slotView.gameObject.SetActive(shouldBeActive);
                }

                if (!shouldBeActive)
                {
                    continue;
                }

                slotView.transform.SetSiblingIndex(i);
                slotView.SetSlotType(SlotOrder[i]);
                slotView.SetDragGhostRoot(_dragGhostRoot);
            }
        }

        private RectTransform ResolveSlotsRoot()
        {
            if (_slotsRoot != null)
            {
                return _slotsRoot;
            }

            _slotsRoot = transform as RectTransform;
            return _slotsRoot;
        }

        private void RebuildSlotCache(RectTransform root)
        {
            _slotViews.Clear();

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                EquipmentDropSlotView slotView = child.GetComponent<EquipmentDropSlotView>();
                if (slotView != null)
                {
                    _slotViews.Add(slotView);
                }
            }
        }

        private EquipmentDropSlotView CreateSlot(RectTransform root, int index)
        {
            EquipmentDropSlotView slotView = null;

            if (_slotPrefab != null)
            {
                slotView = Instantiate(_slotPrefab, root);
            }
            else
            {
                var go = new GameObject(
                    $"EquipmentSlot_{index + 1}",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(LayoutElement),
                    typeof(EquipmentDropSlotView));
                go.transform.SetParent(root, false);
                slotView = go.GetComponent<EquipmentDropSlotView>();

                RectTransform rect = go.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(_runtimeSlotWidth, _runtimeSlotHeight);

                LayoutElement layout = go.GetComponent<LayoutElement>();
                layout.preferredWidth = _runtimeSlotWidth;
                layout.preferredHeight = _runtimeSlotHeight;
                layout.minWidth = _runtimeSlotWidth;
                layout.minHeight = _runtimeSlotHeight;

                Image image = go.GetComponent<Image>();
                image.color = new Color(1f, 1f, 1f, 0.08f);
            }

            if (slotView == null)
            {
                return null;
            }

            slotView.gameObject.SetActive(true);
            slotView.transform.SetSiblingIndex(index);
            slotView.SetDragGhostRoot(_dragGhostRoot);
            return slotView;
        }

        private EquipmentDefinition ResolveDefinition(string definitionId)
        {
            if (string.IsNullOrWhiteSpace(definitionId))
            {
                return null;
            }

            if (_equipmentDefinitionRegistry != null)
            {
                EquipmentDefinition fromRegistry = _equipmentDefinitionRegistry.GetById(definitionId);
                if (fromRegistry != null)
                {
                    return fromRegistry;
                }
            }

            EquipmentDefinitionRegistry[] registries = Resources.FindObjectsOfTypeAll<EquipmentDefinitionRegistry>();
            for (int i = 0; i < registries.Length; i++)
            {
                EquipmentDefinitionRegistry registry = registries[i];
                if (registry == null)
                {
                    continue;
                }

                EquipmentDefinition definition = registry.GetById(definitionId);
                if (definition != null)
                {
                    _equipmentDefinitionRegistry = registry;
                    return definition;
                }
            }

            return null;
        }
    }
}
