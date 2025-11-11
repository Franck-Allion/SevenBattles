using UnityEditor;
using UnityEngine;
using SevenBattles.Battle.Board;
using SevenBattles.Core.Math;

namespace SevenBattles.Battle.Editor
{
    [CustomEditor(typeof(UiPerspectiveBoard))]
    public class UiPerspectiveBoardEditor : UnityEditor.Editor
    {
        private static bool _captureMode;
        private static int _captureIndex; // 0..3 for TL,TR,BR,BL

        private SerializedProperty _boardRect;
        private SerializedProperty _highlight;
        private SerializedProperty _heroParent;
        private SerializedProperty _columns;
        private SerializedProperty _rows;
        private SerializedProperty _tl;
        private SerializedProperty _tr;
        private SerializedProperty _br;
        private SerializedProperty _bl;

        private UiPerspectiveBoard _board;

        private void OnEnable()
        {
            _board = (UiPerspectiveBoard)target;
            _boardRect  = serializedObject.FindProperty("_boardRect");
            _highlight  = serializedObject.FindProperty("_highlight");
            _heroParent = serializedObject.FindProperty("_heroParent");
            _columns    = serializedObject.FindProperty("_columns");
            _rows       = serializedObject.FindProperty("_rows");
            _tl = serializedObject.FindProperty("_topLeft");
            _tr = serializedObject.FindProperty("_topRight");
            _br = serializedObject.FindProperty("_bottomRight");
            _bl = serializedObject.FindProperty("_bottomLeft");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_boardRect);
            EditorGUILayout.PropertyField(_highlight);
            EditorGUILayout.PropertyField(_heroParent);
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(_columns);
            EditorGUILayout.PropertyField(_rows);
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Inner Quad (local)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_tl, new GUIContent("Top Left"));
            EditorGUILayout.PropertyField(_tr, new GUIContent("Top Right"));
            EditorGUILayout.PropertyField(_br, new GUIContent("Bottom Right"));
            EditorGUILayout.PropertyField(_bl, new GUIContent("Bottom Left"));

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (!_captureMode)
                {
                    if (GUILayout.Button("Start Capture (TL->TR->BR->BL)"))
                    {
                        _captureMode = true; _captureIndex = 0;
                        SceneView.RepaintAll();
                    }
                }
                else
                {
                    if (GUILayout.Button("Stop Capture"))
                    {
                        _captureMode = false; SceneView.RepaintAll();
                    }
                    if (GUILayout.Button("Reset Order"))
                    {
                        _captureIndex = 0; SceneView.RepaintAll();
                    }
                }
            }

            if (GUILayout.Button("Fit To Rect Bounds"))
            {
                var rt = _boardRect.objectReferenceValue as RectTransform;
                if (rt != null)
                {
                    var r = rt.rect;
                    _tl.vector2Value = new Vector2(r.xMin, r.yMax);
                    _tr.vector2Value = new Vector2(r.xMax, r.yMax);
                    _br.vector2Value = new Vector2(r.xMax, r.yMin);
                    _bl.vector2Value = new Vector2(r.xMin, r.yMin);
                }
            }

            if (GUILayout.Button("Rebuild Grid"))
            {
                foreach (var t in targets)
                {
                    ((UiPerspectiveBoard)t).RebuildGrid();
                    EditorUtility.SetDirty(t);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void OnSceneGUI()
        {
            // Draw quad and grid preview, allow drag of corners
            var rt = _boardRect.objectReferenceValue as RectTransform;
            if (rt == null) return;

            // Prevent SceneView pan tool from eating clicks while capturing
            if (_captureMode && Event.current.type == EventType.Layout)
            {
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            }

            serializedObject.Update();

            var tl = ToWorld(rt, _tl.vector2Value);
            var tr = ToWorld(rt, _tr.vector2Value);
            var br = ToWorld(rt, _br.vector2Value);
            var bl = ToWorld(rt, _bl.vector2Value);

            Handles.color = new Color(1f, 0.8f, 0.2f, 1f);
            Handles.DrawAAPolyLine(3f, tl, tr, br, bl, tl);

            float size = HandleUtility.GetHandleSize(rt.position) * 0.06f;
            EditorGUI.BeginChangeCheck();
            var fmh_125_50_638983995865014614 = Quaternion.identity; var ntl = Handles.FreeMoveHandle(tl, size, Vector3.zero, Handles.DotHandleCap);
            var fmh_126_50_638983995865031078 = Quaternion.identity; var ntr = Handles.FreeMoveHandle(tr, size, Vector3.zero, Handles.DotHandleCap);
            var fmh_127_50_638983995865036214 = Quaternion.identity; var nbr = Handles.FreeMoveHandle(br, size, Vector3.zero, Handles.DotHandleCap);
            var fmh_128_50_638983995865040721 = Quaternion.identity; var nbl = Handles.FreeMoveHandle(bl, size, Vector3.zero, Handles.DotHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                _tl.vector2Value = ToLocal(rt, ntl);
                _tr.vector2Value = ToLocal(rt, ntr);
                _br.vector2Value = ToLocal(rt, nbr);
                _bl.vector2Value = ToLocal(rt, nbl);
                serializedObject.ApplyModifiedProperties();
                _board.RebuildGrid();
                EditorUtility.SetDirty(target);
            }

            // Grid preview (tile centers)
            var cols = Mathf.Max(1, _columns.intValue);
            var rows = Mathf.Max(1, _rows.intValue);
            var grid = PerspectiveGrid.FromQuad(_tl.vector2Value, _tr.vector2Value, _br.vector2Value, _bl.vector2Value, cols, rows);
            Handles.color = new Color(0.3f, 0.8f, 1f, 0.8f);
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    var lp = grid.TileCenterLocal(x, y);
                    var wp = ToWorld(rt, lp);
                    Handles.DrawSolidDisc(wp, SceneView.currentDrawingSceneView != null ? SceneView.currentDrawingSceneView.camera.transform.forward : Vector3.forward, size * 0.5f);
                }
            }

            // Capture mode: click four times to set TL,TR,BR,BL in order
            if (_captureMode)
            {
                Handles.BeginGUI();
                var rect = new Rect(10, 10, 340, 48);
                GUI.Box(rect, "Capture: click on board plane — order TL → TR → BR → BL");
                GUI.Label(new Rect(20, 34, 320, 20), $"Next: {(new string[]{"Top Left","Top Right","Bottom Right","Bottom Left"})[_captureIndex]}");
                Handles.EndGUI();

                var e = Event.current;
                if (e.type == EventType.MouseDown && e.button == 0)
                {
                    var cam = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.camera : null;
                    var ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                    var plane = new Plane(rt.forward, rt.position);
                    if (plane.Raycast(ray, out var dist))
                    {
                        var wp = ray.origin + ray.direction * dist;
                        var lp = ToLocal(rt, wp);
                        switch (_captureIndex)
                        {
                            case 0: _tl.vector2Value = lp; break;
                            case 1: _tr.vector2Value = lp; break;
                            case 2: _br.vector2Value = lp; break;
                            case 3: _bl.vector2Value = lp; break;
                        }
                        _captureIndex = Mathf.Min(3, _captureIndex + 1);
                        if (_captureIndex == 4) { _captureMode = false; _captureIndex = 0; }
                        serializedObject.ApplyModifiedProperties();
                        _board.RebuildGrid();
                        EditorUtility.SetDirty(target);
                        e.Use();
                    }
                }
            }
        }

        private static Vector3 ToWorld(RectTransform rt, Vector2 local)
        {
            return rt.TransformPoint(new Vector3(local.x, local.y, 0f));
        }
        private static Vector2 ToLocal(RectTransform rt, Vector3 world)
        {
            var lp = rt.InverseTransformPoint(world);
            return new Vector2(lp.x, lp.y);
        }
    }
}
