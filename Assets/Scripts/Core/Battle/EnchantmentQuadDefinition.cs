using System;
using UnityEngine;

namespace SevenBattles.Core.Battle
{
    [Serializable]
    public struct EnchantmentQuadDefinition
    {
        public Vector2 TopLeft;
        public Vector2 TopRight;
        public Vector2 BottomRight;
        public Vector2 BottomLeft;

        public Vector2 Offset;

        [Min(0f)]
        public float Scale;

        public Vector2 Center =>
            (TopLeft + TopRight + BottomRight + BottomLeft) * 0.25f;
    }
}
