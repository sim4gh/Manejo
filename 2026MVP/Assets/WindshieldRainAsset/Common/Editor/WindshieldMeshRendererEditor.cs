using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ShadedTechnology.WindshieldRainAsset
{

    [CustomEditor(typeof(WindshieldMeshRenderer))]
    public class WindshieldMeshRendererEditor : Editor
    {
        private MaterialEditor materialEditor;
        WindshieldMeshRenderer _target;
        WindshieldMeshRenderer Target
        {
            get
            {
                if (_target == null)
                {
                    _target = (WindshieldMeshRenderer)target;
                }
                return _target;
            }
        }

        public override void OnInspectorGUI()
        {
            AssetsManager.CheckWindshieldRendererFeatures();

            // Draw the default inspector fields
            if (Target.GetComponent<MeshRenderer>() != null)
            {
                EditorGUILayout.HelpBox("When using WindshieldMeshRenderer make sure to remove MeshRenderer component!", MessageType.Warning);
                if (GUILayout.Button("Remove MeshRenderer component"))
                {
                    DestroyImmediate(Target.GetComponent<MeshRenderer>());
                }
            }

            DrawDefaultInspector();

            if (Target.material != null)
            {
                if (materialEditor == null || materialEditor.target != Target.material)
                {
                    if (materialEditor != null)
                    {
                        DestroyImmediate(materialEditor);
                    }
                    materialEditor = (MaterialEditor)CreateEditor(Target.material);
                }

                materialEditor.DrawHeader();
                materialEditor.OnInspectorGUI();
            }
            else
            {
                EditorGUILayout.HelpBox("Assign renderer material.", MessageType.Info);
            }
        }

        /*public override bool HasPreviewGUI() => true;

        public override void OnPreviewGUI(Rect r, GUIStyle background)
        {
            if (Target && Target.material != null)
            {
                if (materialEditor == null || materialEditor.target != Target.material)
                    materialEditor = (MaterialEditor)CreateEditor(Target.material);

                materialEditor.OnPreviewGUI(r, background);
            }
        }*/

        private void OnDisable()
        {
            // Cleanup editor instance
            if (materialEditor != null)
            {
                DestroyImmediate(materialEditor);
                materialEditor = null;
            }
        }
    }
}
