using UnityEngine;
using UnityEngine.Rendering;

namespace SevenBattles.Battle.Units
{
    // Utility for preparing wizard visuals (direction, sorting, scale) consistently.
    public static class UnitVisualUtil
    {
        private const string CharacterStateTypeName = "Assets.HeroEditor4D.Common.Scripts.Enums.CharacterState";
        private const string Character4DTypeName = "Assets.HeroEditor4D.Common.Scripts.CharacterScripts.Character4D";
        private const string AnimationManagerTypeName = "Assets.HeroEditor4D.Common.Scripts.CharacterScripts.AnimationManager";
        private static System.Type _cachedCharacterStateType;

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
            TryPlayAnimationInternal(instance, animationName);
        }

        // Backwards compatibility alias if needed, or just redirect
        public static void TrySetState(GameObject instance, string stateName) => TryPlayAnimation(instance, stateName);

        private static bool TryPlayAnimationInternal(GameObject instance, string animationName)
        {
            object animationManager = TryGetCharacter4DAnimationManager(instance, out var characterStateType);
            if (animationManager != null)
            {
                if (TryInvokeAnimationManagerMethod(animationManager, animationName))
                {
                    return true;
                }

                if (TryInvokeCharacterState(animationManager, characterStateType, animationName))
                {
                    return true;
                }
            }

            if (characterStateType == null)
            {
                characterStateType = ResolveCharacterStateType(null);
            }

            return TryPlayAnimatorFallback(instance, animationName, characterStateType);
        }

