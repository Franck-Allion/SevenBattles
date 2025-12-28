using System;
using UnityEngine;

namespace SevenBattles.Core.Battle
{
    [CreateAssetMenu(menuName = "SevenBattles/Battle/Battlefield Definition", fileName = "BattlefieldDefinition")]
    public sealed class BattlefieldDefinition : ScriptableObject
    {
        public const int Columns = 7;
        public const int Rows = 6;
        public const int TileCount = Columns * Rows;

        [Header("Identity")]
        public string Id;

        [Header("Visuals")]
        [SerializeField] private Sprite _backgroundSprite;

        [Header("Grid (row-major, 7x6)")]
        [SerializeField] private BattlefieldTileColor[] _tileColors = new BattlefieldTileColor[TileCount];

        public Sprite BackgroundSprite => _backgroundSprite;

        public bool TryGetTileColor(Vector2Int tile, out BattlefieldTileColor color)
        {
            return TryGetTileColor(tile.x, tile.y, out color);
        }

        public bool TryGetTileColor(int x, int y, out BattlefieldTileColor color)
        {
            if (!IsInBounds(x, y))
            {
                color = BattlefieldTileColor.None;
                return false;
            }

            int index = ToIndex(x, y);
            if (_tileColors == null || index < 0 || index >= _tileColors.Length)
            {
                color = BattlefieldTileColor.None;
                return false;
            }

            color = _tileColors[index];
            return true;
        }

        public BattlefieldTileColor GetTileColor(int x, int y)
        {
            return TryGetTileColor(x, y, out var color) ? color : BattlefieldTileColor.None;
        }

        public static bool IsInBounds(int x, int y)
        {
            return x >= 0 && x < Columns && y >= 0 && y < Rows;
        }

        public static int ToIndex(int x, int y)
        {
            return y * Columns + x;
        }

        private void OnValidate()
        {
            EnsureTileArraySize();
        }

        private void OnEnable()
        {
            EnsureTileArraySize();
        }

        private void EnsureTileArraySize()
        {
            if (_tileColors != null && _tileColors.Length == TileCount)
            {
                return;
            }

            var resized = new BattlefieldTileColor[TileCount];
            if (_tileColors != null)
            {
                Array.Copy(_tileColors, resized, Mathf.Min(_tileColors.Length, resized.Length));
            }

            _tileColors = resized;
        }
    }
}
