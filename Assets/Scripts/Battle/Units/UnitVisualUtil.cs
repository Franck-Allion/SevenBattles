using UnityEngine;
using UnityEngine.Rendering;

namespace SevenBattles.Battle.Units
{
    // Utility for preparing wizard visuals (direction, sorting, scale) consistently.
    public static class UnitVisualUtil
    {
        public static void SetDirectionIfCharacter4D(GameObject instance, Vector2 direction)
        {
            if (instance == null) return;

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
                        method.Invoke(comp, new object[] { direction });
                        break;
                    }
                }
            }
            catch
            {
            }

            var meta = instance.GetComponent<UnitBattleMetadata>();
            if (meta != null)
            {
                meta.Facing = direction;
            }
        }

        public static void InitializeHero(GameObject instance, string sortingLayer, int sortingOrder, Vector2? desiredDirection)
        {
            if (instance == null) return;

            if (desiredDirection.HasValue)
            {
                SetDirectionIfCharacter4D(instance, desiredDirection.Value);
            }

            // Apply sorting to SortingGroup if present, otherwise to all child SpriteRenderers
            var group = instance.GetComponentInChildren<SortingGroup>(true);
            if (group != null)
            {
                group.sortingLayerName = sortingLayer;
                group.sortingOrder = sortingOrder;
            }
            else
            {
                var renderers = instance.GetComponentsInChildren<SpriteRenderer>(true);
                for (int r = 0; r < renderers.Length; r++)
                {
                    var sr = renderers[r];
                    sr.sortingLayerName = sortingLayer;
                    sr.sortingOrder = sortingOrder;
                }
            }
        }

        public static void ApplyScale(GameObject instance, float scaleMultiplier)
        {
            if (instance == null) return;
            if (!Mathf.Approximately(scaleMultiplier, 1f))
            {
                instance.transform.localScale = instance.transform.localScale * scaleMultiplier;
            }
        }
    }
}
