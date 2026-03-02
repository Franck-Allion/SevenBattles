using System;
using System.Collections.Generic;
using SevenBattles.Core;
using SevenBattles.Core.Battle;
using SevenBattles.Core.Diagnostics;
using SevenBattles.Core.Items;
using SevenBattles.Core.Players;
using UnityEngine;
using UnityEngine.UI;

namespace SevenBattles.Preparation
{
    /// <summary>
    /// Discovers and manages inventory equipment slot frames under the preparation inventory panel.
    /// </summary>
    public sealed class InventoryEquipmentPanelPresenter : MonoBehaviour
    {
        private const string InventoryPanelName = "InventoryPanel";
        private const string EquipSlotLeftName = "EquipSlot_L";
        private const string EquipSlotRightName = "EquipSlot_R";
        private const string IconChildName = "Icon";
        private const string BackgroundChildName = "Bg";
        private const string IconFrameObject1Name = "IconFrame_Object1";
        private const string IconFrameObject2Name = "IconFrame_Object2";
        private const string IconFrameObject3Name = "IconFrame_Object3";
        private const string IconFrameObject4Name = "IconFrame_Object4";

        private static readonly Dictionary<string, EquipmentSlotType> IconFrameSlotTypes =
            new Dictionary<string, EquipmentSlotType>(StringComparer.Ordinal)
            {
                { "IconFrame_Weapon", EquipmentSlotType.Weapon },
                { "IconFrame_Shield", EquipmentSlotType.Shield },
                { "IconFrame_Helmet", EquipmentSlotType.Helmet },
                { "IconFrame_Armor", EquipmentSlotType.Armor },
                { "IconFrame_Glove", EquipmentSlotType.Gloves },
                { "IconFrame_Gloves", EquipmentSlotType.Gloves },
                { "IconFrame_Boot", EquipmentSlotType.Boots },
                { "IconFrame_Boots", EquipmentSlotType.Boots },
                { "IconFrame_Ring", EquipmentSlotType.Ring },
                { "IconFrame_Amulet", EquipmentSlotType.Amulet }
            };

        private static readonly EquipmentSlotType[] ExpectedSlotTypes =
        {
            EquipmentSlotType.Weapon,
            EquipmentSlotType.Shield,
            EquipmentSlotType.Helmet,
            EquipmentSlotType.Armor,
            EquipmentSlotType.Gloves,
            EquipmentSlotType.Boots,
            EquipmentSlotType.Ring,
            EquipmentSlotType.Amulet
        };
        private static readonly ConsumableSlotType[] ExpectedConsumableSlotTypes =
        {
            ConsumableSlotType.Object1,
            ConsumableSlotType.Object2,
            ConsumableSlotType.Object3,
            ConsumableSlotType.Object4
        };

        [Header("Panel Roots")]
        [SerializeField, Tooltip("Optional explicit InventoryPanel root. Auto-resolved by name when empty.")]
        private Transform _inventoryPanelRoot;
        [SerializeField, Tooltip("Optional explicit EquipSlot_L root. Auto-resolved when empty.")]
        private Transform _equipSlotL;
        [SerializeField, Tooltip("Optional explicit EquipSlot_R root. Auto-resolved when empty.")]
        private Transform _equipSlotR;
        [SerializeField, Tooltip("Optional explicit inventory drop zone used for reverse drag-to-unequip.")]
        private InventoryDropZone _inventoryDropZone;

        [Header("Dependencies")]
        [SerializeField, Tooltip("Optional player context used when creating EquipmentService at runtime.")]
        private PlayerContext _playerContext;
        [SerializeField, Tooltip("Optional equipment registry used when creating EquipmentService and resolving icons.")]
        private EquipmentDefinitionRegistry _equipmentDefinitionRegistry;
        [SerializeField, Tooltip("Optional item registry used when creating ItemEquipService and resolving consumable icons.")]
        private ItemDefinitionRegistry _itemDefinitionRegistry;
        [SerializeField, Tooltip("Optional rarity palette used for equipped-slot background tinting.")]
        private ItemRarityColorPalette _rarityColorPalette;
        [SerializeField, Tooltip("Optional provider component implementing IEquipmentService.")]
        private MonoBehaviour _equipmentServiceProvider;
        [SerializeField, Tooltip("Optional provider component implementing IItemEquipService.")]
        private MonoBehaviour _itemEquipServiceProvider;
        [SerializeField, Tooltip("Optional squad setup controller used to listen to selected unit changes.")]
        private SquadSetupController _squadSetupController;
        [SerializeField, Tooltip("If enabled, emits detailed consumable-slot wiring diagnostics.")]
        private bool _enableConsumableDiagnostics;