        private static bool TryInvokeAnimationManagerMethod(object animationManager, string animationName)
        {
            try
            {
                var method = animationManager.GetType().GetMethod(animationName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance, null, System.Type.EmptyTypes, null);
                if (method != null)
                {
                    method.Invoke(animationManager, null);
                    return true;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[UnitVisualUtil] Failed to invoke method '{animationName}': {ex.Message}");
            }

            return false;
        }

        private static object TryGetCharacter4DAnimationManager(GameObject instance, out System.Type characterStateType)
        {
            characterStateType = null;
            if (instance == null) return null;

            try
            {
                var components = instance.GetComponentsInChildren<MonoBehaviour>(true);
                if (TryFindAnimationManager(components, ref characterStateType, out var manager))
                {
                    return manager;
                }

                components = instance.GetComponentsInParent<MonoBehaviour>(true);
                if (TryFindAnimationManager(components, ref characterStateType, out manager))
                {
                    return manager;
                }

                if (TryFindAnimationManagerFromCharacter4D(components, ref characterStateType, out manager))
                {
                    return manager;
                }

                components = instance.GetComponentsInChildren<MonoBehaviour>(true);
                if (TryFindAnimationManagerFromCharacter4D(components, ref characterStateType, out manager))
                {
                    return manager;
                }
            }
            catch
            {
                // Ignore and fall through.
            }

            return null;
        }

        private static bool TryFindAnimationManager(MonoBehaviour[] components, ref System.Type characterStateType, out object manager)
        {
            manager = null;
            if (components == null) return false;

            for (int i = 0; i < components.Length; i++)
            {
                var comp = components[i];
                if (comp == null) continue;
                var type = comp.GetType();
                if (type.Name != "AnimationManager" && type.FullName != AnimationManagerTypeName) continue;

                characterStateType = ResolveCharacterStateType(type.Assembly);
                manager = comp;
                return true;
            }

            return false;
        }

        private static bool TryFindAnimationManagerFromCharacter4D(MonoBehaviour[] components, ref System.Type characterStateType, out object manager)
        {
            manager = null;
            if (components == null) return false;

            for (int i = 0; i < components.Length; i++)
            {
                var comp = components[i];
                if (comp == null) continue;
                var type = comp.GetType();
                if (type.Name != "Character4D" && type.FullName != Character4DTypeName) continue;

                var field = type.GetField("AnimationManager", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    var value = field.GetValue(comp);
                    if (value != null)
                    {
                        characterStateType = ResolveCharacterStateType(value.GetType().Assembly);
                        manager = value;
                        return true;
                    }
                }

                var prop = type.GetProperty("AnimationManager", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (prop != null)
                {
                    var value = prop.GetValue(comp, null);
                    if (value != null)
                    {
                        characterStateType = ResolveCharacterStateType(value.GetType().Assembly);
                        manager = value;
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryInvokeCharacterState(object animationManager, System.Type characterStateType, string stateName)
        {
            if (animationManager == null || characterStateType == null || string.IsNullOrEmpty(stateName)) return false;

            try
            {
                var method = animationManager.GetType().GetMethod("SetState", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance, null, new System.Type[] { characterStateType }, null);
                if (method == null) return false;
                
                // Try to parse the enum, ignoring case to be more robust
                try
                {
                    var state = System.Enum.Parse(characterStateType, stateName, true);
                    method.Invoke(animationManager, new object[] { state });
                    return true;
                }
                catch (System.ArgumentException)
                {
                    // Only log if we really expected a state and it wasn't a method either.
                    // But since TryPlayAnimation calls this as fallback, we might want to log here.
                    Debug.LogWarning($"[UnitVisualUtil] Animation/State '{stateName}' not found (checked method and CharacterState enum).");
                    return false;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[UnitVisualUtil] Failed to set state '{stateName}': {ex.Message}");
            }

            return false;
        }

        private static bool TryPlayAnimatorFallback(GameObject instance, string animationName, System.Type characterStateType)
        {
            var animator = instance.GetComponentInChildren<Animator>(true);
            if (animator == null) return false;

            if (TrySetAnimatorState(animator, characterStateType, animationName))
            {
                return true;
            }

            if (TryPlayAnimatorState(animator, animationName))
            {
                return true;
            }

            return TrySetAnimatorTrigger(animator, animationName);
        }

        private static bool TrySetAnimatorState(Animator animator, System.Type characterStateType, string stateName)
        {
            if (animator == null || characterStateType == null || string.IsNullOrEmpty(stateName)) return false;
            if (!HasAnimatorParameter(animator, "State", AnimatorControllerParameterType.Int)) return false;

            try
            {
                var state = System.Enum.Parse(characterStateType, stateName, true);
                animator.SetInteger("State", (int)state);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryPlayAnimatorState(Animator animator, string stateName)
        {
            if (animator == null || string.IsNullOrEmpty(stateName)) return false;

            int stateHash = Animator.StringToHash(stateName);
            if (!animator.HasState(0, stateHash)) return false;

            animator.Play(stateHash, 0, 0f);
            return true;
        }

        private static bool TrySetAnimatorTrigger(Animator animator, string triggerName)
        {
            if (animator == null || string.IsNullOrEmpty(triggerName)) return false;
            if (!HasAnimatorParameter(animator, triggerName, AnimatorControllerParameterType.Trigger)) return false;

            animator.SetTrigger(triggerName);
            return true;
        }

        private static bool HasAnimatorParameter(Animator animator, string name, AnimatorControllerParameterType type)
        {
            if (animator == null || string.IsNullOrEmpty(name)) return false;

            var parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                var param = parameters[i];
                if (param.type == type && param.name == name)
                {
                    return true;
                }
            }

            return false;
        }

        private static System.Type ResolveCharacterStateType(System.Reflection.Assembly preferredAssembly)
        {
            if (_cachedCharacterStateType != null) return _cachedCharacterStateType;

            if (preferredAssembly != null)
            {
                _cachedCharacterStateType = preferredAssembly.GetType(CharacterStateTypeName);
                if (_cachedCharacterStateType != null) return _cachedCharacterStateType;
            }

            var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                var type = assemblies[i].GetType(CharacterStateTypeName);
                if (type != null)
                {
                    _cachedCharacterStateType = type;
                    return _cachedCharacterStateType;
                }
            }

            return null;
        }
    }
}
