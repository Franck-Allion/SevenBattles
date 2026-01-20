using UnityEditor;
using UnityEngine;
using SevenBattles.Core.Battle;

namespace SevenBattles.Preparation.Editor
{
    [CustomEditor(typeof(TournamentBattleEllipseGizmo))]
    public sealed class TournamentBattleEllipseGizmoEditor : UnityEditor.Editor
    {
        private const float MinRadius = 0.05f;

        private SerializedProperty _tournament;
        private SerializedProperty _segments;
        private SerializedProperty _gizmoColor;
        private SerializedProperty _drawCenters;

        private int _selectedIndex;
        private bool _editAll;
        private bool _showRotationHandle = true;
        private float _handleScale = 0.06f;

        private void OnEnable()
        {
            _tournament = serializedObject.FindProperty("_tournament");
            _segments = serializedObject.FindProperty("_segments");
            _gizmoColor = serializedObject.FindProperty("_gizmoColor");
            _drawCenters = serializedObject.FindProperty("_drawCenters");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_tournament);
            EditorGUILayout.PropertyField(_segments);
            EditorGUILayout.PropertyField(_gizmoColor);
            EditorGUILayout.PropertyField(_drawCenters);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Ellipse Editing", EditorStyles.boldLabel);
            _editAll = EditorGUILayout.Toggle("Edit All", _editAll);
            _showRotationHandle = EditorGUILayout.Toggle("Rotation Handle", _showRotationHandle);
            _handleScale = EditorGUILayout.Slider("Handle Size", _handleScale, 0.02f, 0.2f);