        private readonly List<EquipmentDropSlotView> _slotViews = new List<EquipmentDropSlotView>(8);
        private readonly Dictionary<EquipmentSlotType, List<EquipmentDropSlotView>> _slotViewsByType =
            new Dictionary<EquipmentSlotType, List<EquipmentDropSlotView>>(8);
        private readonly HashSet<EquipmentSlotType> _discoveredSlotTypes = new HashSet<EquipmentSlotType>();
        private readonly List<ConsumableDropSlotView> _consumableSlotViews = new List<ConsumableDropSlotView>(4);
        private readonly Dictionary<ConsumableSlotType, List<ConsumableDropSlotView>> _consumableSlotViewsByType =
            new Dictionary<ConsumableSlotType, List<ConsumableDropSlotView>>(4);
        private readonly HashSet<ConsumableSlotType> _discoveredConsumableSlotTypes = new HashSet<ConsumableSlotType>();

        private IEquipmentService _equipmentService;
        private IItemEquipService _itemEquipService;
        private OwnedUnitData _selectedUnitData;
        private bool _isInitialized;
        private bool _squadEventsWired;
        private bool _equipmentEventsWired;
        private bool _consumableEventsWired;

        public IReadOnlyList<EquipmentDropSlotView> SlotViews => _slotViews;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnEnable()
        {
            EnsureInitialized();
            WireSquadEvents();
            WireEquipmentEvents();
            WireConsumableEvents();
            RefreshFromSelectedUnit("enable");
        }

        private void OnDisable()
        {
            UnwireSquadEvents();
            UnwireEquipmentEvents();
            UnwireConsumableEvents();
        }

        private void OnDestroy()
        {
            UnwireSquadEvents();
            UnwireEquipmentEvents();
            UnwireConsumableEvents();
        }

        public void SetEquipmentService(IEquipmentService equipmentService)
        {
            if (ReferenceEquals(_equipmentService, equipmentService))
            {
                return;
            }

            UnwireEquipmentEvents();
            _equipmentService = equipmentService;
            InjectServiceIntoAllSlots();
            InjectServiceIntoInventoryDropZone();
            if (isActiveAndEnabled)
            {
                WireEquipmentEvents();
            }
        }

        public void SetItemEquipService(IItemEquipService itemEquipService)
        {
            if (ReferenceEquals(_itemEquipService, itemEquipService))
            {
                return;
            }

            UnwireConsumableEvents();
            _itemEquipService = itemEquipService;
            InjectServiceIntoAllConsumableSlots();
            InjectServiceIntoInventoryDropZone();
            if (isActiveAndEnabled)
            {
                WireConsumableEvents();
            }
        }

        public void SetRarityColorPalette(ItemRarityColorPalette rarityColorPalette)
        {
            _rarityColorPalette = rarityColorPalette;
            EnsureInitialized();

            for (int i = 0; i < _slotViews.Count; i++)
            {
                EquipmentDropSlotView slotView = _slotViews[i];
                if (slotView == null)
                {
                    continue;
                }

                slotView.SetRarityColorPalette(_rarityColorPalette);
            }

            for (int i = 0; i < _consumableSlotViews.Count; i++)
            {
                ConsumableDropSlotView slotView = _consumableSlotViews[i];
                if (slotView == null)
                {
                    continue;
                }

                slotView.SetRarityColorPalette(_rarityColorPalette);
            }

            RefreshForUnit(_selectedUnitData);
        }

