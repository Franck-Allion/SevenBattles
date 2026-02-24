using System;
using System.Collections.Generic;
using SevenBattles.Core.Diagnostics;
using SevenBattles.Core.Items;
using SevenBattles.Core.Players;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenBattles.Preparation
{
    /// <summary>
    /// Populates InventoryView/Right_Panel/ScrollRect/Viewport/Content with pooled item-entry views.
    /// </summary>
    public sealed class PreparationInventoryListPresenter : MonoBehaviour
    {
        private const string INVENTORY_ITEMS_CONTENT_PATH = "Canvas/InventoryView/Right_Panel/ScrollRect/Viewport/Content";
        private const int PAGE_COLUMNS = 6;
        private const int PAGE_ROWS = 5;
        private const int PAGE_SIZE = PAGE_COLUMNS * PAGE_ROWS;
        private const int MAX_PAGE_COUNT = 3;

        [SerializeField, Tooltip("Optional explicit PlayerContext. RuntimeInstance is used first when available.")]
        private PlayerContext _playerContext;
        [SerializeField, Tooltip("Optional inventory panel root used for context and hierarchy resolution.")]
        private GameObject _inventoryPanelRoot;
        [SerializeField, Tooltip("ScrollRect content root that receives pooled Item entries.")]
        private RectTransform _contentRoot;
        [SerializeField, Tooltip("Item entry prefab. Falls back to existing children under Content when empty.")]
        private GameObject _itemPrefab;
        [SerializeField, Tooltip("Empty slot prefab used for unoccupied page slots.")]
        private GameObject _itemEmptyPrefab;
        [SerializeField, Tooltip("Optional registry to resolve equipment definitions by ID.")]
        private EquipmentDefinitionRegistry _equipmentDefinitionRegistry;
        [SerializeField, Tooltip("Optional registry to resolve item definitions by ID.")]
        private ItemDefinitionRegistry _itemDefinitionRegistry;
        [SerializeField, Tooltip("Fallback icon when a definition is missing icon data.")]
        private Sprite _fallbackIcon;
        [SerializeField, Tooltip("Fallback background tint when a definition is missing color data.")]
        private Color _fallbackBackgroundColor = Color.white;
        [Header("Pagination Buttons")]
        [SerializeField, Tooltip("Optional explicit button for page 1. Auto-resolved when null.")]
        private Button _page1Button;
        [SerializeField, Tooltip("Optional explicit button for page 2. Auto-resolved when null.")]
        private Button _page2Button;
        [SerializeField, Tooltip("Optional explicit button for page 3. Auto-resolved when null.")]
        private Button _page3Button;
        [Header("Category Filter")]
        [SerializeField] private bool _includeEquipment = true;
        [SerializeField] private bool _includeItems = true;

        private readonly List<InventoryEntry> _visibleEntries = new List<InventoryEntry>(64);
        private readonly List<PreparationInventoryItemEntryView> _itemPool = new List<PreparationInventoryItemEntryView>(PAGE_SIZE);
        private readonly List<GameObject> _emptyPool = new List<GameObject>(PAGE_SIZE);
        private readonly Dictionary<string, EquipmentDefinition> _equipmentFallbackLookup = new Dictionary<string, EquipmentDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, ItemDefinition> _itemFallbackLookup = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);

        private PlayerInventory _subscribedInventory;
        private bool _fallbackLookupBuilt;
        private bool _poolWarmedFromContent;
        private bool _missingItemTemplateWarnLogged;
        private bool _missingItemEmptyTemplateWarnLogged;
        private bool _missingContentWarnLogged;
        private bool _pageButtonsWired;
        private int _currentPageIndex;

        public void Configure(
            PlayerContext playerContext,
            GameObject inventoryPanelRoot,
            RectTransform contentRoot,
            GameObject itemPrefab,
            EquipmentDefinitionRegistry equipmentDefinitionRegistry,
            ItemDefinitionRegistry itemDefinitionRegistry)
        {
            Configure(
                playerContext,
                inventoryPanelRoot,
                contentRoot,
                itemPrefab,
                _itemEmptyPrefab,
                equipmentDefinitionRegistry,
                itemDefinitionRegistry);
        }

        public void Configure(
            PlayerContext playerContext,
            GameObject inventoryPanelRoot,
            RectTransform contentRoot,
            GameObject itemPrefab,
            GameObject itemEmptyPrefab,
            EquipmentDefinitionRegistry equipmentDefinitionRegistry,
            ItemDefinitionRegistry itemDefinitionRegistry)
        {
            _playerContext = playerContext;
            _inventoryPanelRoot = inventoryPanelRoot;
            _contentRoot = contentRoot;
            _itemPrefab = itemPrefab;
            _itemEmptyPrefab = itemEmptyPrefab;
            _equipmentDefinitionRegistry = equipmentDefinitionRegistry;
            _itemDefinitionRegistry = itemDefinitionRegistry;

            RefreshNow();
        }

        public void ShowPage(int pageNumber)
        {
            int pageIndex = Mathf.Clamp(pageNumber - 1, 0, MAX_PAGE_COUNT - 1);
            if (_currentPageIndex == pageIndex)
            {
                return;
            }

            _currentPageIndex = pageIndex;
            BindCurrentPage();
        }

        public void RefreshNow()
        {
            ResolveContextAndInventory();
            ResolveContentRootIfMissing();
            WarmPoolFromContentIfNeeded();
            ResolvePageButtonsIfMissing();
            WirePageButtons();

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
            EnsureItemPoolSize(PAGE_SIZE);
            EnsureEmptyPoolSize(PAGE_SIZE);
            BindCurrentPage();
        }

        private void OnEnable()
        {
            ResolveContextAndInventory();
            ResolvePageButtonsIfMissing();
            WirePageButtons();
            RefreshNow();
        }

        private void OnDisable()
        {
            UnwirePageButtons();
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
                Transform resolved = panelTransform.Find(INVENTORY_ITEMS_CONTENT_PATH);
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

                if (view != null)
                {
                    _itemPool.Add(view);
                    view.gameObject.SetActive(false);
                    continue;
                }

                if (string.Equals(child.name, "ItemEmpty", StringComparison.Ordinal))
                {
                    _emptyPool.Add(child.gameObject);
                    child.gameObject.SetActive(false);
                }
            }

            if (_itemPrefab == null && _itemPool.Count > 0)
            {
                _itemPrefab = _itemPool[0].gameObject;
            }

            if (_itemEmptyPrefab == null && _emptyPool.Count > 0)
            {
                _itemEmptyPrefab = _emptyPool[0];
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

        private void EnsureItemPoolSize(int requiredCount)
        {
            if (requiredCount <= _itemPool.Count)
            {
                return;
            }

            if (_contentRoot == null)
            {
                return;
            }

            GameObject prefab = _itemPrefab;
            if (prefab == null && _itemPool.Count > 0)
            {
                prefab = _itemPool[0].gameObject;
            }

            if (prefab == null)
            {
                if (!_missingItemTemplateWarnLogged)
                {
                    SBLog.Warn("PreparationInventoryListPresenter: Item prefab/template is missing. Cannot grow inventory pool.", this);
                    _missingItemTemplateWarnLogged = true;
                }

                return;
            }

            while (_itemPool.Count < requiredCount)
            {
                GameObject instanceObject = Instantiate(prefab, _contentRoot);
                instanceObject.name = "Item";
                instanceObject.SetActive(false);
                PreparationInventoryItemEntryView instance = instanceObject.GetComponent<PreparationInventoryItemEntryView>();
                if (instance == null)
                {
                    instance = instanceObject.AddComponent<PreparationInventoryItemEntryView>();
                }
                _itemPool.Add(instance);
            }
        }

        private void EnsureEmptyPoolSize(int requiredCount)
        {
            if (requiredCount <= _emptyPool.Count)
            {
                return;
            }

            if (_contentRoot == null)
            {
                return;
            }

            GameObject prefab = _itemEmptyPrefab;
            if (prefab == null && _emptyPool.Count > 0)
            {
                prefab = _emptyPool[0];
            }

            if (prefab == null)
            {
                if (!_missingItemEmptyTemplateWarnLogged)
                {
                    SBLog.Warn("PreparationInventoryListPresenter: ItemEmpty prefab/template is missing. Cannot grow empty-slot pool.", this);
                    _missingItemEmptyTemplateWarnLogged = true;
                }

                return;
            }

            while (_emptyPool.Count < requiredCount)
            {
                GameObject instanceObject = Instantiate(prefab, _contentRoot);
                instanceObject.name = "ItemEmpty";
                instanceObject.SetActive(false);
                _emptyPool.Add(instanceObject);
            }
        }

        private void BindCurrentPage()
        {
            int pageStart = _currentPageIndex * PAGE_SIZE;
            for (int slotIndex = 0; slotIndex < PAGE_SIZE; slotIndex++)
            {
                int entryIndex = pageStart + slotIndex;
                bool hasEntry = entryIndex >= 0 && entryIndex < _visibleEntries.Count;

                PreparationInventoryItemEntryView itemView = slotIndex < _itemPool.Count ? _itemPool[slotIndex] : null;
                GameObject emptyView = slotIndex < _emptyPool.Count ? _emptyPool[slotIndex] : null;

                if (hasEntry && itemView != null)
                {
                    InventoryEntry entry = _visibleEntries[entryIndex];
                    ResolvePresentation(entry, out Sprite icon, out Color backgroundColor, out int quantity);
                    itemView.Bind(icon, backgroundColor, quantity, _fallbackIcon, _fallbackBackgroundColor);
                    SetSlotActive(itemView.gameObject, slotIndex, true);
                    SetSlotActive(emptyView, slotIndex, false);
                }
                else
                {
                    SetSlotActive(itemView != null ? itemView.gameObject : null, slotIndex, false);
                    SetSlotActive(emptyView, slotIndex, true);
                }
            }

            DeactivateOverflow(_itemPool, PAGE_SIZE);
            DeactivateOverflow(_emptyPool, PAGE_SIZE);
        }

        private static void SetSlotActive(GameObject slotObject, int siblingIndex, bool active)
        {
            if (slotObject == null)
            {
                return;
            }

            if (active)
            {
                if (!slotObject.activeSelf)
                {
                    slotObject.SetActive(true);
                }

                slotObject.transform.SetSiblingIndex(siblingIndex);
            }
            else if (slotObject.activeSelf)
            {
                slotObject.SetActive(false);
            }
        }

        private static void DeactivateOverflow(List<PreparationInventoryItemEntryView> pool, int usedCount)
        {
            for (int i = usedCount; i < pool.Count; i++)
            {
                PreparationInventoryItemEntryView view = pool[i];
                if (view != null && view.gameObject.activeSelf)
                {
                    view.gameObject.SetActive(false);
                }
            }
        }

        private static void DeactivateOverflow(List<GameObject> pool, int usedCount)
        {
            for (int i = usedCount; i < pool.Count; i++)
            {
                GameObject view = pool[i];
                if (view != null && view.activeSelf)
                {
                    view.SetActive(false);
                }
            }
        }

        private void ResolvePageButtonsIfMissing()
        {
            Transform searchRoot = null;
            if (_inventoryPanelRoot != null)
            {
                searchRoot = _inventoryPanelRoot.transform;
            }
            else if (_contentRoot != null)
            {
                searchRoot = _contentRoot.root;
            }

            if (searchRoot == null)
            {
                return;
            }

            if (_page1Button == null)
            {
                _page1Button = FindPageButton(searchRoot, "1");
            }

            if (_page2Button == null)
            {
                _page2Button = FindPageButton(searchRoot, "2");
            }

            if (_page3Button == null)
            {
                _page3Button = FindPageButton(searchRoot, "3");
            }
        }

        private static Button FindPageButton(Transform root, string pageLabel)
        {
            if (root == null || string.IsNullOrWhiteSpace(pageLabel))
            {
                return null;
            }

            Button[] buttons = root.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = buttons[i];
                if (button == null)
                {
                    continue;
                }

                TMP_Text tmpLabel = button.GetComponentInChildren<TMP_Text>(true);
                if (tmpLabel != null && string.Equals(tmpLabel.text?.Trim(), pageLabel, StringComparison.Ordinal))
                {
                    return button;
                }

                Text uiLabel = button.GetComponentInChildren<Text>(true);
                if (uiLabel != null && string.Equals(uiLabel.text?.Trim(), pageLabel, StringComparison.Ordinal))
                {
                    return button;
                }
            }

            return null;
        }

        private void WirePageButtons()
        {
            if (_pageButtonsWired)
            {
                return;
            }

            if (_page1Button != null)
            {
                _page1Button.onClick.AddListener(HandlePage1Clicked);
            }

            if (_page2Button != null)
            {
                _page2Button.onClick.AddListener(HandlePage2Clicked);
            }

            if (_page3Button != null)
            {
                _page3Button.onClick.AddListener(HandlePage3Clicked);
            }

            _pageButtonsWired = true;
        }

        private void UnwirePageButtons()
        {
            if (!_pageButtonsWired)
            {
                return;
            }

            if (_page1Button != null)
            {
                _page1Button.onClick.RemoveListener(HandlePage1Clicked);
            }

            if (_page2Button != null)
            {
                _page2Button.onClick.RemoveListener(HandlePage2Clicked);
            }

            if (_page3Button != null)
            {
                _page3Button.onClick.RemoveListener(HandlePage3Clicked);
            }

            _pageButtonsWired = false;
        }

        private void HandlePage1Clicked()
        {
            ShowPage(1);
        }

        private void HandlePage2Clicked()
        {
            ShowPage(2);
        }

        private void HandlePage3Clicked()
        {
            ShowPage(3);
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
