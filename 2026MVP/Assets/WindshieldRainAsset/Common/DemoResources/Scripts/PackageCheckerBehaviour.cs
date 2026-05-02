using UnityEngine;
#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using System.IO;
using UnityEditor.UI;
#endif

namespace ShadedTechnology.WindshieldRainAsset.Demo
{
    public class PackageCheckerBehaviour : MonoBehaviour
    {
        // Empty MonoBehaviour – inspector logic is handled in editor
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(PackageCheckerBehaviour))]
    public class PackageCheckerBehaviourEditor : Editor
    {
        private static bool hasChecked = false;
        private static bool hasSearched = false;
        private static bool hasCinemachine = false;
        private static bool hasPostProcessing = false;
        private static bool hasTextMeshPro = false;
        private static string installingPackageName = null;

        private static ListRequest listRequest;
        private static AddRequest addRequest;
        private static SearchRequest searchRequest;

        private void OnEnable()
        {
            if (!hasChecked)
            {
                listRequest = Client.List(true); // include dependencies
                EditorApplication.update += ProgressList;
            }

            if (!hasSearched) {
                searchRequest = Client.Search("com.unity.textmeshpro");
                EditorApplication.update += ProgressSearch;
            }
        }

        private void DrawPackageStatus(string displayName, string packageId, ref bool isInstalled)
        {
            if (installingPackageName == packageId && addRequest != null && !addRequest.IsCompleted)
            {
                // Show installing info
                EditorGUILayout.HelpBox($"Installing {displayName}…", MessageType.Info);
                return;
            }

            if (!isInstalled)
            {
                EditorGUILayout.HelpBox($"{displayName} is not installed!", MessageType.Warning);
                if (GUILayout.Button($"Install {displayName}"))
                {
                    installingPackageName = packageId;
                    addRequest = Client.Add(packageId);
                    EditorApplication.update += ProgressAdd;
                }
            }
            else
            {
                EditorGUILayout.HelpBox($"{displayName} is installed ✅", MessageType.Info);
            }
        }

        private static void AddEventSystemPrefab()
        {
            // Prevent duplicates
            if (UnityEngine.Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() != null)
            {
                return;
            }

            GameObject previousSelection = Selection.activeGameObject;
            // Get the internal MenuOptions type
            var menuOptionsType = Type.GetType("UnityEditor.UI.MenuOptions, UnityEditor.UI");
            if (menuOptionsType == null)
            {
                Debug.LogError("Failed to find MenuOptions type.");
                return;
            }

            // Get the internal CreateEventSystem method
            var method = menuOptionsType.GetMethod(
                "CreateEventSystem",
                BindingFlags.Static | BindingFlags.Public,
                null,
                new Type[] { typeof(MenuCommand) },
                null
            );

            if (method != null)
            {
                // Call it with null MenuCommand (root of scene)
                method.Invoke(null, new object[] { new MenuCommand(null) });
                Selection.activeGameObject = previousSelection;
            }
            else
            {
                Debug.LogError("CreateEventSystem method not found via reflection.");
            }
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            AddEventSystemPrefab();

            if (!hasChecked || !hasSearched)
            {
                EditorGUILayout.HelpBox("Checking installed packages...", MessageType.Info);
                return;
            }

            // Cinemachine
            DrawPackageStatus(
                "Cinemachine",
                "com.unity.cinemachine",
                ref hasCinemachine
            );
            if (hasCinemachine)
            {
                FixCinemachineDefaultBlend();
            }

            if (AssetsManager.CheckPipeline() == AssetsManager.Pipeline.BuiltIn)
            {
                // Post Processing
                DrawPackageStatus(
                    "Post Processing",
                    "com.unity.postprocessing",
                    ref hasPostProcessing
                );
                if (hasPostProcessing)
                {
                    FixPostProcessingResources();
                }
            }

            // TextMeshPro + Essentials
            DrawPackageStatus(
                "TextMeshPro",
                "com.unity.textmeshpro",
                ref hasTextMeshPro
            );
            if (hasTextMeshPro)
            {
                bool hasTMPEssentials = Directory.Exists("Assets/TextMesh Pro/Resources");
                if (!hasTMPEssentials)
                {
                    EditorGUILayout.HelpBox("TMP Essentials are not imported!", MessageType.Warning);
                    if (GUILayout.Button("Import TMP Essentials"))
                    {
                        ImportTmpEssentials();
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("TextMeshPro + Essentials are installed ✅", MessageType.Info);
                }
            }

            AssetsManager.CheckBlurCommandBufferOrRenderFeature();
        }

        public static void ImportTmpEssentials()
        {
            var importerType = Type.GetType(
                "TMPro.TMP_PackageResourceImporter, Unity.TextMeshPro",
                false
            );

            if (importerType == null)
            {
                Debug.LogWarning("TMP_PackageResourceImporter not found (TMP may not be installed).");
                return;
            }

            // Find the static ImportResources method
            var method = importerType.GetMethod(
                "ImportResources",
                BindingFlags.Public | BindingFlags.Static
            );

            if (method == null)
            {
                Debug.LogWarning("ImportResources method not found on TMP_PackageResourceImporter.");
                return;
            }

            // Call ImportResources(true, false, false)
            method.Invoke(null, new object[] { true, false, false });
        }

        public static void FixCinemachineDefaultBlend()
        {
            var cam = Camera.main;
            if (cam == null) return;

            var brainType = System.Type.GetType("Unity.Cinemachine.CinemachineBrain, Unity.Cinemachine");
            if (brainType == null)
            {
                brainType = System.Type.GetType("Cinemachine.CinemachineBrain, Cinemachine");
            }
            if (brainType == null) return; // Cinemachine not installed

            var brain = cam.GetComponent(brainType);
            if (brain == null) return;

            // Get serialized object so we can modify private fields safely
            var so = new SerializedObject((UnityEngine.Object)brain);
            var blendProp = so.FindProperty("DefaultBlend");
            if (blendProp == null)
            {
                blendProp = so.FindProperty("m_DefaultBlend");
            }

            if (blendProp != null)
            {
                var styleProp = blendProp.FindPropertyRelative("Style");
                if (styleProp == null)
                {
                    styleProp = blendProp.FindPropertyRelative("m_Style");
                }
                if (styleProp != null && styleProp.enumValueIndex != 0)
                {
                    styleProp.enumValueIndex = 0; // 0 = Cut
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty((UnityEngine.Object)brain);
                }
            }
        }

        public static void FixPostProcessingResources()
        {
            var cam = Camera.main;
            if (cam == null) return;

            var layerType = System.Type.GetType("UnityEngine.Rendering.PostProcessing.PostProcessLayer, Unity.Postprocessing.Runtime");
            if (layerType == null) return; // PostProcessing not installed

            var layer = cam.GetComponent(layerType);
            if (layer == null) return;

            // Use SerializedObject to access private fields
            var so = new SerializedObject(layer as UnityEngine.Object);
            var resourcesProp = so.FindProperty("m_Resources");

            var volumeLayerProp = so.FindProperty("volumeLayer");

            bool dirty = false;
            if (volumeLayerProp != null && volumeLayerProp.intValue != (1 << 6))
            {
                volumeLayerProp.intValue = (1 << 6);
                dirty = true;
            }

            if (resourcesProp != null && resourcesProp.objectReferenceValue == null)
            {
                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                    "Packages/com.unity.postprocessing/PostProcessing/PostProcessResources.asset");

                if (asset != null)
                {
                    resourcesProp.objectReferenceValue = asset;
                    dirty = true;
                }
                else
                {
                    Debug.LogError("Could not find PostProcessResources.asset in package path!");
                }
            }
            if (dirty)
            {
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(layer as UnityEngine.Object);
            }
        }

        private static void ProgressList()
        {
            if (listRequest.IsCompleted)
            {
                if (listRequest.Status == StatusCode.Success)
                {
                    foreach (var package in listRequest.Result)
                    {
                        if (package.name == "com.unity.cinemachine")
                            hasCinemachine = true;
                        if (package.name == "com.unity.postprocessing")
                            hasPostProcessing = true;
                        if (package.name == "com.unity.textmeshpro")
                            hasTextMeshPro = true;
                    }
                }
                else if (listRequest.Status >= StatusCode.Failure)
                {
                    Debug.LogError("Package list error: " + listRequest.Error.message);
                }
                hasChecked = true;
                EditorApplication.update -= ProgressList;
            }
        }

        private static void ProgressAdd()
        {
            if (addRequest.IsCompleted)
            {
                if (addRequest.Status == StatusCode.Success)
                {
                    Debug.Log("Installed: " + addRequest.Result.packageId);
                    // Reset check so inspector refreshes
                    hasChecked = false;
                    listRequest = Client.List(true);
                    EditorApplication.update += ProgressList;
                }
                else if (addRequest.Status >= StatusCode.Failure)
                {
                    Debug.LogError("Install error: " + addRequest.Error.message);
                }

                installingPackageName = null;
                EditorApplication.update -= ProgressAdd;
            }
        }

        private static void ProgressSearch()
        {
            if (searchRequest == null || !searchRequest.IsCompleted)
                return;

            if (searchRequest.Status == StatusCode.Success)
            {
                bool found = false;
                foreach (var package in searchRequest.Result)
                {
                    if (package.name == "com.unity.textmeshpro")
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    hasTextMeshPro = true; // TMP built-in
                }
            }
            else
            {
                hasTextMeshPro = true; // TMP built-in
            }

            hasSearched = true;
            EditorApplication.update -= ProgressSearch;
        }
    }
#endif

        }