        /// <summary>
        /// Refreshes all discovered slot views for the provided unit equipment state.
        /// </summary>
        public void RefreshForUnit(OwnedUnitData unitData)
        {
            EnsureInitialized();
            EnsureInventoryDropZone();
            InjectSelectedUnitIntoInventoryDropZone(unitData);
            if (_enableConsumableDiagnostics)
            {
                SBLog.Info(
                    $"{nameof(InventoryEquipmentPanelPresenter)}: RefreshForUnit unit='{ResolveOwnedUnitId(unitData)}' equipmentSlots={_slotViews.Count} consumableSlots={_consumableSlotViews.Count} itemService={(_itemEquipService != null ? "yes" : "no")}.",
                    this);
            }
            for (int i = 0; i < ExpectedSlotTypes.Length; i++)
            {
                EquipmentSlotType slotType = ExpectedSlotTypes[i];
                if (!_slotViewsByType.TryGetValue(slotType, out List<EquipmentDropSlotView> views))
                {
                    continue;
                }

                string definitionId = FindEquippedDefinitionId(unitData, slotType);
                EquipmentDefinition definition = ResolveEquipmentDefinition(definitionId);

                for (int viewIndex = 0; viewIndex < views.Count; viewIndex++)
                {
                    EquipmentDropSlotView view = views[viewIndex];
                    if (view == null)
                    {
                        continue;
                    }

                    view.SetSelectedUnit(unitData);
                    view.SetEquippedItem(definitionId, definition);
                    view.SetCompletionVisual(definition != null);
                }
            }

            for (int i = 0; i < ExpectedConsumableSlotTypes.Length; i++)
            {
                ConsumableSlotType slotType = ExpectedConsumableSlotTypes[i];
                if (!_consumableSlotViewsByType.TryGetValue(slotType, out List<ConsumableDropSlotView> views))
                {
                    continue;
                }

                ItemDefinition definition = _itemEquipService != null
                    ? _itemEquipService.GetEquipped(unitData, slotType)
                    : ResolveItemDefinition(FindEquippedConsumableDefinitionId(unitData, slotType));
                string definitionId = definition != null ? definition.Id : FindEquippedConsumableDefinitionId(unitData, slotType);

                for (int viewIndex = 0; viewIndex < views.Count; viewIndex++)
                {
                    ConsumableDropSlotView view = views[viewIndex];
                    if (view == null)
                    {
                        continue;
                    }

                    view.SetSelectedUnit(unitData);
                    view.SetEquippedItem(definitionId, definition);
                    view.SetCompletionVisual(definition != null);
                }

                if (_enableConsumableDiagnostics)
                {
                    SBLog.Info(
                        $"{nameof(InventoryEquipmentPanelPresenter)}: Slot '{slotType}' resolved definitionId='{definitionId ?? "<none>"}' definition={(definition != null ? definition.Id : "<null>")}.",
                        this);
                }
            }
        }

        private void EnsureInitialized()
        {
            if (_isInitialized)
            {
                return;
            }

            ResolvePanelRoots();
            ResolveSquadSetupController();
            ResolveEquipmentService();
            ResolveItemEquipService();
            EnsureInventoryDropZone();
            DiscoverAndConfigureSlots();
            _isInitialized = true;
        }

        private void ResolvePanelRoots()
        {
            if (_inventoryPanelRoot == null)
            {
                _inventoryPanelRoot = FindByName(transform.root != null ? transform.root : transform, InventoryPanelName);
                if (_inventoryPanelRoot == null)
                {
                    GameObject inventoryPanel = GameObject.Find(InventoryPanelName);
                    if (inventoryPanel != null)
                    {
                        _inventoryPanelRoot = inventoryPanel.transform;
                    }
                }
            }

            Transform searchRoot = _inventoryPanelRoot != null ? _inventoryPanelRoot : (transform.root != null ? transform.root : transform);
            if (_equipSlotL == null)
            {
                _equipSlotL = FindByName(searchRoot, EquipSlotLeftName);
            }

            if (_equipSlotR == null)
            {
                _equipSlotR = FindByName(searchRoot, EquipSlotRightName);
            }

            if (_equipSlotL == null)
            {
                SBLog.Warn($"{nameof(InventoryEquipmentPanelPresenter)}: Missing '{EquipSlotLeftName}' transform.", this);
            }

            if (_equipSlotR == null)
            {
                SBLog.Warn($"{nameof(InventoryEquipmentPanelPresenter)}: Missing '{EquipSlotRightName}' transform.", this);
            }
        }

        private void EnsureInventoryDropZone()
        {
            if (_inventoryDropZone != null)
            {
                return;
            }

            Transform searchRoot = _inventoryPanelRoot != null ? _inventoryPanelRoot : (transform.root != null ? transform.root : transform);
            if (searchRoot != null)
            {
                _inventoryDropZone = searchRoot.GetComponentInChildren<InventoryDropZone>(true);
            }

            if (_inventoryDropZone != null)
            {
                return;
            }

            if (_inventoryPanelRoot != null)
            {
                _inventoryDropZone = _inventoryPanelRoot.GetComponent<InventoryDropZone>();
                if (_inventoryDropZone == null)
                {
                    _inventoryDropZone = _inventoryPanelRoot.gameObject.AddComponent<InventoryDropZone>();
                }
            }
        }

        private void ResolveEquipmentService()
        {
            if (_equipmentService != null)
            {
                return;
            }

            if (_equipmentServiceProvider is IEquipmentService providerService)
            {
                _equipmentService = providerService;
                return;
            }

            _playerContext = ResolvePlayerContext();
            _equipmentDefinitionRegistry = ResolveEquipmentDefinitionRegistry();

            if (_playerContext != null && _equipmentDefinitionRegistry != null)
            {
                _equipmentService = new EquipmentService(_playerContext, _equipmentDefinitionRegistry);
                InjectServiceIntoInventoryDropZone();
            }
        }

