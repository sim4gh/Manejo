using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ShadedTechnology.WindshieldRainAsset
{

    [CustomEditor(typeof(WindshieldBlurFeature))]
    public class WindshieldBlurRendererFeatureEditor : Editor
    {
        WindshieldBlurFeature _target;
        WindshieldBlurFeature myTarget
        {
            get
            {
                if (_target == null)
                {
                    _target = (WindshieldBlurFeature)target;
                }
                return _target;
            }
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            myTarget.iterations = Mathf.Clamp(myTarget.iterations, 1, 7);

            if (!AssetsManager.HasBlurShaderCorrectIterationsCount(myTarget.iterations))
            {
                EditorGUILayout.HelpBox("Current blur shader has different `iteration` count set. Regenarete blur shader!", MessageType.Warning);
            }
            if (GUILayout.Button("Regenarate blur shader"))
            {
                AssetsManager.RegenerateBlurShader(myTarget.iterations);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
