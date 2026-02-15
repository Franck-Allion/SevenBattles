using UnityEngine;

namespace SevenBattles.Core.Preload
{
    /// <summary>
    /// Declarative scene preload manifest listing assets and data that should be prepared
    /// before the target scene becomes interactive.
    /// </summary>
    [CreateAssetMenu(menuName = "SevenBattles/Preload/Scene Preload Manifest")]
    public sealed class ScenePreloadManifest : ScriptableObject
    {
        [SerializeField] private string _sceneName;
        [SerializeField] private ShaderVariantCollection[] _shaderCollections;
        [SerializeField] private string[] _localizationTableNames;
        [SerializeField] private Object[] _prefabsToWarm;

        public string SceneName => _sceneName;
        public ShaderVariantCollection[] ShaderCollections => _shaderCollections;
        public string[] LocalizationTableNames => _localizationTableNames;
        public Object[] PrefabsToWarm => _prefabsToWarm;
    }
}
