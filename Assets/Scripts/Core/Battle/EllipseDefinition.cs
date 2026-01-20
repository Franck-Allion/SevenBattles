using System;
using UnityEngine;

namespace SevenBattles.Core.Battle
{
    [Serializable]
    public struct EllipseDefinition
    {
        public Vector2 Center;
        public Vector2 Radii;
        [Range(-180f, 180f)]
        public float RotationDegrees;

        public bool ContainsPoint(Vector2 point)
        {
            if (Radii.x <= 0f || Radii.y <= 0f)
            {
                return false;
            }

            var local = point - Center;
            if (Mathf.Abs(RotationDegrees) > 0.001f)
            {
                float rad = -RotationDegrees * Mathf.Deg2Rad;
                float cos = Mathf.Cos(rad);
                float sin = Mathf.Sin(rad);
                local = new Vector2(local.x * cos - local.y * sin, local.x * sin + local.y * cos);
            }

            float nx = local.x / Radii.x;
            float ny = local.y / Radii.y;
            return (nx * nx + ny * ny) <= 1f;
        }

        public Vector2 GetPointOnPerimeter(float angleRadians)
        {
            var point = new Vector2(Mathf.Cos(angleRadians) * Radii.x, Mathf.Sin(angleRadians) * Radii.y);
            if (Mathf.Abs(RotationDegrees) > 0.001f)
            {
                float rad = RotationDegrees * Mathf.Deg2Rad;
                float cos = Mathf.Cos(rad);
                float sin = Mathf.Sin(rad);
                point = new Vector2(point.x * cos - point.y * sin, point.x * sin + point.y * cos);
            }

            return point;
        }
    }
}