        private void ResolveItemEquipService()
        {
            if (_itemEquipService != null)
            {
                return;
            }

            if (_itemEquipServiceProvider is IItemEquipService providerService)
            {
                _itemEquipService = providerService;
                if (_enableConsumableDiagnostics)
                {
                    SBLog.Info($"{nameof(InventoryEquipmentPanelPresenter)}: Using injected IItemEquipService provider.", this);
                }
                return;
            }

            _itemDefinitionRegistry = ResolveItemDefinitionRegistry();
            if (_itemDefinitionRegistry != null)
            {
                _playerContext = ResolvePlayerContext();
                _itemEquipService = new ItemEquipService(_playerContext, _itemDefinitionRegistry);
                InjectServiceIntoInventoryDropZone();
                if (_enableConsumableDiagnostics)
                {
                    SBLog.Info(
                        $"{nameof(InventoryEquipmentPanelPresenter)}: Created runtime ItemEquipService from ItemDefinitionRegistry (inventory={(_playerContext != null && _playerContext.Inventory != null ? "yes" : "no")}).",
                        this);
                }
                return;
            }

            if (_enableConsumableDiagnostics)
            {
                SBLog.Warn($"{nameof(InventoryEquipmentPanelPresenter)}: Could not resolve ItemDefinitionRegistry, consumable equip service unavailable.", this);
            }
        }

        private void ResolveSquadSetupController()
        {
            if (_squadSetupController != null)
            {
                return;
            }

            _squadSetupController = GetComponentInParent<SquadSetupController>(true);
            if (_squadSetupController != null)
            {
                return;
            }

            if (_inventoryPanelRoot != null)
            {
                _squadSetupController = _inventoryPanelRoot.GetComponentInParent<SquadSetupController>(true);
                if (_squadSetupController != null)
                {
                    return;
                }
            }

            _squadSetupController = FindFirstObjectByType<SquadSetupController>();
            if (_squadSetupController != null)
            {
                return;
            }

            SquadSetupController[] controllers = Resources.FindObjectsOfTypeAll<SquadSetupController>();
            if (controllers != null && controllers.Length > 0)
            {
                _squadSetupController = controllers[0];
            }
        }

        private void DiscoverAndConfigureSlots()
        {
            _slotViews.Clear();
            _slotViewsByType.Clear();
            _discoveredSlotTypes.Clear();
            _consumableSlotViews.Clear();
            _consumableSlotViewsByType.Clear();
            _discoveredConsumableSlotTypes.Clear();

            DiscoverSlotsUnderRoot(_equipSlotL);
            DiscoverSlotsUnderRoot(_equipSlotR);
            DiscoverConsumableSlots();
            WarnForMissingExpectedSlots();
            WarnForMissingExpectedConsumableSlots();
        }

        private void DiscoverSlotsUnderRoot(Transform root)
        {
            if (root == null)
            {
                return;
            }

            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                Transform child = children[i];
                if (child == null || !IconFrameSlotTypes.TryGetValue(child.name, out EquipmentSlotType slotType))
                {
                    continue;
                }

                var slotView = child.GetComponent<EquipmentDropSlotView>();
                if (slotView == null)
                {
                    slotView = child.gameObject.AddComponent<EquipmentDropSlotView>();
                }

                slotView.SetSlotType(slotType);
                TryInjectIconImage(slotView, child);
                TryInjectEquipmentService(slotView);
                RegisterSlotView(slotType, slotView);
            }
        }

        private void DiscoverConsumableSlots()
        {
            Transform searchRoot = _inventoryPanelRoot != null ? _inventoryPanelRoot : (transform.root != null ? transform.root : transform);
            if (searchRoot == null)
            {
                return;
            }

            DiscoverConsumableSlotByName(_equipSlotL, searchRoot, IconFrameObject1Name, ConsumableSlotType.Object1);
            DiscoverConsumableSlotByName(_equipSlotL, searchRoot, IconFrameObject2Name, ConsumableSlotType.Object2);
            DiscoverConsumableSlotByName(_equipSlotR, searchRoot, IconFrameObject3Name, ConsumableSlotType.Object3);
            DiscoverConsumableSlotByName(_equipSlotR, searchRoot, IconFrameObject4Name, ConsumableSlotType.Object4);
        }

