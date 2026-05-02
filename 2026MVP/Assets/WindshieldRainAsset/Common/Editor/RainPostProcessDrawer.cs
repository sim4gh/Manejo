using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ShadedTechnology.WindshieldRainAsset
{
    [CustomPropertyDrawer(typeof(RainPostProcess))]
    public class RainPostProcessDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            // Create property container element.
            var container = new VisualElement();

            // Create property fields.
            var materialField = new PropertyField(property.FindPropertyRelative("material"));
            var renderTextureField = new PropertyField(property.FindPropertyRelative("renderTexture"));
            var texturesToSetField = new PropertyField(property.FindPropertyRelative("texturesToSet"));

            // Add fields to the container.
            container.Add(materialField);
            container.Add(renderTextureField);
            container.Add(texturesToSetField);

            return container;
        }
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float totalHeight = EditorGUIUtility.singleLineHeight;

            string[] elements = { "material", "texturesToSet" };

            if (property.isExpanded)
            {
                foreach (string element in elements)
                {
                    SerializedProperty prop = property.FindPropertyRelative(element);
                    float height = EditorGUI.GetPropertyHeight(prop, null, true) + EditorGUIUtility.standardVerticalSpacing;
                    totalHeight += height;
                }
                totalHeight += EditorGUIUtility.standardVerticalSpacing;
            }
            return totalHeight;
        }

        public void DrawAllChildrensGUI(Rect position, SerializedProperty property)
        {
            EditorGUI.indentLevel++;
            string[] elements = { "material", "texturesToSet" };
            //MaterialGUI(position, property);

            float y = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            foreach (string element in elements)
            {
                SerializedProperty prop = property.FindPropertyRelative(element);
                float height = EditorGUI.GetPropertyHeight(prop, new GUIContent(prop.displayName), true);
                EditorGUI.PropertyField(new Rect(position.x, y, position.width, height), prop, true);
                y += height + EditorGUIUtility.standardVerticalSpacing;
            }

            EditorGUI.indentLevel--;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            property.isExpanded = EditorGUI.Foldout(new Rect(position.x, position.y, EditorGUIUtility.labelWidth, EditorGUIUtility.singleLineHeight),
                                                    property.isExpanded,
                                                    label,
                                                    true);


            if (GUI.changed) property.serializedObject.ApplyModifiedProperties();

            if (property.isExpanded)
            {
                DrawAllChildrensGUI(position, property);
            }
            property.serializedObject.ApplyModifiedProperties();
            EditorGUI.EndProperty();
        }
    }
}