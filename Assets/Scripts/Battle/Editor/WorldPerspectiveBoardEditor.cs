using UnityEditor;
using UnityEngine;
using SevenBattles.Battle.Board;
using SevenBattles.Core.Math;

namespace SevenBattles.Battle.Editor
{
    [CustomEditor(typeof(WorldPerspectiveBoard))]
    public class WorldPerspectiveBoardEditor : UnityEditor.Editor
    {
        private static bool _captureMode;
        private static int _captureIndex; // TL,TR,BR,BL

        private SerializedProperty _columns;
        private SerializedProperty _rows;
        private SerializedProperty _tl;
        private SerializedProperty _tr;
        private SerializedProperty _br;
        private SerializedProperty _bl;
        private SerializedProperty _highlightMat;
        private SerializedProperty _hlLayer;
        private SerializedProperty _hlOrder;

        private void OnEnable()
        {
            _columns = serializedObject.FindProperty("_columns");
            _rows = serializedObject.FindProperty("_rows");
            _tl = serializedObject.FindProperty("_topLeft");
            _tr = serializedObject.FindProperty("_topRight");
            _br = serializedObject.FindProperty("_bottomRight");
            _bl = serializedObject.FindProperty("_bottomLeft");
            _highlightMat = serializedObject.FindProperty("_highlightMaterial");
            _hlLayer = serializedObject.FindProperty("_highlightSortingLayer");
            _hlOrder = serializedObject.FindProperty("_highlightSortingOrder");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(_columns);
            EditorGUILayout.PropertyField(_rows);
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Inner Quad (local)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_tl, new GUIContent("Top Left"));
            EditorGUILayout.PropertyField(_tr, new GUIContent("Top Right"));
            EditorGUILayout.PropertyField(_br, new GUIContent("Bottom Right"));
            EditorGUILayout.PropertyField(_bl, new GUIContent("Bottom Left"));

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(_highlightMat);
            using (new EditorGUI.DisabledScope(_highlightMat.objectReferenceValue == null))
            {
                EditorGUILayout.PropertyField(_hlLayer);
                EditorGUILayout.PropertyField(_hlOrder);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (!_captureMode)
                {
                    if (GUILayout.Button("Start Capture (TL->TR->BR->BL)"))
                    {
                        _captureMode = true; _captureIndex = 0; SceneView.RepaintAll();
                    }
                }
                else
                {
                    if (GUILayout.Button("Stop Capture")) { _captureMode = false; SceneView.RepaintAll(); }
                    if (GUILayout.Button("Reset Order")) { _captureIndex = 0; SceneView.RepaintAll(); }
                }
            }

            if (GUILayout.Button("Rebuild Grid"))
            {
                foreach (var t in targets)
                {
                    ((WorldPerspectiveBoard)t).RebuildGrid();
                    EditorUtility.SetDirty(t);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void OnSceneGUI()
        {
            var board = (WorldPerspectiveBoard)target;
            var tr = board.transform;

            // Consume input while capturing to avoid scene tools eating clicks
            if (_captureMode && Event.current.type == EventType.Layout)
            {
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            }

            // Draw handles for quad in local XY plane
            serializedObject.Update();
            var TL = tr.TransformPoint(new Vector3(_tl.vector2Value.x, _tl.vector2Value.y, 0));
            var TR = tr.TransformPoint(new Vector3(_tr.vector2Value.x, _tr.vector2Value.y, 0));
            var BR = tr.TransformPoint(new Vector3(_br.vector2Value.x, _br.vector2Value.y, 0));
            var BL = tr.TransformPoint(new Vector3(_bl.vector2Value.x, _bl.vector2Value.y, 0));

            Handles.color = new Color(1f, 0.8f, 0.2f, 1f);
            Handles.DrawAAPolyLine(3f, TL, TR, BR, BL, TL);

            float size = HandleUtility.GetHandleSize(tr.position) * 0.06f;
            EditorGUI.BeginChangeCheck();
            var fmh_109_50_638984101151322667 = Quaternion.identity; var nTL = Handles.FreeMoveHandle(TL, size, Vector3.zero, Handles.DotHandleCap);
            var fmh_110_50_638984101151334976 = Quaternion.identity; var nTR = Handles.FreeMoveHandle(TR, size, Vector3.zero, Handles.DotHandleCap);
            var fmh_111_50_638984101151338771 = Quaternion.identity; var nBR = Handles.FreeMoveHandle(BR, size, Vector3.zero, Handles.DotHandleCap);
            var fmh_112_50_638984101151342192 = Quaternion.identity; var nBL = Handles.FreeMoveHandle(BL, size, Vector3.zero, Handles.DotHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                _tl.vector2Value = Local2D(tr, nTL);
                _tr.vector2Value = Local2D(tr, nTR);
                _br.vector2Value = Local2D(tr, nBR);
                _bl.vector2Value = Local2D(tr, nBL);
                serializedObject.ApplyModifiedProperties();
                board.RebuildGrid();
                EditorUtility.SetDirty(target);
            }

            // Grid preview
            var cols = Mathf.Max(1, _columns.intValue);
            var rows = Mathf.Max(1, _rows.intValue);
            var grid = PerspectiveGrid.FromQuad(_tl.vector2Value, _tr.vector2Value, _br.vector2Value, _bl.vector2Value, cols, rows);
            Handles.color = new Color(0.3f, 0.8f, 1f, 0.8f);
            for (int y = 0; y < rows; y++)
            for (int x = 0; x < cols; x++)
            {
                var lp = grid.TileCenterLocal(x, y);
                var wp = tr.TransformPoint(new Vector3(lp.x, lp.y, 0));
                Handles.DrawSolidDisc(wp, SceneView.currentDrawingSceneView != null ? SceneView.currentDrawingSceneView.camera.transform.forward : Vector3.forward, size * 0.5f);
            }

            // Click-to-capture
            if (_captureMode)
            {
                Handles.BeginGUI();
                GUI.Box(new Rect(10, 10, 360, 48), "Capture TL->TR->BR->BL on board plane");
                GUI.Label(new Rect(20, 34, 320, 20), $"Next: {(new string[]{"Top Left","Top Right","Bottom Right","Bottom Left"})[_captureIndex]}");
                Handles.EndGUI();

                var e = Event.current;
                if (e.type == EventType.MouseDown && e.button == 0)
                {
                    var cam = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.camera : null;
                    var ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                    var plane = new Plane(tr.forward, tr.position);
                    if (plane.Raycast(ray, out var dist))
                    {
                        var wp = ray.origin + ray.direction * dist;
                        var lp2 = Local2D(tr, wp);
                        switch (_captureIndex)
                        {
                            case 0: _tl.vector2Value = lp2; break;
                            case 1: _tr.vector2Value = lp2; break;
                            case 2: _br.vector2Value = lp2; break;
                            case 3: _bl.vector2Value = lp2; break;
                        }
                        _captureIndex = Mathf.Min(3, _captureIndex + 1);
                        if (_captureIndex == 4) { _captureMode = false; _captureIndex = 0; }
                        serializedObject.ApplyModifiedProperties();
                        board.RebuildGrid();
                        EditorUtility.SetDirty(target);
                        e.Use();
                    }
                }
            }
        }

        private static Vector2 Local2D(Transform tr, Vector3 wp)
        {
            var lp = tr.InverseTransformPoint(wp);
            return new Vector2(lp.x, lp.y);
        }
    }
}

