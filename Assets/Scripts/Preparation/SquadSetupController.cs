using System;
using System.Collections.Generic;
using SevenBattles.Core;
using SevenBattles.Core.Battle;
using SevenBattles.Core.Diagnostics;
using SevenBattles.Core.Players;
using SevenBattles.Core.Save;
using SevenBattles.Core.Units;
using TMPro;
using UnityEngine;

namespace SevenBattles.Preparation
{
    public sealed class SquadSetupController : MonoBehaviour, ISquadSetupController
    {
        [SerializeField] private PlayerContext _playerContext;
        [SerializeField] private UnitDefinitionRegistry _unitRegistry;
        [SerializeField] private AllUnitsGridView _allUnitsView;
        [SerializeField] private ActiveSquadGridView _activeSquadView;
        [SerializeField] private UnitInfoPanelView _unitInfoView;
        [Header("Squad Value")]
        [SerializeField, Tooltip("Optional explicit reference to the SquadValue TMP label. Auto-found by name when null.")]
        private TMP_Text _squadValueLabel;
        [SerializeField, Tooltip("Object name used to auto-find the squad value label when _squadValueLabel is not assigned.")]
        private string _squadValueObjectName = "SquadValue";
        [SerializeField, Tooltip("Text color used when the active squad is empty (0/x).")]
        private Color _emptySquadValueColor = new Color32(214, 77, 77, 255);
        [SerializeField, Tooltip("Text color used when the active squad has at least one unit.")]
        private Color _nonEmptySquadValueColor = Color.white;

        private PlayerContext _resolvedPlayerContext;
        private IUnitCatalog _unitCatalog;
        private IPlayerInventoryService _inventoryService;
        private ISquadService _squadService;
        private UnitDropZone _allUnitsDropZone;
        private UnitDropZone _activeSquadDropZone;
        private bool _eventsWired;
        private string _selectedOwnedUnitId;
        private UnitSpellLoadout _selectedLoadout;

        private readonly List<UnitSpellLoadout> _allAvailableLoadouts = new List<UnitSpellLoadout>();
        private readonly List<UnitSpellLoadout> _activeSquadLoadouts = new List<UnitSpellLoadout>();
        private readonly Dictionary<string, UnitSpellLoadout> _loadoutByOwnedId = new Dictionary<string, UnitSpellLoadout>(StringComparer.Ordinal);
        private readonly Dictionary<UnitSpellLoadout, string> _ownedIdByLoadout = new Dictionary<UnitSpellLoadout, string>();
        private readonly HashSet<string> _usedOwnedIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> _staleOwnedIdsBuffer = new List<string>();

        public int MaxSquadSize => _squadService != null ? _squadService.MaxSquadSize : 1;
        public int ActiveSquadCount => _activeSquadLoadouts.Count;
        public bool IsSquadFull => _squadService != null && _squadService.IsSquadFull;
        public UnitSpellLoadout SelectedUnit => _selectedLoadout;
        public IReadOnlyList<UnitSpellLoadout> AllAvailableUnits => _allAvailableLoadouts;
        public IReadOnlyList<UnitSpellLoadout> ActiveSquad => _activeSquadLoadouts;

        public event Action<UnitSpellLoadout> UnitAddedToSquad;
        public event Action<UnitSpellLoadout> UnitRemovedFromSquad;
        public event Action SquadChanged;
        public event Action<UnitSpellLoadout> UnitSelected;

