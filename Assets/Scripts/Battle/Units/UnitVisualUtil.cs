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

            // Validate sorting order - should never be 0 or negative (would render behind board)
            if (sortingOrder <= 0)
            {
                Debug.LogWarning($"[UnitVisualUtil] InitializeHero called with invalid sortingOrder={sortingOrder}. " +
                                 $"This will cause rendering issues. Setting to default 100.");
                sortingOrder = 100;
            }

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

        public static void TryPlayAnimation(GameObject instance, string animationName)
        {
            if (instance == null || string.IsNullOrEmpty(animationName)) return;

            object animationManager = TryGetCharacter4DAnimationManager(instance, out var characterStateType);
            if (animationManager == null) return;

            // 1. Try to find and invoke a parameterless method with the given name (e.g. "Attack", "Hit")
            try
            {
                var method = animationManager.GetType().GetMethod(animationName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance, null, System.Type.EmptyTypes, null);
                if (method != null)
                {
                    method.Invoke(animationManager, null);
                    return;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[UnitVisualUtil] Failed to invoke method '{animationName}': {ex.Message}");
            }

            // 2. Fallback: Try to set state if it matches a CharacterState enum (e.g. "Idle", "Walk")
            if (characterStateType != null)
            {
                TryInvokeCharacterState(animationManager, characterStateType, animationName);
            }
        }

        // Backwards compatibility alias if needed, or just redirect
        public static void TrySetState(GameObject instance, string stateName) => TryPlayAnimation(instance, stateName);

        private static object TryGetCharacter4DAnimationManager(GameObject instance, out System.Type characterStateType)
        {
            characterStateType = null;
            if (instance == null) return null;

            try
            {
                var components = instance.GetComponentsInChildren<MonoBehaviour>(true);
                for (int i = 0; i < components.Length; i++)
                {
                    var comp = components[i];
                    if (comp == null) continue;
                    var type = comp.GetType();

                    if (type.Name == "AnimationManager" || type.FullName == "Assets.HeroEditor4D.Common.Scripts.CharacterScripts.AnimationManager")
                    {
                        var asm = type.Assembly;
                        characterStateType = asm.GetType("Assets.HeroEditor4D.Common.Scripts.Enums.CharacterState");
                        if (characterStateType == null) return null;
                        return comp;
                    }
                }
            }
            catch
            {
                // Ignore and fall through.
            }

            return null;
        }

        private static void TryInvokeCharacterState(object animationManager, System.Type characterStateType, string stateName)
        {
            if (animationManager == null || characterStateType == null || string.IsNullOrEmpty(stateName)) return;

            try
            {
                var method = animationManager.GetType().GetMethod("SetState", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance, null, new System.Type[] { characterStateType }, null);
                if (method == null) return;
                
                // Try to parse the enum, ignoring case to be more robust
                try
                {
                    var state = System.Enum.Parse(characterStateType, stateName, true);
                    method.Invoke(animationManager, new object[] { state });
                }
                catch (System.ArgumentException)
                {
                    // Only log if we really expected a state and it wasn't a method either.
                    // But since TryPlayAnimation calls this as fallback, we might want to log here.
                    Debug.LogWarning($"[UnitVisualUtil] Animation/State '{stateName}' not found (checked method and CharacterState enum).");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[UnitVisualUtil] Failed to set state '{stateName}': {ex.Message}");
            }
        }
    }
}
