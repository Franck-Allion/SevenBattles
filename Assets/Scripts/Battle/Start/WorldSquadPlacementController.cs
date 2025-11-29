using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using SevenBattles.Battle.Board;
using SevenBattles.Battle.Units;
using SevenBattles.Core;
using SevenBattles.Core.Players;
using SevenBattles.Core.Units;

namespace SevenBattles.Battle.Start
{
    // Handles interactive placement of a fixed-size wizard squad on a world-space board.
    public class WorldSquadPlacementController : MonoBehaviour, ISquadPlacementController
    {
        [Header("References")]
        [SerializeField] private WorldPerspectiveBoard _board;
        [SerializeField, Tooltip("Optional material used for tile highlight during squad placement (e.g., filled tile). If null, the board's default material is used.")]
        private Material _placementHighlightMaterial;

        [Header("Squad Data")]
        [Tooltip("Player squad asset (1..8 wizard definitions).")]
        [SerializeField] private PlayerSquad _playerSquad;

        [Header("Placement Rules")]
        [SerializeField, Tooltip("Number of front rows on the player's side that are valid for placement.")]
        private int _playerRows = 2;
        [SerializeField] private string _sortingLayer = "Characters";
        [SerializeField] private int _baseSortingOrder = 0;
        [SerializeField] private float _scaleMultiplier = 1f;

        [Header("Feedback (optional)")]
        [SerializeField] private AudioSource _audio;
        [SerializeField, Tooltip("SFX when a wizard is placed (e.g., Assets/Art/SFX/Menu_Percussive_Confirm_1.wav)")]
        private AudioClip _placeClip;
        [SerializeField, Tooltip("SFX when a placed wizard is removed from the board")]
        private AudioClip _removeClip;
        [SerializeField] private Color _validColor = new Color(0.3f, 1f, 0.3f, 0.5f);
        [SerializeField] private bool _logSelection;
        [SerializeField] private Color _invalidColor = new Color(1f, 0.3f, 0.3f, 0.5f);

        [Header("Events")]
        public UnityEvent<int> OnWizardSelected; // wizard index
        public UnityEvent<int> OnWizardPlaced;   // wizard index
        public UnityEvent<int> OnWizardRemoved;  // wizard index
        public UnityEvent<bool> OnReadyChanged;  // true when all placed
        public UnityEvent OnPlacementLocked;     // invoked when Start Battle pressed

        // Core contract events for UI/domain decoupling
        public event System.Action<int> WizardSelected;
        public event System.Action<int> WizardPlaced;
        public event System.Action<int> WizardRemoved;
        public event System.Action<bool> ReadyChanged;
        public event System.Action PlacementLocked;

        private readonly Dictionary<int, GameObject> _instances = new Dictionary<int, GameObject>();
        private SquadPlacementModel _model;
        private int _selected = -1;
        private bool _locked;

        private void Start()
        {
            EnsureModel();
            if (_board != null)
            {
                if (_placementHighlightMaterial != null)
                {
                    _board.SetHighlightMaterial(_placementHighlightMaterial);
                }
                _board.SetHighlightVisible(true);
                _board.SetHighlightColor(_invalidColor);
            }
        }

        private void EnsureModel()
        {
            int cols = _board != null ? _board.Columns : 0;
            int rows = _board != null ? _board.Rows : 0;
            int size = GetSquadSizeInternal();
            if (cols <= 0 || rows <= 0 || size <= 0)
            {
                // Defer until board is ready
                return;
            }
            _model = new SquadPlacementModel(cols, rows, Mathf.Max(1, _playerRows), size);
        }

        private void Update()
        {
            if (_locked || _board == null) return;
            if (_model == null) EnsureModel();
            if (_model == null) return;

            var screen = Input.mousePosition;
            if (_board.TryScreenToTile(screen, out int x, out int y))
            {
                var tile = new Vector2Int(x, y);
                _board.MoveHighlightToTile(x, y);
                bool valid = _selected >= 0 ? _model.CanPlace(tile) : _model.IsInPlayerPlacementArea(tile) && !_model.IsOccupied(tile);
                _board.SetHighlightColor(valid ? _validColor : _invalidColor);

                if (Input.GetMouseButtonDown(0))
                {
                    HandleClick(tile);
                }
            }
        }

        private void HandleClick(Vector2Int tile)
        {
            if (_locked) return;

            // If clicking a placed wizard, remove it
            if (_model.TryGetWizardAt(tile, out int wIndex))
            {
                RemoveWizard(wIndex);
                return;
            }

            // If a wizard is selected, attempt to place
            if (_selected >= 0 && _model.CanPlace(tile))
            {
                PlaceWizard(_selected, tile);
                _selected = -1;
            }
        }

        public void SelectWizard(int index)
        {
            if (_locked) { if (_logSelection) Debug.Log("SelectWizard ignored: placement locked.", this); return; }
            if (index < 0 || index >= SquadSize) { if (_logSelection) Debug.Log($"SelectWizard ignored: index {index} out of range.", this); return; }
            // Ignore already placed selections; selecting again could be used to move, but spec removes on click so keep simple
            if (_model != null && _model.TryGetTileOfWizard(index, out _))
            {
                _selected = -1; // cannot select already placed
                if (_logSelection) Debug.Log($"SelectWizard rejected: wizard {index} already placed.", this);
                return;
            }
            _selected = index;
            OnWizardSelected?.Invoke(index);
            if (_logSelection) Debug.Log($"SelectWizard accepted: wizard {index}.", this);
            WizardSelected?.Invoke(index);
        }

