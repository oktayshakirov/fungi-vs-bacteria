using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;
using Debug = UnityEngine.Debug;

// Unity always names the exported Xcode project, main app target, workspace
// and scheme "Unity-iPhone", regardless of Player Settings. This rewrites
// every place that name surfaces in Xcode (project bundle, main app target,
// workspace, scheme, Podfile target) to the real game name, then re-runs
// `pod install` so CocoaPods' generated xcconfig files match.
//
// Deliberately NOT renamed: the "Unity-iPhone Tests" target and the
// "Unity-iPhone" / "Unity-iPhone Tests" group folders on disk. Those are real
// filesystem paths Unity's exporter still writes into every time; renaming
// them would only break path references for no visible benefit (nobody but
// Xcode's navigator sees a group folder name).
//
// Runs on every export, not just the first: EDM4U's iOS resolver regenerates
// the whole Podfile from Dependencies.xml as part of every export, hardcoding
// `target 'Unity-iPhone' do` again, and Unity's "append" export can re-emit
// the Unity-named scheme file alongside an already-renamed one.
public static class IosPostProcess
{
  private const string OldName = "Unity-iPhone";
  private const string NewName = "Fungi vs Bacteria";

  // Stable GUIDs baked into Unity's iOS Xcode template - the same on every
  // export - identifying the main app target, its build configuration list,
  // and the project's own build configuration list.
  private const string AppTargetGuid = "1D6058900D05DD3D006BFB54";
  private const string AppConfigListGuid = "1D6058960D05DD3E006BFB54";
  private const string ProjectConfigListGuid = "C01FCF4E08A954540054247B";

  // LevelPlay requires ATS to allow arbitrary loads: some mediated networks
  // still serve creatives over HTTP, and without this their ad requests are
  // blocked by the OS. `IronSource.Agent.validateIntegration()` reports
  // "App Transport Security settings MISSING" until this is set.
  //
  // ironSource is explicit that NSAllowsArbitraryLoads must be the *only* key
  // in the dictionary - sibling exceptions conflict with it - so the whole
  // dictionary is replaced rather than merged into.
  //
  // App Store review: Apple expects a justification for this, and "the app
  // displays ads from third-party networks" is an accepted one. It is a
  // documented requirement of every major mediation SDK.
  private static void ApplyAppTransportSecurity(string pathToBuiltProject)
  {
    string plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
    if (!File.Exists(plistPath))
    {
      Debug.LogWarning($"IOS POST-PROCESS: no Info.plist at {plistPath}; ATS not applied.");
      return;
    }

    var plist = new PlistDocument();
    plist.ReadFromFile(plistPath);

    PlistElementDict ats = plist.root.CreateDict("NSAppTransportSecurity");
    ats.SetBoolean("NSAllowsArbitraryLoads", true);

    plist.WriteToFile(plistPath);
    Debug.Log("IOS POST-PROCESS: NSAllowsArbitraryLoads set for ad network traffic.");
  }

  [PostProcessBuild(100)]
  public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
  {
    if (target != BuildTarget.iOS) return;

    ApplyAppTransportSecurity(pathToBuiltProject);
    RenameXcodeProject(pathToBuiltProject);
  }

  private static void RenameXcodeProject(string pathToBuiltProject)
  {
    string oldProjDir = Path.Combine(pathToBuiltProject, OldName + ".xcodeproj");
    string newProjDir = Path.Combine(pathToBuiltProject, NewName + ".xcodeproj");
    string projDir = Directory.Exists(oldProjDir) ? oldProjDir : newProjDir;

    if (!Directory.Exists(projDir))
    {
      Debug.LogWarning(
        $"IOS POST-PROCESS: no Xcode project at {oldProjDir} or {newProjDir}; skipping rename.");
      return;
    }

    RewritePbxproj(Path.Combine(projDir, "project.pbxproj"));
    RewriteScheme(projDir);

    if (projDir == oldProjDir)
    {
      Directory.Move(oldProjDir, newProjDir);
    }

    RewriteWorkspace(pathToBuiltProject);
    RewritePodfile(pathToBuiltProject);
    RunPodInstall(pathToBuiltProject);

    Debug.Log($"IOS POST-PROCESS: Xcode project is now \"{NewName}\".");
  }

