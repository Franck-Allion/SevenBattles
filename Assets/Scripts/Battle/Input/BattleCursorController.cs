using UnityEngine;
using SevenBattles.Core.Battle;

namespace SevenBattles.Battle.Cursors
{
    /// <summary>
    /// Manages battle cursor states (move, attack, selection, spell targeting).
    /// Ensures only one cursor is active at a time and handles transitions cleanly.
    /// </summary>
    public class BattleCursorController : MonoBehaviour
    {
        private enum CursorKind
        {
            None = 0,
            Default = 1,
            Move = 2,
            Selection = 3,
            Attack = 4,
            Spell = 5
        }

        [Header("Default Cursor")]
        [SerializeField, Tooltip("Cursor texture used by default during battle when no specific cursor is active.")]
        private Texture2D _defaultCursorTexture;
        [SerializeField, Tooltip("Hotspot offset for the default battle cursor (typically center of the texture).")]
        private Vector2 _defaultCursorHotspot = new Vector2(16f, 16f);

        private CursorKind _cursorKind;
        private Texture2D _cursorTexture;
        private Vector2 _cursorHotspot;
        private ICursorBackend _cursorBackend = new UnityCursorBackend();

        private void OnEnable()
        {
            if (_cursorKind == CursorKind.None)
            {
                ResetToDefaultOrSystem();
            }
        }

        internal void SetCursorBackendForTests(ICursorBackend backend)
        {
            _cursorBackend = backend ?? new UnityCursorBackend();
        }

        /// <summary>
        /// Sets the move cursor (displayed when hovering over legal movement tiles).
        /// </summary>
        public void SetMoveCursor(bool active, Texture2D texture, Vector2 hotspot)
        {
            if (active)
            {
                ApplyCursor(CursorKind.Move, texture, hotspot);
                return;
            }

            ClearCursorIfActive(CursorKind.Move);
        }

        /// <summary>
        /// Sets the selection cursor (displayed when a movement tile has been selected, awaiting confirmation).
        /// </summary>
        public void SetSelectionCursor(bool active, Texture2D texture, Vector2 hotspot)
        {
            if (active)
            {
                ApplyCursor(CursorKind.Selection, texture, hotspot);
                return;
            }

            ClearCursorIfActive(CursorKind.Selection);
        }

        /// <summary>
        /// Sets the attack cursor (displayed when hovering over attackable enemies).
        /// </summary>
        public void SetAttackCursor(bool active, Texture2D texture, Vector2 hotspot)
        {
            if (active)
            {
                ApplyCursor(CursorKind.Attack, texture, hotspot);
                return;
            }

            ClearCursorIfActive(CursorKind.Attack);
        }

        /// <summary>
        /// Sets the spell targeting cursor (uses the spell's targeting cursor texture if provided).
        /// </summary>
        public void SetSpellCursor(bool active, SpellDefinition spell)
        {
            if (!active)
            {
                ClearCursorIfActive(CursorKind.Spell);
                return;
            }

            var texture = spell != null ? spell.TargetingCursorTexture : null;
            var hotspot = spell != null ? spell.TargetingCursorHotspot : Vector2.zero;
            ApplyCursor(CursorKind.Spell, texture, hotspot);
        }

        /// <summary>
        /// Clears all cursors and resets to the default battle cursor (or system cursor if none configured).
        /// </summary>
        public void ClearAll()
        {
            _cursorKind = CursorKind.None;
            _cursorTexture = null;
            _cursorHotspot = Vector2.zero;
            ResetToDefaultOrSystem();
        }

        /// <summary>
        /// Applies the configured default cursor for battle, or falls back to the system cursor.
        /// </summary>
        public void ApplyDefaultCursor()
        {
            ResetToDefaultOrSystem();
        }

        private void ApplyCursor(CursorKind kind, Texture2D texture, Vector2 hotspot)
        {
            if (texture == null && _defaultCursorTexture != null)
            {
                kind = CursorKind.Default;
                texture = _defaultCursorTexture;
                hotspot = _defaultCursorHotspot;
            }

            // Avoid redundant SetCursor calls
            if (_cursorKind == kind && ReferenceEquals(_cursorTexture, texture) && _cursorHotspot == hotspot)
            {
                return;
            }

            _cursorKind = kind;
            _cursorTexture = texture;
            _cursorHotspot = hotspot;

            if (texture != null)
            {
                _cursorBackend.SetCursor(texture, hotspot, CursorMode.Auto);
            }
            else
            {
                _cursorBackend.SetCursor(null, Vector2.zero, CursorMode.Auto);
            }
        }

        private void ClearCursorIfActive(CursorKind kind)
        {
            if (_cursorKind != kind)
            {
                return;
            }

            ResetToDefaultOrSystem();
        }

        private void ResetToDefaultOrSystem()
        {
            if (_defaultCursorTexture != null)
            {
                ApplyCursor(CursorKind.Default, _defaultCursorTexture, _defaultCursorHotspot);
                return;
            }

            _cursorKind = CursorKind.None;
            _cursorTexture = null;
            _cursorHotspot = Vector2.zero;
            _cursorBackend.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }

        internal interface ICursorBackend
        {
            void SetCursor(Texture2D texture, Vector2 hotspot, CursorMode mode);
        }

        private sealed class UnityCursorBackend : ICursorBackend
        {
            public void SetCursor(Texture2D texture, Vector2 hotspot, CursorMode mode)
            {
                Cursor.SetCursor(texture, hotspot, mode);
            }
        }
    }
}