            var tournament = _tournament.objectReferenceValue as TournamentDefinition;
            if (tournament != null)
            {
                var count = tournament.Battles != null ? tournament.Battles.Length : 0;
                if (count > 0)
                {
                    _selectedIndex = Mathf.Clamp(_selectedIndex, 0, count - 1);
                    using (new EditorGUI.DisabledScope(_editAll))
                    {
                        _selectedIndex = EditorGUILayout.IntSlider("Selected Battle", _selectedIndex + 1, 1, count) - 1;
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Prev")) _selectedIndex = Mathf.Max(0, _selectedIndex - 1);
                        if (GUILayout.Button("Next")) _selectedIndex = Mathf.Min(count - 1, _selectedIndex + 1);
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("Tournament has no battles configured.", MessageType.Info);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Assign a TournamentDefinition to edit ellipses.", MessageType.Info);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void OnSceneGUI()
        {
            var gizmo = (TournamentBattleEllipseGizmo)target;
            var tournament = _tournament.objectReferenceValue as TournamentDefinition;
            if (gizmo == null || tournament == null)
            {
                return;
            }

            var battles = tournament.Battles;
            if (battles == null || battles.Length == 0)
            {
                return;
            }

            var serializedTournament = new SerializedObject(tournament);
            serializedTournament.Update();
            var battlesProp = serializedTournament.FindProperty("_battles");
            if (battlesProp == null || battlesProp.arraySize == 0)
            {
                return;
            }

            var prevMatrix = Handles.matrix;
            Handles.matrix = gizmo.transform.localToWorldMatrix;

            int steps = Mathf.Max(12, _segments.intValue);
            for (int i = 0; i < battlesProp.arraySize; i++)
            {
                var battleProp = battlesProp.GetArrayElementAtIndex(i);
                var ellipseProp = battleProp.FindPropertyRelative("_ellipse");
                if (ellipseProp == null)
                {
                    continue;
                }

                var centerProp = ellipseProp.FindPropertyRelative("Center");
                var radiiProp = ellipseProp.FindPropertyRelative("Radii");
                var rotationProp = ellipseProp.FindPropertyRelative("RotationDegrees");

                var center = centerProp.vector2Value;
                var radii = radiiProp.vector2Value;
                float rotation = rotationProp.floatValue;

                bool isSelected = _editAll || i == _selectedIndex;
                DrawEllipseOutline(center, radii, rotation, steps, isSelected);
                DrawBattleLabel(gizmo.transform, center, i, isSelected);

                float pickSize = HandleUtility.GetHandleSize(gizmo.transform.TransformPoint(center)) * _handleScale;
                if (Handles.Button(center, Quaternion.identity, pickSize, pickSize * 1.3f, Handles.DotHandleCap))
                {
                    _selectedIndex = i;
                    Repaint();
                }

                if (!isSelected)
                {
                    continue;
                }

                EditorGUI.BeginChangeCheck();

                var newCenter = (Vector2)Handles.FreeMoveHandle(center, pickSize, Vector3.zero, Handles.DotHandleCap);

                var rot = Quaternion.Euler(0f, 0f, rotation);
                var axisX = rot * Vector3.right;
                var axisY = rot * Vector3.up;

                var handleX = newCenter + (Vector2)(axisX * Mathf.Max(MinRadius, radii.x));
                var handleY = newCenter + (Vector2)(axisY * Mathf.Max(MinRadius, radii.y));

                var newHandleX = Handles.Slider(handleX, axisX, pickSize * 1.2f, Handles.ConeHandleCap, 0f);
                var newHandleY = Handles.Slider(handleY, axisY, pickSize * 1.2f, Handles.ConeHandleCap, 0f);

                var newRadii = radii;
                newRadii.x = Mathf.Max(MinRadius, Mathf.Abs(Vector3.Dot(newHandleX - (Vector3)newCenter, axisX)));
                newRadii.y = Mathf.Max(MinRadius, Mathf.Abs(Vector3.Dot(newHandleY - (Vector3)newCenter, axisY)));

                float newRotation = rotation;
                if (_showRotationHandle)
                {
                    var worldCenter = gizmo.transform.TransformPoint(newCenter);
                    float discSize = HandleUtility.GetHandleSize(worldCenter) * 0.4f;
                    var newRot = Handles.Disc(rot, newCenter, Vector3.forward, discSize, false, 0f);
                    newRotation = NormalizeAngle(newRot.eulerAngles.z);
                }

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(tournament, "Edit Tournament Ellipse");
                    centerProp.vector2Value = newCenter;
                    radiiProp.vector2Value = newRadii;
                    rotationProp.floatValue = newRotation;
                    serializedTournament.ApplyModifiedProperties();
                    EditorUtility.SetDirty(tournament);
                }
            }

            Handles.matrix = prevMatrix;
        }

        private static void DrawEllipseOutline(Vector2 center, Vector2 radii, float rotation, int steps, bool isSelected)
        {
            if (radii.x <= 0f || radii.y <= 0f)
            {
                return;
            }

            var points = new Vector3[steps + 1];
            var rot = Quaternion.Euler(0f, 0f, rotation);
            float step = Mathf.PI * 2f / steps;

            for (int i = 0; i <= steps; i++)
            {
                float angle = step * i;
                var p = new Vector2(Mathf.Cos(angle) * radii.x, Mathf.Sin(angle) * radii.y);
                var rotated = rot * new Vector3(p.x, p.y, 0f);
                p = new Vector2(rotated.x, rotated.y) + center;
                points[i] = new Vector3(p.x, p.y, 0f);
            }

            Handles.color = isSelected ? new Color(1f, 0.9f, 0.3f, 1f) : new Color(0.9f, 0.8f, 0.6f, 0.7f);
            Handles.DrawAAPolyLine(3f, points);
        }

        private static void DrawBattleLabel(Transform root, Vector2 center, int index, bool isSelected)
        {
            var world = root.TransformPoint(new Vector3(center.x, center.y, 0f));
            var style = new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = isSelected ? Color.yellow : Color.white }
            };
            Handles.Label(world + Vector3.up * 0.1f, $"#{index + 1}", style);
        }

        private static float NormalizeAngle(float angle)
        {
            while (angle > 180f) angle -= 360f;
            while (angle < -180f) angle += 360f;
            return angle;
        }
    }
}