        private void DiscoverConsumableSlotByName(Transform preferredRoot, Transform fallbackSearchRoot, string frameName, ConsumableSlotType slotType)
        {
            if (string.IsNullOrWhiteSpace(frameName))
            {
                return;
            }

            Transform slotFrame = FindByName(preferredRoot, frameName);
            if (slotFrame == null)
            {
                slotFrame = FindByName(fallbackSearchRoot, frameName);
            }

            if (slotFrame == null)
            {
                SBLog.Warn(
                    $"{nameof(InventoryEquipmentPanelPresenter)}: Missing consumable slot frame '{frameName}'.",
                    this);
                return;
            }

            var slotView = slotFrame.GetComponent<ConsumableDropSlotView>();
            if (slotView == null)
            {
                slotView = slotFrame.gameObject.AddComponent<ConsumableDropSlotView>();
            }

            slotView.SetSlotType(slotType);
            TryInjectIconImage(slotView, slotFrame);
            TryInjectItemEquipService(slotView);
            RegisterConsumableSlotView(slotType, slotView);
            if (_enableConsumableDiagnostics)
            {
                SBLog.Info(
                    $"{nameof(InventoryEquipmentPanelPresenter)}: Wired consumable slot '{slotType}' on frame '{slotFrame.name}' (service={(_itemEquipService != null ? "yes" : "no")}).",
                    this);
            }
        }

        private void RegisterSlotView(EquipmentSlotType slotType, EquipmentDropSlotView slotView)
        {
            if (slotView == null)
            {
                return;
            }

            _slotViews.Add(slotView);
            _discoveredSlotTypes.Add(slotType);

            if (!_slotViewsByType.TryGetValue(slotType, out List<EquipmentDropSlotView> views))
            {
                views = new List<EquipmentDropSlotView>(1);
                _slotViewsByType.Add(slotType, views);
            }

            views.Add(slotView);
        }

        private void RegisterConsumableSlotView(ConsumableSlotType slotType, ConsumableDropSlotView slotView)
        {
            if (slotView == null)
            {
                return;
            }

            _consumableSlotViews.Add(slotView);
            _discoveredConsumableSlotTypes.Add(slotType);

            if (!_consumableSlotViewsByType.TryGetValue(slotType, out List<ConsumableDropSlotView> views))
            {
                views = new List<ConsumableDropSlotView>(1);
                _consumableSlotViewsByType.Add(slotType, views);
            }

            views.Add(slotView);
        }

        private void InjectServiceIntoAllSlots()
        {
            for (int i = 0; i < _slotViews.Count; i++)
            {
                EquipmentDropSlotView slotView = _slotViews[i];
                if (slotView == null)
                {
                    continue;
                }

                TryInjectEquipmentService(slotView);
            }
        }

        private void InjectServiceIntoAllConsumableSlots()
        {
            for (int i = 0; i < _consumableSlotViews.Count; i++)
            {
                ConsumableDropSlotView slotView = _consumableSlotViews[i];
                if (slotView == null)
                {
                    continue;
                }

                TryInjectItemEquipService(slotView);
            }
        }

        private void InjectServiceIntoInventoryDropZone()
        {
            EnsureInventoryDropZone();
            if (_inventoryDropZone == null)
            {
                if (_enableConsumableDiagnostics)
                {
                    SBLog.Warn($"{nameof(InventoryEquipmentPanelPresenter)}: InventoryDropZone missing while injecting services.", this);
                }
                return;
            }

            _inventoryDropZone.SetEquipmentService(_equipmentService);
            _inventoryDropZone.SetItemEquipService(_itemEquipService);
            if (_enableConsumableDiagnostics)
            {
                SBLog.Info(
                    $"{nameof(InventoryEquipmentPanelPresenter)}: Injected services into InventoryDropZone (equipment={(_equipmentService != null ? "yes" : "no")}, item={(_itemEquipService != null ? "yes" : "no")}).",
                    this);
            }
        }

        private void InjectSelectedUnitIntoInventoryDropZone(OwnedUnitData unitData)
        {
            if (_inventoryDropZone == null)
            {
                return;
            }

            _inventoryDropZone.SetSelectedUnit(unitData);
        }

        private void TryInjectIconImage(EquipmentDropSlotView slotView, Transform iconFrameRoot)
        {
            Image iconImage = ResolveIconImage(iconFrameRoot);
            if (iconImage == null)
            {
                SBLog.Warn(
                    $"{nameof(InventoryEquipmentPanelPresenter)}: Missing '{IconChildName}' Image under '{iconFrameRoot.name}'.",
                    this);
                return;
            }

            Image backgroundImage = ResolveBackgroundImage(iconFrameRoot);
            if (backgroundImage == null)
            {
                SBLog.Warn(
                    $"{nameof(InventoryEquipmentPanelPresenter)}: Missing '{BackgroundChildName}' Image under '{iconFrameRoot.name}'.",
                    this);
            }

            slotView.SetIconImage(iconImage);
            slotView.SetBackgroundImage(backgroundImage);
            if (_rarityColorPalette != null)
            {
                slotView.SetRarityColorPalette(_rarityColorPalette);
            }
        }

