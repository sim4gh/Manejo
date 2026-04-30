using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Pipeline de build invocable desde Unity en modo batch:
///
///   /Applications/Unity/Hub/Editor/6000.3.5f2/Unity.app/Contents/MacOS/Unity \
///     -batchmode -nographics -quit \
///     -projectPath /path/to/2026MVP \
///     -buildTarget Win64 \
///     -executeMethod BuildScript.BuildWindows \
///     -logFile /tmp/unity-build.log
///
/// Output: build/{bundleVersion}/{productName}.exe + {productName}_Data/
/// productName actual: Tlax2026-RC (NO cambiar — los kioskos lookup el .exe por nombre).
/// </summary>
public static class BuildScript
{
    [MenuItem("Tlax/Build Windows x64")]
    public static void BuildWindows()
    {
        string version = PlayerSettings.bundleVersion;
        string productName = PlayerSettings.productName;
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string outputDir = Path.Combine(projectRoot, "build", version);
        Directory.CreateDirectory(outputDir);
        string exePath = Path.Combine(outputDir, productName + ".exe");

        string[] scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        if (scenes.Length == 0)
        {
            Debug.LogError("[BuildScript] No hay escenas habilitadas en EditorBuildSettings — abort.");
            EditorApplication.Exit(2);
            return;
        }

        Debug.Log($"[BuildScript] Build {productName} v{version} → {exePath}");
        Debug.Log($"[BuildScript] {scenes.Length} escenas: {string.Join(", ", scenes.Select(Path.GetFileNameWithoutExtension))}");

        BuildPlayerOptions opts = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = exePath,
            target = BuildTarget.StandaloneWindows64,
            targetGroup = BuildTargetGroup.Standalone,
            options = BuildOptions.None,
        };

        BuildReport report = BuildPipeline.BuildPlayer(opts);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            double sizeMB = summary.totalSize / 1024.0 / 1024.0;
            double seconds = summary.totalTime.TotalSeconds;
            Debug.Log($"[BuildScript] OK — {sizeMB:F1} MB en {seconds:F1}s → {summary.outputPath}");
            EditorApplication.Exit(0);
        }
        else
        {
            Debug.LogError($"[BuildScript] FAILED — result={summary.result} errors={summary.totalErrors} warnings={summary.totalWarnings}");
            foreach (var step in report.steps)
            {
                foreach (var msg in step.messages)
                {
                    if (msg.type == LogType.Error || msg.type == LogType.Exception)
                        Debug.LogError($"[BuildScript] step={step.name}: {msg.content}");
                }
            }
            EditorApplication.Exit(1);
        }
    }
}
