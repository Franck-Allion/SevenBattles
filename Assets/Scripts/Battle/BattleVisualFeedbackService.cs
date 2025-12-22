using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace SevenBattles.Battle
{
    /// <summary>
    /// Service managing battle visual effects such as damage numbers, healing effects, and buff indicators.
    /// Should be attached to the _System GameObject in BattleScene.
    /// </summary>
    public class BattleVisualFeedbackService : MonoBehaviour
    {
        [Header("Damage Numbers")]
        [SerializeField, Tooltip("DamageNumbersPro prefab to instantiate when showing damage. Select from Assets/DamageNumbersPro/Demo/Prefabs/2D/ (e.g., 'Red Glow', 'Blood Text').")]
        private GameObject _damageNumberPrefab;

        [SerializeField, Tooltip("Vertical offset in world units to display damage numbers above the unit.")]
        private float _damageNumberYOffset = 2f;

        [SerializeField, Tooltip("Scale multiplier for the damage number prefab. Increase to make numbers larger, decrease to make them smaller.")]
        private float _damageNumberScale = 1f;

        [Header("Heal Numbers")]
        [SerializeField, Tooltip("DamageNumbersPro prefab to instantiate when showing healing. Use a visually distinct style (e.g., green) from damage numbers.")]
        private GameObject _healNumberPrefab;

        [SerializeField, Tooltip("Vertical offset in world units to display heal numbers above the unit.")]
        private float _healNumberYOffset = 2f;

        [SerializeField, Tooltip("Scale multiplier for the heal number prefab.")]
        private float _healNumberScale = 1f;

        [SerializeField, Tooltip("Sorting layer used to render heal numbers in front of units.")]
        private string _healNumberSortingLayer = "Characters";

        [SerializeField, Tooltip("Sorting order used to render heal numbers in front of all world items.")]
        private int _healNumberSortingOrder = short.MaxValue;

        /// <summary>
        /// Displays a damage number at the specified world position.
        /// </summary>
        /// <param name="worldPosition">World position where the damage occurred (typically unit position).</param>
        /// <param name="damage">Damage value to display.</param>
        public void ShowDamageNumber(Vector3 worldPosition, int damage)
        {
            if (damage < 0)
            {
                Debug.LogWarning($"[BattleVisualFeedbackService] Invalid damage value: {damage}. Damage should be non-negative.", this);
                return;
            }

            ShowNumber(_damageNumberPrefab, _damageNumberYOffset, _damageNumberScale, worldPosition, damage, "damage", "_damageNumberPrefab");
        }

        /// <summary>
        /// Displays a healing number at the specified world position.
        /// </summary>
        public void ShowHealNumber(Vector3 worldPosition, int healAmount)
        {
            if (healAmount < 0)
            {
                Debug.LogWarning($"[BattleVisualFeedbackService] Invalid heal value: {healAmount}. Heal should be non-negative.", this);
                return;
            }

            ShowNumber(
                _healNumberPrefab,
                _healNumberYOffset,
                _healNumberScale,
                worldPosition,
                healAmount,
                "heal",
                "_healNumberPrefab",
                sortingLayerName: _healNumberSortingLayer,
                sortingOrder: _healNumberSortingOrder);
        }

        /// <summary>
        /// Future extension point for showing buff/debuff text.
        /// </summary>
        public void ShowBuffText(Vector3 worldPosition, string text)
        {
            // TODO: Implement buff/debuff visual feedback
            Debug.Log($"[BattleVisualFeedbackService] ShowBuffText not yet implemented: '{text}' at {worldPosition}");
        }

        private void ShowNumber(
            GameObject prefab,
            float yOffset,
            float scaleMultiplier,
            Vector3 worldPosition,
            int value,
            string label,
            string prefabFieldNameForLogs,
            string sortingLayerName = null,
            int? sortingOrder = null)
        {
            if (prefab == null)
            {
                Debug.LogWarning($"[BattleVisualFeedbackService] Cannot show {label} number: {prefabFieldNameForLogs} is not assigned.", this);
                return;
            }

            Vector3 spawnPosition = worldPosition;
            spawnPosition.y += yOffset;

            try
            {
                GameObject numberInstance = Instantiate(prefab, spawnPosition, Quaternion.identity);

                if (!Mathf.Approximately(scaleMultiplier, 1f))
                {
                    numberInstance.transform.localScale = numberInstance.transform.localScale * scaleMultiplier;
                }

                if (!string.IsNullOrWhiteSpace(sortingLayerName) && sortingOrder.HasValue)
                {
                    ApplySorting(numberInstance, sortingLayerName, sortingOrder.Value);
                }

                bool valueSet = TrySetNumberValue(numberInstance, value);
                if (!valueSet)
                {
                    Debug.LogWarning($"[BattleVisualFeedbackService] Could not set {label} value on prefab. The prefab may need manual configuration or a different integration approach.", this);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[BattleVisualFeedbackService] Failed to instantiate {label} number prefab: {ex.Message}", this);
            }
        }

        private static void ApplySorting(GameObject instance, string sortingLayerName, int sortingOrder)
        {
            if (instance == null)
            {
                return;
            }

            if (!SortingLayerExists(sortingLayerName))
            {
                sortingLayerName = "Default";
            }

            var sortingGroups = instance.GetComponentsInChildren<SortingGroup>(true);
            for (int i = 0; i < sortingGroups.Length; i++)
            {
                if (sortingGroups[i] == null) continue;
                sortingGroups[i].sortingLayerName = sortingLayerName;
                sortingGroups[i].sortingOrder = sortingOrder;
            }

            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                renderers[i].sortingLayerName = sortingLayerName;
                renderers[i].sortingOrder = sortingOrder;
            }
        }

        private static bool SortingLayerExists(string sortingLayerName)
        {
            if (string.IsNullOrWhiteSpace(sortingLayerName))
            {
                return false;
            }

            var layers = SortingLayer.layers;
            for (int i = 0; i < layers.Length; i++)
            {
                if (string.Equals(layers[i].name, sortingLayerName, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TrySetNumberValue(GameObject instance, int value)
        {
            if (instance == null)
            {
                return false;
            }

            var components = instance.GetComponents<MonoBehaviour>();
            for (int i = 0; i < components.Length; i++)
            {
                var component = components[i];
                if (component == null)
                {
                    continue;
                }

                var type = component.GetType();

                var setNumberMethod = type.GetMethod("SetNumber", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (setNumberMethod != null && setNumberMethod.GetParameters().Length == 1)
                {
                    try
                    {
                        setNumberMethod.Invoke(component, new object[] { (float)value });
                        return true;
                    }
                    catch
                    {
                    }
                }

                var numberField = type.GetField("number", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                                ?? type.GetField("value", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (numberField != null && (numberField.FieldType == typeof(float) || numberField.FieldType == typeof(int)))
                {
                    try
                    {
                        numberField.SetValue(component, numberField.FieldType == typeof(float) ? (float)value : value);
                        return true;
                    }
                    catch
                    {
                    }
                }
            }

            return false;
        }
    }
}