        private void Start()
        {
            _resolvedPlayerContext = PlayerContext.RuntimeInstance ?? _playerContext;
            if (_resolvedPlayerContext == null)
            {
                RefreshSquadValueDisplay();
                SBLog.Warn("SquadSetupController: No PlayerContext resolved. Squad UI cannot be populated.", this);
                return;
            }

            SBLog.Info(
                $"SquadSetupController: Context source={(PlayerContext.RuntimeInstance != null ? "RuntimeInstance" : "SerializedAsset")}, owned={_resolvedPlayerContext.OwnedUnits.Count}, activeIds={_resolvedPlayerContext.ActiveSquadOwnedUnitIds.Count}.",
                this);

            _unitCatalog = new UnitCatalogService(_unitRegistry);
            _inventoryService = new PlayerInventoryService(_resolvedPlayerContext, _unitCatalog);
            _squadService = new SquadService(_resolvedPlayerContext, _inventoryService);
            if (_allUnitsView != null)
            {
                _allUnitsView.SetDisplayNameProvider(ResolveDisplayNameForLoadout);
            }

            if (_activeSquadView != null)
            {
                _activeSquadView.SetDisplayNameProvider(ResolveDisplayNameForLoadout);
            }
            // Preparation UX: do not auto-seed the active squad when no selection exists yet.
            // Owned units should appear in "All Units" until the player explicitly adds them to the squad.

            WireEvents();
            RebuildViewData();
            RefreshViews();
            RefreshSquadValueDisplay();
            SBLog.Info($"SquadSetupController: View data built. active={_activeSquadLoadouts.Count}, available={_allAvailableLoadouts.Count}.", this);
            LogContextDetails();
            if (_unitInfoView != null)
            {
                _unitInfoView.Clear();
            }

            if (_activeSquadLoadouts.Count == 0 && _allAvailableLoadouts.Count == 0)
            {
                SBLog.Warn("SquadSetupController: Both ActiveSquad and AllUnits are empty after initialization.", this);
            }
        }

        private void LogContextDetails()
        {
            if (_resolvedPlayerContext == null)
            {
                return;
            }

            // Keep this lightweight; it runs once when the panel activates.
            var owned = _resolvedPlayerContext.OwnedUnits;
            int ownedCount = owned != null ? owned.Count : 0;
            if (ownedCount == 0)
            {
                SBLog.Info("SquadSetupController: OwnedUnits list is empty.", this);
                return;
            }

            int sampleCount = Mathf.Min(ownedCount, 6);
            for (int i = 0; i < sampleCount; i++)
            {
                OwnedUnitData unit = owned[i];
                string id = unit != null ? unit.OwnedUnitId : "<null>";
                string defId = unit != null && unit.Definition != null ? unit.Definition.Id : "<null>";
                bool hasPortrait = unit != null && unit.Definition != null && unit.Definition.Portrait != null;
                SBLog.Info($"SquadSetupController: Owned[{i}] ownedId='{id}' defId='{defId}' portrait={(hasPortrait ? "yes" : "no")}.", this);
            }
        }

        private void OnDisable()
        {
            UnwireEvents();
        }

        private void OnEnable()
        {
            if (_squadService == null)
            {
                return;
            }

            WireEvents();
            RebuildViewData();
            RefreshViews();
            RefreshSquadValueDisplay();

            if (_unitInfoView == null)
            {
                return;
            }

            if (TryResolveSelectedOwnedUnit(out OwnedUnitData selectedOwnedUnit))
            {
                UnitSpellLoadout selectedLoadout = GetOrCreateViewLoadout(selectedOwnedUnit);
                _selectedLoadout = selectedLoadout;
                _unitInfoView.ShowUnit(
                    selectedLoadout,
                    selectedOwnedUnit.OwnedUnitId,
                    OwnedUnitNamingPolicy.ResolveDisplayName(selectedOwnedUnit));
            }
            else
            {
                _selectedOwnedUnitId = null;
                _selectedLoadout = null;
                _unitInfoView.Clear();
            }
        }

        public bool TryAddToSquad(UnitSpellLoadout loadout)
        {
            if (loadout == null || _squadService == null || !_ownedIdByLoadout.TryGetValue(loadout, out string ownedId))
            {
                return false;
            }

            return _squadService.TryAddToSquad(ownedId);
        }

        public bool TryRemoveFromSquad(UnitSpellLoadout loadout)
        {
            if (loadout == null || _squadService == null || !_ownedIdByLoadout.TryGetValue(loadout, out string ownedId))
            {
                return false;
            }

            return _squadService.TryRemoveFromSquad(ownedId);
        }

        public void SelectUnit(UnitSpellLoadout loadout)
        {
            if (_squadService != null && loadout != null && _ownedIdByLoadout.TryGetValue(loadout, out string ownedId))
            {
                _squadService.SelectUnit(ownedId);
                return;
            }

            _selectedOwnedUnitId = null;
            _selectedLoadout = null;
            if (_unitInfoView != null)
            {
                _unitInfoView.Clear();
            }

            UnitSelected?.Invoke(null);
        }

