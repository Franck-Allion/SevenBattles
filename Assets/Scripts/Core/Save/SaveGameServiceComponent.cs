using System.Threading.Tasks;
using UnityEngine;

namespace SevenBattles.Core.Save
{
    /// <summary>
    /// MonoBehaviour wrapper around SaveGameService so it can be configured entirely from the Unity inspector.
    /// Assign a MonoBehaviour that implements IGameStateSaveProvider, then reference this component from UI.
    /// </summary>
    public class SaveGameServiceComponent : MonoBehaviour, ISaveGameService
    {
        [SerializeField, Tooltip("Reference to a MonoBehaviour that implements IGameStateSaveProvider (e.g., PlayerSquadGameStateSaveProvider).")]
        private MonoBehaviour _gameStateProviderBehaviour;

        [SerializeField, Tooltip("Optional custom base directory for save files. Leave empty to use Application.persistentDataPath.")]
        private string _baseDirectoryOverride;

        private IGameStateSaveProvider _gameStateProvider;
        private SaveGameService _service;

        public int MaxSlots => _service != null ? _service.MaxSlots : SaveGameService.DefaultMaxSlots;

        private void Awake()
        {
            ResolveProviderAndService();
        }

        private void OnValidate()
        {
            if (_gameStateProviderBehaviour != null && !(_gameStateProviderBehaviour is IGameStateSaveProvider))
            {
                Debug.LogWarning(
                    $"SaveGameServiceComponent on '{name}': Assigned GameStateProvider does not implement IGameStateSaveProvider.",
                    this);
            }
        }

        private void ResolveProviderAndService()
        {
            if (_gameStateProviderBehaviour == null)
            {
                Debug.LogWarning("SaveGameServiceComponent: GameState provider is not assigned.", this);
                _gameStateProvider = null;
                _service = null;
                return;
            }

            _gameStateProvider = _gameStateProviderBehaviour as IGameStateSaveProvider;
            if (_gameStateProvider == null)
            {
                Debug.LogWarning("SaveGameServiceComponent: Assigned provider does not implement IGameStateSaveProvider.", this);
                _service = null;
                return;
            }

            string baseDir = string.IsNullOrEmpty(_baseDirectoryOverride)
                ? Application.persistentDataPath
                : _baseDirectoryOverride;

            _service = new SaveGameService(_gameStateProvider, baseDir);
        }

        public Task<SaveSlotMetadata[]> LoadAllSlotMetadataAsync()
        {
            if (_service == null)
            {
                ResolveProviderAndService();
                if (_service == null)
                {
                    return Task.FromResult(new SaveSlotMetadata[SaveGameService.DefaultMaxSlots]);
                }
            }

            return _service.LoadAllSlotMetadataAsync();
        }

        public Task<SaveSlotMetadata> SaveSlotAsync(int slotIndex)
        {
            if (_service == null)
            {
                ResolveProviderAndService();
                if (_service == null)
                {
                    Debug.LogWarning("SaveGameServiceComponent: Cannot save because underlying service is not initialized.", this);
                    return Task.FromResult(new SaveSlotMetadata(slotIndex, false, null, 0));
                }
            }

            return _service.SaveSlotAsync(slotIndex);
        }

        public Task<SaveGameData> LoadSlotDataAsync(int slotIndex)
        {
            if (_service == null)
            {
                ResolveProviderAndService();
                if (_service == null)
                {
                    Debug.LogWarning("SaveGameServiceComponent: Cannot load because underlying service is not initialized.", this);
                    return Task.FromResult<SaveGameData>(null);
                }
            }

            return _service.LoadSlotDataAsync(slotIndex);
        }
    }
}
