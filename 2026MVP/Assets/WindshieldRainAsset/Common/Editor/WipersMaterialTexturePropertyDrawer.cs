using UnityEditor;
using UnityEngine;

namespace ShadedTechnology.WindshieldRainAsset {

    [CustomPropertyDrawer(typeof(WipersMaterialTexture))]
    public class WipersMaterialTexturePropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            // Get child properties
            SerializedProperty textureNameProp = property.FindPropertyRelative("textureName");
            SerializedProperty materialProp = property.FindPropertyRelative("material");
            SerializedProperty useCustomProp = property.FindPropertyRelative("useCustomName");

            // Set default if empty
            if (!useCustomProp.boolValue)
            {
                textureNameProp.stringValue = "_WipersTexture";
            }

            // Calculate individual lines
            Rect useDefaultRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            Rect textureNameRect = new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight + 2, position.width, EditorGUIUtility.singleLineHeight);
            Rect materialRect = new Rect(position.x, position.y + (EditorGUIUtility.singleLineHeight + 2) * 2, position.width, EditorGUIUtility.singleLineHeight);

            // Draw each property
            EditorGUI.PropertyField(useDefaultRect, useCustomProp, new GUIContent("Custom Texture Name"));
            if (!useCustomProp.boolValue)
            {
                GUI.enabled = false;
            }
            EditorGUI.PropertyField(textureNameRect, textureNameProp, new GUIContent("Texture Name"));
            GUI.enabled = true;
            EditorGUI.PropertyField(materialRect, materialProp, new GUIContent("Material"));

            EditorGUI.EndProperty();
        }

        // Tell Unity how much vertical space to reserve
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return (EditorGUIUtility.singleLineHeight + 2) * 3;
        }
    }

}