        private void TryInjectIconImage(ConsumableDropSlotView slotView, Transform iconFrameRoot)
        {
            Image iconImage = ResolveIconImage(iconFrameRoot);
            if (iconImage == null)
            {
                SBLog.Warn(
                    $"{nameof(InventoryEquipmentPanelPresenter)}: Missing '{IconChildName}' Image under '{iconFrameRoot.name}'.",
                    this);
                return;
            }

            Image backgroundImage = ResolveBackgroundImage(iconFrameRoot);
            if (backgroundImage == null)
            {
                SBLog.Warn(
                    $"{nameof(InventoryEquipmentPanelPresenter)}: Missing '{BackgroundChildName}' Image under '{iconFrameRoot.name}'.",
                    this);
            }

            slotView.SetIconImage(iconImage);
            slotView.SetBackgroundImage(backgroundImage);
            if (_rarityColorPalette != null)
            {
                slotView.SetRarityColorPalette(_rarityColorPalette);
            }
        }

        private void TryInjectEquipmentService(EquipmentDropSlotView slotView)
        {
            if (slotView == null || _equipmentService == null)
            {
                return;
            }

            slotView.SetEquipmentService(_equipmentService);
        }

        private void TryInjectItemEquipService(ConsumableDropSlotView slotView)
        {
            if (slotView == null || _itemEquipService == null)
            {
                return;
            }

            slotView.SetItemEquipService(_itemEquipService);
        }

        private static Transform FindByName(Transform root, string objectName)
        {
            if (root == null || string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            Transform[] nodes = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < nodes.Length; i++)
            {
                Transform node = nodes[i];
                if (node != null && string.Equals(node.name, objectName, StringComparison.Ordinal))
                {
                    return node;
                }
            }

            return null;
        }

        private static Image ResolveIconImage(Transform iconFrameRoot)
        {
            if (iconFrameRoot == null)
            {
                return null;
            }

            Transform iconTransform = FindByName(iconFrameRoot, IconChildName);
            if (iconTransform == null)
            {
                return null;
            }

            Image iconImage = iconTransform.GetComponent<Image>();
            if (iconImage != null)
            {
                return iconImage;
            }

            return iconTransform.GetComponentInChildren<Image>(true);
        }

        private static Image ResolveBackgroundImage(Transform iconFrameRoot)
        {
            if (iconFrameRoot == null)
            {
                return null;
            }

            Transform backgroundTransform = FindByName(iconFrameRoot, BackgroundChildName);
            if (backgroundTransform == null)
            {
                return null;
            }

            Image backgroundImage = backgroundTransform.GetComponent<Image>();
            if (backgroundImage != null)
            {
                return backgroundImage;
            }

            return backgroundTransform.GetComponentInChildren<Image>(true);
        }

        private void WarnForMissingExpectedSlots()
        {
            for (int i = 0; i < ExpectedSlotTypes.Length; i++)
            {
                EquipmentSlotType slotType = ExpectedSlotTypes[i];
                if (_discoveredSlotTypes.Contains(slotType))
                {
                    continue;
                }

                SBLog.Warn(
                    $"{nameof(InventoryEquipmentPanelPresenter)}: Missing IconFrame mapping for slot '{slotType}' under '{EquipSlotLeftName}'/'{EquipSlotRightName}'.",
                    this);
            }
        }

        private void WarnForMissingExpectedConsumableSlots()
        {
            for (int i = 0; i < ExpectedConsumableSlotTypes.Length; i++)
            {
                ConsumableSlotType slotType = ExpectedConsumableSlotTypes[i];
                if (_discoveredConsumableSlotTypes.Contains(slotType))
                {
                    continue;
                }

                SBLog.Warn(
                    $"{nameof(InventoryEquipmentPanelPresenter)}: Missing mapped consumable slot frame for '{slotType}'.",
                    this);
            }
        }