        private void PlaceWizard(int index, Vector2Int tile)
        {
            if (index < 0 || index >= SquadSize) return;
            if (!_model.TryPlace(index, tile)) return;
            var prefab = ResolvePrefab(index);
            if (prefab == null) return;
            var go = Instantiate(prefab);
            SevenBattles.Battle.Units.UnitVisualUtil.ApplyScale(go, _scaleMultiplier);
            int sortingOrder = _board != null ? _board.ComputeSortingOrder(tile.x, tile.y, _baseSortingOrder, rowStride: 10, intraRowOffset: index % 10) : (_baseSortingOrder + index);
            ApplyMetadataIfAny(go, index, tile);
            SevenBattles.Battle.Units.UnitVisualUtil.InitializeHero(go, _sortingLayer, sortingOrder, Vector2.up);
            _board.PlaceHero(go.transform, tile.x, tile.y, _sortingLayer, sortingOrder);
            ApplyStatsIfAny(go, index);
            _instances[index] = go;
            Play(_placeClip);
            OnWizardPlaced?.Invoke(index);
            WizardPlaced?.Invoke(index);
            var ready = _model.IsComplete();
            OnReadyChanged?.Invoke(ready);
            ReadyChanged?.Invoke(ready);
        }

        private void RemoveWizard(int index)
        {
            if (!_model.TryRemoveWizard(index)) return;
            if (_instances.TryGetValue(index, out var go) && go != null)
            {
                Destroy(go);
            }
            _instances.Remove(index);
            Play(_removeClip);
            OnWizardRemoved?.Invoke(index);
            WizardRemoved?.Invoke(index);
            var ready = _model.IsComplete();
            OnReadyChanged?.Invoke(ready);
            ReadyChanged?.Invoke(ready);
        }

        public void ConfirmAndLock()
        {
            if (_locked) return;
            if (_model == null || !_model.IsComplete()) return;
            _locked = true;
            _board.SetHighlightVisible(false);
            OnPlacementLocked?.Invoke();
            PlacementLocked?.Invoke();
        }

        private void Play(AudioClip clip)
        {
            if (clip == null) return;
            if (_audio != null)
            {
                _audio.PlayOneShot(clip);
                return;
            }
            // Fallback if no AudioSource was assigned
            AudioSource.PlayClipAtPoint(clip, Vector3.zero, 1f);
        }

        // For external UI/HUD queries
        public bool IsPlaced(int index)
        {
            return _model != null && _model.TryGetTileOfWizard(index, out _);
        }

        public bool IsReady => _model != null && _model.IsComplete();
        public bool IsLocked => _locked;

        public int SquadSize => GetSquadSizeInternal();

        public Sprite GetPortrait(int index)
        {
            if (_playerSquad != null && _playerSquad.Wizards != null && index >= 0 && index < _playerSquad.Wizards.Length)
                return _playerSquad.Wizards[index] != null ? _playerSquad.Wizards[index].Portrait : null;
            return null;
        }

        public bool TryPlaceAt(int index, Vector2Int tile)
        {
            if (_locked) return false;
            if (_model == null)
            {
                EnsureModel();
                if (_model == null) return false;
            }
            if (index < 0 || index >= SquadSize) return false;
            if (!_model.CanPlace(tile)) return false;
            PlaceWizard(index, tile);
            return true;
        }

        private int GetSquadSizeInternal()
        {
            int defCount = _playerSquad != null && _playerSquad.Wizards != null ? _playerSquad.Wizards.Length : 0;
            return Mathf.Clamp(defCount, 0, 8);
        }

        private GameObject ResolvePrefab(int index)
        {
            if (_playerSquad == null || _playerSquad.Wizards == null) return null;
            if (index < 0 || index >= _playerSquad.Wizards.Length) return null;
            var def = _playerSquad.Wizards[index];
            return def != null ? def.Prefab : null;
        }

        private void ApplyStatsIfAny(GameObject go, int index)
        {
            if (_playerSquad == null || _playerSquad.Wizards == null) return;
            if (index < 0 || index >= _playerSquad.Wizards.Length) return;
            var def = _playerSquad.Wizards[index];
            if (def == null) return;
            var stats = go.GetComponent<UnitStats>();
            if (stats == null) stats = go.AddComponent<UnitStats>();
            stats.ApplyBase(def.BaseStats);
        }

        private void ApplyMetadataIfAny(GameObject go, int index, Vector2Int tile)
        {
            if (_playerSquad == null || _playerSquad.Wizards == null) return;
            if (index < 0 || index >= _playerSquad.Wizards.Length) return;
            var def = _playerSquad.Wizards[index];
            if (def == null) return;
            UnitBattleMetadata.Ensure(go, true, def, tile);
        }

        private void OnValidate()
        {
            if (_playerRows < 1) _playerRows = 1;
            if (_playerSquad != null && _playerSquad.Wizards != null && _playerSquad.Wizards.Length > 8)
            {
                Debug.LogWarning("WorldSquadPlacementController: Only first 8 WizardDefinitions will be used.", this);
            }
            if (_playerSquad == null || _playerSquad.Wizards == null || _playerSquad.Wizards.Length == 0)
            {
                Debug.LogWarning("WorldSquadPlacementController: Assign a PlayerSquad with 1..8 WizardDefinitions.", this);
            }
        }
    }
}
