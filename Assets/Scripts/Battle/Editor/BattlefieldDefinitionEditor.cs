using UnityEditor;
using UnityEngine;
using SevenBattles.Battle.Board;
using SevenBattles.Core.Battle;

namespace SevenBattles.Battle.Editor
{
    [CustomEditor(typeof(BattlefieldDefinition))]
    public sealed class BattlefieldDefinitionEditor : UnityEditor.Editor
    {
        private static WorldPerspectiveBoard _referenceBoard;
        private static bool _captureMode;
        private static int _captureIndex;
        private static int _selectedQuadIndex;

        private SerializedProperty _enchantmentQuads;

        private void OnEnable()
        {
            _enchantmentQuads = serializedObject.FindProperty("_enchantmentQuads");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawPropertiesExcluding(serializedObject, "_enchantmentQuads");
            DrawEnchantmentQuadSection();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawEnchantmentQuadSection()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Enchantment Quads", EditorStyles.boldLabel);

            _referenceBoard = (WorldPerspectiveBoard)EditorGUILayout.ObjectField(
                "Reference Board",
                _referenceBoard,
                typeof(WorldPerspectiveBoard),
                true);

            if (_enchantmentQuads == null)
            {
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add Quad"))
                {
                    int index = _enchantmentQuads.arraySize;
                    _enchantmentQuads.InsertArrayElementAtIndex(index);
                    var element = _enchantmentQuads.GetArrayElementAtIndex(index);
                    element.FindPropertyRelative("TopLeft").vector2Value = Vector2.zero;
                    element.FindPropertyRelative("TopRight").vector2Value = Vector2.zero;
                    element.FindPropertyRelative("BottomRight").vector2Value = Vector2.zero;
                    element.FindPropertyRelative("BottomLeft").vector2Value = Vector2.zero;
                    element.FindPropertyRelative("Scale").floatValue = 1f;
                    _selectedQuadIndex = index;
                }

                using (new EditorGUI.DisabledScope(_enchantmentQuads.arraySize == 0))
                {
                    if (GUILayout.Button("Remove Selected"))
                    {
                        _selectedQuadIndex = Mathf.Clamp(_selectedQuadIndex, 0, _enchantmentQuads.arraySize - 1);
                        _enchantmentQuads.DeleteArrayElementAtIndex(_selectedQuadIndex);
                        _selectedQuadIndex = Mathf.Clamp(_selectedQuadIndex, 0, _enchantmentQuads.arraySize - 1);
                    }
                }
            }

            if (_enchantmentQuads.arraySize == 0)
            {
                EditorGUILayout.HelpBox("Add quads to enable enchantment placement on this battlefield.", MessageType.Info);
                return;
            }

            _selectedQuadIndex = Mathf.Clamp(_selectedQuadIndex, 0, _enchantmentQuads.arraySize - 1);
            _selectedQuadIndex = EditorGUILayout.IntSlider("Selected Quad", _selectedQuadIndex, 0, _enchantmentQuads.arraySize - 1);

            var selected = _enchantmentQuads.GetArrayElementAtIndex(_selectedQuadIndex);
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
            }
        }

        private void OnSceneGUI()
        {
            if (_referenceBoard == null || _enchantmentQuads == null || _enchantmentQuads.arraySize == 0)
            {
                return;
            }

            serializedObject.Update();
            _selectedQuadIndex = Mathf.Clamp(_selectedQuadIndex, 0, _enchantmentQuads.arraySize - 1);
            var element = _enchantmentQuads.GetArrayElementAtIndex(_selectedQuadIndex);

            var tlProp = element.FindPropertyRelative("TopLeft");
            var trProp = element.FindPropertyRelative("TopRight");
            var brProp = element.FindPropertyRelative("BottomRight");
            var blProp = element.FindPropertyRelative("BottomLeft");

            var tr = _referenceBoard.transform;
            var tl = tr.TransformPoint(new Vector3(tlProp.vector2Value.x, tlProp.vector2Value.y, 0f));
            var trw = tr.TransformPoint(new Vector3(trProp.vector2Value.x, trProp.vector2Value.y, 0f));
            var br = tr.TransformPoint(new Vector3(brProp.vector2Value.x, brProp.vector2Value.y, 0f));
            var bl = tr.TransformPoint(new Vector3(blProp.vector2Value.x, blProp.vector2Value.y, 0f));

            Handles.color = new Color(0.2f, 1f, 0.6f, 1f);
            Handles.DrawAAPolyLine(3f, tl, trw, br, bl, tl);

            float size = HandleUtility.GetHandleSize(tr.position) * 0.06f;
            EditorGUI.BeginChangeCheck();
            var fmh_129_50_638983995865055368 = Quaternion.identity; var ntl = Handles.FreeMoveHandle(tl, size, Vector3.zero, Handles.DotHandleCap);
            var fmh_130_50_638983995865071000 = Quaternion.identity; var ntr = Handles.FreeMoveHandle(trw, size, Vector3.zero, Handles.DotHandleCap);
            var fmh_131_50_638983995865074917 = Quaternion.identity; var nbr = Handles.FreeMoveHandle(br, size, Vector3.zero, Handles.DotHandleCap);
            var fmh_132_50_638983995865078707 = Quaternion.identity; var nbl = Handles.FreeMoveHandle(bl, size, Vector3.zero, Handles.DotHandleCap);
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
    }
}
