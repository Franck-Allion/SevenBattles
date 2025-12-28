using System.Globalization;
using UnityEditor;
using UnityEngine;
using SevenBattles.Battle.Board;

namespace SevenBattles.Battle.Editor
{
    [CustomEditor(typeof(EnchantmentQuadAuthoring))]
    public sealed class EnchantmentQuadAuthoringEditor : UnityEditor.Editor
    {
        private static bool _captureMode;
        private static int _captureIndex;
        private static int _selectedQuadIndex;

        private SerializedProperty _board;
        private SerializedProperty _drawGizmos;
        private SerializedProperty _quadColor;
        private SerializedProperty _centerColor;
        private SerializedProperty _centerGizmoRadius;
        private SerializedProperty _quads;

        private void OnEnable()
        {
            _board = serializedObject.FindProperty("_board");
            _drawGizmos = serializedObject.FindProperty("_drawGizmos");
            _quadColor = serializedObject.FindProperty("_quadColor");
            _centerColor = serializedObject.FindProperty("_centerColor");
            _centerGizmoRadius = serializedObject.FindProperty("_centerGizmoRadius");
            _quads = serializedObject.FindProperty("_quads");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_board);
            EditorGUILayout.PropertyField(_drawGizmos);
            EditorGUILayout.PropertyField(_quadColor);
            EditorGUILayout.PropertyField(_centerColor);
            EditorGUILayout.PropertyField(_centerGizmoRadius);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Enchantment Quads", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add Quad"))
                {
                    int index = _quads.arraySize;
                    _quads.InsertArrayElementAtIndex(index);
                    var element = _quads.GetArrayElementAtIndex(index);
                    element.FindPropertyRelative("TopLeft").vector2Value = Vector2.zero;
                    element.FindPropertyRelative("TopRight").vector2Value = Vector2.zero;
                    element.FindPropertyRelative("BottomRight").vector2Value = Vector2.zero;
                    element.FindPropertyRelative("BottomLeft").vector2Value = Vector2.zero;
                    element.FindPropertyRelative("Scale").floatValue = 1f;
                    _selectedQuadIndex = index;
                }

                using (new EditorGUI.DisabledScope(_quads.arraySize == 0))
                {
                    if (GUILayout.Button("Remove Selected"))
                    {
                        _selectedQuadIndex = Mathf.Clamp(_selectedQuadIndex, 0, _quads.arraySize - 1);
                        _quads.DeleteArrayElementAtIndex(_selectedQuadIndex);
                        _selectedQuadIndex = Mathf.Clamp(_selectedQuadIndex, 0, _quads.arraySize - 1);
                    }
                }
            }

            if (_quads.arraySize == 0)
            {
                EditorGUILayout.HelpBox("Add quads to capture coordinates. Use the scene view to click TL, TR, BR, BL.", MessageType.Info);
                serializedObject.ApplyModifiedProperties();
                return;
            }

            _selectedQuadIndex = Mathf.Clamp(_selectedQuadIndex, 0, _quads.arraySize - 1);
            _selectedQuadIndex = EditorGUILayout.IntSlider("Selected Quad", _selectedQuadIndex, 0, _quads.arraySize - 1);

            var selected = _quads.GetArrayElementAtIndex(_selectedQuadIndex);
            EditorGUILayout.PropertyField(selected, new GUIContent("Selected Quad"), true);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (!_captureMode)
                {
                    if (GUILayout.Button("Start Capture (TL->TR->BR->BL)"))
                    {
                        _captureMode = true;
                        _captureIndex = 0;
                        SceneView.RepaintAll();
                    }
                }
                else
                {
                    if (GUILayout.Button("Stop Capture"))
                    {
                        _captureMode = false;
                        SceneView.RepaintAll();
                    }
                    if (GUILayout.Button("Reset Order"))
                    {
                        _captureIndex = 0;
                        SceneView.RepaintAll();
                    }
                }

                if (GUILayout.Button("Copy Selected"))
                {
                    EditorGUIUtility.systemCopyBuffer = BuildQuadClipboardText(selected);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void OnSceneGUI()
        {
            var authoring = (EnchantmentQuadAuthoring)target;
            var board = authoring != null ? authoring.Board : null;
            if (board == null)
            {
                return;
            }

            if (_quads == null || _quads.arraySize == 0)
            {
                return;
            }

            serializedObject.Update();
            _selectedQuadIndex = Mathf.Clamp(_selectedQuadIndex, 0, _quads.arraySize - 1);
            var element = _quads.GetArrayElementAtIndex(_selectedQuadIndex);

            var tlProp = element.FindPropertyRelative("TopLeft");
            var trProp = element.FindPropertyRelative("TopRight");
            var brProp = element.FindPropertyRelative("BottomRight");
            var blProp = element.FindPropertyRelative("BottomLeft");

            var tr = board.transform;
            var tl = tr.TransformPoint(new Vector3(tlProp.vector2Value.x, tlProp.vector2Value.y, 0f));
            var trw = tr.TransformPoint(new Vector3(trProp.vector2Value.x, trProp.vector2Value.y, 0f));
            var br = tr.TransformPoint(new Vector3(brProp.vector2Value.x, brProp.vector2Value.y, 0f));
            var bl = tr.TransformPoint(new Vector3(blProp.vector2Value.x, blProp.vector2Value.y, 0f));

            Handles.color = new Color(0.2f, 1f, 0.6f, 1f);
            Handles.DrawAAPolyLine(3f, tl, trw, br, bl, tl);

            float size = HandleUtility.GetHandleSize(tr.position) * 0.06f;
            EditorGUI.BeginChangeCheck();
            var fmh_144_50_638983995865120000 = Quaternion.identity; var ntl = Handles.FreeMoveHandle(tl, size, Vector3.zero, Handles.DotHandleCap);
            var fmh_145_50_638983995865130000 = Quaternion.identity; var ntr = Handles.FreeMoveHandle(trw, size, Vector3.zero, Handles.DotHandleCap);
            var fmh_146_50_638983995865140000 = Quaternion.identity; var nbr = Handles.FreeMoveHandle(br, size, Vector3.zero, Handles.DotHandleCap);
            var fmh_147_50_638983995865150000 = Quaternion.identity; var nbl = Handles.FreeMoveHandle(bl, size, Vector3.zero, Handles.DotHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                tlProp.vector2Value = Local2D(tr, ntl);
                trProp.vector2Value = Local2D(tr, ntr);
                brProp.vector2Value = Local2D(tr, nbr);
                blProp.vector2Value = Local2D(tr, nbl);
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);
            }

            if (_captureMode)
            {
                if (Event.current.type == EventType.Layout)
                {
                    HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
                }

                Handles.BeginGUI();
                GUI.Box(new Rect(10, 10, 360, 48), "Capture quad points on board plane");
                GUI.Label(new Rect(20, 34, 320, 20), $"Next: {(new string[] { "Top Left", "Top Right", "Bottom Right", "Bottom Left" })[_captureIndex]}");
                Handles.EndGUI();

                var e = Event.current;
                if (e.type == EventType.MouseDown && e.button == 0)
                {
                    var ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                    var plane = new Plane(tr.forward, tr.position);
                    if (plane.Raycast(ray, out var dist))
                    {
                        var wp = ray.origin + ray.direction * dist;
                        var lp = Local2D(tr, wp);
                        switch (_captureIndex)
                        {
                            case 0: tlProp.vector2Value = lp; break;
                            case 1: trProp.vector2Value = lp; break;
                            case 2: brProp.vector2Value = lp; break;
                            case 3: blProp.vector2Value = lp; break;
                        }

                        _captureIndex = Mathf.Min(3, _captureIndex + 1);
                        if (_captureIndex == 4)
                        {
                            _captureMode = false;
                            _captureIndex = 0;
                        }

                        serializedObject.ApplyModifiedProperties();
                        EditorUtility.SetDirty(target);
                        e.Use();
                    }
                }
            }
        }

        private static Vector2 Local2D(Transform tr, Vector3 world)
        {
            var lp = tr.InverseTransformPoint(world);
            return new Vector2(lp.x, lp.y);
        }

        private static string BuildQuadClipboardText(SerializedProperty quadProperty)
        {
            if (quadProperty == null)
            {
                return string.Empty;
            }

            var tl = quadProperty.FindPropertyRelative("TopLeft").vector2Value;
            var tr = quadProperty.FindPropertyRelative("TopRight").vector2Value;
            var br = quadProperty.FindPropertyRelative("BottomRight").vector2Value;
            var bl = quadProperty.FindPropertyRelative("BottomLeft").vector2Value;
            var offset = quadProperty.FindPropertyRelative("Offset").vector2Value;
            var scale = quadProperty.FindPropertyRelative("Scale").floatValue;

            return string.Format(
                CultureInfo.InvariantCulture,
                "TL=({0:F3}, {1:F3}) TR=({2:F3}, {3:F3}) BR=({4:F3}, {5:F3}) BL=({6:F3}, {7:F3}) Offset=({8:F3}, {9:F3}) Scale={10:F3}",
                tl.x, tl.y, tr.x, tr.y, br.x, br.y, bl.x, bl.y, offset.x, offset.y, scale);
        }
    }
}
