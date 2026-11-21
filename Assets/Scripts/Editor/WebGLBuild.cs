using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// One-click WebGL builds for itch.io (needs "Web Build Support" added to Unity 6000.6.0f1 in Unity Hub).
// Stratafield > Build > WebGL (itch.io)      release build into Builds/WebGL, zip that folder's contents for itch
// Stratafield > Build > WebGL Development    same plus the profiler and F3 overlay stats, into Builds/WebGL-Dev
// Also runs from the command line: -executeMethod WebGLBuild.Release (or .Development).
// It sets these Player Settings first (they stay set):
// - Gzip compression + Decompression Fallback: itch.io doesn't send the headers compressed builds need, so the
//   loader unpacks them itself. Without this the game shows a decompression error on itch
// - Data caching on (returning players skip the download), exceptions = explicitly thrown only (smaller, faster),
//   IL2CPP optimised for speed, engine code stripping on
public static class WebGLBuild
{
    [MenuItem("Stratafield/Build/WebGL (itch.io)")]
    public static void Release() => Build(false);

    [MenuItem("Stratafield/Build/WebGL Development")]
    public static void Development() => Build(true);

    static void Build(bool development)
    {
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
        {
            const string message = "Web Build Support isn't installed. In Unity Hub: Installs > Unity 6000.6.0f1 > Add modules > Web Build Support";
            if (Application.isBatchMode) throw new BuildFailedException(message);
            EditorUtility.DisplayDialog("WebGL Build", message, "OK");
            return;
        }

        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.dataCaching = true;
        PlayerSettings.WebGL.exceptionSupport = development ? WebGLExceptionSupport.FullWithStacktrace : WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
        PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.WebGL, Il2CppCodeGeneration.OptimizeSpeed);
        PlayerSettings.stripEngineCode = true;

        string folder = development ? "Builds/WebGL-Dev" : "Builds/WebGL";
        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            locationPathName = folder,
            target = BuildTarget.WebGL,
            options = development ? BuildOptions.Development | BuildOptions.ConnectWithProfiler : BuildOptions.None,
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;
        string size = Directory.Exists(folder)
            ? (Directory.GetFiles(folder, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length) / (1024f * 1024f)).ToString("0.0") + " MB"
            : "?";
        Debug.Log($"WebGL build {summary.result}: {folder}, {size}, {summary.totalTime.TotalMinutes:0.0} min, {summary.totalErrors} errors");
        if (Application.isBatchMode && summary.result != BuildResult.Succeeded) throw new BuildFailedException("WebGL build failed");
        if (!Application.isBatchMode && summary.result == BuildResult.Succeeded) EditorUtility.RevealInFinder(folder);
    }
}