        private EquipmentDefinition ResolveEquipmentDefinition(string definitionId)
        {
            if (string.IsNullOrWhiteSpace(definitionId))
            {
                return null;
            }

            if (_equipmentDefinitionRegistry != null)
            {
                EquipmentDefinition definition = _equipmentDefinitionRegistry.GetById(definitionId);
                if (definition != null)
                {
                    return definition;
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
                if (definition == null)
                {
                    continue;
                }

                _equipmentDefinitionRegistry = registry;
                return definition;
            }

            return null;
        }

        private PlayerContext ResolvePlayerContext()
        {
            if (_playerContext != null)
            {
                return _playerContext;
            }

            _playerContext = PlayerContext.RuntimeInstance;
            if (_playerContext != null)
            {
                return _playerContext;
            }

            PlayerContext[] contexts = Resources.FindObjectsOfTypeAll<PlayerContext>();
            if (contexts != null && contexts.Length > 0)
            {
                _playerContext = contexts[0];
            }

            return _playerContext;
        }

        private EquipmentDefinitionRegistry ResolveEquipmentDefinitionRegistry()
        {
            if (_equipmentDefinitionRegistry != null)
            {
                return _equipmentDefinitionRegistry;
            }

            EquipmentDefinitionRegistry[] registries = Resources.FindObjectsOfTypeAll<EquipmentDefinitionRegistry>();
            if (registries != null && registries.Length > 0)
            {
                _equipmentDefinitionRegistry = registries[0];
            }

            return _equipmentDefinitionRegistry;
        }

        private ItemDefinitionRegistry ResolveItemDefinitionRegistry()
        {
            if (_itemDefinitionRegistry != null)
            {
                return _itemDefinitionRegistry;
            }

            ItemDefinitionRegistry[] registries = Resources.FindObjectsOfTypeAll<ItemDefinitionRegistry>();
            if (registries != null && registries.Length > 0)
            {
                _itemDefinitionRegistry = registries[0];
            }

            return _itemDefinitionRegistry;
        }

        private void WireSquadEvents()
        {
            ResolveSquadSetupController();
            if (_squadEventsWired || _squadSetupController == null)
            {
                return;
            }

            _squadSetupController.UnitSelected -= HandleUnitSelected;
            _squadSetupController.UnitSelected += HandleUnitSelected;
            _squadEventsWired = true;
        }

        private void UnwireSquadEvents()
        {
            if (!_squadEventsWired || _squadSetupController == null)
            {
                return;
            }

            _squadSetupController.UnitSelected -= HandleUnitSelected;
            _squadEventsWired = false;
        }

        private void WireEquipmentEvents()
        {
            if (_equipmentEventsWired || _equipmentService == null)
            {
                return;
            }

            _equipmentService.EquipmentChanged -= HandleEquipmentChanged;
            _equipmentService.EquipmentChanged += HandleEquipmentChanged;
            _equipmentEventsWired = true;
        }

        private void WireConsumableEvents()
        {
            if (_consumableEventsWired || _itemEquipService == null)
            {
                return;
            }

            _itemEquipService.ConsumableChanged -= HandleConsumableChanged;
            _itemEquipService.ConsumableChanged += HandleConsumableChanged;
            _consumableEventsWired = true;
        }

        private void UnwireEquipmentEvents()
        {
            if (!_equipmentEventsWired || _equipmentService == null)
            {
                return;
            }

            _equipmentService.EquipmentChanged -= HandleEquipmentChanged;
            _equipmentEventsWired = false;
        }

        private void UnwireConsumableEvents()
        {
            if (!_consumableEventsWired || _itemEquipService == null)
            {
                return;
            }

            _itemEquipService.ConsumableChanged -= HandleConsumableChanged;
            _consumableEventsWired = false;
        }

        private void HandleUnitSelected(UnitSpellLoadout _)
        {
            RefreshFromSelectedUnit("selection");
        }

        private void HandleEquipmentChanged(OwnedUnitData changedUnit, EquipmentSlotType _, EquipmentDefinition __)
        {
            OwnedUnitData selected = ResolveSelectedOwnedUnit();
            _selectedUnitData = selected;
            if (!AreSameOwnedUnit(changedUnit, selected))
            {
                return;
            }

            RefreshForUnit(selected);
            SBLog.Info(
                $"{nameof(InventoryEquipmentPanelPresenter)}: Refreshed equipment slots due to equipment change on selected unit '{ResolveOwnedUnitId(selected)}'.",
                this);
        }

        private void HandleConsumableChanged(OwnedUnitData changedUnit, ConsumableSlotType _, ItemDefinition __)
        {
            OwnedUnitData selected = ResolveSelectedOwnedUnit();
            _selectedUnitData = selected;
            if (!AreSameOwnedUnit(changedUnit, selected))
            {
                return;
            }

            RefreshForUnit(selected);
            SBLog.Info(
                $"{nameof(InventoryEquipmentPanelPresenter)}: Refreshed consumable slots due to consumable change on selected unit '{ResolveOwnedUnitId(selected)}'.",
                this);
        }

        private void RefreshFromSelectedUnit(string reason)
        {
            OwnedUnitData selected = ResolveSelectedOwnedUnit();
            _selectedUnitData = selected;
            RefreshForUnit(selected);
            SBLog.Info(
                $"{nameof(InventoryEquipmentPanelPresenter)}: Refreshed equipment slots due to {reason} change. SelectedUnit='{ResolveOwnedUnitId(selected)}'.",
                this);
        }

        private OwnedUnitData ResolveSelectedOwnedUnit()
        {
            ResolveSquadSetupController();
            if (_squadSetupController == null)
            {
                return null;
            }

            return ResolveOwnedUnitData(_squadSetupController.SelectedUnit);
        }

        private OwnedUnitData ResolveOwnedUnitData(UnitSpellLoadout loadout)
        {
            if (loadout == null)
            {
                return null;
            }

            PlayerContext context = ResolvePlayerContext();
            if (context == null || context.OwnedUnits == null || context.OwnedUnits.Count == 0)
            {
                return null;
            }

            if (MatchesLoadout(_selectedUnitData, loadout))
            {
                return _selectedUnitData;
            }

            OwnedUnitData fallback = null;
            IReadOnlyList<OwnedUnitData> ownedUnits = context.OwnedUnits;
            for (int i = 0; i < ownedUnits.Count; i++)
            {
                OwnedUnitData candidate = ownedUnits[i];
                if (candidate == null || candidate.Definition != loadout.Definition)
                {
                    continue;
                }

                if (MatchesLoadout(candidate, loadout))
                {
                    return candidate;
                }

                if (fallback == null)
                {
                    fallback = candidate;
                }
            }

            return fallback;
        }

        private static bool MatchesLoadout(OwnedUnitData ownedUnit, UnitSpellLoadout loadout)
        {
            if (ownedUnit == null || loadout == null)
            {
                return false;
            }

            if (ownedUnit.Definition != loadout.Definition)
            {
                return false;
            }

            if (ownedUnit.EffectiveLevel != loadout.EffectiveLevel || ownedUnit.EffectiveXp != loadout.EffectiveXp)
            {
                return false;
            }

            SpellDefinition[] ownedSpells = ownedUnit.Spells;
            SpellDefinition[] loadoutSpells = loadout.Spells;
            int ownedCount = ownedSpells != null ? ownedSpells.Length : 0;
            int loadoutCount = loadoutSpells != null ? loadoutSpells.Length : 0;
            if (ownedCount != loadoutCount)
            {
                return false;
            }

            for (int i = 0; i < ownedCount; i++)
            {
                if (ownedSpells[i] != loadoutSpells[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static bool AreSameOwnedUnit(OwnedUnitData first, OwnedUnitData second)
        {
            if (ReferenceEquals(first, second))
            {
                return true;
            }

            if (first == null || second == null)
            {
                return false;
            }

            return string.Equals(first.OwnedUnitId, second.OwnedUnitId, StringComparison.Ordinal);
        }

        private static string ResolveOwnedUnitId(OwnedUnitData ownedUnit)
        {
            return ownedUnit != null && !string.IsNullOrWhiteSpace(ownedUnit.OwnedUnitId)
                ? ownedUnit.OwnedUnitId
                : "<none>";
        }

        private static string FindEquippedDefinitionId(OwnedUnitData unitData, EquipmentSlotType slotType)
        {
            if (unitData == null || unitData.EquippedItems == null)
            {
                return null;
            }

            for (int i = 0; i < unitData.EquippedItems.Length; i++)
            {
                EquipmentSlotEntry entry = unitData.EquippedItems[i];
                if (entry.SlotType != slotType || string.IsNullOrWhiteSpace(entry.DefinitionId))
                {
                    continue;
                }

                return entry.DefinitionId;
            }

            return null;
        }

        private ItemDefinition ResolveItemDefinition(string definitionId)
        {
            if (string.IsNullOrWhiteSpace(definitionId))
            {
                return null;
            }

            if (_itemDefinitionRegistry != null)
            {
                ItemDefinition definition = _itemDefinitionRegistry.GetById(definitionId);
                if (definition != null)
                {
                    return definition;
                }
            }

            ItemDefinitionRegistry[] registries = Resources.FindObjectsOfTypeAll<ItemDefinitionRegistry>();
            for (int i = 0; i < registries.Length; i++)
            {
                ItemDefinitionRegistry registry = registries[i];
                if (registry == null)
                {
                    continue;
                }

                ItemDefinition definition = registry.GetById(definitionId);
                if (definition == null)
                {
                    continue;
                }

                _itemDefinitionRegistry = registry;
                return definition;
            }

            return null;
        }

        private static string FindEquippedConsumableDefinitionId(OwnedUnitData unitData, ConsumableSlotType slotType)
        {
            if (unitData == null || unitData.EquippedConsumables == null)
            {
                return null;
            }

            for (int i = 0; i < unitData.EquippedConsumables.Length; i++)
            {
                ConsumableSlotEntry entry = unitData.EquippedConsumables[i];
                if (entry.SlotType != slotType || string.IsNullOrWhiteSpace(entry.DefinitionId))
                {
                    continue;
                }

                return entry.DefinitionId;
            }

            return null;
        }
    }
}
