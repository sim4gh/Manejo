using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ShadedTechnology.WindshieldRainAsset
{
    [CustomEditor(typeof(SimpleRainManager))]
    public class SimpleRainManagerInspector : Editor
    {
        SimpleRainManager _myTarget;
        SimpleRainManager myTarget {
            get {
                if (_myTarget == null) {
                    _myTarget = target as SimpleRainManager;
                }
                return _myTarget;
            }
        }

        void ShowGUI()
        {
            AssetsManager.CheckBlurCommandBufferOrRenderFeature();

            using (new EditorGUI.DisabledScope(true)) {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
            }
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(myTarget.m_addVelocityFactor)));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(myTarget.m_accelerationScale)));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(myTarget.m_gravityVector)));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(myTarget.m_timeMod)));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(myTarget.m_rainMaterials)));
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(myTarget.m_SetAccelerationForcibly)));
            if (myTarget.m_SetAccelerationForcibly) {
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(myTarget.m_ForcedAcceleration)));
            }
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(myTarget.m_triplanarFacesRotation)));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(myTarget.m_DebugMaterial)));
            if (myTarget.m_rainMaterials != null)
            {
                if (myTarget.m_DebugMaterial)
                {
                    foreach (var mat in myTarget.m_rainMaterials)
                    {
                        if(mat == null)
                        {
                            continue;
                        }
                        mat.SetInt("_Debug", 1);
                        mat.EnableKeyword("DEBUG");
                    }
                }
                else
                {
                    foreach (var mat in myTarget.m_rainMaterials)
                    {
                        if (mat == null)
                        {
                            continue;
                        }
                        mat.SetInt("_Debug", 0);
                        mat.DisableKeyword("DEBUG");
                    }
                }
            }
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(myTarget.m_ShowDebugGizmos)));
            if (myTarget.m_ShowDebugGizmos) {
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(myTarget.m_GizmoScale)));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(myTarget.m_GizmoDistance)));
            }

            serializedObject.ApplyModifiedProperties();
        }

        public override void OnInspectorGUI()
        {
            Undo.RecordObject(myTarget, "Update in " + myTarget.name);

            EditorGUI.BeginChangeCheck();
            ShowGUI();
            if (EditorGUI.EndChangeCheck())
            {
                if(myTarget.m_DebugMaterial || myTarget.m_ShowDebugGizmos) {
                    myTarget.UpdateTriplanarRotation();
                }
                EditorUtility.SetDirty(myTarget);
            }
            serializedObject.ApplyModifiedProperties();
        }
    }
}