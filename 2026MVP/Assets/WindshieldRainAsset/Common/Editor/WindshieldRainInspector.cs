using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadedTechnology.WindshieldRainAsset
{
    [CustomEditor(typeof(WindshieldRain))]
    public class WindshieldRainInspector : Editor
    {
        Texture _warningIcon;
        Texture warningIcon
        {
            get
            {
                if (_warningIcon == null)
                {
                    _warningIcon = EditorGUIUtility.IconContent("console.warnicon").image;
                }
                return _warningIcon;
            }
        }

        WindshieldRain _myTarget;
        WindshieldRain myTarget {
            get {
                if (_myTarget == null) {
                    _myTarget = target as WindshieldRain;
                }
                return _myTarget;
            }
        }
        private Editor _editor;

        void OnEnable()
        {
            if (myTarget.m_WindshieldPlane == null)
            {
                myTarget.m_WindshieldPlane = new WindshieldPlane(myTarget);
            }
        }

        private void CreateNewProfile(SerializedProperty property)
        {
            RainPostProcessProfile profile = AssetsManager.CreateNewScriptableObjectOfType<RainPostProcessProfile>("Rain Post Process Profile location",
                                                                                    AssetsManager.GetScenePath(property),
                                                                                    "RainPostProcessProfile",
                                                                                    "asset");
            if (null != profile)
            {
                myTarget.m_RainPostProcessProfile = profile;
            }
        }

        WindshieldPlaneEditor _windshieldPlaneEditor = new WindshieldPlaneEditor();

        private void UsePostProcessGUI()
        {
            myTarget._postProcessProfileFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(myTarget._postProcessProfileFoldout, "Rain Post Process");
            EditorGUILayout.EndFoldoutHeaderGroup();
            if (!myTarget._postProcessProfileFoldout) {
                return;
            }
            SerializedProperty postProcessProfile = serializedObject.FindProperty(nameof(myTarget.m_RainPostProcessProfile));
            serializedObject.Update();
            if (postProcessProfile != null)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(postProcessProfile, new GUIContent("Rain Post Process Profile",
                       "Set Post Process Profile to add special effects for rain effect"));
                GUIStyle buttonStyle = new GUIStyle(EditorStyles.miniButton);
                const int buttonWidth = 35;
                buttonStyle.fixedWidth = buttonWidth;
                if (GUILayout.Button("+", buttonStyle))
                {
                    CreateNewProfile(postProcessProfile);
                }

                EditorGUILayout.EndHorizontal();

                EditorGUI.indentLevel++;
                CreateCachedEditor(postProcessProfile.objectReferenceValue, null, ref _editor);
                if (_editor != null)
                {
                    _editor.OnInspectorGUI();
                }
                EditorGUI.indentLevel--;

            }
            serializedObject.ApplyModifiedProperties();
        }

        void WindshieldPlaneGUI()
        {
            myTarget._windshieldPlaneSettingsFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(myTarget._windshieldPlaneSettingsFoldout, "Windshield Plane Settings");
            if (myTarget._windshieldPlaneSettingsFoldout)
            {
                _windshieldPlaneEditor.ShowGUI(myTarget.m_WindshieldPlane);
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        void TextureSettingsGUI()
        {
            myTarget._textureSettingsFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(myTarget._textureSettingsFoldout, "Texture Settings");
            if (myTarget._textureSettingsFoldout) {
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(myTarget.m_Resolution)));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(myTarget.m_GridDimensions)));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(myTarget.m_MaxDropletsInCell)));
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        void TurbulanceTextureSettingsGUI()
        {
            GUIContent turbulanceLabel = new GUIContent("Turbulance Texture Settings");
            if (myTarget.m_EnableTurbulance && myTarget.m_TurbulanceTexture == null)
            {
                turbulanceLabel.image = warningIcon;
                turbulanceLabel.tooltip = "Turbulance texture is required when turbulance is enabled!";
            }
            myTarget._turbulanceTextureSettingsFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(myTarget._turbulanceTextureSettingsFoldout, turbulanceLabel);
            if (myTarget._turbulanceTextureSettingsFoldout) {
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(myTarget.m_EnableTurbulance)));
                if (myTarget.m_EnableTurbulance) {
                    GUIContent turbulanceTextureLabel = new GUIContent(turbulanceLabel);
                    turbulanceTextureLabel.text = "Turbulance Texture";
                    EditorGUILayout.ObjectField(serializedObject.FindProperty(nameof(myTarget.m_TurbulanceTexture)), turbulanceTextureLabel);
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(myTarget.m_TurbulanceScale)));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(myTarget.m_TurbulanceImpact)));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(myTarget.m_TurbulanceSpeedImpactMultiplier)));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(myTarget.m_MaxTurbulanceSpeedImpact)));
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        void RainDropsSettingsGUI()
        {
            myTarget._rainDropsSettingsFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(myTarget._rainDropsSettingsFoldout, "Rain Drops Settings");
            if (myTarget._rainDropsSettingsFoldout) {
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(myTarget.m_Movement)));
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel("Min Max Droplets Radius");
                SerializedProperty minValue = serializedObject.FindProperty(nameof(myTarget.m_MinDropletRadius));
                SerializedProperty maxValue = serializedObject.FindProperty(nameof(myTarget.m_MaxDropletRadius));
                float min = minValue.floatValue;
                float max = maxValue.floatValue;
                min = EditorGUILayout.FloatField(min, GUILayout.Width(50));
                min = Mathf.Clamp(min, 0, max);
                EditorGUILayout.MinMaxSlider(ref min, ref max, 0, 1);
                max = EditorGUILayout.FloatField(max, GUILayout.Width(50));
                max = Mathf.Clamp(max, min, 1);
                if (min != minValue.floatValue || max != maxValue.floatValue)
                {
                    minValue.floatValue = min;
                    maxValue.floatValue = max;
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(myTarget.m_MaxDropVelocity)));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(myTarget.m_SpawnRate)));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(myTarget.m_SpawnAmount)));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(myTarget.m_Drag)));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(myTarget.m_SpeedMultiplier)));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(myTarget.m_StartDropsVelocity)));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(myTarget.m_RainStreakDecayRate)));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(myTarget.m_EnableDropsInfluence)));
                if (myTarget.m_EnableDropsInfluence) {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(myTarget.m_DropsInfluenceDistance)));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(myTarget.m_DropsInfluenceProportion)));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(myTarget.m_DropsInfluenceAddition)));
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.Space();
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_fixedDeltaTime"));
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        void WipersSettingsGUI()
        {
            GUIContent wipersLabel = new GUIContent("Wipers Settings");
            if (myTarget.m_WipersEnabled && myTarget.m_WipersScript == null)
            {
                wipersLabel.image = warningIcon;
                wipersLabel.tooltip = "Wipers are enabled but wipers script is not set!";
            }

            myTarget._wipersSettingsFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(myTarget._wipersSettingsFoldout, wipersLabel);
            if (myTarget._wipersSettingsFoldout)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(myTarget.m_WipersEnabled)));
                if (myTarget.m_WipersEnabled)
                {
                    GUIContent wipersScriptLabel = new GUIContent(wipersLabel);
                    wipersScriptLabel.text = "Wipers Script";
                    EditorGUILayout.ObjectField(serializedObject.FindProperty(nameof(myTarget.m_WipersScript)), wipersScriptLabel);
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(myTarget.m_WipersThreshold)));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(myTarget.m_WipersSpeedMultiplier)));
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        void RainMaterialGUI()
        {
            const string noMaterialWarning = "Select rain material!";
            GUIContent rainMaterialLabel = new GUIContent("Rain Material");
            if (myTarget.m_SetRainMaterialTexture && myTarget.m_RainMaterial == null)
            {
                rainMaterialLabel.image = warningIcon;
                rainMaterialLabel.tooltip = noMaterialWarning;
            }

            myTarget._rainMaterialFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(myTarget._rainMaterialFoldout, rainMaterialLabel);
            if (myTarget._rainMaterialFoldout) {
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(myTarget.m_SetRainMaterialTexture)));
                if (myTarget.m_SetRainMaterialTexture) {
                    EditorGUILayout.ObjectField(serializedObject.FindProperty(nameof(myTarget.m_RainMaterial)), rainMaterialLabel);
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        void ShowGUI()
        {
            if (myTarget.GetComponent<DropletsAcceleration>() == null)
            {
                EditorGUILayout.HelpBox("If you want droplets to react to the gravity and acceleration add the DropletsAcceleration component.", MessageType.Info);
                if (GUILayout.Button("Add DropletsAcceleration component"))
                {
                    myTarget.gameObject.AddComponent<DropletsAcceleration>();
                }
                EditorGUILayout.Space();
            }

            AssetsManager.CheckBlurCommandBufferOrRenderFeature();

            using (new EditorGUI.DisabledScope(true)) {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
            }
            EditorGUILayout.Space();
            WindshieldPlaneGUI();
            EditorGUILayout.Space();
            TextureSettingsGUI();
            EditorGUILayout.Space();
            TurbulanceTextureSettingsGUI();
            EditorGUILayout.Space();
            RainDropsSettingsGUI();
            EditorGUILayout.Space();
            WipersSettingsGUI();
            EditorGUILayout.Space();
            RainMaterialGUI();
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.Space();
            UsePostProcessGUI();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            Undo.RecordObject(myTarget, "Update in " + myTarget.name);

            EditorGUI.BeginChangeCheck();
            ShowGUI();
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(myTarget);
            }
            serializedObject.ApplyModifiedProperties();
        }

        void OnSceneGUI()
        {
            if (myTarget == null)
            {
                return;
            }
            _windshieldPlaneEditor.OnSceneGUI(myTarget, myTarget.m_WindshieldPlane);
        }
    }
}
