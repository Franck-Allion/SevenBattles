using UnityEngine;
using SevenBattles.Battle.Board;

namespace SevenBattles.Battle.Start
{
    // Spawns a squad of wizards (world-space SpriteRenderer prefabs) on the first row of the board.
    // Default orientation of HeroEditor4D wizards faces the top of the screen, so no rotation is applied.
    public class WorldSquadStartController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private WorldPerspectiveBoard _board;

        [Header("Squad Prefabs")]
        [Tooltip("Wizards to spawn. Expected size: 3.")]
        [SerializeField] private GameObject[] _wizardPrefabs = new GameObject[3];

        [Header("Placement")]
        [Tooltip("Tile X indices for each wizard on row 0. If empty or shorter than prefabs, defaults to 0,1,2.")]
        [SerializeField] private int[] _tileXs;
        [SerializeField] private int _rowY = 0; // First row

        [Header("Rendering")]
        [SerializeField] private string _sortingLayer = "Characters";
        [SerializeField] private int _baseSortingOrder = 0; // Each subsequent wizard increments by 1 to avoid z-fighting
        [SerializeField, Tooltip("Uniform scale multiplier applied to each spawned wizard instance.")]
        private float _scaleMultiplier = 1f;

        [Header("Behavior")]
        [SerializeField] private bool _autoStartOnPlay = true;

        private void Start()
        {
            if (_autoStartOnPlay)
            {
                StartSquad();
            }
        }

        // Public entry to spawn all configured wizards.
        public void StartSquad()
        {
            if (_board == null)
            {
                Debug.LogWarning("WorldSquadStartController: Missing board reference.");
                return;
            }

            if (_wizardPrefabs == null || _wizardPrefabs.Length == 0)
            {
                Debug.LogWarning("WorldSquadStartController: No wizard prefabs configured.");
                return;
            }

            for (int i = 0; i < _wizardPrefabs.Length; i++)
            {
                var prefab = _wizardPrefabs[i];
                if (prefab == null) continue;

                int tileX = GetTileXForIndex(i);
                var go = Object.Instantiate(prefab);
                if (!Mathf.Approximately(_scaleMultiplier, 1f))
                {
                    go.transform.localScale = go.transform.localScale * _scaleMultiplier;
                }
                InitializeHeroEditor4D(go, desired: DirectionFacing.Up, sortingOrder: _baseSortingOrder + i);
                _board.PlaceHero(go.transform, tileX, _rowY, _sortingLayer, _baseSortingOrder + i);
            }
        }

        private int GetTileXForIndex(int index)
        {
            if (_tileXs != null && index < _tileXs.Length)
                return _tileXs[index];
            // Default to first three columns: 0,1,2,...
            return index;
        }

        private enum DirectionFacing { Front, Back, Left, Right, Up = Back, Down = Front }

        // Uses HeroEditor4D Character4D.SetDirection if present (via reflection).
        // If not available, logs a debug message and does not try to toggle directional children.
        private void InitializeHeroEditor4D(GameObject instance, DirectionFacing desired, int sortingOrder)
        {
            if (instance == null) return;

            // Try to find the Character4D component and call SetDirection(Vector2)
            bool setByCharacter4D = false;
            try
            {
                var components = instance.GetComponents<MonoBehaviour>();
                for (int i = 0; i < components.Length; i++)
                {
                    var comp = components[i];
                    if (comp == null) continue;
                    var type = comp.GetType();
                    if (type.Name != "Character4D" && type.FullName != "Assets.HeroEditor4D.Common.Scripts.CharacterScripts.Character4D") continue;

                    var method = type.GetMethod("SetDirection", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance, null, new System.Type[] { typeof(Vector2) }, null);
                    if (method != null)
                    {
                        // In our other project, Vector2.down shows the back-facing view (towards top of screen)
                        method.Invoke(comp, new object[] { Vector2.up });
                        setByCharacter4D = true;
                        break;
                    }
                }
            }
            catch
            {
                // Intentionally swallow and proceed to debug log below
            }

            if (!setByCharacter4D)
            {
                Debug.Log("WorldSquadStartController: Character4D.SetDirection(Vector2) not found. Prefab shows its default visuals.");
            }

            var renderers = instance.GetComponentsInChildren<SpriteRenderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                var sr = renderers[r];
                sr.sortingLayerName = _sortingLayer;
                sr.sortingOrder = sortingOrder;
            }
        }
    }
}
