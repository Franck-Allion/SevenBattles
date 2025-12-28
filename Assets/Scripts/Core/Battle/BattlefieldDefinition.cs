using System;
using SevenBattles.Core.Math;
using UnityEngine;

namespace SevenBattles.Core.Battle
{
    [CreateAssetMenu(menuName = "SevenBattles/Battle/Battlefield Definition", fileName = "BattlefieldDefinition")]
    public sealed class BattlefieldDefinition : ScriptableObject
    {
        public const int DefaultColumns = 7;
        public const int DefaultRows = 6;

        [Header("Identity")]
        public string Id;

        [Header("Visuals")]
        [SerializeField] private Sprite _backgroundSprite;

        [Header("Grid")]
        [SerializeField] private int _columns = DefaultColumns;
        [SerializeField] private int _rows = DefaultRows;
        [SerializeField] private PerspectiveGridMappingMode _gridMappingMode = PerspectiveGridMappingMode.Homography;

        [Header("Inner Quad (local, in this transform's plane)")]
        [SerializeField] private Vector2 _topLeft;
        [SerializeField] private Vector2 _topRight;
        [SerializeField] private Vector2 _bottomRight;
        [SerializeField] private Vector2 _bottomLeft;

        [Header("Board Highlight (optional)")]
        [SerializeField, Range(0f, 0.45f), Tooltip("Inset applied to tile highlight quads so highlights sit inside painted borders. 0 = no inset.")]
        private float _tileHighlightInset01;

        [Header("Tile Colors (row-major, columns x rows)")]
        [SerializeField] private BattlefieldTileColor[] _tileColors = new BattlefieldTileColor[DefaultColumns * DefaultRows];

        [Header("Enchantment Quads (local, in this transform's plane)")]
        [SerializeField] private EnchantmentQuadDefinition[] _enchantmentQuads = new EnchantmentQuadDefinition[0];

        public Sprite BackgroundSprite => _backgroundSprite;
        public int Columns => _columns <= 0 ? DefaultColumns : _columns;
        public int Rows => _rows <= 0 ? DefaultRows : _rows;
        public int TileCount => Columns * Rows;
        public PerspectiveGridMappingMode GridMappingMode => _gridMappingMode;

        public Vector2 TopLeft => _topLeft;
        public Vector2 TopRight => _topRight;
        public Vector2 BottomRight => _bottomRight;
        public Vector2 BottomLeft => _bottomLeft;
        public float TileHighlightInset01 => Mathf.Clamp(_tileHighlightInset01, 0f, 0.45f);
        public EnchantmentQuadDefinition[] EnchantmentQuads => _enchantmentQuads ?? Array.Empty<EnchantmentQuadDefinition>();

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

        public bool IsInBounds(int x, int y)
        {
            return x >= 0 && x < Columns && y >= 0 && y < Rows;
        }

        public int ToIndex(int x, int y)
        {
            return y * Columns + x;
        }

        private void OnValidate()
        {
            EnsureTileArraySize();
            EnsureEnchantmentQuads();
        }

        private void OnEnable()
        {
            EnsureTileArraySize();
            EnsureEnchantmentQuads();
        }

        private void EnsureTileArraySize()
        {
            _columns = _columns <= 0 ? DefaultColumns : _columns;
            _rows = _rows <= 0 ? DefaultRows : _rows;
            _tileHighlightInset01 = Mathf.Clamp(_tileHighlightInset01, 0f, 0.45f);

            int targetCount = _columns * _rows;
            if (_tileColors != null && _tileColors.Length == targetCount)
            {
                return;
            }

            var resized = new BattlefieldTileColor[targetCount];
            if (_tileColors != null)
            {
                Array.Copy(_tileColors, resized, Mathf.Min(_tileColors.Length, resized.Length));
            }

            _tileColors = resized;
        }

        private void EnsureEnchantmentQuads()
        {
            if (_enchantmentQuads == null)
            {
                _enchantmentQuads = Array.Empty<EnchantmentQuadDefinition>();
                return;
            }

            for (int i = 0; i < _enchantmentQuads.Length; i++)
            {
                var quad = _enchantmentQuads[i];
                if (quad.Scale <= 0f)
                {
                    quad.Scale = 1f;
                    _enchantmentQuads[i] = quad;
                }
            }
        }
    }
}