        private void WireEvents()
        {
            if (_eventsWired || _squadService == null)
            {
                return;
            }

            _squadService.SquadChanged += HandleSquadChanged;
            _squadService.UnitAddedToSquad += HandleUnitAddedToSquad;
            _squadService.UnitRemovedFromSquad += HandleUnitRemovedFromSquad;
            _squadService.UnitSelected += HandleUnitSelected;
            if (_inventoryService != null)
            {
                _inventoryService.OwnedUnitChanged += HandleOwnedUnitChanged;
            }

            if (_allUnitsView != null)
            {
                _allUnitsView.PortraitClicked += HandlePortraitClicked;
                _allUnitsDropZone = _allUnitsView.GetComponent<UnitDropZone>();
                if (_allUnitsDropZone != null)
                {
                    _allUnitsDropZone.DropReceived += HandleDropReceived;
                }
            }

            if (_activeSquadView != null)
            {
                _activeSquadView.PortraitClicked += HandlePortraitClicked;
                _activeSquadDropZone = _activeSquadView.GetComponent<UnitDropZone>();
                if (_activeSquadDropZone != null)
                {
                    _activeSquadDropZone.DropReceived += HandleDropReceived;
                }
            }

            if (_unitInfoView != null)
            {
                _unitInfoView.NameCommitRequested += HandleNameCommitRequested;
            }

            _eventsWired = true;
        }

        private void UnwireEvents()
        {
            if (!_eventsWired || _squadService == null)
            {
                return;
            }

            _squadService.SquadChanged -= HandleSquadChanged;
            _squadService.UnitAddedToSquad -= HandleUnitAddedToSquad;
            _squadService.UnitRemovedFromSquad -= HandleUnitRemovedFromSquad;
            _squadService.UnitSelected -= HandleUnitSelected;
            if (_inventoryService != null)
            {
                _inventoryService.OwnedUnitChanged -= HandleOwnedUnitChanged;
            }

            if (_allUnitsView != null)
            {
                _allUnitsView.PortraitClicked -= HandlePortraitClicked;
            }

            if (_activeSquadView != null)
            {
                _activeSquadView.PortraitClicked -= HandlePortraitClicked;
            }

            if (_allUnitsDropZone != null)
            {
                _allUnitsDropZone.DropReceived -= HandleDropReceived;
            }

            if (_activeSquadDropZone != null)
            {
                _activeSquadDropZone.DropReceived -= HandleDropReceived;
            }

            if (_unitInfoView != null)
            {
                _unitInfoView.NameCommitRequested -= HandleNameCommitRequested;
            }

            _eventsWired = false;
        }

        private void HandleSquadChanged()
        {
            RebuildViewData();
            RefreshViews();
            RefreshSquadValueDisplay();
            SquadChanged?.Invoke();
            TryAutoSavePlayerContext();
        }

        private void HandleUnitAddedToSquad(OwnedUnitData ownedUnit)
        {
            UnitAddedToSquad?.Invoke(GetOrCreateViewLoadout(ownedUnit));
        }

        private void HandleUnitRemovedFromSquad(OwnedUnitData ownedUnit)
        {
            UnitRemovedFromSquad?.Invoke(GetOrCreateViewLoadout(ownedUnit));
        }

        private void HandleUnitSelected(OwnedUnitData ownedUnit)
        {
            UnitSpellLoadout loadout = GetOrCreateViewLoadout(ownedUnit);
            _selectedLoadout = loadout;
            _selectedOwnedUnitId = ownedUnit != null ? ownedUnit.OwnedUnitId : null;
            if (_unitInfoView != null)
            {
                if (loadout == null)
                {
                    _unitInfoView.Clear();
                }
                else
                {
                    _unitInfoView.ShowUnit(loadout, _selectedOwnedUnitId, OwnedUnitNamingPolicy.ResolveDisplayName(ownedUnit));
                }
            }

            UnitSelected?.Invoke(loadout);
        }

        private void HandlePortraitClicked(UnitSpellLoadout loadout)
        {
            SelectUnit(loadout);
        }

