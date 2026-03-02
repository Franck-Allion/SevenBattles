using System;
using System.Collections.Generic;
using SevenBattles.Core.Diagnostics;
using SevenBattles.Core.Items;
using SevenBattles.Core.Players;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SevenBattles.Preparation
{
    /// <summary>
    /// Populates InventoryView/Right_Panel/ScrollRect/Viewport/Content with pooled item-entry views.
    /// </summary>
    public sealed class PreparationInventoryListPresenter : MonoBehaviour
    {
        private const string INVENTORY_ITEMS_CONTENT_PATH = "Canvas/InventoryView/Right_Panel/ScrollRect/Viewport/Content";
        private const string DEFAULT_PAGE_BUTTONS_ROOT_NAME = "Pages";
        private const string DEFAULT_DRAG_GHOST_NAME = "InventoryDragGhost";
        private const int PAGE_COLUMNS = 6;
        private const int PAGE_ROWS = 5;
        private const int PAGE_SIZE = PAGE_COLUMNS * PAGE_ROWS;

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
        [SerializeField, Tooltip("Optional rarity palette. When set, inventory backgrounds use palette colors by rarity.")]
        private ItemRarityColorPalette _rarityColorPalette;
        [Header("Pagination Buttons")]
        [SerializeField, Tooltip("Optional explicit root that receives dynamic page buttons. Auto-resolved by object name when null.")]
        private RectTransform _pageButtonsRoot;
        [SerializeField, Tooltip("Optional page button prefab used to grow the page-button pool.")]
        private GameObject _pageButtonPrefab;
        [SerializeField, Tooltip("Object name used to auto-find the page-buttons root under InventoryPanel.")]
        private string _pageButtonsRootObjectName = DEFAULT_PAGE_BUTTONS_ROOT_NAME;
        [SerializeField, Tooltip("Legacy explicit button for page 1. Used as fallback seed if dynamic root/prefab are missing.")]
        private Button _page1Button;
        [SerializeField, Tooltip("Legacy explicit button for page 2. Used as fallback seed if dynamic root/prefab are missing.")]
        private Button _page2Button;
        [SerializeField, Tooltip("Legacy explicit button for page 3. Used as fallback seed if dynamic root/prefab are missing.")]
        private Button _page3Button;
        [Header("Category Filter")]
        [SerializeField] private bool _includeEquipment = true;
        [SerializeField] private bool _includeItems = true;
        [Header("Inventory Tooltip")]
        [SerializeField, Tooltip("If enabled, inventory item tooltips use a custom cursor offset.")]
        private bool _overrideInventoryTooltipCursorOffset = true;
        [SerializeField, Tooltip("Tooltip offset from mouse position in canvas-space UI units.")]
        private Vector2 _inventoryTooltipCursorOffset = new Vector2(36f, -30f);
        [Header("Inventory Drag")]
        [SerializeField, Tooltip("Optional shared drag ghost root used by inventory item drag handlers. Auto-created under the root canvas when null.")]
        private RectTransform _dragGhostRoot;

        private readonly List<InventoryEntry> _visibleEntries = new List<InventoryEntry>(64);
        private readonly Dictionary<string, InventoryEntry> _visibleEntriesByKey = new Dictionary<string, InventoryEntry>(64);
        private readonly List<PreparationInventoryItemEntryView> _itemPool = new List<PreparationInventoryItemEntryView>(PAGE_SIZE);
        private readonly List<GameObject> _emptyPool = new List<GameObject>(PAGE_SIZE);
        private readonly Dictionary<string, EquipmentDefinition> _equipmentFallbackLookup = new Dictionary<string, EquipmentDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, ItemDefinition> _itemFallbackLookup = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);
        private readonly List<PageButtonView> _pageButtonPool = new List<PageButtonView>(8);

        private PlayerInventory _subscribedInventory;
        private bool _fallbackLookupBuilt;
        private bool _poolWarmedFromContent;
        private bool _missingItemTemplateWarnLogged;
        private bool _missingItemEmptyTemplateWarnLogged;
        private bool _missingContentWarnLogged;
        private bool _pageButtonsWarmed;
        private bool _missingPageButtonsRootWarnLogged;
        private bool _missingPageTemplateWarnLogged;
        private bool _missingDragGhostWarnLogged;
        private int _currentPageIndex;
        private int _activePageCount = 1;
        private InventoryDropZone _inventoryDropZone;

        private sealed class PageButtonView
        {
            private readonly TMP_Text _labelTMP;
            private readonly Text _labelText;
            private UnityAction _clickAction;

            public PageButtonView(Button button, TMP_Text labelTMP, Text labelText)
            {
                Button = button;
                _labelTMP = labelTMP;
                _labelText = labelText;
            }

            public Button Button { get; }

            public void Bind(int pageNumber, Action<int> onPageSelected)
            {
                RemoveBinding();
                _clickAction = () => onPageSelected?.Invoke(pageNumber);
                Button.onClick.AddListener(_clickAction);

                string label = pageNumber.ToString();
                if (_labelTMP != null)
                {
                    _labelTMP.text = label;
                }
                else if (_labelText != null)
                {
                    _labelText.text = label;
                }

                Button.gameObject.name = $"Page{label}";
                if (!Button.gameObject.activeSelf)
                {
                    Button.gameObject.SetActive(true);
                }
            }

            public void SetSelected(bool selected)
            {
                if (Button != null)
                {
                    Button.interactable = !selected;
                }
            }

            public void Hide()
            {
                RemoveBinding();
                if (Button != null && Button.gameObject.activeSelf)
                {
                    Button.gameObject.SetActive(false);
                }
            }

            public void RemoveBinding()
            {
                if (Button != null && _clickAction != null)
                {
                    Button.onClick.RemoveListener(_clickAction);
                }

                _clickAction = null;
            }
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
            Configure(
                playerContext,
                inventoryPanelRoot,
                contentRoot,
                itemPrefab,
                itemEmptyPrefab,
                _pageButtonsRoot,
                _pageButtonPrefab,
                equipmentDefinitionRegistry,
                itemDefinitionRegistry);
        }

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
                _pageButtonsRoot,
                _pageButtonPrefab,
                equipmentDefinitionRegistry,
                itemDefinitionRegistry);
        }

        public void Configure(
            PlayerContext playerContext,
            GameObject inventoryPanelRoot,
            RectTransform contentRoot,
            GameObject itemPrefab,
            GameObject itemEmptyPrefab,
            RectTransform pageButtonsRoot,
            GameObject pageButtonPrefab,
            EquipmentDefinitionRegistry equipmentDefinitionRegistry,
            ItemDefinitionRegistry itemDefinitionRegistry)
        {
            _playerContext = playerContext;
            _inventoryPanelRoot = inventoryPanelRoot;
            _contentRoot = contentRoot;
            _itemPrefab = itemPrefab;
            _itemEmptyPrefab = itemEmptyPrefab;
            _pageButtonsRoot = pageButtonsRoot;
            _pageButtonPrefab = pageButtonPrefab;
            _equipmentDefinitionRegistry = equipmentDefinitionRegistry;
            _itemDefinitionRegistry = itemDefinitionRegistry;

            RefreshNow();
        }

        public void SetRarityColorPalette(ItemRarityColorPalette rarityColorPalette)
        {
            _rarityColorPalette = rarityColorPalette;
            RefreshNow();
        }

        public void ShowPage(int pageNumber)
        {
            int pageIndex = Mathf.Clamp(pageNumber - 1, 0, Mathf.Max(0, _activePageCount - 1));
            if (_currentPageIndex == pageIndex)
            {
                return;
            }

            CancelActiveItemDrags();
            _currentPageIndex = pageIndex;
            BindCurrentPage();
            RefreshPageButtonSelection();
        }

        public void RefreshNow()
        {
            ResolveContextAndInventory();
            ResolveContentRootIfMissing();
            ResolveDragGhostRootIfMissing();
            EnsureInventoryDropZone();
            WarmPoolFromContentIfNeeded();
            ResolvePageButtonsRootIfMissing();
            WarmPageButtonsFromRootIfNeeded();

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
            _activePageCount = CalculatePageCount(_visibleEntries.Count);
            _currentPageIndex = Mathf.Clamp(_currentPageIndex, 0, _activePageCount - 1);
            EnsurePageButtonPoolSize(_activePageCount);
            BindPageButtons(_activePageCount);
            EnsureItemPoolSize(PAGE_SIZE);
            EnsureEmptyPoolSize(PAGE_SIZE);
            BindCurrentPage();
            RefreshPageButtonSelection();
        }

        private void OnEnable()
        {
            ResolveContextAndInventory();
            RefreshNow();
        }

        private void OnDisable()
        {
            CancelActiveItemDrags();
            UnbindAllPageButtons();
            UnsubscribeFromInventory();
            if (_dragGhostRoot != null)
            {
                _dragGhostRoot.gameObject.SetActive(false);
            }
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
                    EnsureDragComponents(view);
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
            _visibleEntriesByKey.Clear();
            if (_subscribedInventory == null)
            {
                return;
            }

            List<InventoryEntry> sourceEntries = _subscribedInventory.Entries;
            if (sourceEntries == null || sourceEntries.Count == 0)
            {
                return;
            }

            for (int i = 0; i < sourceEntries.Count; i++)
            {
                InventoryEntry sourceEntry = sourceEntries[i];
                if (sourceEntry == null || string.IsNullOrWhiteSpace(sourceEntry.DefinitionId))
                {
                    continue;
                }

                if (!ShouldIncludeEntryKind(sourceEntry.Kind))
                {
                    continue;
                }

                int quantity = sourceEntry.Kind == InventoryEntry.EntryKind.Item ? sourceEntry.Quantity : 1;
                if (quantity <= 0)
                {
                    continue;
                }

                string entryKey = sourceEntry.EntryKey;
                if (_visibleEntriesByKey.TryGetValue(entryKey, out InventoryEntry groupedEntry))
                {
                    groupedEntry.Quantity += quantity;
                    continue;
                }

                groupedEntry = new InventoryEntry
                {
                    Kind = sourceEntry.Kind,
                    DefinitionId = sourceEntry.DefinitionId,
                    Quantity = quantity
                };

                _visibleEntriesByKey.Add(entryKey, groupedEntry);
                _visibleEntries.Add(groupedEntry);
            }

            _visibleEntries.Sort(CompareEntries);
        }

        private bool ShouldIncludeEntryKind(InventoryEntry.EntryKind kind)
        {
            switch (kind)
            {
                case InventoryEntry.EntryKind.Equipment:
                    return _includeEquipment;
                case InventoryEntry.EntryKind.Item:
                    return _includeItems;
                case InventoryEntry.EntryKind.Spell:
                    return false;
                default:
                    return false;
            }
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
                PreparationInventoryItemEntryView instance = WarmInventoryItemInstances(instanceObject);
                if (instance == null)
                {
                    continue;
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
                    ResolvePresentation(
                        entry,
                        out Sprite icon,
                        out Color backgroundColor,
                        out int quantity,
                        out string displayName,
                        out EquipmentDefinition equipmentDefinition,
                        out ItemDefinition itemDefinition);
                    itemView.ConfigureTooltipCursorOffset(_overrideInventoryTooltipCursorOffset, _inventoryTooltipCursorOffset);
                    itemView.Bind(icon, backgroundColor, quantity, _fallbackIcon, _fallbackBackgroundColor, displayName);
                    itemView.SetBoundData(entry, equipmentDefinition, itemDefinition);
                    ConfigureItemDrag(itemView, entry, equipmentDefinition, itemDefinition);
                    SetSlotActive(itemView.gameObject, slotIndex, true);
                    SetSlotActive(emptyView, slotIndex, false);
                }
                else
                {
                    if (itemView != null)
                    {
                        itemView.SetBoundData(null, null, null);
                    }
                    ConfigureItemDrag(itemView, null, null, null);
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

        private void ResolvePageButtonsRootIfMissing()
        {
            if (_pageButtonsRoot != null)
            {
                return;
            }

            if (_inventoryPanelRoot == null || string.IsNullOrWhiteSpace(_pageButtonsRootObjectName))
            {
                return;
            }

            Transform[] nodes = _inventoryPanelRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < nodes.Length; i++)
            {
                Transform node = nodes[i];
                if (node is RectTransform rect &&
                    string.Equals(node.name, _pageButtonsRootObjectName, StringComparison.Ordinal))
                {
                    _pageButtonsRoot = rect;
                    return;
                }
            }
        }

        private void WarmPageButtonsFromRootIfNeeded()
        {
            if (_pageButtonsWarmed)
            {
                return;
            }

            _pageButtonsWarmed = true;
            _pageButtonPool.Clear();

            if (_pageButtonsRoot != null)
            {
                for (int i = 0; i < _pageButtonsRoot.childCount; i++)
                {
                    Transform child = _pageButtonsRoot.GetChild(i);
                    if (child == null)
                    {
                        continue;
                    }

                    Button button = child.GetComponent<Button>();
                    if (button == null)
                    {
                        button = child.GetComponentInChildren<Button>(true);
                    }

                    if (button == null)
                    {
                        continue;
                    }

                    AddPageButtonToPool(button);
                }
            }

            AddLegacyButtonToPool(_page1Button);
            AddLegacyButtonToPool(_page2Button);
            AddLegacyButtonToPool(_page3Button);

            if (_pageButtonPrefab == null && _pageButtonPool.Count > 0)
            {
                _pageButtonPrefab = _pageButtonPool[0].Button.gameObject;
            }
        }

        private void AddLegacyButtonToPool(Button button)
        {
            if (button == null)
            {
                return;
            }

            if (_pageButtonsRoot == null && button.transform.parent is RectTransform parent)
            {
                _pageButtonsRoot = parent;
            }

            AddPageButtonToPool(button);
        }

        private void AddPageButtonToPool(Button button)
        {
            if (button == null)
            {
                return;
            }

            for (int i = 0; i < _pageButtonPool.Count; i++)
            {
                if (ReferenceEquals(_pageButtonPool[i].Button, button))
                {
                    return;
                }
            }

            TMP_Text labelTMP = button.GetComponentInChildren<TMP_Text>(true);
            Text labelText = labelTMP == null ? button.GetComponentInChildren<Text>(true) : null;
            var view = new PageButtonView(button, labelTMP, labelText);
            view.Hide();
            _pageButtonPool.Add(view);
        }

        private void EnsurePageButtonPoolSize(int requiredCount)
        {
            if (requiredCount <= _pageButtonPool.Count)
            {
                return;
            }

            if (_pageButtonsRoot == null)
            {
                if (!_missingPageButtonsRootWarnLogged)
                {
                    SBLog.Warn("PreparationInventoryListPresenter: Page-buttons root is missing. Dynamic pagination buttons cannot be created.", this);
                    _missingPageButtonsRootWarnLogged = true;
                }

                return;
            }

            GameObject template = _pageButtonPrefab;
            if (template == null && _pageButtonPool.Count > 0)
            {
                template = _pageButtonPool[0].Button.gameObject;
            }

            if (template == null)
            {
                if (!_missingPageTemplateWarnLogged)
                {
                    SBLog.Warn("PreparationInventoryListPresenter: Page button prefab/template is missing. Cannot grow page-button pool.", this);
                    _missingPageTemplateWarnLogged = true;
                }

                return;
            }

            while (_pageButtonPool.Count < requiredCount)
            {
                GameObject instanceObject = Instantiate(template, _pageButtonsRoot);
                Button button = instanceObject.GetComponent<Button>();
                if (button == null)
                {
                    button = instanceObject.GetComponentInChildren<Button>(true);
                }

                if (button == null)
                {
                    SBLog.Warn("PreparationInventoryListPresenter: Page button instance has no Button component.", this);
                    Destroy(instanceObject);
                    return;
                }

                AddPageButtonToPool(button);
            }
        }

        private void BindPageButtons(int pageCount)
        {
            int boundCount = Mathf.Min(pageCount, _pageButtonPool.Count);
            for (int i = 0; i < boundCount; i++)
            {
                PageButtonView view = _pageButtonPool[i];
                int pageNumber = i + 1;
                view.Bind(pageNumber, HandleDynamicPageClicked);
                view.Button.transform.SetSiblingIndex(i);
            }

            for (int i = boundCount; i < _pageButtonPool.Count; i++)
            {
                _pageButtonPool[i].Hide();
            }
        }

        private void RefreshPageButtonSelection()
        {
            int visibleCount = Mathf.Min(_activePageCount, _pageButtonPool.Count);
            for (int i = 0; i < visibleCount; i++)
            {
                _pageButtonPool[i].SetSelected(i == _currentPageIndex);
            }
        }

        private void UnbindAllPageButtons()
        {
            for (int i = 0; i < _pageButtonPool.Count; i++)
            {
                _pageButtonPool[i].RemoveBinding();
            }
        }

        private static int CalculatePageCount(int itemCount)
        {
            if (itemCount <= 0)
            {
                return 1;
            }

            return ((itemCount - 1) / PAGE_SIZE) + 1;
        }

        private void HandleDynamicPageClicked(int pageNumber)
        {
            ShowPage(pageNumber);
        }

        private void ResolvePresentation(
            InventoryEntry entry,
            out Sprite icon,
            out Color backgroundColor,
            out int quantity,
            out string displayName,
            out EquipmentDefinition equipmentDefinition,
            out ItemDefinition itemDefinition)
        {
            icon = null;
            backgroundColor = _fallbackBackgroundColor;
            quantity = 1;
            displayName = string.Empty;
            equipmentDefinition = null;
            itemDefinition = null;

            if (entry == null)
            {
                return;
            }

            switch (entry.Kind)
            {
                case InventoryEntry.EntryKind.Equipment:
                {
                    EquipmentDefinition definition = ResolveEquipmentDefinition(entry.DefinitionId);
                    equipmentDefinition = definition;
                    if (definition != null)
                    {
                        icon = definition.Icon;
                        backgroundColor = ItemRarityColorUtility.GetInventoryBackgroundColor(definition.Rarity, _rarityColorPalette);
                    }

                    quantity = Mathf.Max(1, entry.Quantity);
                    displayName = ResolveDisplayName(definition != null ? definition.Name : null, entry.DefinitionId);
                    break;
                }
                case InventoryEntry.EntryKind.Item:
                {
                    ItemDefinition definition = ResolveItemDefinition(entry.DefinitionId);
                    itemDefinition = definition;
                    if (definition != null)
                    {
                        icon = definition.Icon;
                        backgroundColor = ItemRarityColorUtility.GetInventoryBackgroundColor(definition.Rarity, _rarityColorPalette);
                    }

                    quantity = Mathf.Max(1, entry.Quantity);
                    displayName = ResolveDisplayName(definition != null ? definition.Name : null, entry.DefinitionId);
                    break;
                }
            }
        }

        private void EnsureDragComponents(PreparationInventoryItemEntryView itemView)
        {
            if (itemView == null)
            {
                return;
            }

            CanvasGroup canvasGroup = itemView.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = itemView.gameObject.AddComponent<CanvasGroup>();
            }

            var dragHandler = itemView.GetComponent<InventoryItemDragHandler>();
            if (dragHandler == null)
            {
                dragHandler = itemView.gameObject.AddComponent<InventoryItemDragHandler>();
            }

            dragHandler.Initialize(_dragGhostRoot);
        }

        private void ConfigureItemDrag(
            PreparationInventoryItemEntryView itemView,
            InventoryEntry entry,
            EquipmentDefinition equipmentDefinition,
            ItemDefinition itemDefinition)
        {
            if (itemView == null)
            {
                return;
            }

            EnsureDragComponents(itemView);
            var dragHandler = itemView.GetComponent<InventoryItemDragHandler>();
            if (dragHandler == null)
            {
                return;
            }

            dragHandler.Initialize(_dragGhostRoot);
            dragHandler.ConfigureDragPayload(entry, equipmentDefinition, itemDefinition);
        }

        private PreparationInventoryItemEntryView WarmInventoryItemInstances(GameObject instanceObject)
        {
            if (instanceObject == null)
            {
                return null;
            }

            PreparationInventoryItemEntryView instance = instanceObject.GetComponent<PreparationInventoryItemEntryView>();
            if (instance == null)
            {
                instance = instanceObject.AddComponent<PreparationInventoryItemEntryView>();
            }

            CanvasGroup canvasGroup = instanceObject.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = instanceObject.AddComponent<CanvasGroup>();
            }

            InventoryItemDragHandler dragHandler = instanceObject.GetComponent<InventoryItemDragHandler>();
            if (dragHandler == null)
            {
                dragHandler = instanceObject.AddComponent<InventoryItemDragHandler>();
            }

            dragHandler.Initialize(_dragGhostRoot);
            return instance;
        }

        private void ResolveDragGhostRootIfMissing()
        {
            Canvas rootCanvas = _contentRoot != null ? _contentRoot.GetComponentInParent<Canvas>() : null;
            if (rootCanvas == null)
            {
                if (!_missingDragGhostWarnLogged)
                {
                    SBLog.Warn("PreparationInventoryListPresenter: Could not resolve root canvas for inventory drag ghost.", this);
                    _missingDragGhostWarnLogged = true;
                }

                return;
            }

            if (_dragGhostRoot != null)
            {
                if (IsDragGhostRootUsable(_dragGhostRoot, rootCanvas))
                {
                    _dragGhostRoot.gameObject.SetActive(false);
                    _missingDragGhostWarnLogged = false;
                    return;
                }

                if (!_missingDragGhostWarnLogged)
                {
                    SBLog.Warn(
                        $"PreparationInventoryListPresenter: Assigned drag ghost root '{_dragGhostRoot.name}' is not usable for inventory canvas '{rootCanvas.name}'. Recreating under inventory canvas.",
                        this);
                    _missingDragGhostWarnLogged = true;
                }

                _dragGhostRoot = null;
            }

            Transform existing = rootCanvas.transform.Find(DEFAULT_DRAG_GHOST_NAME);
            if (existing is RectTransform existingRect)
            {
                _dragGhostRoot = existingRect;
                _dragGhostRoot.gameObject.SetActive(false);
                return;
            }

            var ghostObject = new GameObject(DEFAULT_DRAG_GHOST_NAME, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            ghostObject.transform.SetParent(rootCanvas.transform, false);

            _dragGhostRoot = ghostObject.GetComponent<RectTransform>();
            _dragGhostRoot.anchorMin = new Vector2(0.5f, 0.5f);
            _dragGhostRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _dragGhostRoot.pivot = new Vector2(0.5f, 0.5f);
            _dragGhostRoot.sizeDelta = new Vector2(80f, 80f);
            _dragGhostRoot.localScale = Vector3.one;
            _dragGhostRoot.SetAsLastSibling();

            Image image = ghostObject.GetComponent<Image>();
            image.raycastTarget = false;
            image.enabled = false;

            ghostObject.SetActive(false);
            _missingDragGhostWarnLogged = false;
        }

        private static bool IsDragGhostRootUsable(RectTransform dragGhostRoot, Canvas expectedRootCanvas)
        {
            if (dragGhostRoot == null || expectedRootCanvas == null)
            {
                return false;
            }

            Canvas ghostCanvas = dragGhostRoot.GetComponentInParent<Canvas>();
            if (ghostCanvas == null)
            {
                return false;
            }

            Canvas ghostRootCanvas = ghostCanvas.isRootCanvas ? ghostCanvas : ghostCanvas.rootCanvas;
            if (ghostRootCanvas != expectedRootCanvas)
            {
                return false;
            }

            return IsParentChainActive(dragGhostRoot);
        }

        private static bool IsParentChainActive(Transform transform)
        {
            if (transform == null)
            {
                return false;
            }

            Transform parent = transform.parent;
            while (parent != null)
            {
                if (!parent.gameObject.activeSelf)
                {
                    return false;
                }

                parent = parent.parent;
            }

            return true;
        }

        private void EnsureInventoryDropZone()
        {
            if (_inventoryDropZone != null)
            {
                return;
            }

            GameObject zoneRoot = null;
            if (_inventoryPanelRoot != null)
            {
                zoneRoot = _inventoryPanelRoot;
            }
            else if (_contentRoot != null)
            {
                zoneRoot = _contentRoot.gameObject;
            }

            if (zoneRoot == null)
            {
                return;
            }

            _inventoryDropZone = zoneRoot.GetComponent<InventoryDropZone>();
            if (_inventoryDropZone == null)
            {
                _inventoryDropZone = zoneRoot.AddComponent<InventoryDropZone>();
            }
        }

        private static void CancelActiveItemDrags()
        {
            if (InventoryItemDragHandler.IsDraggingItem)
            {
                InventoryItemDragHandler.CancelActiveDrag();
            }

            if (EquipmentDropSlotView.IsDraggingEquippedItem)
            {
                EquipmentDropSlotView.CancelActiveDrag();
            }
        }

        private static string ResolveDisplayName(string definitionName, string fallbackDefinitionId)
        {
            if (!string.IsNullOrWhiteSpace(definitionName))
            {
                return definitionName;
            }

            return fallbackDefinitionId ?? string.Empty;
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
