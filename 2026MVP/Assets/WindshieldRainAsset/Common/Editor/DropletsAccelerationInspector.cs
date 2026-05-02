using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ShadedTechnology.WindshieldRainAsset
{
    [CustomEditor(typeof(DropletsAcceleration))]
    public class DropletsAccelerationInspector : Editor
    {
        DropletsAcceleration _myTarget;
        DropletsAcceleration myTarget
        {
            get
            {
                if (_myTarget == null)
                {
                    _myTarget = target as DropletsAcceleration;
                }
                return _myTarget;
            }
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            if (myTarget.m_RainScript == null)
            {
                myTarget.m_RainScript = myTarget.GetComponent<WindshieldRain>();
            }
            serializedObject.ApplyModifiedProperties();
        }
    }

}
