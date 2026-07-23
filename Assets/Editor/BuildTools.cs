using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildTools
{
  private static string[] Scenes =>
    EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();

  [MenuItem("Tools/Build/Android (AAB for Play Store)")]
  public static void BuildAndroidAab()
  {
    EditorUserBuildSettings.buildAppBundle = true;
    Build(BuildTarget.Android, "Builds/Android/FungiVsBacteria.aab");
  }

  [MenuItem("Tools/Build/Android (APK for testing)")]
  public static void BuildAndroidApk()
  {
    EditorUserBuildSettings.buildAppBundle = false;
    Build(BuildTarget.Android, "Builds/Android/FungiVsBacteria.apk");
  }

  [MenuItem("Tools/Build/iOS (Xcode project)")]
  public static void BuildIos()
  {
    Build(BuildTarget.iOS, "Builds/iOS");
  }

  [MenuItem("Tools/Build/macOS")]
  public static void BuildMac()
  {
    Build(BuildTarget.StandaloneOSX, "Builds/macOS/FungiVsBacteria.app");
  }

  [MenuItem("Tools/Build/WebGL")]
  public static void BuildWebGL()
  {
    Build(BuildTarget.WebGL, "Builds/WebGL");
  }

  private static void Build(BuildTarget target, string outputPath)
  {
    BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
    {
      scenes = Scenes,
      locationPathName = outputPath,
      target = target,
      options = BuildOptions.None
    });

    BuildSummary summary = report.summary;
    if (summary.result == BuildResult.Succeeded)
    {
      Debug.Log($"BUILD OK: {target} -> {outputPath} ({summary.totalSize / (1024 * 1024)} MB)");
      if (Application.isBatchMode) EditorApplication.Exit(0);
    }
    else
    {
      Debug.LogError($"BUILD FAILED: {target} ({summary.totalErrors} errors)");
      if (Application.isBatchMode) EditorApplication.Exit(1);
    }
  }

  // Batch entry point used by CI / command line
  public static void BuildMacBatch() => BuildMac();
  public static void BuildAndroidAabBatch() => BuildAndroidAab();
}
