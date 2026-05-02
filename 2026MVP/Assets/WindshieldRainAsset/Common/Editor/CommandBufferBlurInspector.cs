using UnityEditor;
using UnityEngine;

namespace ShadedTechnology.WindshieldRainAsset
{

    [CustomEditor(typeof(CommandBufferBlur))]
    public class CommandBufferBlurInspector : Editor
    {
        CommandBufferBlur _target;
        CommandBufferBlur myTarget
        {
            get
            {
                if (_target == null)
                {
                    _target = (CommandBufferBlur)target;
                }
                return _target;
            }
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            myTarget.m_Iterations = Mathf.Clamp(myTarget.m_Iterations, 1, 7);

            if (!AssetsManager.HasBlurShaderCorrectIterationsCount(myTarget.m_Iterations))
            {
                EditorGUILayout.HelpBox("Current blur shader has different `iteration` count set. Regenarete blur shader!", MessageType.Warning);
            }
            if (GUILayout.Button("Regenarate blur shader"))
            {
                AssetsManager.RegenerateBlurShader(myTarget.m_Iterations);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}