using UnityEngine;

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

        /// <summary>
        /// Displays a damage number at the specified world position.
        /// </summary>
        /// <param name="worldPosition">World position where the damage occurred (typically unit position).</param>
        /// <param name="damage">Damage value to display.</param>
        public void ShowDamageNumber(Vector3 worldPosition, int damage)
        {
            if (_damageNumberPrefab == null)
            {
                Debug.LogWarning("[BattleVisualFeedbackService] Cannot show damage number: _damageNumberPrefab is not assigned.", this);
                return;
            }

            if (damage < 0)
            {
                Debug.LogWarning($"[BattleVisualFeedbackService] Invalid damage value: {damage}. Damage should be non-negative.", this);
                return;
            }

            // Calculate spawn position with vertical offset
            Vector3 spawnPosition = worldPosition;
            spawnPosition.y += _damageNumberYOffset;

            try
            {
                // Instantiate the damage number prefab
                GameObject damageNumberInstance = Instantiate(_damageNumberPrefab, spawnPosition, Quaternion.identity);

                // Apply scale if different from 1.0
                if (!Mathf.Approximately(_damageNumberScale, 1f))
                {
                    damageNumberInstance.transform.localScale = damageNumberInstance.transform.localScale * _damageNumberScale;
                }

                // DamageNumbersPro prefabs typically auto-configure themselves based on the prefab settings.
                // The damage value is usually set via a component on the prefab.
                // Common patterns:
                // 1. DamageNumber component with a public field/method
                // 2. DamageNumberMesh component
                // We'll try to find and set the number value dynamically.

                // Try to set the number value using reflection to avoid hard dependency
                var components = damageNumberInstance.GetComponents<MonoBehaviour>();
                bool valueSet = false;

                foreach (var component in components)
                {
                    if (component == null) continue;

                    var type = component.GetType();
                    
                    // Try common method names for setting the number
                    var setNumberMethod = type.GetMethod("SetNumber", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (setNumberMethod != null && setNumberMethod.GetParameters().Length == 1)
                    {
                        try
                        {
                            setNumberMethod.Invoke(component, new object[] { (float)damage });
                            valueSet = true;
                            break;
                        }
                        catch { }
                    }

                    // Try setting a public field named "number" or "value"
                    var numberField = type.GetField("number", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (numberField != null && (numberField.FieldType == typeof(float) || numberField.FieldType == typeof(int)))
                    {
                        try
                        {
                            numberField.SetValue(component, numberField.FieldType == typeof(float) ? (float)damage : damage);
                            valueSet = true;
                            break;
                        }
                        catch { }
                    }
                }

                if (!valueSet)
                {
                    Debug.LogWarning($"[BattleVisualFeedbackService] Could not set damage value on prefab. The prefab may need manual configuration or a different integration approach.", this);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[BattleVisualFeedbackService] Failed to instantiate damage number prefab: {ex.Message}", this);
            }
        }

        /// <summary>
        /// Future extension point for showing healing numbers.
        /// </summary>
        public void ShowHealNumber(Vector3 worldPosition, int healAmount)
        {
            // TODO: Implement healing visual feedback (could use different prefab or color)
            Debug.Log($"[BattleVisualFeedbackService] ShowHealNumber not yet implemented: {healAmount} at {worldPosition}");
        }

        /// <summary>
        /// Future extension point for showing buff/debuff text.
        /// </summary>
        public void ShowBuffText(Vector3 worldPosition, string text)
        {
            // TODO: Implement buff/debuff visual feedback
            Debug.Log($"[BattleVisualFeedbackService] ShowBuffText not yet implemented: '{text}' at {worldPosition}");
        }
    }
}
