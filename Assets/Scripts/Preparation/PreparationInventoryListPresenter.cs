using System;
using System.Collections.Generic;
using SevenBattles.Core.Diagnostics;
using SevenBattles.Core.Items;
using SevenBattles.Core.Players;
using UnityEngine;

namespace SevenBattles.Preparation
{
    /// <summary>
    /// Populates InventoryView/Right_Panel/ScrollRect/Viewport/Content with pooled item-entry views.
    /// </summary>
    public sealed class PreparationInventoryListPresenter : MonoBehaviour
    {
        [SerializeField, Tooltip("Optional explicit PlayerContext. RuntimeInstance is used first when available.")]
        private PlayerContext _playerContext;
        [SerializeField, Tooltip("Optional inventory panel root used for context and hierarchy resolution.")]
        private GameObject _inventoryPanelRoot;
        [SerializeField, Tooltip("ScrollRect content root that receives pooled Item entries.")]
        private RectTransform _contentRoot;
        [SerializeField, Tooltip("Item entry prefab. Falls back to existing children under Content when empty.")]
        private GameObject _itemPrefab;
        [SerializeField, Tooltip("Optional registry to resolve equipment definitions by ID.")]
        private EquipmentDefinitionRegistry _equipmentDefinitionRegistry;
        [SerializeField, Tooltip("Optional registry to resolve item definitions by ID.")]
        private ItemDefinitionRegistry _itemDefinitionRegistry;
        [SerializeField, Tooltip("Fallback icon when a definition is missing icon data.")]
        private Sprite _fallbackIcon;
        [SerializeField, Tooltip("Fallback background tint when a definition is missing color data.")]
        private Color _fallbackBackgroundColor = Color.white;
        [Header("Category Filter")]
        [SerializeField] private bool _includeEquipment = true;
        [SerializeField] private bool _includeItems = true;

        private readonly List<InventoryEntry> _visibleEntries = new List<InventoryEntry>(64);
        private readonly List<PreparationInventoryItemEntryView> _pool = new List<PreparationInventoryItemEntryView>(64);
        private readonly Dictionary<string, EquipmentDefinition> _equipmentFallbackLookup = new Dictionary<string, EquipmentDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, ItemDefinition> _itemFallbackLookup = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);

        private PlayerInventory _subscribedInventory;
        private bool _fallbackLookupBuilt;
        private bool _poolWarmedFromContent;
        private bool _missingContentWarnLogged;

        public void Configure(
            PlayerContext playerContext,
            GameObject inventoryPanelRoot,
            RectTransform contentRoot,
            GameObject itemPrefab,
            EquipmentDefinitionRegistry equipmentDefinitionRegistry,
            ItemDefinitionRegistry itemDefinitionRegistry)
        {
            _playerContext = playerContext;
            _inventoryPanelRoot = inventoryPanelRoot;
            _contentRoot = contentRoot;
            _itemPrefab = itemPrefab;
            _equipmentDefinitionRegistry = equipmentDefinitionRegistry;
            _itemDefinitionRegistry = itemDefinitionRegistry;

            RefreshNow();
        }

        public void RefreshNow()
        {
            ResolveContextAndInventory();
            ResolveContentRootIfMissing();
            WarmPoolFromContentIfNeeded();

            if (_contentRoot == null)
            {
                if (!_missingContentWarnLogged)
                {
                    SBLog.Warn("PreparationInventoryListPresenter: Content root is missing. Inventory list cannot be populated.", this);
                    _missingContentWarnLogged = true;
                }

                return;
            }

            BuildVisibleEntries();
            EnsurePoolSize(_visibleEntries.Count);
            BindVisibleEntries();
            DeactivateUnusedEntries(_visibleEntries.Count);
        }

        private void OnEnable()
        {
            ResolveContextAndInventory();
            RefreshNow();
        }

        private void OnDisable()
        {
            UnsubscribeFromInventory();
        }

