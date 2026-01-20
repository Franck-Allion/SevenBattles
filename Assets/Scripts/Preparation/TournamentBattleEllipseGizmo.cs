using UnityEngine;
using SevenBattles.Core.Battle;

namespace SevenBattles.Preparation
{
    public sealed class TournamentBattleEllipseGizmo : MonoBehaviour
    {
        [SerializeField] private TournamentDefinition _tournament;
        [SerializeField, Range(12, 128)] private int _segments = 48;
        [SerializeField] private Color _gizmoColor = new Color(0.95f, 0.8f, 0.3f, 0.7f);
        [SerializeField] private bool _drawCenters = true;

        private void OnDrawGizmos()
        {
            if (_tournament == null)
            {
                return;
            }

            var battles = _tournament.Battles;
            if (battles == null)
            {
                return;
            }

            var previousMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = _gizmoColor;

            int steps = Mathf.Max(12, _segments);
            float step = Mathf.PI * 2f / steps;

            for (int i = 0; i < battles.Length; i++)
            {
                var battle = battles[i];
                if (battle == null)
                {
                    continue;
                }

                DrawEllipse(battle.Ellipse, steps, step);
            }

            Gizmos.matrix = previousMatrix;
        }

        private void DrawEllipse(EllipseDefinition ellipse, int steps, float step)
        {
            var center = ellipse.Center;
            var previous = center + ellipse.GetPointOnPerimeter(0f);

            for (int i = 1; i <= steps; i++)
            {
                float angle = step * i;
                var current = center + ellipse.GetPointOnPerimeter(angle);
                Gizmos.DrawLine(new Vector3(previous.x, previous.y, 0f), new Vector3(current.x, current.y, 0f));
                previous = current;
            }

            if (_drawCenters)
            {
                Gizmos.DrawSphere(new Vector3(center.x, center.y, 0f), 0.05f);
            }
        }
    }
}