        private void HandleDropReceived(UnitSpellLoadout loadout, UnitDropZone.ZoneType zoneType)
        {
            if (loadout == null)
            {
                return;
            }

            if (zoneType == UnitDropZone.ZoneType.ActiveSquad)
            {
                TryAddToSquad(loadout);
            }
            else if (zoneType == UnitDropZone.ZoneType.AllUnits)
            {
                TryRemoveFromSquad(loadout);
            }
        }

        private void HandleNameCommitRequested(string ownedUnitId, string enteredName)
        {
            if (_inventoryService == null || string.IsNullOrWhiteSpace(ownedUnitId))
            {
                return;
            }

            if (!_inventoryService.TryRenameOwnedUnit(ownedUnitId, enteredName, out string appliedName))
            {
                return;
            }

            RefreshUnitVisualsByOwnedId(ownedUnitId);
            if (string.Equals(_selectedOwnedUnitId, ownedUnitId, StringComparison.Ordinal) && _unitInfoView != null)
            {
                _unitInfoView.SetDisplayedName(appliedName);
            }
        }

        private void HandleOwnedUnitChanged(OwnedUnitData ownedUnit)
        {
            if (ownedUnit == null || string.IsNullOrWhiteSpace(ownedUnit.OwnedUnitId))
            {
                return;
            }

            RefreshUnitVisualsByOwnedId(ownedUnit.OwnedUnitId);
            if (string.Equals(_selectedOwnedUnitId, ownedUnit.OwnedUnitId, StringComparison.Ordinal) && _unitInfoView != null)
            {
                _unitInfoView.SetDisplayedName(OwnedUnitNamingPolicy.ResolveDisplayName(ownedUnit));
            }

            TryAutoSavePlayerContext();
        }

        private void RebuildViewData()
        {
            _allAvailableLoadouts.Clear();
            _activeSquadLoadouts.Clear();
            _usedOwnedIds.Clear();

            if (_squadService == null)
            {
                PruneLoadoutCache();
                return;
            }

            IReadOnlyList<OwnedUnitData> active = _squadService.ActiveSquad;
            for (int i = 0; i < active.Count; i++)
            {
                UnitSpellLoadout loadout = GetOrCreateViewLoadout(active[i]);
                if (loadout == null)
                {
                    continue;
                }

                _activeSquadLoadouts.Add(loadout);
            }

            IReadOnlyList<OwnedUnitData> available = _squadService.AvailableUnits;
            for (int i = 0; i < available.Count; i++)
            {
                UnitSpellLoadout loadout = GetOrCreateViewLoadout(available[i]);
                if (loadout == null)
                {
                    continue;
                }

                _allAvailableLoadouts.Add(loadout);
            }

            PruneLoadoutCache();
        }

        private UnitSpellLoadout GetOrCreateViewLoadout(OwnedUnitData ownedUnit)
        {
            if (ownedUnit == null || ownedUnit.Definition == null || string.IsNullOrWhiteSpace(ownedUnit.OwnedUnitId))
            {
                return null;
            }

            _usedOwnedIds.Add(ownedUnit.OwnedUnitId);
            if (!_loadoutByOwnedId.TryGetValue(ownedUnit.OwnedUnitId, out UnitSpellLoadout loadout) || loadout == null)
            {
                loadout = new UnitSpellLoadout();
                _loadoutByOwnedId[ownedUnit.OwnedUnitId] = loadout;
                _ownedIdByLoadout[loadout] = ownedUnit.OwnedUnitId;
            }

            loadout.Definition = ownedUnit.Definition;
            loadout.Level = ownedUnit.EffectiveLevel;
            loadout.Xp = ownedUnit.EffectiveXp;
            loadout.Spells = ownedUnit.Spells != null ? (SpellDefinition[])ownedUnit.Spells.Clone() : Array.Empty<SpellDefinition>();
            return loadout;
        }

