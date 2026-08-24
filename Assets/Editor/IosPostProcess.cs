using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

// Unity always names the exported Xcode project, target and scheme
// "Unity-iPhone", regardless of Player Settings — which is why Xcode shows a
// Unity name rather than the game's. The shipped app is unaffected
// (CFBundleDisplayName comes from productName, so the home screen already reads
// "Fungi vs Bacteria"), but the scheme is what Xcode puts in its toolbar and in
// the run/archive menus, and that one can be renamed safely: Xcode reads
// whatever .xcscheme files it finds, and Unity's append builds do not touch
// xcshareddata.
//
// The .xcodeproj folder itself is deliberately left alone. Renaming it would
// break Unity's "append" export, and it is only ever visible in Finder.
public static class IosPostProcess
{
  private const string SchemeName = "Fungi vs Bacteria";

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

    string schemeFolder = Path.Combine(
      pathToBuiltProject, "Unity-iPhone.xcodeproj", "xcshareddata", "xcschemes");
    string generated = Path.Combine(schemeFolder, "Unity-iPhone.xcscheme");
    string renamed = Path.Combine(schemeFolder, SchemeName + ".xcscheme");

    if (!File.Exists(generated)) return;

    // Append builds re-emit the Unity-named scheme next to ours; replace rather
    // than pile up duplicates in Xcode's scheme list.
    if (File.Exists(renamed)) File.Delete(renamed);
    File.Move(generated, renamed);

    Debug.Log($"IOS POST-PROCESS: Xcode scheme renamed to \"{SchemeName}\"");
  }
}
