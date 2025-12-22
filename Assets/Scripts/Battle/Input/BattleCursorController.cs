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
            Move = 1,
            Selection = 2,
            Attack = 3,
            Spell = 4
        }

        private CursorKind _cursorKind;
        private Texture2D _cursorTexture;
        private Vector2 _cursorHotspot;

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
        /// Clears all cursors and resets to the default system cursor.
        /// </summary>
        public void ClearAll()
        {
            _cursorKind = CursorKind.None;
            _cursorTexture = null;
            _cursorHotspot = Vector2.zero;
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }

        private void ApplyCursor(CursorKind kind, Texture2D texture, Vector2 hotspot)
        {
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
                Cursor.SetCursor(texture, hotspot, CursorMode.Auto);
            }
            else
            {
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            }
        }

        private void ClearCursorIfActive(CursorKind kind)
        {
            if (_cursorKind != kind)
            {
                return;
            }

            ClearAll();
        }
    }
}