  private static void RewritePbxproj(string pbxprojPath)
  {
    if (!File.Exists(pbxprojPath)) return;
    string text = File.ReadAllText(pbxprojPath);
    string rewritten = text
      .Replace($"{AppTargetGuid} /* {OldName} */", $"{AppTargetGuid} /* {NewName} */")
      .Replace(
        $"{AppConfigListGuid} /* Build configuration list for PBXNativeTarget \"{OldName}\" */",
        $"{AppConfigListGuid} /* Build configuration list for PBXNativeTarget \"{NewName}\" */")
      .Replace(
        $"{ProjectConfigListGuid} /* Build configuration list for PBXProject \"{OldName}\" */",
        $"{ProjectConfigListGuid} /* Build configuration list for PBXProject \"{NewName}\" */")
      .Replace($"name = \"{OldName}\";", $"name = \"{NewName}\";")
      .Replace($"remoteInfo = \"{OldName}\";", $"remoteInfo = \"{NewName}\";");
    if (rewritten != text) File.WriteAllText(pbxprojPath, rewritten);
  }

  private static void RewriteScheme(string projDir)
  {
    string schemeDir = Path.Combine(projDir, "xcshareddata", "xcschemes");
    string oldScheme = Path.Combine(schemeDir, OldName + ".xcscheme");
    string newScheme = Path.Combine(schemeDir, NewName + ".xcscheme");

    if (File.Exists(oldScheme))
    {
      // Append builds re-emit the Unity-named scheme; replace rather than
      // pile up duplicates in Xcode's scheme list.
      if (File.Exists(newScheme)) File.Delete(newScheme);
      File.Move(oldScheme, newScheme);
    }
    if (!File.Exists(newScheme)) return;

    string text = File.ReadAllText(newScheme);
    // Unity writes a fresh scheme in compact `Attr="Value"` form (no spaces
    // around `=`); once Xcode has opened and re-saved it, it reformats to
    // spaced `Attr = "Value"`. Match both.
    string rewritten = Regex.Replace(
      text,
      $@"ReferencedContainer\s*=\s*""container:{Regex.Escape(OldName)}\.xcodeproj""",
      $"ReferencedContainer=\"container:{NewName}.xcodeproj\"");
    rewritten = Regex.Replace(
      rewritten,
      $@"BlueprintName\s*=\s*""{Regex.Escape(OldName)}""",
      $"BlueprintName=\"{NewName}\"");
    if (rewritten != text) File.WriteAllText(newScheme, rewritten);
  }

  private static void RewriteWorkspace(string pathToBuiltProject)
  {
    string oldWs = Path.Combine(pathToBuiltProject, OldName + ".xcworkspace");
    string newWs = Path.Combine(pathToBuiltProject, NewName + ".xcworkspace");

    if (Directory.Exists(oldWs))
    {
      if (Directory.Exists(newWs)) Directory.Delete(newWs, true);
      Directory.Move(oldWs, newWs);
    }
    if (!Directory.Exists(newWs)) return;

    string dataPath = Path.Combine(newWs, "contents.xcworkspacedata");
    if (!File.Exists(dataPath)) return;
    string text = File.ReadAllText(dataPath);
    string rewritten = text.Replace(
      $"location = \"group:{OldName}.xcodeproj\"",
      $"location = \"group:{NewName}.xcodeproj\"");
    if (rewritten != text) File.WriteAllText(dataPath, rewritten);
  }

  private static void RewritePodfile(string pathToBuiltProject)
  {
    string podfilePath = Path.Combine(pathToBuiltProject, "Podfile");
    if (!File.Exists(podfilePath)) return;
    string text = File.ReadAllText(podfilePath);
    string rewritten = text.Replace($"target '{OldName}' do", $"target '{NewName}' do");
    if (rewritten != text) File.WriteAllText(podfilePath, rewritten);
  }

  private static void RunPodInstall(string pathToBuiltProject)
  {
    if (!File.Exists(Path.Combine(pathToBuiltProject, "Podfile"))) return;

    try
    {
      var psi = new ProcessStartInfo
      {
        FileName = "/bin/bash",
        Arguments = "-lc \"pod install\"",
        WorkingDirectory = pathToBuiltProject,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true,
      };
      // CocoaPods refuses to run under a non-UTF-8 locale, which batch-mode
      // Unity/CI shells often default to.
      psi.EnvironmentVariables["LANG"] = "en_US.UTF-8";
      psi.EnvironmentVariables["LC_ALL"] = "en_US.UTF-8";

      using (Process proc = Process.Start(psi))
      {
        string stdout = proc.StandardOutput.ReadToEnd();
        string stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit(180000);

        if (proc.ExitCode == 0)
        {
          Debug.Log($"IOS POST-PROCESS: pod install completed for \"{NewName}\".\n{stdout}");
        }
        else
        {
          Debug.LogWarning(
            $"IOS POST-PROCESS: pod install failed (exit {proc.ExitCode}); " +
            $"run it manually in {pathToBuiltProject}.\n{stderr}");
        }
      }
    }
    catch (System.Exception e)
    {
      Debug.LogWarning(
        $"IOS POST-PROCESS: could not run pod install automatically ({e.Message}); " +
        $"run it manually in {pathToBuiltProject}.");
    }
  }
}