        private void PruneLoadoutCache()
        {
            if (_loadoutByOwnedId.Count == 0)
            {
                return;
            }

            _ownedIdByLoadout.Clear();

            _staleOwnedIdsBuffer.Clear();
            foreach (var pair in _loadoutByOwnedId)
            {
                if (!_usedOwnedIds.Contains(pair.Key))
                {
                    _staleOwnedIdsBuffer.Add(pair.Key);
                    continue;
                }

                if (pair.Value != null)
                {
                    _ownedIdByLoadout[pair.Value] = pair.Key;
                }
            }

            for (int i = 0; i < _staleOwnedIdsBuffer.Count; i++)
            {
                _loadoutByOwnedId.Remove(_staleOwnedIdsBuffer[i]);
            }
        }

        private void RefreshViews()
        {
            if (_allUnitsView != null)
            {
                _allUnitsView.Refresh(_allAvailableLoadouts);
            }

            if (_activeSquadView != null)
            {
                _activeSquadView.SetIsFull(IsSquadFull);
                _activeSquadView.Refresh(_activeSquadLoadouts);
            }
        }

        private void RefreshUnitVisualsByOwnedId(string ownedUnitId)
        {
            if (string.IsNullOrWhiteSpace(ownedUnitId))
            {
                return;
            }

            if (!_loadoutByOwnedId.TryGetValue(ownedUnitId, out UnitSpellLoadout loadout) || loadout == null)
            {
                return;
            }

            if (_allUnitsView != null)
            {
                _allUnitsView.RefreshPortrait(loadout);
            }

            if (_activeSquadView != null)
            {
                _activeSquadView.RefreshPortrait(loadout);
            }
        }

        private void RefreshSquadValueDisplay()
        {
            ResolveSquadValueLabel();
            if (_squadValueLabel == null)
            {
                return;
            }

            int activeCount = Mathf.Max(0, ActiveSquadCount);
            int maxSquadSize = Mathf.Max(1, MaxSquadSize);
            _squadValueLabel.SetText("{0}/{1}", activeCount, maxSquadSize);
            _squadValueLabel.color = activeCount == 0 ? _emptySquadValueColor : _nonEmptySquadValueColor;
        }

        private void ResolveSquadValueLabel()
        {
            if (_squadValueLabel != null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_squadValueObjectName))
            {
                return;
            }

            _squadValueLabel = FindLabelByName(transform, _squadValueObjectName);
            if (_squadValueLabel != null)
            {
                return;
            }

            Transform root = transform.root;
            if (root != null && root != transform)
            {
                _squadValueLabel = FindLabelByName(root, _squadValueObjectName);
            }
        }

        private static TMP_Text FindLabelByName(Transform root, string objectName)
        {
            if (root == null || string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            TMP_Text[] labels = root.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                TMP_Text label = labels[i];
                if (label == null)
                {
                    continue;
                }

                if (string.Equals(label.name, objectName, StringComparison.Ordinal))
                {
                    return label;
                }
            }

            return null;
        }

        private string ResolveDisplayNameForLoadout(UnitSpellLoadout loadout)
        {
            if (loadout == null)
            {
                return string.Empty;
            }

            if (_inventoryService != null &&
                _ownedIdByLoadout.TryGetValue(loadout, out string ownedId) &&
                _inventoryService.TryGetOwnedUnit(ownedId, out OwnedUnitData owned))
            {
                return OwnedUnitNamingPolicy.ResolveDisplayName(owned);
            }

            if (loadout.Definition == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(loadout.Definition.name))
            {
                return loadout.Definition.name;
            }

            return loadout.Definition.Id ?? string.Empty;
        }

        private bool TryResolveSelectedOwnedUnit(out OwnedUnitData ownedUnit)
        {
            ownedUnit = null;
            if (_inventoryService == null || string.IsNullOrWhiteSpace(_selectedOwnedUnitId))
            {
                return false;
            }

            return _inventoryService.TryGetOwnedUnit(_selectedOwnedUnitId, out ownedUnit);
        }

        private void TryAutoSavePlayerContext()
        {
            PlayerContext context = _resolvedPlayerContext ?? PlayerContext.RuntimeInstance ?? _playerContext;
            if (context == null)
            {
                SBLog.Warn("SquadSetupController: Autosave skipped because no PlayerContext is available.", this);
                return;
            }

            if (!PlayerContextAutoSaveUtility.TrySaveFromPlayerContext(context, out string path))
            {
                SBLog.Warn($"SquadSetupController: Failed to autosave player context to '{path}'.", this);
            }
        }
    }
}
