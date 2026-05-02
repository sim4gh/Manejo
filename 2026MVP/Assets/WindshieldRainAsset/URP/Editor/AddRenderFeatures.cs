using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ShadedTechnology.WindshieldRainAsset
{
    public class AddRenderFeatures
    {
        private static int GetDefaultRendererIndex(UniversalRenderPipelineAsset asset)
        {
            return (int)typeof(UniversalRenderPipelineAsset).GetField("m_DefaultRendererIndex", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(asset);
        }

        /// <summary>
        /// Gets the renderer from the current pipeline asset that's marked as default
        /// </summary>
        /// <returns></returns>
        public static ScriptableRendererData GetDefaultRenderer()
        {
            if (UniversalRenderPipeline.asset)
            {
                ScriptableRendererData[] rendererDataList = (ScriptableRendererData[])typeof(UniversalRenderPipelineAsset)
                        .GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance)
                        .GetValue(UniversalRenderPipeline.asset);
                int defaultRendererIndex = GetDefaultRendererIndex(UniversalRenderPipeline.asset);

                return rendererDataList[defaultRendererIndex];
            }
            else
            {
                Debug.LogError("No Universal Render Pipeline is currently active.");
                return null;
            }
        }


        public static bool HasRendererFeatures()
        {
            ScriptableRendererData scriptableRendererData = GetDefaultRenderer();
            if (scriptableRendererData == null)
            {
                return true;
            }

            bool hasWindshieldRenderer = false;
            bool hasWindshieldBlur = false;

            // Add to the renderer
            foreach(var rendererFeature in scriptableRendererData.rendererFeatures)
            {
                hasWindshieldRenderer = hasWindshieldRenderer || rendererFeature.GetType() == typeof(WindshieldMeshRendererFeature);
                hasWindshieldBlur = hasWindshieldBlur || rendererFeature.GetType() == typeof(WindshieldBlurFeature);
                if (hasWindshieldRenderer && !hasWindshieldBlur)
                {
                    return false;
                }
                if (hasWindshieldRenderer)
                {
                    break;
                }
            }
            return hasWindshieldRenderer && hasWindshieldBlur;
        }

        static void removeRenderFeature(ScriptableRendererData data, int id)
        {
            // Let's mirror what Unity does.
            var serializedObject = new SerializedObject(data);

            var renderFeaturesProp = serializedObject.FindProperty("m_RendererFeatures"); // Let's hope they don't change these.
            var renderFeaturesMapProp = serializedObject.FindProperty("m_RendererFeatureMap");

            serializedObject.Update();

            SerializedProperty property = renderFeaturesProp.GetArrayElementAtIndex(id);
            Object component = property.objectReferenceValue;
            property.objectReferenceValue = null;

            Undo.SetCurrentGroupName(component == null ? "Remove Renderer Feature" : $"Remove {component.name}");

            // remove the array index itself from the list
            renderFeaturesProp.DeleteArrayElementAtIndex(id);
            renderFeaturesMapProp.DeleteArrayElementAtIndex(id);
            serializedObject.ApplyModifiedProperties();

            // Destroy the setting object after ApplyModifiedProperties(). If we do it before, redo
            // actions will be in the wrong order and the reference to the setting object in the
            // list will be lost.
            if (component != null)
            {
                Undo.DestroyObjectImmediate(component);

                ScriptableRendererFeature feature = component as ScriptableRendererFeature;
                feature?.Dispose();
            }

            // Force save / refresh
            EditorUtility.SetDirty(data);
        }

        static void removeRenderFeatures(ScriptableRendererData data)
        {
            while(true)
            {
                int idToRemove = -1;
                for (int i = 0; i < data.rendererFeatures.Count; ++i)
                {
                    System.Type featureType = data.rendererFeatures[i].GetType();
                    if (featureType == typeof(WindshieldMeshRendererFeature) || featureType == typeof(WindshieldBlurFeature))
                    {
                        idToRemove = i;
                        break;
                    }
                }
                if (idToRemove < 0)
                {
                    return;
                }
                removeRenderFeature(data, idToRemove);
            }
        }

        static void addRenderFeature(ScriptableRendererData data, ScriptableRendererFeature feature)
        {
            // Let's mirror what Unity does.
            var serializedObject = new SerializedObject(data);

            var renderFeaturesProp = serializedObject.FindProperty("m_RendererFeatures"); // Let's hope they don't change these.
            var renderFeaturesMapProp = serializedObject.FindProperty("m_RendererFeatureMap");

            serializedObject.Update();

            // Store this new effect as a sub-asset so we can reference it safely afterwards.
            // Only when we're not dealing with an instantiated asset
            if (EditorUtility.IsPersistent(data))
                AssetDatabase.AddObjectToAsset(feature, data);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out var guid, out long localId);

            // Grow the list first, then add - that's how serialized lists work in Unity
            renderFeaturesProp.arraySize++;
            var componentProp = renderFeaturesProp.GetArrayElementAtIndex(renderFeaturesProp.arraySize - 1);
            componentProp.objectReferenceValue = feature;

            // Update GUID Map
            renderFeaturesMapProp.arraySize++;
            var guidProp = renderFeaturesMapProp.GetArrayElementAtIndex(renderFeaturesMapProp.arraySize - 1);
            guidProp.longValue = localId;

            // Force save / refresh
            if (EditorUtility.IsPersistent(data))
            {
                AssetDatabase.SaveAssetIfDirty(data);
            }

            serializedObject.ApplyModifiedProperties();
        }

        public static void AddWindshieldFeatures()
        {
            ScriptableRendererData scriptableRendererData = GetDefaultRenderer();
            if (scriptableRendererData == null)
            {
                return;
            }
            removeRenderFeatures(scriptableRendererData);
            var blurFeature = ScriptableObject.CreateInstance<WindshieldBlurFeature>();
            blurFeature.name = "WindshieldBlurFeature";
            addRenderFeature(scriptableRendererData, blurFeature);
            var meshRendererFeature = ScriptableObject.CreateInstance<WindshieldMeshRendererFeature>();
            meshRendererFeature.name = "WindshieldMeshRendererFeature";
            addRenderFeature(scriptableRendererData, meshRendererFeature);
        }

    }
}
