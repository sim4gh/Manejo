#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadedTechnology.WindshieldRainAsset
{
    /// <summary>
    /// Helper class for managing grass asset files
    /// </summary>
    public class AssetsManager : Editor
    {

        static MethodInfo setIconEnabled;
        static MethodInfo SetIconEnabled => setIconEnabled = setIconEnabled ??
            Assembly.GetAssembly(typeof(Editor))
            ?.GetType("UnityEditor.AnnotationUtility")
            ?.GetMethod("SetIconEnabled", BindingFlags.Static | BindingFlags.NonPublic);


        static MethodInfo getAnnotations;
        static MethodInfo GetAnnotations => getAnnotations = getAnnotations ??
            Assembly.GetAssembly(typeof(Editor))
            ?.GetType("UnityEditor.AnnotationUtility")
            ?.GetMethod("GetAnnotations", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);

        public static void SetGizmoIconEnabled(Type type, bool on)
        {
            var annotation = Type.GetType("UnityEditor.Annotation, UnityEditor");
            if (SetIconEnabled == null || GetAnnotations == null || annotation == null) return;
            var annotations = (Array)getAnnotations.Invoke(null, null);
            var classId = annotation.GetField("classID");
            var scriptClass = annotation.GetField("scriptClass");
            foreach (var a in annotations)
            {
                var scriptClassValue = (string)scriptClass.GetValue(a);
                var classIdValue = (int)classId.GetValue(a);
                if (scriptClassValue == type.Name)
                {
                    SetIconEnabled.Invoke(null, new object[] { classIdValue, scriptClassValue, on ? 1 : 0 });
                }
            }
        }

        public static bool HasBlurShaderCorrectIterationsCount(int iterations)
        {
            string sampleBlurPath = AssetsManager.GetWindshieldRainAssetPath() + "/Common/Shaders/SampleBlur.hlsl";
            string expectedFirstLine = $"// BLUR_ITER {iterations}";
            return !(!File.Exists(sampleBlurPath) || File.ReadLines(sampleBlurPath).FirstOrDefault() != expectedFirstLine);
        }

        public static void RegenerateBlurShader(int iterations)
        {
            string sampleBlurPath = GetWindshieldRainAssetPath() + "/Common/Shaders/SampleBlur.hlsl";
            List<string> lines = new();
            lines.Add($"// BLUR_ITER {iterations}");
            lines.Add("// This file is generated don't modify it");
            lines.Add("#ifndef _SHADED_TECH_WINDSHIELD_RAIN_ASSET__SAMPLE_BLUR_INCLUDE__");
            lines.Add("#define _SHADED_TECH_WINDSHIELD_RAIN_ASSET__SAMPLE_BLUR_INCLUDE__");
            lines.Add($"#define BLUR_ITER {iterations}.0f");
            lines.Add("");
            lines.Add("#if defined(IS_HDRP)");
            lines.Add("#define BLUR_UV (uv * _RTHandleScale.xy)");
            lines.Add("#else");
            lines.Add("#define BLUR_UV uv");
            lines.Add("#endif");
            lines.Add("");
            lines.Add("#if !defined(IS_HDRP) && !defined(IS_URP)");
            lines.Add("");

            lines.Add("#define PRESAMPLE_BLUR_TEXTURES(uv) \\");
            lines.Add("float step = (1.0f / BLUR_ITER); \\");
            lines.Add("float offset = 0; \\");
            lines.Add("float4 tempGrab = tex2D(_WindshieldGrabTexture, uv); \\");
            for (int i = 0; i < iterations; ++i)
            {
                string line_ending = (i == (iterations - 1)) ? "" : " \\";
                lines.Add($"float4 tempBlurColor{i} = tex2D(_GrabBlurTexture_{i}, uv);{line_ending}");
            }
            lines.Add("");
            lines.Add("sampler2D _WindshieldGrabTexture;");
            for (int i = 0; i < iterations; ++i)
            {
                lines.Add($"sampler2D _GrabBlurTexture_{i};");
            }
            lines.Add("");
            lines.Add("#else");
            lines.Add("");
            lines.Add("#define PRESAMPLE_BLUR_TEXTURES(uv) \\");
            lines.Add("float step = (1.0f / BLUR_ITER); \\");
            lines.Add("float offset = 0; \\");
            lines.Add("float4 tempGrab = SAMPLE_TEXTURE2D_X(_WindshieldGrabTexture, sampler_WindshieldGrabTexture, BLUR_UV); \\");
            for (int i = 0; i < iterations; ++i)
            {
                string line_ending = (i == (iterations - 1)) ? "" : " \\";
                lines.Add($"float4 tempBlurColor{i} = SAMPLE_TEXTURE2D_X(_GrabBlurTexture_{i}, sampler_GrabBlurTexture_{i}, BLUR_UV);{line_ending}");
            }
            lines.Add("");
            lines.Add("TEXTURE2D_X(_WindshieldGrabTexture);");
            lines.Add("SAMPLER(sampler_WindshieldGrabTexture);");
            for (int i = 0; i < iterations; ++i)
            {
                lines.Add($"TEXTURE2D_X(_GrabBlurTexture_{i});");
                lines.Add($"SAMPLER(sampler_GrabBlurTexture_{i});");
            }
            lines.Add("");
            lines.Add("#endif");
            lines.Add("");
            lines.Add("#define SET_PRESAMPLE_GRAB_COLOR(grab_color) grab_color = tempGrab;");
            lines.Add("");
            lines.Add("#define GET_BLUR_COLOR_FROM_PRESAMPLE(out_color, strength) \\");
            lines.Add("offset = 0; \\");


            for (int i = 0; i < iterations; ++i)
            {
                if (i != (iterations - 1))
                {
                    lines.Add("if (strength <= (offset + step)) { \\");
                }
                if (i == 0)
                {
                    string line_ending = (iterations <= 1) ? "" : " \\";
                    lines.Add($"out_color = lerp(tempGrab, tempBlurColor{i}, (strength - offset) / step);{line_ending}");
                }
                else
                {
                    lines.Add($"out_color = lerp(tempBlurColor{i - 1}, tempBlurColor{i}, (strength - offset) / step); \\");
                }
                if (i != (iterations - 1))
                {
                    lines.Add("} else { \\");
                    lines.Add(" offset += step; \\");
                }
                else
                {
                    lines.Add(string.Concat(Enumerable.Repeat("} ", (iterations - 1))));
                }
            }
            lines.Add("");
            lines.Add("#endif");

            File.WriteAllLines(sampleBlurPath, lines);
            AssetDatabase.Refresh();
        }

        public enum Pipeline
        {
            BuiltIn,
            URP,
            HDRP
        };

        static bool AreURPRenderFeaturesOK()
        {
            var editorAssembly = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var asm in editorAssembly)
            {
                if (asm.GetName().Name == "WindshieldRainURP.Editor")
                {
                    // Get type
                    var type = asm.GetType("ShadedTechnology.WindshieldRainAsset.AddRenderFeatures");
                    if (type != null)
                    {
                        // Get static method
                        var method = type.GetMethod("HasRendererFeatures", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                        if (method != null)
                        {
                            object result = method.Invoke(null, null); // call without params
                            return (bool)result;
                        }
                    }
                }
            }
            return true;
        }

        public static void CheckWindshieldRendererFeatures()
        {
            if (!AreURPRenderFeaturesOK())
            {
                EditorGUILayout.HelpBox("For the blur texture and Windshield Renderers to work you need to add WindshieldBlur and WindshieldMeshRenderer features to render pipeline data.", MessageType.Warning);
                if (GUILayout.Button("Add Render Features"))
                {
                    var editorAssembly = AppDomain.CurrentDomain.GetAssemblies();
                    foreach (var asm in editorAssembly)
                    {
                        if (asm.GetName().Name == "WindshieldRainURP.Editor")
                        {
                            // Get type
                            var type = asm.GetType("ShadedTechnology.WindshieldRainAsset.AddRenderFeatures");
                            if (type != null)
                            {
                                // Get static method
                                var method = type.GetMethod("AddWindshieldFeatures", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                                if (method != null)
                                {
                                    method.Invoke(null, null);
                                }
                            }
                        }
                    }
                }
            }
        }

        static bool HasHdrpCustomPassVolume()
        {
            var editorAssembly = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var asm in editorAssembly)
            {
                if (asm.GetName().Name == "WindshieldRainHDRP.Editor")
                {
                    // Get type
                    var type = asm.GetType("ShadedTechnology.WindshieldRainAsset.AddWindshieldVolume");
                    if (type != null)
                    {
                        // Get static method
                        var method = type.GetMethod("HasWindshieldVolume", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                        if (method != null)
                        {
                            object result = method.Invoke(null, null); // call without params
                            return (bool)result;
                        }
                    }
                }
            }
            return true;
        }

        public static void CheckWindshieldHdrpPassVolume()
        {
            if (!HasHdrpCustomPassVolume())
            {
                EditorGUILayout.HelpBox("For the blur texture and Windshield Renderers to work you need to add WindshieldBlur and WindshieldMeshRenderer custom passes to CustomPassVolume.", MessageType.Warning);
                if (GUILayout.Button("Add Passes"))
                {
                    var editorAssembly = AppDomain.CurrentDomain.GetAssemblies();
                    foreach (var asm in editorAssembly)
                    {
                        if (asm.GetName().Name == "WindshieldRainHDRP.Editor")
                        {
                            // Get type
                            var type = asm.GetType("ShadedTechnology.WindshieldRainAsset.AddWindshieldVolume");
                            if (type != null)
                            {
                                // Get static method
                                var method = type.GetMethod("AddWindshieldVolumeObject", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                                if (method != null)
                                {
                                    method.Invoke(null, null);
                                }
                            }
                        }
                    }
                }
            }
        }

        public static void CheckBlurCommandBufferOrRenderFeature()
        {
            Pipeline pipeline = CheckPipeline();
            if (pipeline == Pipeline.BuiltIn)
            {
                AddAlwaysIncludedShader("Hidden/SeparableGlassBlur");
                if (Camera.main.GetComponent<CommandBufferBlur>() == null)
                {

                    EditorGUILayout.HelpBox("For the blur texture to work you need to add CommandBufferBlur component to the main camera game object.", MessageType.Warning);
                    if (GUILayout.Button("Add CommandBufferBlur to main camera"))
                    {
                        Camera.main.gameObject.AddComponent<CommandBufferBlur>();
                    }
                    EditorGUILayout.Space();
                }
            } 
            else if (pipeline == Pipeline.URP)
            {
#if UNITY_6000_0_OR_NEWER
                AddAlwaysIncludedShader("Hidden/SeparableGlassBlurURP");
#else
                AssetsManager.AddAlwaysIncludedShader("Hidden/SeparableGlassBlurURP_Old");
#endif
                CheckWindshieldRendererFeatures();
                if (WindshieldMeshRenderer.ActiveRenderers.Count == 0)
                {
                    EditorGUILayout.HelpBox("Make sure to use WindshieldMeshRenderer components instead of MeshRenderer for your windshield glass meshes!", MessageType.Warning);
                }
            } 
            else if (pipeline == Pipeline.HDRP)
            {
                AddAlwaysIncludedShader("Hidden/SeparableGlassBlurHDRP");
                CheckWindshieldHdrpPassVolume();
                if (WindshieldMeshRenderer.ActiveRenderers.Count == 0)
                {
                    EditorGUILayout.HelpBox("Make sure to use WindshieldMeshRenderer components instead of MeshRenderer for your windshield glass meshes!", MessageType.Warning);
                }
            }
        }

        public static Pipeline CheckPipeline()
        {
            var pipelineAsset = GraphicsSettings.currentRenderPipeline;

            if (pipelineAsset == null)
            {
                return Pipeline.BuiltIn;
            }
            else if (pipelineAsset.GetType().ToString().Contains("UniversalRenderPipelineAsset"))
            {
                return Pipeline.URP;
            }
            else if (pipelineAsset.GetType().ToString().Contains("HDRenderPipelineAsset"))
            {
                return Pipeline.HDRP;
            }
            else
            {
                return Pipeline.BuiltIn;
            }
        }

        /// <summary>
        /// Returns path to root asset folder
        /// </summary>
        /// <returns>Path to root asset folder</returns>
        public static string GetWindshieldRainAssetPath()
        {
            AssetsManager script = ScriptableObject.CreateInstance<AssetsManager>();
            MonoScript ms = MonoScript.FromScriptableObject(script);
            string filePath = AssetDatabase.GetAssetPath(ms);
            DestroyImmediate(script);
            DirectoryInfo dir = Directory.GetParent(filePath).Parent.Parent.Parent;
            string folderPath = AbsolutePathToRelative(dir.ToString());
            return folderPath;
        }

        public static void AddAlwaysIncludedShader(string shaderName)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null)
                return;

            var graphicsSettingsObj = AssetDatabase.LoadAssetAtPath<GraphicsSettings>("ProjectSettings/GraphicsSettings.asset");
            var serializedObject = new SerializedObject(graphicsSettingsObj);
            var arrayProp = serializedObject.FindProperty("m_AlwaysIncludedShaders");
            bool hasShader = false;
            for (int i = 0; i < arrayProp.arraySize; ++i)
            {
                var arrayElem = arrayProp.GetArrayElementAtIndex(i);
                if (shader == arrayElem.objectReferenceValue)
                {
                    hasShader = true;
                    break;
                }
            }

            if (!hasShader)
            {
                int arrayIndex = arrayProp.arraySize;
                arrayProp.InsertArrayElementAtIndex(arrayIndex);
                var arrayElem = arrayProp.GetArrayElementAtIndex(arrayIndex);
                arrayElem.objectReferenceValue = shader;

                serializedObject.ApplyModifiedProperties();

                AssetDatabase.SaveAssets();
            }
        }

        /// <summary>
        /// Creates path relative to the project folder from absolute path
        /// </summary>
        /// <param name="absolutePath">Absolute path</param>
        /// <returns>Path relative to the project folder</returns>
        public static string AbsolutePathToRelative(string absolutePath)
        {
            return absolutePath.Replace("\\", "/").Replace(Application.dataPath, "Assets");
        }

        /// <summary>
        /// Pop ups <see cref="EditorUtility.SaveFilePanel"/> opened in given directory, 
        /// then if chosen path is correct (selection wasn't canceled)
        /// <see cref="ScriptableObject"/> is created and saved in chosen path
        /// and then returned
        /// </summary>
        /// <param name="defaultPath">Path where <see cref="EditorUtility.SaveFilePanel"/> opens</param>
        /// <returns>Created <see cref="ScriptableObject"/> asset</returns>
        public static T CreateNewScriptableObjectOfType<T>(string title, string directory, string defaultName, string extension) where T : ScriptableObject
        {
            string path = EditorUtility.SaveFilePanel(title, directory, defaultName, extension);
            path = AssetsManager.AbsolutePathToRelative(path);
            if (!string.IsNullOrEmpty(path))
            {
                T profile = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(profile, path);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                return profile;
            }
            return null;
        }

        /// <summary>
        /// Pop ups <see cref="EditorUtility.SaveFilePanel"/> opened in given directory, 
        /// then if chosen path is correct (selection wasn't canceled)
        /// <see cref="ScriptableObject"/> is created and saved in chosen path
        /// and then returned
        /// </summary>
        /// <param name="defaultPath">Path where <see cref="EditorUtility.SaveFilePanel"/> opens</param>
        /// <returns>Created <see cref="ScriptableObject"/> asset</returns>
        public static ScriptableObject CreateNewScriptableObjectOfType(Type type, string title, string directory, string defaultName, string extension)
        {
            string path = EditorUtility.SaveFilePanel(title, directory, defaultName, extension);
            path = AssetsManager.AbsolutePathToRelative(path);
            if (!string.IsNullOrEmpty(path))
            {
                ScriptableObject profile = ScriptableObject.CreateInstance(type);
                AssetDatabase.CreateAsset(profile, path);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                return profile;
            }
            return null;
        }

        /// <summary>
        /// Gets scene path of current <see cref="SerializedProperty"/>
        /// </summary>
        /// <param name="property"><see cref="SerializedProperty"/> with scene path we are searching for</param>
        /// <returns>Scene path relative to the project directory in a <see cref="string"/></returns>
        public static string GetScenePath(SerializedProperty property)
        {
            string scenePath = (property.serializedObject.targetObject as MonoBehaviour).gameObject.scene.path;
            scenePath = Path.GetDirectoryName(scenePath);
            if (string.IsNullOrEmpty(scenePath))
            {
                scenePath = "Assets/";
            }
            return scenePath;
        }

    }
}
#endif