        private void ResolveContextAndInventory()
        {
            if (PlayerContext.HasRuntimeInstance && PlayerContext.RuntimeInstance != null)
            {
                _playerContext = PlayerContext.RuntimeInstance;
            }
            else if (_playerContext == null)
            {
                var contexts = Resources.FindObjectsOfTypeAll<PlayerContext>();
                if (contexts != null && contexts.Length > 0)
                {
                    _playerContext = contexts[0];
                }
            }

            PlayerInventory nextInventory = _playerContext != null ? _playerContext.Inventory : null;
            if (ReferenceEquals(nextInventory, _subscribedInventory))
            {
                return;
            }

            UnsubscribeFromInventory();

            _subscribedInventory = nextInventory;
            if (_subscribedInventory != null)
            {
                _subscribedInventory.InventoryChanged += HandleInventoryChanged;
            }
        }

        private void UnsubscribeFromInventory()
        {
            if (_subscribedInventory == null)
            {
                return;
            }

            _subscribedInventory.InventoryChanged -= HandleInventoryChanged;
            _subscribedInventory = null;
        }

        private void HandleInventoryChanged()
        {
            RefreshNow();
        }

        private void ResolveContentRootIfMissing()
        {
            if (_contentRoot != null)
            {
                return;
            }

            Transform panelTransform = _inventoryPanelRoot != null ? _inventoryPanelRoot.transform : null;
            if (panelTransform != null)
            {
                Transform resolved = panelTransform.Find("Canvas/InventoryView/Right_Panel/ScrollRect/Viewport/Content");
                _contentRoot = resolved as RectTransform;
            }
        }

        private void WarmPoolFromContentIfNeeded()
        {
            if (_poolWarmedFromContent || _contentRoot == null)
            {
                return;
            }

            _poolWarmedFromContent = true;
            for (int i = 0; i < _contentRoot.childCount; i++)
            {
                Transform child = _contentRoot.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                var view = child.GetComponent<PreparationInventoryItemEntryView>();
                if (view == null && string.Equals(child.name, "Item", StringComparison.Ordinal))
                {
                    view = child.gameObject.AddComponent<PreparationInventoryItemEntryView>();
                }

                if (view == null)
                {
                    continue;
                }

                _pool.Add(view);
                view.gameObject.SetActive(false);
            }

            if (_itemPrefab == null && _pool.Count > 0)
            {
                _itemPrefab = _pool[0].gameObject;
            }
        }

        private void BuildVisibleEntries()
        {
            _visibleEntries.Clear();
            if (_subscribedInventory == null)
            {
                return;
            }

            int count = _subscribedInventory.CollectEntriesNonAlloc(
                _visibleEntries,
                includeEquipment: _includeEquipment,
                includeSpells: false,
                includeItems: _includeItems);
            if (count <= 0)
            {
                return;
            }

            _visibleEntries.Sort(CompareEntries);
        }

        private static int CompareEntries(InventoryEntry a, InventoryEntry b)
        {
            if (ReferenceEquals(a, b))
            {
                return 0;
            }

            if (a == null)
            {
                return 1;
            }

            if (b == null)
            {
                return -1;
            }

            int kindCompare = a.Kind.CompareTo(b.Kind);
            if (kindCompare != 0)
            {
                return kindCompare;
            }

            return string.Compare(a.DefinitionId, b.DefinitionId, StringComparison.Ordinal);
        }

        private void EnsurePoolSize(int requiredCount)
        {
            if (requiredCount <= _pool.Count)
            {
                return;
            }

            if (_contentRoot == null)
            {
                return;
            }

            GameObject prefab = _itemPrefab;
            if (prefab == null && _pool.Count > 0)
            {
                prefab = _pool[0].gameObject;
            }

            if (prefab == null)
            {
                SBLog.Warn("PreparationInventoryListPresenter: Item prefab/template is missing. Cannot grow inventory pool.", this);
                return;
            }

            while (_pool.Count < requiredCount)
            {
                GameObject instanceObject = Instantiate(prefab, _contentRoot);
                instanceObject.name = "Item";
                instanceObject.SetActive(false);
                PreparationInventoryItemEntryView instance = instanceObject.GetComponent<PreparationInventoryItemEntryView>();
                if (instance == null)
                {
                    instance = instanceObject.AddComponent<PreparationInventoryItemEntryView>();
                }
                _pool.Add(instance);
            }
        }

