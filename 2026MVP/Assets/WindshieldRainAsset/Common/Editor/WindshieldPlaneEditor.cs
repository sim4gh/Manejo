using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ShadedTechnology.WindshieldRainAsset
{
    //[CustomEditor(typeof(WindshieldPlane))]
    public class WindshieldPlaneEditor
    {

        public void ShowGUI(WindshieldPlane windshieldPlane)
        {
            var resolution = windshieldPlane.m_WindshieldRain.m_Resolution;
            float aspectRatio = (float)resolution.y / (float)resolution.x;
            float prevWidth = windshieldPlane.width;
            float prevHeight = windshieldPlane.height;
            windshieldPlane.width = EditorGUILayout.FloatField("Width", windshieldPlane.width);
            if (windshieldPlane.fixAspectRatio)
            {
                windshieldPlane.height = windshieldPlane.width * aspectRatio;
            }
            windshieldPlane.height = EditorGUILayout.FloatField("Height", windshieldPlane.height);
            if (windshieldPlane.fixAspectRatio)
            {
                windshieldPlane.width = windshieldPlane.height / aspectRatio;
            }
            if (prevWidth != windshieldPlane.width || prevHeight != windshieldPlane.height)
            {
                SceneView.RepaintAll();
            }
            windshieldPlane.fixAspectRatio = EditorGUILayout.Toggle(new GUIContent("Fix aspect ratio", "Fix windshield plane aspect ratio to texture aspect ratio"), windshieldPlane.fixAspectRatio);
            windshieldPlane.windshieldMesh = (MeshFilter)EditorGUILayout.ObjectField("Windshield Mesh", windshieldPlane.windshieldMesh, typeof(MeshFilter), true);

            if (windshieldPlane.windshieldMesh == null)
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                GUILayout.Label(new GUIContent("Select MeshFilter to recalculate its UVs", EditorGUIUtility.IconContent("console.infoicon").image), GUILayout.Height(20));
                EditorGUILayout.EndHorizontal();
                GUI.enabled = false;
            }
            if (GUILayout.Button("Recalculate Mesh UVs"))
            {
                Mesh mesh = windshieldPlane.RecalculateMeshUVs();
                if (mesh != null)
                {
                    string path = EditorUtility.SaveFilePanelInProject("Choose Location for Mesh to save", windshieldPlane.windshieldMesh.name, "asset", "Save your mesh");
                    if (!string.IsNullOrEmpty(path))
                    {
                        AssetDatabase.CreateAsset(mesh, path);
                        AssetDatabase.Refresh();
                        windshieldPlane.windshieldMesh.mesh = AssetDatabase.LoadAssetAtPath(path, typeof(Mesh)) as Mesh;
                    }
                }
            }
            GUI.enabled = true;
        }

        private readonly MethodInfo resizeHandlesGUI = typeof(EditorWindow).Assembly.GetType("UnityEditor.RectTool").GetMethod("ResizeHandlesGUI", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

        private readonly FieldInfo s_Moving = typeof(EditorWindow).Assembly.GetType("UnityEditor.RectTool").GetField("s_Moving", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
       
        private bool isPointerDown = false;
        private int activeRect3DIndex = -1;

        private float snapshotWidth = 0;
        private float snapshotHeight = 0;
        private Vector3 snapshotCenter = Vector3.zero;

        public void OnSceneGUI(WindshieldRain windshieldRain, WindshieldPlane windshieldPlane)
        {
            Transform t = windshieldRain.transform;

            Vector3 center = t.position;
            Vector3 scale3d = windshieldRain.transform.TransformVector(new Vector3(windshieldPlane.width, windshieldPlane.height, 0));
            Vector2 scale2d = new Vector2(Vector3.Dot(scale3d, windshieldRain.transform.right), Vector3.Dot(scale3d, windshieldRain.transform.up));
            Vector3 right = t.right * scale2d.x / 2f;
            Vector3 up = t.up * scale2d.y / 2f;

            Vector3[] corners = new Vector3[4];
            corners[0] = center - right + up;   // Top Left
            corners[1] = center + right + up;   // Top Right
            corners[2] = center + right - up;   // Bottom Right
            corners[3] = center - right - up;   // Bottom Left

            Event ev = Event.current;
            Color handlesColor = Handles.color;
            int controlID = GUIUtility.hotControl;
            bool isPaintEvent = (!isPointerDown || ev.type == EventType.Repaint);

            if (ev.type == EventType.MouseDown && ev.button == 0 && !ev.alt)
            {
                isPointerDown = true;
                activeRect3DIndex = -1;

                snapshotWidth = windshieldPlane.width;
                snapshotHeight = windshieldPlane.height;
                snapshotCenter = windshieldRain.transform.position;
            }
            else if (ev.type == EventType.MouseUp && ev.button == 0)
            {
                isPointerDown = false;
                if (activeRect3DIndex >= 0)
                {
                    s_Moving.SetValue(null, true);
                }
            }

            if (ev.type == EventType.Repaint)
            {
                // Draw outlines
                Handles.DrawAAPolyLine(
                    corners[0],
                    corners[1],
                    corners[2],
                    corners[3],
                    corners[0]
                );
            }

            // Rect resize handles
            for (int i = 0; i < corners.Length; i++)
            {
                EditorGUI.BeginChangeCheck();
                object[] parameters = new object[4]{ new Rect(scale2d * -0.5f, scale2d), isPaintEvent ? center : snapshotCenter, t.rotation, null };
                Vector3 newScale = (Vector3)resizeHandlesGUI.Invoke(null, parameters);
                if (EditorGUI.EndChangeCheck() && isPointerDown && (activeRect3DIndex < 0 || activeRect3DIndex == i))
                {
                    Undo.RecordObject(windshieldRain, "windshield plane");
                    Undo.RecordObject(windshieldRain.transform, "Windshield rain transform");
                    Quaternion rectRotation = (Quaternion)parameters[2];
                    Vector3 scalePivot = (Vector3)parameters[3];
                    windshieldPlane.width = snapshotWidth * newScale.x;
                    windshieldPlane.height = snapshotHeight * newScale.y;
                    if (windshieldRain != null && windshieldPlane.fixAspectRatio)
                    {
                        float dotProduct = Vector3.Dot(windshieldRain.transform.right, (snapshotCenter - scalePivot).normalized);
                        var resolution = windshieldRain.m_Resolution;
                        float aspectRatio = (float)resolution.y / (float)resolution.x;
                        if (dotProduct == 1 || dotProduct == -1)
                        {
                            windshieldPlane.height = windshieldPlane.width * aspectRatio;
                            newScale.y = windshieldPlane.height / snapshotHeight;
                        } else if (dotProduct == 0)
                        {
                            windshieldPlane.width = windshieldPlane.height / aspectRatio;
                            newScale.x = windshieldPlane.width / snapshotWidth;
                        } else
                        {
                            if (newScale.x > newScale.y)
                            {
                                windshieldPlane.height = windshieldPlane.width * aspectRatio;
                                newScale.y = windshieldPlane.height / snapshotHeight;
                            } else
                            {
                                windshieldPlane.width = windshieldPlane.height / aspectRatio;
                                newScale.x = windshieldPlane.width / snapshotWidth;
                            }
                        }
                    }

                    windshieldRain.transform.position = rectRotation * Vector3.Scale(Quaternion.Inverse(rectRotation) * (snapshotCenter - scalePivot), newScale) + scalePivot;
                }
                if (GUIUtility.hotControl != controlID && activeRect3DIndex < 0)
                {
                    activeRect3DIndex = i;
                }
            }


            Handles.color = handlesColor;
        }
    }
}