        private void BindVisibleEntries()
        {
            int count = Mathf.Min(_visibleEntries.Count, _pool.Count);
            for (int i = 0; i < count; i++)
            {
                InventoryEntry entry = _visibleEntries[i];
                PreparationInventoryItemEntryView view = _pool[i];
                if (entry == null || view == null)
                {
                    continue;
                }

                ResolvePresentation(entry, out Sprite icon, out Color backgroundColor, out int quantity);
                view.Bind(icon, backgroundColor, quantity, _fallbackIcon, _fallbackBackgroundColor);
                view.transform.SetSiblingIndex(i);
                view.gameObject.SetActive(true);
            }
        }

        private void DeactivateUnusedEntries(int usedCount)
        {
            for (int i = usedCount; i < _pool.Count; i++)
            {
                PreparationInventoryItemEntryView view = _pool[i];
                if (view != null && view.gameObject.activeSelf)
                {
                    view.gameObject.SetActive(false);
                }
            }
        }

        private void ResolvePresentation(InventoryEntry entry, out Sprite icon, out Color backgroundColor, out int quantity)
        {
            icon = null;
            backgroundColor = _fallbackBackgroundColor;
            quantity = 1;

            if (entry == null)
            {
                return;
            }

            switch (entry.Kind)
            {
                case InventoryEntry.EntryKind.Equipment:
                {
                    EquipmentDefinition definition = ResolveEquipmentDefinition(entry.DefinitionId);
                    if (definition != null)
                    {
                        icon = definition.Icon;
                        backgroundColor = definition.InventoryBackgroundColor;
                    }

                    quantity = Mathf.Max(1, entry.Quantity);
                    break;
                }
                case InventoryEntry.EntryKind.Item:
                {
                    ItemDefinition definition = ResolveItemDefinition(entry.DefinitionId);
                    if (definition != null)
                    {
                        icon = definition.Icon;
                        backgroundColor = definition.InventoryBackgroundColor;
                    }

                    quantity = Mathf.Max(1, entry.Quantity);
                    break;
                }
            }
        }

        private EquipmentDefinition ResolveEquipmentDefinition(string definitionId)
        {
            if (string.IsNullOrWhiteSpace(definitionId))
            {
                return null;
            }

            EquipmentDefinition fromRegistry = _equipmentDefinitionRegistry != null
                ? _equipmentDefinitionRegistry.GetById(definitionId)
                : null;
            if (fromRegistry != null)
            {
                return fromRegistry;
            }

            EnsureFallbackLookupsBuilt();
            _equipmentFallbackLookup.TryGetValue(definitionId, out EquipmentDefinition fromFallback);
            return fromFallback;
        }

        private ItemDefinition ResolveItemDefinition(string definitionId)
        {
            if (string.IsNullOrWhiteSpace(definitionId))
            {
                return null;
            }

            ItemDefinition fromRegistry = _itemDefinitionRegistry != null
                ? _itemDefinitionRegistry.GetById(definitionId)
                : null;
            if (fromRegistry != null)
            {
                return fromRegistry;
            }

            EnsureFallbackLookupsBuilt();
            _itemFallbackLookup.TryGetValue(definitionId, out ItemDefinition fromFallback);
            return fromFallback;
        }

        private void EnsureFallbackLookupsBuilt()
        {
            if (_fallbackLookupBuilt)
            {
                return;
            }

            _fallbackLookupBuilt = true;
            _equipmentFallbackLookup.Clear();
            _itemFallbackLookup.Clear();

            EquipmentDefinition[] equipmentDefinitions = Resources.FindObjectsOfTypeAll<EquipmentDefinition>();
            for (int i = 0; i < equipmentDefinitions.Length; i++)
            {
                EquipmentDefinition definition = equipmentDefinitions[i];
                if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
                {
                    continue;
                }

                if (!_equipmentFallbackLookup.ContainsKey(definition.Id))
                {
                    _equipmentFallbackLookup.Add(definition.Id, definition);
                }
            }

            ItemDefinition[] itemDefinitions = Resources.FindObjectsOfTypeAll<ItemDefinition>();
            for (int i = 0; i < itemDefinitions.Length; i++)
            {
                ItemDefinition definition = itemDefinitions[i];
                if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
                {
                    continue;
                }

                if (!_itemFallbackLookup.ContainsKey(definition.Id))
                {
                    _itemFallbackLookup.Add(definition.Id, definition);
                }
            }
        }
    }
}
